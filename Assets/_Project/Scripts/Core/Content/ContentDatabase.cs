using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Talents;
using UnityEngine;

namespace PrincesPalace.Content
{
    // The single lookup point for all authored content. At runtime it loads
    // every definition asset out of Resources/Content; tests can bypass that
    // entirely via Initialize(), so they never depend on what happens to be
    // sitting in the project's asset folders.
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

        private static List<CharacterDefinition> _characters;
        private static List<TalentDefinition> _talents;
        private static List<UpgradeDefinition> _upgrades;
        private static List<EnemyDefinition> _enemies;
        private static List<ItemDefinition> _items;
        private static List<SpellTierDefinition> _spellTiers;
        private static List<SkillDefinition> _skills;
        private static List<RelicDefinition> _relics;
        private static List<AchievementDefinition> _achievements;

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
        // gate today — all of them are always available to assign; see
        // RelicLoadout for what actually limits who can carry which.
        public static IReadOnlyList<RelicDefinition> Relics
        {
            get { EnsureLoaded(); return _relics; }
        }

        public static IReadOnlyList<AchievementDefinition> Achievements
        {
            get { EnsureLoaded(); return _achievements; }
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
                .OrderBy(t => t.row)
                .ThenBy(t => t.column)
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
            // was granted outright — right now that only ever means an
            // Event room's mage taught it (see Character.unlockedSkillIds'
            // own header). A skill authored with a level so high it can
            // never be reached by levelling alone (see frost_flare/
            // lightning_bolt in skills.json) is how a skill is marked
            // "event-only" without a separate content field for it.
            // ...or because a TALENT granted it. The third route in, and the
            // reason the reworked tree needs no parallel ability system at
            // all: Provoke, Headbutt and Black Ram Mode are ordinary skills
            // authored at an unreachable level, exactly like the two
            // event-taught spells, and the node that unlocks them names them
            // by id (see TalentGrantedSkillsFor).
            var granted = TalentGrantedSkillsFor(character);

            return _skills
                .Where(s => s.characterId == character.definitionId
                    && (s.unlockLevel <= character.level
                        || character.unlockedSkillIds.Contains(s.id)
                        || granted.Contains(s)))
                .OrderBy(s => s.unlockLevel)
                .ThenBy(s => s.sortOrder)
                .ToList();
        }

        // Injects content directly, skipping Resources. Intended for tests.
        public static void Initialize(
            IEnumerable<CharacterDefinition> characters,
            IEnumerable<TalentDefinition> talents,
            IEnumerable<UpgradeDefinition> upgrades,
            IEnumerable<EnemyDefinition> enemies = null,
            IEnumerable<ItemDefinition> items = null,
            IEnumerable<SpellTierDefinition> spellTiers = null,
            IEnumerable<SkillDefinition> skills = null,
            IEnumerable<RelicDefinition> relics = null)
        {
            // THROUGH THE SAME ORDERING AS THE REAL LOAD, which it did not used
            // to be. This held its own copy of all eight sorts, including the
            // spell tier's by-level special case, so a test's content could
            // have been ordered differently from the game's with nothing to
            // say so -- and a test that orders its fixture differently from
            // production is a test of something else.
            _characters = Ordered(characters);
            _talents = Ordered(talents);
            _upgrades = Ordered(upgrades);
            _enemies = Ordered(enemies);
            _items = Ordered(items);
            _spellTiers = Ordered(spellTiers);
            _skills = Ordered(skills);
            _relics = Ordered(relics);
        }

        // Drops the cache so the next access reloads from Resources.
        public static void Reset()
        {
            _characters = null;
            _talents = null;
            _upgrades = null;
            _enemies = null;
            _items = null;
            _spellTiers = null;
            _skills = null;
            _relics = null;
            _achievements = null;
        }

        public static CharacterDefinition GetCharacter(string id)
        {
            EnsureLoaded();
            return _characters.FirstOrDefault(c => c.id == id);
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
                if (tier.level <= characterLevel)
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
                if (tier.level > characterLevel)
                {
                    break;
                }

                if (scores.Meets(RequirementCurve.Apply(tier.requirements)))
                {
                    best = tier;
                }
            }

            return best;
        }

        // True when ANY ONE prerequisite of `talent` is already unlocked
        // (OR, not AND) -- see TalentDefinition.prerequisites' own comment.
        // A chain node names exactly one parent, so AND and OR agree there;
        // this only actually branches behaviour for the two convergence
        // nodes per path, which name all 3 and unlock from any completed
        // route.
        public static bool PrerequisitesMet(TalentDefinition talent, Character character)
        {
            if (talent.prerequisites == null || talent.prerequisites.Length == 0)
            {
                return true;
            }

            return talent.prerequisites.Any(p => p != null && character.unlockedTalentIds.Contains(p.id));
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
        // tree (`tree.Set(talent.column, talent.row, …)`), and
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

            int slot = talent.row;
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
        public static TalentDefinition AllegianceRootOf(Character character)
        {
            if (character == null)
            {
                return null;
            }

            return TalentsFor(character)
                .FirstOrDefault(t => t.row == 0 && character.unlockedTalentIds.Contains(t.id));
        }

        // Whether this specific talent is blocked by an allegiance already
        // sworn. False for every node that is not a root, and false for the
        // root already lit (so the UI can still offer to refund it).
        public static bool IsBlockedByAllegiance(TalentDefinition talent, Character character)
        {
            if (talent == null || talent.row != 0)
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
        public static int SpentInPath(Character character, TalentDefinition talent)
        {
            return TalentsFor(character)
                .Where(t => t.column == talent.column && character.unlockedTalentIds.Contains(t.id))
                .Sum(t => OrbCost(t));
        }

        // True when talent's point-gate (if any) is satisfied. 0 always
        // passes -- most nodes carry no gate at all.
        public static bool MinSpentMet(TalentDefinition talent, Character character)
        {
            return talent.minSpent <= 0 || SpentInPath(character, talent) >= talent.minSpent;
        }

        // Every rule about whether a node may be lit, EXCEPT affordability.
        //
        // One function rather than four checks the caller composes, because
        // there are now four of them and they have to agree between the
        // talent screen's colouring pass, its click handler and its tests.
        // Three of the four are new in one change, which is exactly when a
        // "the caller remembers to && them all" arrangement starts silently
        // dropping one.
        //
        // Affordability is deliberately NOT in here. The wallet is a
        // GameplayManager-owned save concern and ContentDatabase is a content
        // lookup; folding it in would drag a save dependency into every
        // content test. The two callers ask the wallet themselves, through
        // its own atomic TrySpend.
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

        private static void EnsureLoaded()
        {
            if (_characters != null)
            {
                return;
            }

            _characters = LoadOrdered<CharacterDefinition>(CharacterResourcePath);
            _talents = LoadOrdered<TalentDefinition>(TalentResourcePath);
            _upgrades = LoadOrdered<UpgradeDefinition>(UpgradeResourcePath);
            _enemies = LoadOrdered<EnemyDefinition>(EnemyResourcePath);
            _items = LoadOrdered<ItemDefinition>(ItemResourcePath);
            _spellTiers = LoadOrdered<SpellTierDefinition>(SpellTierResourcePath);
            _skills = LoadOrdered<SkillDefinition>(SkillResourcePath);
            _achievements = LoadOrdered<AchievementDefinition>(AchievementResourcePath);
            _relics = LoadOrdered<RelicDefinition>(RelicResourcePath);
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

        // The ordering itself, shared with Initialize's test-injection path so
        // there is one answer to "what order is this list in" rather than two
        // that happen to agree.
        //
        // Null-tolerant because Initialize's later parameters are optional and
        // a caller passing none of them should get an empty list, not a throw.
        private static List<T> Ordered<T>(IEnumerable<T> content) where T : IOrderedContent
            => (content ?? Enumerable.Empty<T>()).OrderBy(x => x.SortOrder).ToList();
    }
}
