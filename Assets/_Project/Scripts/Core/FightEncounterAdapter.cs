using System;
using PrincesPalace.Domain.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // Turns authored content into a fight the session can resolve.
    //
    // THE SEAM THE WHOLE DECOMPOSITION EXISTS FOR. Everything above it is
    // engine-free Domain that knows nothing about ScriptableObjects; everything
    // below is Core reading Resources. v1 had no such boundary, which is why its
    // combat logic could not be tested without a scene.
    //
    // Note what comes back: a session, AND two parallel lists of art paths. The
    // kit is what COMBAT needs and a sprite folder is not that -- v1 kept both in
    // one `_playerOwners` dictionary and that single conflation is a large part
    // of why the controller was 7,000 lines.
    public static class FightEncounterAdapter
    {
        public sealed class BuiltFight
        {
            public FightSession Session;
            public IReadOnlyList<CombatantState> Party;
            public IReadOnlyList<string> PartyArt;

            // WHO EACH COMBATANT IN Party ACTUALLY IS, parallel to it and to
            // PartyArt, and the only list anything downstream may index against
            // Party.
            //
            // Build SKIPS an id naming no content, in place, so the ids it was
            // HANDED stop lining up with the combatants it produced the moment
            // one of them fails to resolve -- and a squad id outliving its
            // content is an ordinary rename, not an exotic save. Carrying the
            // built ids here is what makes the correspondence a fact of the
            // build rather than something each caller has to reconstruct
            // correctly.
            public IReadOnlyList<string> PartyIds;
        }

        // ---- content -> Domain ------------------------------------------------

        // The conversion that used to have to exist, and no longer converts
        // anything.
        //
        // This WAS a hand-written copy of 34 arguments out of EnemyDefinition
        // and back into a ResolvedEnemy -- one of the three field lists this
        // type's facts were written on, and the same shape that dropped
        // `transform` and then `bookOnly`/`bookTier` on the skill side. The
        // asset now STORES the ResolvedEnemy, so there is nothing here to drop.
        public static ResolvedEnemy Resolve(EnemyDefinition definition) => definition.Data;

        // PHASE 5B (D6): closes AUDIT #54. An Elite pack's stats used to be
        // the authored baseStats verbatim -- StatBlock.ScaledForElite existed,
        // had its multipliers reasoned about in its own header, and had NO
        // production caller anywhere in the game. Elite is HP x1.40 / ATK
        // x1.15 / both broad Defenses x1.15, applied to the AUTHORED stats
        // before depth scaling -- so an Elite fought at any depth is that
        // depth's normal encounter scaled up by the same fixed ratio, not a
        // ratio that itself drifts with depth.
        private const float EliteHealthMultiplier = 1.40f;
        private const float EliteAttackMultiplier = 1.15f;
        private const float EliteDefenseMultiplier = 1.15f;

        // DEPTH IS APPLIED HERE, and this is the only place it is applied.
        //
        // DifficultyCurve was written to scale enemies and then called by
        // nothing but VictoryRewards, so every fight at every depth used the
        // authored baseStats verbatim -- the "infinite corridor of trivial
        // fights" that file exists to prevent is what shipped. This line is
        // the fix; the curve itself only needed retuning.
        //
        // Health and attack take DIFFERENT rates because the player's own two
        // axes grow at different speeds. See DifficultyCurve.
        private static CombatantState ToCombatant(EnemyDefinition definition, int depthStep, bool isElite)
        {
            var stats = definition.Data.BaseStats;
            if (isElite)
            {
                stats = stats.ScaledForElite(EliteHealthMultiplier, EliteDefenseMultiplier, EliteAttackMultiplier);
            }

            // Mana is NOT part of StatBlock -- it is derived from ability scores
            // for a character, and monsters have none. They get the `mana` row
            // at its authored capacity so a monster skill that costs mana can
            // still be paid for, rather than a zero that would silently make
            // every such skill unusable.
            //
            // NEUTRAL SCORES AND NO RELICS, passed explicitly: an enemy has
            // never had a Wisdom term or a relic on its pool, and saying so
            // in the arguments is what keeps that true now that the capacity
            // chain is shared with the two character paths.
            // Speed is NOT depth-scaled. It is a rate, feeding a scheduler that
            // clamps at 2.5x anyway, and the same reasoning AbilityDerivation
            // applies to the player's Dexterity applies to a monster: an enemy
            // at 40,000 Speed does not act more often, it acts always. (An
            // Elite's speed DOES ride the elite multiplier above, same as its
            // health -- ScaledForElite's own main `multiplier` parameter,
            // unchanged behaviour from before this phase.)
            //
            // MANA REGEN IS NOT IN THAT LIST, and this comment used to say it
            // was. `stats` really has been elite-scaled two blocks up, so the
            // claim reads plausibly -- but stats.manaRegen is never read on
            // this path at all: the call below passes manaRegen: 0 outright,
            // and no enemy in the game has ever regenerated a point of mana,
            // before this phase or after it. The behaviour is fine; the
            // sentence was the kind that sends the next reader hunting a bug
            // that is not there, or "fixing" it and silently re-tuning every
            // elite in the game. Passing stats.manaRegen instead would be a
            // balance change and the owner's call, not a comment's.
            var state = new CombatantState(definition.Data.DisplayName, false,
                DifficultyCurve.ScaleHealth(stats.maxHealth, depthStep),
                ContentDatabase.BuildPrimaryPool(ContentDatabase.ManaPoolId,
                    ScalingProfile.NeutralScores, null, manaRegen: 0),
                DifficultyCurve.ScaleAttack(stats.attack, depthStep),
                stats.speed);

            // PHASE 5B (D6): enemy defenses no longer depth-scale at all --
            // used at their AUTHORED (step-0) value, only elite-scaled above
            // when this encounter is an Elite. The R_broad/(100+R_broad)
            // mitigation curve (DamagePipeline) is already asymptotic, so
            // scaling a defense on top of it double-dips and was running boss
            // time-to-kill away past floor 4. See DifficultyCurve.ScaleAttack's
            // own note.
            state.PhysicalDefense = stats.physicalDefense;
            state.MagicalDefense = stats.magicalDefense;

            if (definition.Data.BreakShieldPoints > 0)
            {
                state.BreakShield = new BreakShield(
                    DifficultyCurve.ScaleShield(definition.Data.BreakShieldPoints, depthStep));
            }

            return state;
        }

        // Turns the run's relic ids into the resolved shape combat reads.
        // An id naming content that no longer exists is DROPPED rather than
        // throwing -- a save referencing a deleted relic must still be
        // playable, which is the house posture everywhere else.
        private static List<ResolvedRelic> ResolveRelics(IReadOnlyList<string> relicIds)
        {
            var resolved = new List<ResolvedRelic>();
            if (relicIds == null) return resolved;

            foreach (string id in relicIds)
            {
                var definition = ContentDatabase.Relics.FirstOrDefault(r => r != null && r.id == id);
                if (definition == null) continue;

                // Was a ten-argument copy out of RelicDefinition and back into
                // a ResolvedRelic, with a second hand-written conversion
                // (ToModifiers) for the modifier list. The asset stores the
                // resolved value now.
                resolved.Add(definition.Data);
            }

            return resolved;
        }

        // The IN-RUN party member: what they actually walk into the room with.
        //
        // THIS IS WHERE EQUIPMENT REACHES COMBAT, and until it existed nothing
        // did. The definition-based overload below reads baseStats, so a
        // character in full plate swung exactly as hard as one in nothing --
        // every item, every talent and every upgrade level was inert in a
        // fight. ContentDatabase.Effective.cs had thirteen accessors for this
        // and combat called none of them; only relics got through, because
        // relics were wired deliberately and the rest was assumed.
        //
        // The effective figures are used RAW. EffectiveStats already ends with
        // `total += AbilityDerivation.DerivedStats(loadout.Scores)` and
        // EffectiveMaxMana already adds MaxManaBonus, so re-applying the
        // derivation here -- as the definition overload legitimately must --
        // would silently double a character's health and attack.
        private static CombatantState ToCombatant(Character character,
                                                  CharacterDefinition definition,
                                                  IReadOnlyList<RelicModifier> modifiers = null)
        {
            var stats = ContentDatabase.EffectiveStats(character);
            var scores = ContentDatabase.EffectiveAbilityScores(character);

            // THE WEAPON MODEL -- balance redesign Phase 3 (D3). A LIVE
            // main-hand weapon's honed WeaponPower REPLACES base+gear+ability
            // Attack entirely; EffectiveStats.attack no longer carries a gear
            // contribution at all (see its own note), so `stats.attack` here
            // is already exactly the unarmed fallback the plan calls for --
            // the character's own authored figure, no multiplier -- and this
            // is the one place the two are chosen between.
            int attack = ContentDatabase.EquippedWeaponPower(character) ?? stats.attack;

            // RESOLVED BEFORE THE COMBATANT, not after it, because the pool
            // needs them: FlatMaxManaBonus and FlatManaRegenBonus are gear
            // terms in the capacity chain, and a pool that arrives at
            // construction cannot be topped up by a line further down without
            // reopening the question of whether Current followed Max. Nothing
            // else about this moved -- ModifierEffects is a pure function of
            // the character's loadout and was always computable here.
            var modifierEffects = ContentDatabase.ModifierEffects(character);

            var state = new CombatantState(definition.Data.DisplayName, true,
                RelicModifiers.Apply(stats.maxHealth, RelicStat.MaxHealth, modifiers),
                ContentDatabase.BuildPrimaryPool(character, modifiers, modifierEffects, stats.manaRegen),
                RelicModifiers.Apply(attack, RelicStat.Attack, modifiers),
                RelicModifiers.Apply(stats.speed, RelicStat.Speed, modifiers));

            state.ArmorPenetration = RelicModifiers.Apply(0, RelicStat.ArmorPenetration, modifiers);

            // Balance pass 2: Jo-Sun's Book of Anatomy and Vampire Dentures,
            // the same "flat stat set once at kit-build time from a
            // RelicModifier" shape ArmorPenetration just above already uses.
            state.WeaknessMultiplierBonusPercent = RelicModifiers.Apply(0, RelicStat.WeaknessBonus, modifiers);
            state.RelicLifestealPercent = RelicModifiers.Apply(0, RelicStat.Lifesteal, modifiers);

            state.AbilityScores = scores;

            // Never assigned anywhere in the real adapter path before this --
            // see ContentDatabase.EffectiveWeaponScaling's own header. Without
            // it every real fight built combatants with ScalingSet.None
            // regardless of what was equipped, so a weapon's own ATK grade and
            // a spell tier's INT/WIS grade never touched real damage at all.
            state.WeaponScaling = ContentDatabase.EffectiveWeaponScaling(character);
            state.SkillScaling = ContentDatabase.EffectiveSkillScaling(character);

            // Set by neither path before this. A resistance rolled onto a
            // breastplate was written to the save, shown on the sheet, folded
            // into EffectiveStats -- and then dropped on the way into the one
            // system it exists for.
            //
            // RelicStat.Defence APPLIES TO BOTH broad Defenses now -- there is
            // no longer one generic Defense stat for a relic modifier to name,
            // so "Defence" reads as "both of them", exactly the reading
            // whetstone_heart's -10% needs to keep meaning what it always
            // meant.
            state.PhysicalDefense = RelicModifiers.Apply(stats.physicalDefense, RelicStat.Defence, modifiers);
            state.MagicalDefense = RelicModifiers.Apply(stats.magicalDefense, RelicStat.Defence, modifiers);

            // And whatever a relic adds against ONE element, on top of those.
            state.TypedResistance = RelicModifiers.ApplyResistance(state.TypedResistance, modifiers);

            // Built from the CHARACTER, so a signature whose capacity a talent
            // widened arrives at that width. Its single call site until now was
            // its own definition.
            state.SignaturePool = ContentDatabase.BuildSignatureResource(character);

            // Every TalentEffectType-gated rule a character's unlocked
            // talents grant -- Sharp Horns' penetration/shred, Last Stand's
            // below-health bonuses, Provoke's damage reduction, Trample's
            // execute/splash/extra-attack, every Wool income hook, the whole
            // Fragile Lamb ward tree -- read this via target.Talents.Best/
            // BestBelowHealth/Has (CombatMath, FightSession.Talents.cs,
            // FightSession.Enemies.cs/.Riders.cs/.Skills.cs, StatusEffects).
            // Until now nothing at this seam ever set it, so every one of
            // those rules resolved against the shared CombatantState default
            // (TalentEffectSet.Empty) in every real fight -- a player who
            // sank Embers into a talent node with a TalentEffectType effect
            // got exactly nothing from it in play. Only test fixtures ever
            // assigned .Talents, which is why nothing caught the gap.
            state.Talents = ContentDatabase.TalentEffects(character);

            // Real, droppable content as of Phase C — every equipped item's
            // rolled modifiers, already scaled by tier/riftTier (see
            // ContentDatabase.ModifierEffects' own header).
            state.ModifierEffects = modifierEffects;

            // RUNIC'S MANA POOL MOVED INTO THE CHAIN. FlatMaxManaBonus and
            // FlatManaRegenBonus used to be added to the CombatantState here,
            // after construction, with the comment that Max and Current had
            // to be bumped by the same amount or the wearer would open the
            // fight one modifier short of full. That pairing is now
            // structural rather than remembered: the pool is built at its
            // final capacity and its startRule fills it (see
            // ContentDatabase.BuildPrimaryPool), so there is no second
            // number to keep in step.

            // The elemental family's typed-resistance half -- the SAME seam
            // relic-granted typed resistance already reaches state.TypedResistance
            // through, just walked over ModifierEffects.All (see
            // ModifierEffectSet.Best's own header on why a resistance
            // consumer must read .All rather than Best, which would collapse
            // every element down to whichever one is strongest) instead of
            // RelicModifiers.ApplyResistance, which only ever reads
            // RelicModifier. Summed ON TOP of whatever a relic already
            // granted -- the two sources stack, same as PhysicalDefense/
            // MagicalDefense already do for relics vs gear stats.
            foreach (var effect in state.ModifierEffects.All)
            {
                if (effect.Type != ModifierEffectType.TypedResistanceFlat) continue;

                state.TypedResistance = effect.AgainstMagical
                    ? state.TypedResistance.WithMagical(effect.Magnitude)
                    : effect.Against.HasValue
                        ? state.TypedResistance.With(effect.Against.Value, effect.Magnitude)
                        : state.TypedResistance;
            }

            return state;
        }

        // The TOOLING party member, built from content alone.
        //
        // Still needed: opening the Fight scene directly has no save to read a
        // loadout from, and screenshot comparison depends on that path
        // producing the same stage every time.
        private static CombatantState ToCombatant(CharacterDefinition definition,
                                                  IReadOnlyList<RelicModifier> modifiers = null)
        {
            var stats = definition.Data.BaseStats;
            var scores = definition.Data.AbilityScores;

            // Health is BASE PLUS DERIVED, because that is what the ability
            // scores are for -- reading the StatBlock alone would give a
            // character none of the pool their own build earned them. The
            // primary pool takes the same shape, inside BuildPrimaryPool: the
            // row's authored capacity plus the Wisdom term, then relics --
            // and only when the row says WisdomDerived.
            int maxHealth = stats.maxHealth + AbilityDerivation.MaxHealthBonus(scores);

            // Relic modifiers land on the FINAL figures, after the ability
            // scores have contributed -- a +15% attack relic is 15% of what the
            // character actually swings with, not of a base nobody sees.
            //
            // Attack carries NO ability-score term any more (Strength derives
            // nothing until Phase 3 wires weapon power in -- see
            // AbilityDerivation's header); `stats.attack` alone is what this
            // path swings for, same as the save-backed overload's `total`
            // already reflects via DerivedStats.
            var state = new CombatantState(definition.Data.DisplayName, true,
                RelicModifiers.Apply(maxHealth, RelicStat.MaxHealth, modifiers),
                ContentDatabase.BuildPrimaryPool(definition.Data.PrimaryPoolId, scores, modifiers, stats.manaRegen),
                RelicModifiers.Apply(stats.attack, RelicStat.Attack, modifiers),
                RelicModifiers.Apply(AbilityDerivation.BaseSpeed(stats, scores), RelicStat.Speed, modifiers));

            state.ArmorPenetration = RelicModifiers.Apply(0, RelicStat.ArmorPenetration, modifiers);

            // Balance pass 2 -- same reasoning as the save-backed overload
            // above.
            state.WeaknessMultiplierBonusPercent = RelicModifiers.Apply(0, RelicStat.WeaknessBonus, modifiers);
            state.RelicLifestealPercent = RelicModifiers.Apply(0, RelicStat.Lifesteal, modifiers);

            state.AbilityScores = scores;

            // Same "Defence applies to both broad Defenses" reading the
            // save-backed overload above uses -- and, like health/mana above,
            // BASE PLUS DERIVED: Constitution/Wisdom now feed these two
            // (AbilityDerivation D2), and the save-backed overload picks that
            // up automatically through DerivedStats. This overload builds the
            // CombatantState by hand instead of through a summed StatBlock, so
            // it has to add the same two terms explicitly or a tooling-only
            // fight would under-mitigate relative to a real one.
            state.PhysicalDefense = RelicModifiers.Apply(
                stats.physicalDefense + AbilityDerivation.PhysicalDefenseBonus(scores), RelicStat.Defence, modifiers);
            state.MagicalDefense = RelicModifiers.Apply(
                stats.magicalDefense + AbilityDerivation.MagicalDefenseBonus(scores), RelicStat.Defence, modifiers);

            // The same relic-typed-resistance line the save-backed overload
            // has -- missed here even though every other relic modifier above
            // (health/mana/attack/defense/speed) already applies symmetrically
            // on both paths. A ResistanceFlat relic drafted while this overload
            // builds the party -- the placeholder/tooling fight, or any real
            // fight where a party member has no matching save Character --
            // silently did nothing. Found by EveryRelicReachesCombatTests,
            // which drafts every relic alone and checks every stat moved.
            state.TypedResistance = RelicModifiers.ApplyResistance(state.TypedResistance, modifiers);

            if (definition.Data.HasSignatureResource)
            {
                state.SignaturePool = new ResourcePool(
                    definition.Data.SignatureId, definition.Data.SignatureDisplayName,
                    definition.Data.SignatureCapacity, definition.Data.SignatureGainPerTurn,
                    definition.Data.SignatureGainOnAttack, definition.Data.SignatureGainOnDamageTaken,
                    absorbsDamage: definition.Data.SignatureAbsorbsDamage);
            }

            // NO EQUIVALENT ModifierEffects LINE HERE, EXPLICITLY. This
            // overload has no Character — only a CharacterDefinition — and
            // ContentDatabase.ModifierEffects reads a character's live
            // equipment loadout (modifierIds on each worn EquipmentSlotEntry)
            // to build the set. There is no equipment to read here, so
            // state.ModifierEffects is left at its CombatantState default,
            // ModifierEffectSet.Empty, which is the correct answer rather
            // than an omission — the tooling party never had gear-derived
            // effects to represent. Recorded explicitly (rather than left
            // silent) per the plan's own note that a prior redesign phase
            // found and fixed exactly this class of bug — the save-backed
            // and tooling-only overloads quietly disagreeing about what a
            // seam applies — for TypedResistance and EffectiveWeaponScaling.

            // SAME REASONING, for Talents. ContentDatabase.TalentEffects
            // reads a Character's unlockedTalentIds, and this overload has
            // no Character -- only a bare CharacterDefinition, which has no
            // concept of "unlocked" (a definition-only fight has nothing a
            // player could have invested Embers into). state.Talents is left
            // explicitly at TalentEffectSet.Empty rather than silently
            // inheriting the CombatantState default with no comment marking
            // the decision -- this is the tooling path (no-save fights,
            // screenshot tooling), so there is no save to derive talents
            // from, and that is correct, not a gap.
            state.Talents = TalentEffectSet.Empty;

            return state;
        }

        // ---- building a fight ---------------------------------------------------

        // One fight, from ids. Anything that cannot be found is SKIPPED rather
        // than substituted, and a fight left with no side at all returns null --
        // the caller gets to decide what an unfightable room means, which is not
        // a decision this layer can make.
        public static BuiltFight Build(
            IReadOnlyList<string> partyIds,
            IReadOnlyList<string> enemyIds,
            SeededRandom rng,
            bool isBoss = false,
            bool isElite = false,
            IReadOnlyList<string> relicIds = null,
            int depthStep = 0,
            // The save's own party members, matched to partyIds by
            // definitionId. Supplied by the run; null on the tooling path,
            // which has no save to read a loadout from.
            //
            // When present, every party member is built from their EFFECTIVE
            // figures -- equipment, talents, upgrade levels and all -- instead
            // of from the bare content definition.
            IReadOnlyList<Character> partyCharacters = null,

            // PREVIEW ONLY, and it exists so tools/preview.ps1 -Spell can show
            // a skill nobody has unlocked, bought the book for, or has the
            // level to reach.
            //
            // Appended to the FIRST party member's kit and to nothing else.
            // The alternative -- editing the skills.json row, or writing an
            // unlock onto a save -- would make looking at a spell a thing that
            // changes the game, and an author who previewed a level-20 spell
            // would find their level-1 character still holding it afterwards.
            // A kit is built fresh for each fight and dies with it, which is
            // exactly the lifetime a preview wants. Null on every other path,
            // including the whole of the real game.
            IReadOnlyList<string> previewExtraSkillIds = null)
        {
            var party = new List<CombatantState>();
            var kits = new List<PlayerKit>();
            var art = new List<string>();
            var builtPartyIds = new List<string>();

            // THE RUN'S RELICS, resolved once and given to the whole party.
            //
            // Party-wide rather than per-character because a relic is drafted
            // for the DESCENT, not for a person -- see RelicDraftScreen. Until
            // this existed, KitFor passed null and no relic had ever fired in
            // an actual fight: the three effects were implemented in
            // FightSession, covered by Domain tests that hand-build their own
            // kits, and unreachable from play.
            var relics = ResolveRelics(relicIds);
            var modifiers = relics.SelectMany(r => r.Modifiers).ToList();

            foreach (string id in partyIds ?? new List<string>())
            {
                var definition = ContentDatabase.Characters.FirstOrDefault(c => c.id == id);
                if (definition == null) continue;

                // Matched by definitionId rather than by position, so a party
                // list and a squad list that disagree on order cannot hand one
                // character another's loadout.
                var character = partyCharacters?.FirstOrDefault(
                    c => c != null && c.definitionId == id);

                if (character != null)
                {
                    party.Add(ToCombatant(character, definition, modifiers));
                    kits.Add(KitFor(character, definition, relics));
                }
                else
                {
                    party.Add(ToCombatant(definition, modifiers));
                    kits.Add(KitFor(definition, relics));
                }

                // The first member only -- see previewExtraSkillIds' own note.
                // Done here rather than inside either KitFor overload because
                // both would need it and neither knows whether it is building
                // the caster or a squad-mate.
                if (kits.Count == 1 && previewExtraSkillIds != null && previewExtraSkillIds.Count > 0)
                {
                    kits[0] = WithPreviewSkills(kits[0], previewExtraSkillIds);
                }

                art.Add(definition.Data.BattleSpritePath);
                builtPartyIds.Add(definition.id);
            }

            var enemies = new List<CombatantState>();
            var enemyKits = new List<EnemyKit>();

            foreach (string id in enemyIds ?? new List<string>())
            {
                var definition = ContentDatabase.Enemies.FirstOrDefault(e => e.id == id);
                if (definition == null) continue;

                enemies.Add(ToCombatant(definition, depthStep, isElite));
                enemyKits.Add(EnemyKitFor(definition, isElite));
            }

            if (party.Count == 0 || enemies.Count == 0) return null;

            var encounter = new CombatEncounter(party, enemies);

            // A CLOSURE OVER THIS BUILD'S OWN depthStep/isElite, so a rat
            // called in on floor 20 by a Roar scales exactly like one that
            // was in the room from the start -- the same ToCombatant/
            // EnemyKitFor pair every enemy in `enemies` above was just built
            // through, not a second, drifting copy of that logic.
            bool SummonFactory(string enemyId, out CombatantState state, out EnemyKit kit)
            {
                var definition = ContentDatabase.Enemies.FirstOrDefault(e => e.id == enemyId);
                if (definition == null)
                {
                    state = null;
                    kit = null;
                    return false;
                }

                state = ToCombatant(definition, depthStep, isElite);
                kit = EnemyKitFor(definition, isElite);
                return true;
            }

            var session = new FightSession(encounter, kits, enemyKits, rng, isBoss, isElite,
                summonFactory: SummonFactory);

            // Mechanic (d): whatever Amassing Star has banked onto the run
            // so far applies to THIS fight too, not just the ones after the
            // kill that earned it.
            session.RunWideBonusDamagePercent = RunManager.Run?.bonusDamagePercent ?? 0;

            // The Cold One (docs/PLAN_PETTING_ZOO.md): an event's
            // fillSpecialPool buff, in force while the run is still on the leg
            // it was granted on. Resolved HERE, the way the line above is, so
            // the session gets a plain flag and never reads the run.
            var run = RunManager.Run;
            session.FillsSpecialPoolAtTurnStart = run != null
                && EventBuffs.AnyActive(run.eventBuffs, EventBuffs.FillSpecialPool, run.legStartStep);

            return new BuiltFight
            {
                Session = session, Party = party, PartyArt = art, PartyIds = builtPartyIds,
            };
        }

        // A MONSTER'S KIT, with its abilities looked up.
        //
        // THE ONE PLACE THAT LEGITIMATELY SEES BOTH CATALOGUES. Enemies and
        // skills resolve independently and in no guaranteed order, so a monster
        // carries ability IDS out of content and the skills behind them are
        // found here -- the same file, and the same moment, that already turns a
        // character definition into a PlayerKit.
        //
        // A missing id is DROPPED rather than fatal. ContentBuilder refuses one
        // at build time, so reaching this with a broken id means content that
        // was edited past the guard; losing one ability beats losing the fight.
        private static EnemyKit EnemyKitFor(EnemyDefinition definition, bool isElite)
        {
            var source = Resolve(definition);
            if (source.Abilities == null || source.Abilities.Length == 0)
            {
                // No list authored: the kit synthesises the legacy pool itself.
                return new EnemyKit(source, isElite);
            }

            var pool = new List<EnemyAbility>();

            // The basic attack, always in the mix unless authored out.
            if (source.AttackWeight > 0f)
            {
                pool.Add(EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, source.AttackWeight));
            }

            foreach (var reference in source.Abilities)
            {
                var skill = ContentDatabase.Skills.FirstOrDefault(sk => sk != null && sk.id == reference.SkillId);
                if (skill == null)
                {
                    Debug.LogWarning(
                        $"[FightEncounterAdapter] Enemy '{definition.id}' names ability " +
                        $"'{reference.SkillId}', which no skill matches. Dropping it.");
                    continue;
                }

                pool.Add(EnemyAbility.Of(Resolve(skill), reference.Weight));
            }

            // Every entry dropped or weighted out leaves nothing to draw, and a
            // monster that cannot act is worse than one that only swings.
            if (pool.Count == 0)
            {
                pool.Add(EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f));
            }

            return new EnemyKit(source, isElite, pool);
        }

        // A copy of a kit with extra skills on the end. PlayerKit's fields are
        // readonly by design -- a kit describes what a combatant walked into
        // the fight with and must not shift under them mid-round -- so the
        // preview builds a new one rather than reaching into the old.
        //
        // Appended rather than merged into the sort: a preview exists to show
        // ONE skill, and having it land wherever unlockLevel/sortOrder happen
        // to put it would leave the author counting rows.
        private static PlayerKit WithPreviewSkills(PlayerKit kit, IReadOnlyList<string> skillIds)
        {
            var skills = kit.Skills.ToList();

            foreach (string id in skillIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (skills.Any(s => s != null && s.Id == id)) continue;

                var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == id);
                if (definition == null) continue;

                skills.Add(Resolve(definition));
            }

            return new PlayerKit(kit.Id, kit.Role, skills, kit.Relics, kit.AttackType,
                kit.Level, kit.SkillPowerMultiplier, kit.PlateTheme, kit.PlateArt, kit.Facing);
        }

        private static PlayerKit KitFor(CharacterDefinition definition,
                                        IReadOnlyList<ResolvedRelic> relics,
                                        int level = 1)
        {
            // The character's own strip, plus whichever basic spell tier their
            // level grants. Both are looked up here rather than carried on the
            // definition, so adding a skill stays a line in skills.json.
            //
            // THROUGH ContentDatabase.SkillsUnlockedByLevel now, not a second
            // hand-rolled copy of it. This used to restate the predicate
            // itself, with its own "!s.bookOnly is belt and braces" line
            // explaining why a book-only entry's unlockLevel of int.MaxValue
            // already excluded it without that extra clause -- true, but it
            // was still a second copy of a rule one place should own. One
            // hand-rolled copy of this predicate already dropped the level
            // filter entirely once (see the Character overload below for what
            // that cost); the shared function is what stops the next copy
            // from being able to.
            //
            // No run and no talent list here (no Character record to read
            // them off), so this is the base predicate alone -- the
            // Character overload below unions it with what a talent, a book,
            // or an Event room's teaching adds on top.
            var skills = ContentDatabase.SkillsUnlockedByLevel(definition.id, level)
                .Select(Resolve)
                .ToList();

            return new PlayerKit(definition.id, definition.Data.Role, skills, relics,
                definition.Data.AttackType, level, DefinitionOnlySkillPowerMultiplier(level),
                definition.Data.PlateTheme, definition.Data.PlateArt, definition.Data.BattleSpriteFacing);
        }

        // Test-only door to the overload above, named ...ForTest per house
        // convention (FightBeatPlayer.WireStageForTest, FightController.
        // StageShakesForTest) since Core's InternalsVisibleTo names only the
        // Editor assembly and a PlayMode test sits outside that grant. Skill
        // ids only, rather than the whole PlayerKit: that is the one thing
        // commit 2 is actually about, and it is what a caller can compare
        // against ContentDatabase.SkillsUnlockedByLevel without also having
        // to fake a relic list.
        public static IReadOnlyList<string> SkillIdsForDefinitionForTest(CharacterDefinition definition, int level) =>
            KitFor(definition, System.Array.Empty<ResolvedRelic>(), level).Skills.Select(s => s.Id).ToList();

        // The IN-RUN kit: the character's own strip PLUS whatever their tree
        // granted them, at their real level.
        //
        // THROUGH ContentDatabase.AvailableSkillsFor, which is the one place
        // that knows what "this character can press this" means -- unlocked by
        // level, OR taught outright (unlockedSkillIds), OR granted by a talent,
        // and player-selectable either way.
        //
        // It used to hand-roll the union here, and the hand-rolled version
        // dropped the level filter entirely: every skill authored against the
        // character id went into the kit regardless of unlockLevel. The whole
        // convention that marks a skill "granted rather than earned" is
        // authoring it at level 999 (see AvailableSkillsFor's own header), so
        // dropping that filter handed a level-1 Shawn the entire talent tree's
        // worth of abilities for free -- Ward, Shatter, Wail, all three Gifts,
        // Provoke, Headbutt and Black Ram Mode, plus golden_fleece (unlockLevel
        // 8 at the time; later made bookOnly, then removed 2026-09-15,
        // AUDIT #150) seven levels early. The balance bot found it from the
        // outside: a level-2 run with
        // an empty talentIds list recorded fleece_ward, gift_haste and shatter
        // in skillsUsed, and the defensive policy spent 41% of its deep-fight
        // turns on a Ward it had never bought.
        //
        // AvailableSkillsFor had no production caller at all before this, which
        // is why nothing caught the divergence: the correct answer existed and
        // the fight asked a second, wrong copy of the question instead.
        private static PlayerKit KitFor(Character character, CharacterDefinition definition,
                                        IReadOnlyList<ResolvedRelic> relics)
        {
            // PHASE 3: SpellCostDelta/SkillCostDelta/SkillFlatDelta applied
            // ONCE HERE, at kit-build time -- the same "the kit is built once
            // per fight" seam docs/archive/PLAN_REWARD_TRACKS.md §3j already
            // establishes for AvailableSkillsFor itself. Every later reader
            // of a kit skill (affordability, the charge, the HUD's cost
            // label) sees the same already-discounted ResolvedSkill, so
            // there is nowhere downstream that could read the authored cost
            // instead of the collected one by accident.
            var skills = ContentDatabase.AvailableSkillsFor(character)
                .Select(Resolve)
                .Select(s => ContentDatabase.ApplyRewardTrackSkillDeltas(character, s))
                .ToList();

            return new PlayerKit(definition.id, definition.Data.Role, skills, relics,
                definition.Data.AttackType, character.level, ContentDatabase.EffectiveSkillPowerMultiplier(character),
                definition.Data.PlateTheme, definition.Data.PlateArt, definition.Data.BattleSpriteFacing);
        }

        // THE SPELL TIER'S OWN powerMultiplier, kept even though the tier's
        // name/mana cost/DisplayName it used to travel with (as PlayerKit.
        // BasicSpell) did not survive the flip. FightSession.Skills.cs'
        // ResolveDamageInstances/PreviewSkillPower multiply every FIXED-
        // damage skill (frost_flare, lightning_bolt) by this alongside
        // EffectiveSkillScaling's INT/WIS grade -- "the two never stood in
        // for each other" is that file's own words for why dropping this to
        // 1 would have been a silent damage nerf, not a cleanup.
        //
        // THROUGH ContentDatabase, not a private re-walk of SpellTiers: the
        // in-run overload above uses EffectiveSkillPowerMultiplier, which is
        // ability-score-aware (a tier's `requirements` can gate it, same as
        // AvailableSkillsFor's own level gate). A first cut of this method
        // re-implemented the tier lookup locally, level-only, with no scores
        // parameter -- silently correct today only because no authored tier
        // carries a non-zero requirement yet, and a live divergence from
        // every other Effective* consumer the moment one does. This overload
        // has no Character to ask (see its own header: "no run to ask"), so
        // it falls back to the level-only GetSpellTierForLevel instead --
        // still one canonical lookup, not a second one.
        private static float DefinitionOnlySkillPowerMultiplier(int level) =>
            ContentDatabase.GetSpellTierForLevel(level)?.Data.PowerMultiplier ?? 1.5f;

        // The other half of the content conversion, and it is no longer a
        // conversion at all.
        //
        // This WAS a hand-written copy of 34 fields out of SkillDefinition and
        // back into a ResolvedSkill, and its own comment called it "mechanical,
        // field for field" -- which is exactly the kind of copy where a missing
        // line is invisible. Two lines went missing. `transform` was written
        // onto the asset by ContentBuilder and never read back here, so every
        // skill resolved for a fight -- including the player's own -- arrived
        // with a null grant and Black Ram Mode did nothing at all. `bookOnly`
        // and `bookTier` were dropped the same way afterwards, defaulting to
        // false/0 for every skill in every fight.
        //
        // The fix was the one SpellPresentation already made for the six VFX
        // fields, one level up: the asset now STORES the ResolvedSkill, so
        // there is nothing here to drop. ContentRoundTripTests still guards
        // the journey end to end.
        public static ResolvedSkill Resolve(SkillDefinition definition) => definition.Data;
    }
}
