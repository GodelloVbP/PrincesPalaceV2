using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Talents;
using UnityEngine;

namespace PrincesPalace.Content
{
    // The single lookup point for all authored content. At runtime it loads
    // every definition asset out of Resources/Content. Tests substitute
    // content one layer down, where it is cheaper and engine-free: write real
    // assets via ContentBuilder and call Reset(), or bypass ContentDatabase
    // altogether and call a resolver directly against the source JSON (see
    // EnemyContentPinTests, which never touches this type).
    //
    // Nothing in the game should hard-code a character, talent or upgrade —
    // adding content means adding an asset, not editing code.
    //
    // Split across three files: this root (caches, accessors, lookups),
    // .Validation.cs (ValidateContent, the content-authoring sanity check),
    // and .Effective.cs (EffectiveStats/EffectiveAbilityScores/etc — the
    // "what does this character actually have right now, gear and talents
    // included" half). See docs/CODE_MAP.md.
    public static partial class ContentDatabase
    {
        private const string CharacterResourcePath = "Content/Characters";
        private const string TalentResourcePath = "Content/Talents";
        private const string UpgradeResourcePath = "Content/Upgrades";
        private const string EnemyResourcePath = "Content/Enemies";
        private const string ItemResourcePath = "Content/Items";
        private const string SpellTierResourcePath = "Content/SpellTiers";
        private const string SkillResourcePath = "Content/Skills";
        private const string RelicResourcePath = "Content/Relics";
        private const string AchievementResourcePath = "Content/Achievements";
        private const string ModifierResourcePath = "Content/Modifiers";
        private const string RewardTrackResourcePath = "Content/RewardTracks";
        private const string PoolResourcePath = "Content/Pools";
        private const string LevelCurveResourcePath = "Content/LevelCurve";

        private static List<CharacterDefinition> _characters;
        private static List<TalentDefinition> _talents;
        private static List<UpgradeDefinition> _upgrades;
        private static List<EnemyDefinition> _enemies;
        private static List<ItemDefinition> _items;
        private static List<SpellTierDefinition> _spellTiers;
        private static List<SkillDefinition> _skills;
        private static List<RelicDefinition> _relics;
        private static List<AchievementDefinition> _achievements;
        private static List<ModifierDefinition> _modifiers;
        private static List<RewardTrackDefinitionAsset> _rewardTracks;
        private static List<PoolDefinition> _pools;
        private static List<LevelCurveDefinition> _levelCurve;

        // The flat projection Domain actually reads, built once beside the
        // assets rather than per call: Character.ExpToNextLevel asks for it
        // inside AddExperience's loop and twice more per reward row.
        private static List<int> _levelCosts;

        // THE POOL EVERYTHING WITH NO OPINION GETS: every enemy, every
        // character who does not name another one, and the fallback when a
        // named row has gone missing from a save's catalogue. Named here
        // rather than typed at each of those sites, because "which row is
        // the default" is one decision and the three sites must not be able
        // to disagree about it.
        public const string ManaPoolId = "mana";

        // Every combat resource pool, authored order. A character's
        // primaryPoolId names one (ContentDatabase.PrimaryPoolFor), and
        // CombatantState.PrimaryPool is built from it for every combatant in
        // every fight -- see BuildPrimaryPool for the capacity chain.
        public static IReadOnlyList<PoolDefinition> Pools
        {
            get { EnsureLoaded(); return _pools; }
        }

        // The authored level cost table, ascending by level. One row per
        // level from LevelCurve.FirstPaidLevel to RewardTrack.MaxLevel.
        public static IReadOnlyList<LevelCurveDefinition> LevelCurveRows
        {
            get { EnsureLoaded(); return _levelCurve; }
        }

        // The same table as the flat list Domain.Progression.LevelCurve
        // takes: index 0 is the cost to enter level 2.
        //
        // THE ONE ROUTE FROM CONTENT TO THE LEVEL CURVE. LevelCurve is
        // engine-free and cannot reach this type, so somebody in Core has to
        // hand it the numbers; Character.ExpToNextLevel and
        // Character.AddExperience are the only two callers, and they are the
        // seam every level question in the project already goes through.
        //
        // EMPTY BEFORE THE FIRST CONTENT BUILD, which LevelCurve answers with
        // NoTableCost rather than a guess -- see its own comment for why a
        // guessed default is the worse of the two failures.
        public static IReadOnlyList<int> LevelCosts
        {
            get { EnsureLoaded(); return _levelCosts; }
        }

        // Characters in authored roster order.
        public static IReadOnlyList<CharacterDefinition> Characters
        {
            get { EnsureLoaded(); return _characters; }
        }

        // Talents in grid reading order: bottom row first, left to right.
        // The talent UI relies on this matching its button order.
        public static IReadOnlyList<TalentDefinition> Talents
        {
            get { EnsureLoaded(); return _talents; }
        }

        // Every relic in the game, authored order. There is no "unlocked"
        // gate today — all of them are always available. Who is carrying what
        // is RunSnapshot.relicIds, which is run-scoped; there is no
        // per-character assignment (the save field that named one was deleted
        // unwired, AUDIT #119).
        public static IReadOnlyList<RelicDefinition> Relics
        {
            get { EnsureLoaded(); return _relics; }
        }

        public static IReadOnlyList<AchievementDefinition> Achievements
        {
            get { EnsureLoaded(); return _achievements; }
        }

        // Every item modifier ("Rift affix") in the pool, authored order.
        // Phase A2 only -- nothing rolls one onto an item yet (Phase A3), so
        // this is read today only by ModifierEffects below and by content
        // tooling (the future modifier browser).
        public static IReadOnlyList<ModifierDefinition> Modifiers
        {
            get { EnsureLoaded(); return _modifiers; }
        }

        // Every character's authored reward track, in roster order. One
        // reader: RewardTracks.For, which turns the one that matches a
        // character into the RewardTrackDefinition every read site asks.
        //
        // NAMED FOR THE ASSET rather than for the concept, unlike its eleven
        // neighbours, and not out of pedantry: `RewardTracks` unqualified
        // inside this namespace binds to THIS property, so
        // `RewardTracks.For(character)` from any file in PrincesPalace.Content
        // resolves to a member lookup on a list and does not compile. Naming
        // the raw list after what it holds leaves the good name free for the
        // thing callers actually want.
        public static IReadOnlyList<RewardTrackDefinitionAsset> RewardTrackAssets
        {
            get { EnsureLoaded(); return _rewardTracks; }
        }

        // The talents a given character can actually see and take: their own
        // plus every shared node, in grid reading order.
        //
        // Deliberately NOT folded into PrerequisitesMet, which SceneBuilder
        // and TalentController both reuse for the connector line — "this node
        // is somebody else's" and "this node's prerequisites are unmet" are
        // different questions, and conflating them would make the line lie.
        public static List<TalentDefinition> TalentsFor(Character character)
        {
            EnsureLoaded();

            if (character == null)
            {
                return new List<TalentDefinition>();
            }

            return _talents
                .Where(t => t.IsAvailableTo(character))
                .OrderBy(t => t.Data.Row)
                .ThenBy(t => t.Data.Column)
                .ToList();
        }

        public static IReadOnlyList<UpgradeDefinition> Upgrades
        {
            get { EnsureLoaded(); return _upgrades; }
        }

        public static IReadOnlyList<EnemyDefinition> Enemies
        {
            get { EnsureLoaded(); return _enemies; }
        }

        public static IReadOnlyList<ItemDefinition> Items
        {
            get { EnsureLoaded(); return _items; }
        }

        // Ascending by level.
        public static IReadOnlyList<SpellTierDefinition> SpellTiers
        {
            get { EnsureLoaded(); return _spellTiers; }
        }

        // Items split by kind. Almost every caller wants one or the other,
        // not both: the Store sells Consumables (a Weapon is loot, and its
        // cost of 0 would read as a free purchase in that list), combat's
        // Item action can only spend Consumables, and the Inventory screen
        // only offers Weapons to equip.
        public static IReadOnlyList<ItemDefinition> Consumables
        {
            get { EnsureLoaded(); return _items.Where(i => i.kind == ItemKind.Consumable).ToList(); }
        }

        public static IReadOnlyList<ItemDefinition> Weapons
        {
            get { EnsureLoaded(); return _items.Where(i => i.kind == ItemKind.Weapon).ToList(); }
        }

        // Everything that can go on a character, Weapons included. This is
        // what the Equipment screen offers; Weapons stays narrower because
        // the loot table specifically wants weapons ranked by attackBonus.
        public static IReadOnlyList<ItemDefinition> Equippables
        {
            get { EnsureLoaded(); return _items.Where(i => i.IsEquippable).ToList(); }
        }

        // Everything the end-of-combat "choose one of three" screen may
        // offer: GENERATED gear, armour and weapons alike.
        //
        // The test is "was this generated with a tier", not the kind —
        // because the offer screen scales what it shows by run depth, and an
        // item with no tier cannot be ranked on that axis at all. setId and
        // weaponFamilyId are what mark the two generated families; neither is
        // parsed out of an id, which is the whole reason those fields exist.
        //
        // That leaves out the hand-authored one-offs in items.json — potions
        // and the starting kit — which have their own routes in and would
        // otherwise turn up as a "reward" the player already owns six of.
        public static IReadOnlyList<ItemDefinition> Offerable
        {
            get
            {
                EnsureLoaded();
                return _items
                    .Where(i => i.IsSetPiece || !string.IsNullOrEmpty(i.weaponFamilyId))
                    .ToList();
            }
        }

        // One of each item flagged startingStock, in authored order — what a
        // brand new profile begins with (see SaveData.CreateNew). Granted
        // once, never on load, so this is a starting kit rather than income.
        public static IReadOnlyList<ItemDefinition> StartingStock
        {
            get { EnsureLoaded(); return _items.Where(i => i.startingStock).ToList(); }
        }

        // TalentColumns and TalentRows used to live here, deriving the grid's
        // dimensions from the authored talents ("re-shaping the tree is purely
        // a content change"). They are gone because the tree's size has one
        // home and it is not this one: TalentPage.PathCount and
        // TalentSkeleton.SlotCount are what the screen, the resolver and the
        // controller all read, and these two offered a SECOND answer, derived
        // from a different source, that nothing consulted.
        //
        // Recorded rather than deleted silently because "derived from content"
        // reads like the better design and would invite re-adding them. It is
        // not: the skeleton is a fixed graph the content is authored AGAINST,
        // so a max() over what happens to be authored describes the content,
        // not the tree. Content that fills fewer rows than the skeleton has
        // would have made this pair quietly disagree with every other reader.

        public static IReadOnlyList<SkillDefinition> Skills
        {
            get { EnsureLoaded(); return _skills; }
        }

        // THE BASE PREDICATE two call sites used to restate by hand: owned by
        // this character, player-selectable, and reachable by level alone --
        // no talents, no books, no Event-room grants. AvailableSkillsFor
        // unions this with the three routes only a real Character record can
        // supply; FightEncounterAdapter.KitFor's no-Character-record overload
        // (dev-forced/preview fights, which have neither a talent list nor a
        // run to check learnedSpells against) has nothing to union it with,
        // so this is the whole of what it needs. One hand-rolled copy of this
        // already dropped the level filter entirely and handed a level-2 run
        // the whole talent tree (see that overload's own header) -- this is
        // the fix for there being two places the drop could happen at all.
        //
        // Ordered UnlockLevel then SortOrder: the same order the fight strip
        // and this file's other skill listing have always used.
        public static IReadOnlyList<SkillDefinition> SkillsUnlockedByLevel(string characterId, int level)
        {
            EnsureLoaded();

            return _skills
                .Where(s => s.Data.PlayerSelectable
                            && s.Data.CharacterId == characterId
                            && s.Data.UnlockLevel <= level)
                .OrderBy(s => s.Data.UnlockLevel)
                .ThenBy(s => s.Data.SortOrder)
                .ToList();
        }

        // THE question the fight UI asks: what can this character actually
        // press right now? Owned by them, and unlocked by their level.
        //
        // Availability is not affordability. A skill the caster cannot
        // currently pay for is still AVAILABLE — it appears, greyed, with its
        // cost showing. Hiding it would mean the kit visibly changes shape
        // from turn to turn as mana and Wool move, which reads as buttons
        // appearing at random rather than as a resource being short.
        public static IReadOnlyList<SkillDefinition> AvailableSkillsFor(Character character)
        {
            EnsureLoaded();

            if (character == null)
            {
                return new List<SkillDefinition>();
            }

            // A skill is available by LEVEL, same as always, OR because it
            // was granted outright — an Event room's mage teaching it (see
            // Character.unlockedSkillIds' own header). A skill authored with a
            // level so high it can never be reached by levelling alone is how a
            // skill is marked "granted rather than earned" without a separate
            // content field for it.
            //
            // THE TWO EXAMPLES THIS NAMED ARE GONE. frost_flare and
            // lightning_bolt were authored at 999 as the event-taught pair, and
            // in the whole codebase nothing ever writes unlockedSkillIds except
            // one test: the mage is not implemented, so neither spell was
            // reachable by any route. They were removed on 2026-08-22 rather
            // than left as content that cannot be played.
            //
            // The technique still has live examples and they are the talent
            // ones below — Provoke, Headbutt and Black Ram Mode are authored at
            // 999 and reached through grantsSkillId. The read path here keeps
            // its unlockedSkillIds branch: it is the seam the mage will use,
            // and it costs a Contains on a list that is empty today.
            // ...or because a TALENT granted it. The third route in, and the
            // reason the reworked tree needs no parallel ability system at
            // all: Provoke, Headbutt and Black Ram Mode are ordinary skills
            // authored at an unreachable level, exactly like the two
            // event-taught spells, and the node that unlocks them names them
            // by id (see TalentGrantedSkillsFor).
            var granted = TalentGrantedSkillsFor(character);

            // ...or was LEARNED FROM A BOOK this run (docs/PLAN_SHOP.md §1a),
            // the fourth of five. Reads RunManager.Run directly rather than
            // taking it as a parameter -- the same ambient-run-state posture
            // every other Core method that needs "the run right now" already
            // takes (RunOrchestrator, MapController) -- and is null-safe:
            // outside a run (a definition preview, a main-menu character
            // card) this contributes nothing, which is the correct reading.
            //
            // GATED ON s.bookOnly, and NOT on whose skill it is. That second
            // half is the point (plan Step 4, E4).
            //
            // A book is a thing the player bought and handed to somebody. Who
            // it was AUTHORED against is an artefact of where it was first
            // written down -- every one of the six book spells carries
            // characterId "sheep" because Shawn is who they were drafted for,
            // and the shop has never once asked (RunOrchestrator.Shop's
            // AvailableBookOptions offers any bookTier > 0 skill that not
            // every fielded character already knows). So Odette could buy
            // Frost Flare, be charged for it, watch it land in one of her
            // three slots, and then find it was not on her kit in the fight,
            // because this method still asked whether the skill was hers by
            // authorship. It was a purchase with no effect.
            //
            // LearnedSpellEntry already carries the characterId it was learned
            // for, and that -- not the skill's -- is the ownership that means
            // anything for a book.
            //
            // AND GATED ON THE POOL (plan P6, gate 4). SaveData.Reconcile
            // prunes a learned book from a character who cannot hold one, so
            // in a live run this arm is unreachable -- but Reconcile runs on
            // LOAD, and a save written before a character's pool changed
            // reaches a fight through routes that never opened a save file
            // (the tooling party, a preview). Cheap to state here, and it is
            // the difference between "the entry is gone" and "the entry is
            // gone AND could not have cast anything anyway".
            var run = RunManager.Run;
            bool canHoldBooks = CanHoldSpellBooks(character.definitionId);
            bool LearnedThisRun(SkillDefinition s) =>
                s.Data.BookOnly && canHoldBooks && run != null && run.learnedSpells != null
                && run.learnedSpells.Exists(e => e.characterId == character.definitionId && e.skillId == s.id);

            // THE SAME BASE PREDICATE the no-Character-record route uses,
            // through SkillsUnlockedByLevel rather than a second copy of it.
            var byLevel = SkillsUnlockedByLevel(character.definitionId, character.level);

            // ...or because the character's own REWARD TRACK handed it over
            // and they have collected that far -- the fifth route.
            //
            // NO OWNERSHIP TEST, deliberately, and this is the opposite of
            // what ContentDatabase.Validation's talent arm enforces two
            // catalogues over ("a character cannot hand out another
            // character's kit"). The difference is real rather than an
            // oversight: a TALENT belongs to a character and could name
            // somebody else's skill by typo, while a TRACK DEFINITION IS
            // per-character -- naming the skill on Odette's track has already
            // said whose it is. Restoring the symmetry here would refuse
            // Odette every book spell in the game, since all six carry
            // characterId "sheep" for the reason the comment above gives.
            //
            // Read off claimedTrackLevel, not level: a track reward is
            // COLLECTED, and a spell arriving before the player pressed the
            // node would be the auto-claim this whole design removed. A fresh
            // character sits at 0, where SkillsCollected returns nothing --
            // which is the invariant SkillUnlockFilterTests'
            // TheSharedFunctionAgreesWithAvailableSkillsForAFreshLevelOne-
            // Character rests on.
            var fromTrack = RewardTracks.For(character).SkillsCollected(character.claimedTrackLevel);

            // THREE ROUTES WITH DIFFERENT OWNERSHIP RULES, so the CharacterId
            // check sits inside the levelled branch rather than in front of
            // them all. A levelled/granted skill is available because it is
            // THIS character's; a learned book is available because THIS
            // character learned it; a track-collected skill is available
            // because THIS character's own track paid it.
            return _skills
                .Where(s => s.Data.PlayerSelectable
                            && (LearnedThisRun(s)
                                || fromTrack.Contains(s.id)
                                || byLevel.Contains(s)
                                || (s.Data.CharacterId == character.definitionId
                                    && (character.unlockedSkillIds.Contains(s.id)
                                        || granted.Contains(s)))))
                .OrderBy(s => s.Data.UnlockLevel)
                .ThenBy(s => s.Data.SortOrder)
                .ToList();
        }

        // Drops the cache so the next access reloads from Resources.
        public static void Reset()
        {
            // FIRST, so a load that somehow ran during this Reset would refill
            // rather than answer off half-dropped catalogues.
            _loaded = false;

            _characters = null;
            _talents = null;
            _upgrades = null;
            _enemies = null;
            _items = null;
            _spellTiers = null;
            _skills = null;
            _relics = null;
            _achievements = null;
            _modifiers = null;
            _rewardTracks = null;
            _pools = null;
            _levelCurve = null;
            _levelCosts = null;

            // Portraits are keyed by character id and resolved through the
            // roster above, so a swapped roster has to drop them too -- a test
            // that installs its own characters would otherwise see the last
            // one's face.
            CharacterPortraits.Reset();

            // The fight-HUD plates are keyed by the PATH a row authored
            // rather than by id, so a swapped roster changes which path is
            // asked for rather than what a path resolves to -- but a test
            // that also swaps the PNG behind a path would otherwise keep the
            // old sprite forever. Same drop, same call site.
            PcPlateSprites.Reset();

            // Same shape, same reason: RewardTracks memoises one
            // RewardTrackDefinition per character id, built out of the
            // _rewardTracks assets just dropped above, so a swapped catalogue
            // has to drop the memo or every read site keeps answering off the
            // last one's track.
            RewardTracks.Reset();
        }

        public static CharacterDefinition GetCharacter(string id)
        {
            EnsureLoaded();
            return _characters.FirstOrDefault(c => c.id == id);
        }

        // The pool a character's primaryPoolId names. Null when nothing
        // matches, the same graceful shape every other Get*(id) here has --
        // and the content build already refused an unknown id, so a null
        // here means the catalogue was swapped out from under a save, not
        // that an author typed the wrong thing.
        public static PoolDefinition GetPool(string id)
        {
            EnsureLoaded();
            return _pools.FirstOrDefault(p => p.id == id);
        }

        public static TalentDefinition GetTalent(string id)
        {
            EnsureLoaded();
            return _talents.FirstOrDefault(t => t.id == id);
        }

        public static RelicDefinition GetRelic(string id)
        {
            EnsureLoaded();
            return _relics.FirstOrDefault(r => r.id == id);
        }

        public static ModifierDefinition GetModifier(string id)
        {
            EnsureLoaded();
            return _modifiers.FirstOrDefault(m => m.id == id);
        }

        public static UpgradeDefinition GetUpgrade(string id)
        {
            EnsureLoaded();
            return _upgrades.FirstOrDefault(u => u.id == id);
        }

        // Kept despite having only test callers today: it is one of five
        // identical Get*(id) lookups, and deleting exactly one of a matched
        // set costs more in surprise than it saves in lines.
        public static EnemyDefinition GetEnemy(string id)
        {
            EnsureLoaded();
            return _enemies.FirstOrDefault(e => e.id == id);
        }

        public static ItemDefinition GetItem(string id)
        {
            EnsureLoaded();
            return _items.FirstOrDefault(i => i.id == id);
        }

        // One skill by id, ignoring who owns it and whether they have
        // unlocked it — the sixth of the matched Get*(id) set. Talent-granted
        // abilities need it (a talent names a skill id, and both the
        // content check and the combat strip have to resolve that name), and
        // AvailableSkillsFor cannot answer for them: the whole point of a
        // granted ability is that it is authored OUTSIDE the level ladder
        // that method walks.
        public static SkillDefinition GetSkill(string id)
        {
            EnsureLoaded();
            return _skills.FirstOrDefault(s => s.id == id);
        }

        // The tier a caster at `characterLevel` should actually use: the
        // highest-level tier not exceeding it. Null only if no spell tiers
        // are loaded at all (content failed to build) or every tier
        // requires a level higher than characterLevel — with a real
        // spells.json (starting at level 1) that second case can't happen
        // for any real character, since Character.level starts at 1.
        public static SpellTierDefinition GetSpellTierForLevel(int characterLevel)
        {
            EnsureLoaded();

            SpellTierDefinition best = null;
            foreach (var tier in _spellTiers)
            {
                if (tier.Data.Level <= characterLevel)
                {
                    best = tier;
                }
                else
                {
                    break;
                }
            }

            return best;
        }

        // The tier a caster's Skill ACTUALLY uses right now: the highest
        // tier at or below their level whose requirements `scores` meets,
        // falling back through progressively lower tiers rather than to no
        // spell at all. The level-1 tier is guaranteed to have no
        // requirement (ContentDatabase.ValidateContent enforces it), so
        // this is non-null under the same conditions the level-only
        // overload above already documents.
        public static SpellTierDefinition GetSpellTierForLevel(int characterLevel, AbilityScoreBlock scores)
        {
            EnsureLoaded();

            SpellTierDefinition best = null;
            foreach (var tier in _spellTiers)
            {
                if (tier.Data.Level > characterLevel)
                {
                    break;
                }

                if (scores.Meets(RequirementCurve.Apply(tier.Data.Requirements)))
                {
                    best = tier;
                }
            }

            return best;
        }

        // True when ANY ONE prerequisite of `talent` is already unlocked
        // (OR, not AND) -- see ResolvedTalent.Prerequisites' own comment.
        // A chain node names exactly one parent, so AND and OR agree there;
        // this only actually branches behaviour for the two convergence
        // nodes per path, which name all 3 and unlock from any completed
        // route.
        //
        // NOT THE LIVE RULE, and no production caller. The prerequisite check
        // the screen, the bot and the frontier all pass through is
        // TalentPage.Evaluate's walk of TalentSkeleton.Parents
        // (Refusal.PrerequisiteMissing); the two agree by measurement rather
        // than by assumption -- talents.json declares its prerequisites
        // explicitly and they match the skeleton for all 294 authored talents,
        // which TalentPage's own header records. Kept as the content-side
        // statement of the rule for anything reading talents.json directly.
        public static bool PrerequisitesMet(TalentDefinition talent, Character character)
        {
            var prerequisites = talent.Data.Prerequisites;
            if (prerequisites == null || prerequisites.Length == 0)
            {
                return true;
            }

            return prerequisites.Any(id => !string.IsNullOrEmpty(id) && character.unlockedTalentIds.Contains(id));
        }

        // What one orb costs in EMBERS.
        //
        // Derived from the tree's own STRUCTURE rather than authored per node,
        // for two reasons. The shape carries the design already -- a path is a
        // chain of ordinary nodes leading to a convergence, and
        // TalentSkeleton.Kind marks exactly which those are -- so pricing off
        // Kind states "a signature costs more than a step toward it" once,
        // instead of restating it 315 times in content and hoping the numbers
        // stay consistent through a re-author. And it means the tree can be
        // redesigned freely without anyone having to re-price it: a node moved
        // deeper simply becomes worth more.
        //
        // A talent's `row` IS its slot index in the skeleton and its `column`
        // IS its path — TalentController reads both that way when it fills the
        // tree (`tree.Set(talent.Data.Column, talent.Data.Row, …)`), and
        // TalentEntryResolver bounds them against TalentPage.PathCount and
        // TalentSkeleton.SlotCount. That is what makes this lookup legitimate
        // rather than a coincidence.
        //
        // Lives here in Core rather than in the Domain resolver because it
        // takes a TalentDefinition, which is a Core ScriptableObject that
        // Domain cannot see. TalentSkeleton itself is Domain — an earlier
        // version of this comment named it as the reason, which would have sent
        // anyone trying to move this at a wall that is not there. There is no
        // authored cost field to consult any more — see ResolvedTalent's
        // constructor for why the one that used to exist was deleted.
        //
        // The scheme (handoff §7, all [LOCKED]):
        //
        //     root         slot 0        0
        //     bottom       slots 1-9     1 / 2 / 3 by tier
        //     convergence  slot 10       0, gated at 9 spent in this path
        //     top          slots 11-19   2 / 3 / 4 by tier
        //     capstone     slot 20       0, gated at 20 spent in this path
        //
        // Three things changed from the old 1/2/4/8-by-Kind pricing, and each
        // one is doing work.
        //
        // Price now keys off POSITION WITHIN A STRAND, not just Kind. A
        // strand is one idea developed three times, escalating number →
        // number → rule-change, and the prereq chain already forces the
        // player up it in order — so the price should escalate with it. Kind
        // alone cannot see that: it reports "normal" for all eighteen strand
        // nodes.
        //
        // The convergence and the capstone are FREE. They were the two most
        // expensive orbs in the tree and are now the two cheapest, which
        // reads backwards until you notice what replaced the price: a gate on
        // embers already spent in that path. A price says "save up"; a gate
        // says "commit". Only the second one produces the Deep / Wide /
        // Fusion archetypes the whole economy is built around, because only
        // the second one cares WHERE the embers went.
        //
        // The root is free because it is not a purchase at all. It is an
        // allegiance, and you may hold exactly one across all three paths —
        // see AllegianceRootOf. Charging for it would make the exclusivity
        // rule read as a punishment rather than as the thing that lets you
        // own nodes from every path without the wool economy collapsing.
        //
        // Total for one full path: 18 below + 27 above = 45. Against a
        // lifetime budget of EmberSpendCap that is about two thirds of one
        // path, which is what makes the tree a choice rather than a checklist.
        // How many depth tiers sit below the top strands: the root, the three
        // bottom tiers, and the convergence. Subtracting it turns the branch's
        // depths (5, 6, 7) into its prices (2, 3, 4) — the top strands start
        // one tier dearer than the bottom ones did and climb at the same rate.
        private const int TiersBelowTheBranch = 3;

        // What a node with no slot in the skeleton falls back to. Unreachable
        // through content (TalentEntryResolver rejects a row outside 0-20),
        // and cheapest-thing-in-the-tree rather than free so a hole can never
        // be the profitable one to buy.
        private const int OrbCostUnplaced = 1;

        public static int OrbCost(TalentDefinition talent)
        {
            if (talent == null)
            {
                return 0;
            }

            int slot = talent.Data.Row;
            if (slot < 0 || slot >= TalentSkeleton.SlotCount)
            {
                return OrbCostUnplaced;
            }

            string kind = TalentSkeleton.Kind[slot];
            if (kind == "merge" || kind == "cap")
            {
                return 0;
            }

            // Depth IS the strand tier: 0 for the root, 1-3 climbing the 3x3
            // grid, 4 at the convergence (handled above), 5-7 climbing the
            // branch. So a bottom strand prices as its depth outright — 1, 2,
            // 3 — and a top strand as its depth less the tiers beneath the
            // branch, which lands exactly on 2, 3, 4.
            int depth = TalentSkeleton.Depth[slot];
            if (depth == 0)
            {
                return 0;
            }

            return depth <= TiersBelowTheBranch ? depth : depth - TiersBelowTheBranch;
        }

        // A character's whole lifetime talent budget, in embers.
        //
        // A full path costs 45, so 30 buys about two thirds of one — the tree
        // is a genuine choice. The three shapes it produces: 30 deep into one
        // path (its capstone included, since the capstone is free once its
        // 20-spent gate is met); 9+9+9 wide for all three convergence
        // abilities; or 20+9 fused, which is 29 of the 30 and leaves exactly
        // one ember spare.
        //
        // PER CHARACTER, not per save. The wallet itself
        // (CurrencyType.Embers) stays shared and uncapped across the roster —
        // this caps how deep any ONE hero's tree can go, which is the axis
        // the archetypes above are measured on. It also answers, in passing,
        // the handoff's open question about what embers are for once 30 are
        // spent (§7): they go to somebody else's tree.
        public const int EmberSpendCap = 30;

        // Embers this character has committed across all three of their
        // paths. The denominator of the cap, and what the talent screen's
        // budget readout shows.
        public static int SpentBy(Character character)
        {
            if (character == null)
            {
                return 0;
            }

            return TalentsFor(character)
                .Where(t => character.unlockedTalentIds.Contains(t.id))
                .Sum(t => OrbCost(t));
        }

        // How many embers this character may still commit before hitting the
        // lifetime cap. Never negative — a save from before the cap existed
        // is over budget rather than broken, and reports 0 left rather than a
        // negative allowance the UI would then have to special-case.
        //
        // THIS IS THE BUDGET THE GATE READS. It had no caller at all until the
        // cap became a refusal on the player's own path; it is now the number
        // TalentOps.Kindle, the talent screen's colouring pass and the bot's
        // preset builder all hand to TalentPage.Evaluate, which refuses
        // BudgetSpent against it.
        public static int EmbersLeftFor(Character character)
        {
            return Mathf.Max(0, EmberSpendCap - SpentBy(character));
        }

        // The path root this character has already lit, or null.
        //
        // Slot 0 of each path is that path's WOOL GENERATION RULE, and
        // exactly one may ever be lit across all three (handoff §3). This is
        // the load-bearing rule of the whole design: three generation rules
        // stacking would produce roughly +4-5 wool a turn against abilities
        // priced at 7-8, and the economy stops existing. Locking the ENGINE
        // rather than the whole tree is precisely what lets a player own
        // nodes from all three paths without breaking anything.
        //
        // Nothing in the prerequisite system could express this: prerequisites
        // only ever look UPWARD within one path, so no arrangement of them
        // can say "and not that one, over there".
        //
        // THE RULE IS ENFORCED IN Domain/Talents/TalentPage, NOT HERE, and
        // this member has no production caller. It was written when the screen
        // resolved its own colouring against the content; TalentPage replaced
        // that pass and the rule did not come across, so for a long while
        // nothing enforced it anywhere and both of a character's roots could be
        // lit (TalentPage.Refusal.AllegianceSworn is the fix). It could not
        // simply be called from there: TalentPage is Domain and cannot see a
        // TalentDefinition, a Character or this class. So the rule lives one
        // layer down, where the screen, the bot and the frontier all reach it,
        // and this stays as the content-side statement of WHY -- which is what
        // the paragraphs above are, and they are still true.
        public static TalentDefinition AllegianceRootOf(Character character)
        {
            if (character == null)
            {
                return null;
            }

            return TalentsFor(character)
                .FirstOrDefault(t => t.Data.Row == 0 && character.unlockedTalentIds.Contains(t.id));
        }

        // Whether this specific talent is blocked by an allegiance already
        // sworn. False for every node that is not a root, and false for the
        // root already lit (so the UI can still offer to refund it).
        //
        // No production caller: TalentPage.Evaluate answers this for the live
        // path, in the same two shapes -- only a root, and never the one
        // already sworn, which still reads AlreadyTaken. See AllegianceRootOf
        // above for why the rule sits in Domain rather than here.
        public static bool IsBlockedByAllegiance(TalentDefinition talent, Character character)
        {
            if (talent == null || talent.Data.Row != 0)
            {
                return false;
            }

            var sworn = AllegianceRootOf(character);
            return sworn != null && sworn.id != talent.id;
        }

        // Embers already spent in talent's OWN path (same owner + column),
        // regardless of which specific nodes earned them -- what the two
        // convergence nodes' gates check against, on top of (not instead of)
        // PrerequisitesMet.
        //
        // No caller of any kind, production or test. TalentPage.SpentOn is the
        // live version, counted against the tree the screen holds rather than
        // against the catalogue, and its own header says why the two cannot be
        // one function: "real content ids carry no path in them". This is the
        // content-side reading of the same quantity.
        public static int SpentInPath(Character character, TalentDefinition talent)
        {
            return TalentsFor(character)
                .Where(t => t.Data.Column == talent.Data.Column && character.unlockedTalentIds.Contains(t.id))
                .Sum(t => OrbCost(t));
        }

        // True when talent's point-gate (if any) is satisfied. 0 always
        // passes -- most nodes carry no gate at all.
        //
        // No production caller. This is the one of the three whose rule DID
        // come back: TalentPage.Refusal.Gated restored it after the migration
        // dropped it, and TalentGateTests pins it. The gate itself travels on
        // TalentSlot.MinSpent, which is how Domain answers without ever seeing
        // a TalentDefinition.
        public static bool MinSpentMet(TalentDefinition talent, Character character)
        {
            return talent.Data.MinSpent <= 0 || SpentInPath(character, talent) >= talent.Data.MinSpent;
        }

        // Every rule about whether a node may be lit, EXCEPT affordability.
        //
        // THE CALLERS THIS ONCE NAMED DO NOT EXIST, and the correction is
        // worth more than the paragraph it replaces. It said "there are now
        // four of them and they have to agree between the talent screen's
        // colouring pass, its click handler and its tests" -- there were none,
        // and there never have been. The screen's colouring pass and its click
        // handler both go through TalentPage.Evaluate (Domain), which carried
        // three of these four rules and not the other two: the allegiance and
        // the spend cap were enforced NOWHERE until they were added there as
        // Refusal.AllegianceSworn and Refusal.BudgetSpent.
        //
        // A dead function that reads as the rulebook is worse than no function
        // at all, and that is the whole lesson of this one: the composition
        // argument was right, and the composed version simply was not what
        // anybody called. Evaluate is now the single place the four are
        // composed. This is kept as the content-side statement of the same
        // four -- accurate again, but documentation rather than the rule, and
        // a fifth rule added here would reach nothing.
        //
        // Affordability is deliberately NOT in here. The wallet is a
        // GameplayManager-owned save concern and ContentDatabase is a content
        // lookup; folding it in would drag a save dependency into every
        // content test. TalentPage takes the wallet as a parameter for the
        // mirror-image reason -- it is Domain, and the save is not its
        // business.
        public static bool MeetsGates(TalentDefinition talent, Character character)
        {
            if (talent == null || character == null)
            {
                return false;
            }

            return PrerequisitesMet(talent, character)
                   && MinSpentMet(talent, character)
                   && !IsBlockedByAllegiance(talent, character)
                   && OrbCost(talent) <= EmbersLeftFor(character);
        }

        // GUARDED ON A FLAG SET LAST, not on the second field assigned.
        //
        // The guard used to read `_characters != null`, and _characters is the
        // SECOND of twelve catalogues this method fills. A re-entrant call made
        // anywhere in the window between that assignment and the last one would
        // find the guard satisfied and return with _talents.._rewardTracks
        // still null -- and RewardTracks.Build would then dereference one.
        //
        // The comment on _rewardTracks below already describes this shape
        // exactly (a lazy load "re-entered from a future per-character read ...
        // would find _characters already non-null at the guard on this method's
        // first line") and answers it by loading that one field here rather
        // than lazily. That closes the one re-entry anybody had found; a flag
        // set after every assignment closes the window itself, for the eleven
        // other fields and for the next reader who adds a thirteenth.
        //
        // Nothing re-enters today, so there is no failing test to point at --
        // an honest "latent" rather than a fixed crash. Reset() clears the flag
        // with the fields, or the next load would be skipped entirely.
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            // POOLS FIRST, mirroring the order ContentBuilder writes them in
            // -- a character names a pool, never the other way round.
            _pools = LoadOrdered<PoolDefinition>(PoolResourcePath);
            _characters = LoadOrdered<CharacterDefinition>(CharacterResourcePath);
            _talents = LoadOrdered<TalentDefinition>(TalentResourcePath);
            _upgrades = LoadOrdered<UpgradeDefinition>(UpgradeResourcePath);
            _enemies = LoadOrdered<EnemyDefinition>(EnemyResourcePath);
            _items = LoadOrdered<ItemDefinition>(ItemResourcePath);
            _spellTiers = LoadOrdered<SpellTierDefinition>(SpellTierResourcePath);
            _skills = LoadOrdered<SkillDefinition>(SkillResourcePath);
            _achievements = LoadOrdered<AchievementDefinition>(AchievementResourcePath);
            _relics = LoadOrdered<RelicDefinition>(RelicResourcePath);
            _modifiers = LoadOrdered<ModifierDefinition>(ModifierResourcePath);

            // ORDERED BY LEVEL (LevelCurveDefinition.SortOrder), which is what
            // makes the flat projection below index-addressable at all: row 0
            // is level 2's cost only because the list is sorted.
            _levelCurve = LoadOrdered<LevelCurveDefinition>(LevelCurveResourcePath);
            _levelCosts = _levelCurve
                .Where(row => row != null && row.Data != null)
                .Select(row => row.Data.Cost)
                .ToList();

            // Loaded HERE, not lazily off the RewardTracks property: a lazy
            // load re-entered from a future per-character read (the way
            // AvailableSkillsFor calls EnsureLoaded() at :238 above and reads
            // a track) would find _characters already non-null at the guard
            // on this method's first line and return before ever assigning
            // this field. See docs/PLAN_REWARD_TRACKS.md §4's touch-point
            // table for the citation this mirrors.
            _rewardTracks = LoadOrdered<RewardTrackDefinitionAsset>(RewardTrackResourcePath);

            // LAST, and that is the whole of the guard above: everything this
            // method promises is in place before anything can skip it.
            _loaded = true;
        }

        // THE ONLY PLACE CONTENT IS LOADED, and the constraint is what makes
        // that worth anything: a type that has not said how it is ordered
        // cannot be passed to this, so "I forgot the sort" is a compile error
        // rather than a shipped list in filename order.
        //
        // Nine near-identical `.OrderBy(x => x.sortOrder).ToList()` chains used
        // to sit inline here, and two of them quietly were not that -- spell
        // tiers sorted by level, talents by row then column. Written out nine
        // times, the two exceptions read as ordinary lines; collected into one
        // helper, they have to be stated on the types themselves, which is
        // where a reader looks for them.
        //
        // ContentLoadingLintTests keeps Resources.LoadAll from reappearing
        // anywhere else, for the same reason UiEmitter owns `new GameObject`:
        // a helper you are entitled to bypass gets bypassed.
        private static List<T> LoadOrdered<T>(string resourcePath)
            where T : UnityEngine.Object, IOrderedContent
            => Ordered(Resources.LoadAll<T>(resourcePath));

        // The ordering itself, the one answer to "what order is this list in".
        //
        // Null-tolerant so a caller with nothing to pass gets an empty list,
        // not a throw.
        private static List<T> Ordered<T>(IEnumerable<T> content) where T : IOrderedContent
            => (content ?? Enumerable.Empty<T>()).OrderBy(x => x.SortOrder).ToList();
    }
}
