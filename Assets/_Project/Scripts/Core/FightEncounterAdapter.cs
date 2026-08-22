using PrincesPalace.Domain.Dungeon;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
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
        }

        // ---- content -> Domain ------------------------------------------------

        // The one conversion that has to exist. Every field is copied straight
        // across: ResolvedEnemy was designed as the shape EnemyDefinition already
        // had, so there are no mirror enums and no translation to get wrong.
        public static ResolvedEnemy Resolve(EnemyDefinition definition)
        {
            return new ResolvedEnemy(
                definition.id, definition.displayName, definition.baseStats,
                definition.expReward, definition.currencyReward, definition.isBoss,
                definition.weakness, definition.resistance, definition.sortOrder,
                spritePath: definition.spritePath,
                facing: definition.facing,
                active: true,
                skillName: definition.skillName,
                skillPower: definition.skillPower,
                skillChance: definition.skillChance,
                breakShieldPoints: definition.breakShieldPoints,
                vfxPath: definition.vfxPath,
                vfxSeconds: definition.vfxSeconds,
                vfxImpactFrame: definition.vfxImpactFrame,
                sfxPath: definition.sfxPath,

                // hasStatus is the AUTHORING gate, and it is deliberately not the
                // same question as "is a status type set". appliesStatus is a
                // plain enum with a valid zero value, so every enemy has one
                // whether or not anyone meant it to. Reading the flag is what
                // stops every monster in the game inflicting the first entry.
                appliesStatus: definition.hasStatus ? definition.appliesStatus : (StatusEffectType?)null,
                statusMagnitude: definition.statusMagnitude,
                statusDuration: definition.statusDuration,
                avoidsFrontSlot: definition.avoidsFrontSlot,
                attackHoldsPosition: definition.attackHoldsPosition);
        }

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
        private static CombatantState ToCombatant(EnemyDefinition definition, int depthStep)
        {
            var stats = definition.baseStats;

            // Mana is NOT part of StatBlock -- it is derived from ability scores
            // for a character, and monsters have none. They get the default pool
            // so a monster skill that costs mana can still be paid for, rather
            // than a zero that would silently make every such skill unusable.
            // Speed is NOT scaled. It is a rate, feeding a scheduler that
            // clamps at 2.5x anyway, and the same reasoning AbilityDerivation
            // applies to the player's Dexterity applies to a monster: an enemy
            // at 40,000 Speed does not act more often, it acts always.
            var state = new CombatantState(definition.displayName, false,
                DifficultyCurve.ScaleHealth(stats.maxHealth, depthStep),
                GameplayConstants.DefaultMaxMana,
                DifficultyCurve.ScaleAttack(stats.attack, depthStep),
                DifficultyCurve.ScaleAttack(stats.defense, depthStep),
                stats.speed);

            if (definition.breakShieldPoints > 0)
            {
                state.BreakShield = new BreakShield(
                    DifficultyCurve.ScaleShield(definition.breakShieldPoints, depthStep));
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

                resolved.Add(new ResolvedRelic(definition.id, definition.displayName, definition.description,
                    definition.effect, definition.sortOrder, definition.iconPath,
                    definition.rarity, definition.unlockedBy, definition.ToModifiers()));
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

            var state = new CombatantState(definition.displayName, true,
                RelicModifiers.Apply(stats.maxHealth, RelicStat.MaxHealth, modifiers),
                RelicModifiers.Apply(ContentDatabase.EffectiveMaxMana(character), RelicStat.MaxMana, modifiers),
                RelicModifiers.Apply(stats.attack, RelicStat.Attack, modifiers),
                RelicModifiers.Apply(stats.defense, RelicStat.Defence, modifiers),
                RelicModifiers.Apply(stats.speed, RelicStat.Speed, modifiers));

            state.ManaRegen = stats.manaRegen;
            state.AbilityScores = scores;

            // Set by neither path before this. A resistance rolled onto a
            // breastplate was written to the save, shown on the sheet, folded
            // into EffectiveStats -- and then dropped on the way into the one
            // system it exists for.
            state.PhysicalResistance = stats.physicalResistance;
            state.MagicalResistance = stats.magicalResistance;

            // Built from the CHARACTER, so a signature whose capacity a talent
            // widened arrives at that width. Its single call site until now was
            // its own definition.
            state.Signature = ContentDatabase.BuildSignatureResource(character);

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
            var stats = definition.baseStats;
            var scores = definition.baseAbilityScores;

            // Health and mana are BASE PLUS DERIVED, because that is what the
            // ability scores are for -- reading the StatBlock alone would give a
            // character none of the pool their own build earned them.
            int maxHealth = stats.maxHealth + AbilityDerivation.MaxHealthBonus(scores);
            int maxMana = GameplayConstants.DefaultMaxMana + AbilityDerivation.MaxManaBonus(scores);

            // Relic modifiers land on the FINAL figures, after the ability
            // scores have contributed -- a +15% attack relic is 15% of what the
            // character actually swings with, not of a base nobody sees.
            var state = new CombatantState(definition.displayName, true,
                RelicModifiers.Apply(maxHealth, RelicStat.MaxHealth, modifiers),
                RelicModifiers.Apply(maxMana, RelicStat.MaxMana, modifiers),
                RelicModifiers.Apply(stats.attack + AbilityDerivation.AttackBonus(scores), RelicStat.Attack, modifiers),
                RelicModifiers.Apply(stats.defense, RelicStat.Defence, modifiers),
                RelicModifiers.Apply(stats.speed + AbilityDerivation.SpeedBonus(scores), RelicStat.Speed, modifiers));

            state.ManaRegen = stats.manaRegen;
            state.AbilityScores = scores;

            if (definition.HasSignatureResource)
            {
                state.Signature = new SignatureResource(
                    definition.signatureResourceId, definition.signatureResourceDisplayName,
                    definition.signatureResourceCapacity, definition.signatureGainPerTurn,
                    definition.signatureGainOnAttack, definition.signatureGainOnDamageTaken,
                    absorbsDamage: definition.signatureAbsorbsDamage);
            }

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
            IReadOnlyList<Character> partyCharacters = null)
        {
            var party = new List<CombatantState>();
            var kits = new List<PlayerKit>();
            var art = new List<string>();

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

                art.Add(definition.battleSpritePath);
            }

            var enemies = new List<CombatantState>();
            var enemyKits = new List<EnemyKit>();

            foreach (string id in enemyIds ?? new List<string>())
            {
                var definition = ContentDatabase.Enemies.FirstOrDefault(e => e.id == id);
                if (definition == null) continue;

                enemies.Add(ToCombatant(definition, depthStep));
                enemyKits.Add(new EnemyKit(Resolve(definition), isElite));
            }

            if (party.Count == 0 || enemies.Count == 0) return null;

            var encounter = new CombatEncounter(party, enemies);
            var session = new FightSession(encounter, kits, enemyKits, rng, isBoss, isElite);

            return new BuiltFight { Session = session, Party = party, PartyArt = art };
        }

        private static PlayerKit KitFor(CharacterDefinition definition,
                                        IReadOnlyList<ResolvedRelic> relics,
                                        int level = 1)
        {
            // The character's own strip, plus whichever basic spell tier their
            // level grants. Both are looked up here rather than carried on the
            // definition, so adding a skill stays a line in skills.json.
            var skills = ContentDatabase.Skills
                .Where(s => s.characterId == definition.id)
                .OrderBy(s => s.sortOrder)
                .Select(Resolve)
                .ToList();

            // Spell tiers are keyed by LEVEL alone, not by character -- the
            // basic spell is the same ladder for everyone and only its tier
            // differs. Highest available AT OR BELOW the character's level.
            //
            // The level filter is the fix: this read "highest available wins"
            // and took the top of the ladder unconditionally, so a level 1
            // character cast the endgame tier. With no character to ask, level
            // 1 is the honest floor rather than the top.
            var tier = TierAtLevel(level);

            return new PlayerKit(definition.id, definition.role, skills, relics,
                definition.attackType,
                tier == null ? (ResolvedSpellTier?)null : SpellTierFor(tier),
                level);
        }

        // The IN-RUN kit: the character's own strip PLUS whatever their tree
        // granted them, at their real level.
        //
        // TalentGrantedSkillsFor had no call site at all before this, so a
        // talent that granted a skill wrote it nowhere the fight could see.
        private static PlayerKit KitFor(Character character, CharacterDefinition definition,
                                        IReadOnlyList<ResolvedRelic> relics)
        {
            var skills = ContentDatabase.Skills
                .Where(s => s.characterId == definition.id)
                .OrderBy(s => s.sortOrder)
                .Select(Resolve)
                .ToList();

            // Appended, not merged by id: a granted skill the strip already
            // holds would otherwise appear twice in the submenu.
            var granted = ContentDatabase.TalentGrantedSkillsFor(character);
            foreach (var extra in granted)
            {
                if (extra == null) continue;
                if (skills.Any(s => s.Id == extra.id)) continue;
                skills.Add(Resolve(extra));
            }

            var tier = TierAtLevel(character.level);

            return new PlayerKit(definition.id, definition.role, skills, relics,
                definition.attackType,
                tier == null ? (ResolvedSpellTier?)null : SpellTierFor(tier),
                character.level);
        }

        // The highest tier a character of this level has actually reached.
        // Null when the ladder starts above them, which PlayerKit already
        // treats as "no basic spell yet".
        private static SpellTierDefinition TierAtLevel(int level) =>
            ContentDatabase.SpellTiers
                .Where(t => t != null && t.level <= level)
                .OrderByDescending(t => t.level)
                .FirstOrDefault();

        private static ResolvedSpellTier SpellTierFor(SpellTierDefinition tier) =>
            new ResolvedSpellTier(tier.level, tier.displayName, tier.manaCost, tier.powerMultiplier, tier.sortOrder);

        // The other half of the content conversion. Mechanical, field for field:
        // ResolvedSkill was designed as the shape SkillDefinition already had.
        public static ResolvedSkill Resolve(SkillDefinition definition)
        {
            return new ResolvedSkill(
                definition.id, definition.displayName, definition.description,
                definition.characterId, definition.unlockLevel, definition.effect,
                definition.targeting, definition.manaCost, definition.resourceCost,
                definition.spendsAllResource, definition.power, definition.flatAmount,
                definition.ignoresDefense, definition.damageInstances,
                definition.vfxPath, definition.vfxSeconds, definition.vfxImpactFrame,
                definition.sfxPath, definition.sortOrder, definition.vfxFromCaster,

                // Same authoring-gate rule as the enemy conversion: appliesStatus
                // is an enum with a valid zero, so the FLAG is what says whether
                // anyone meant it.
                definition.hasStatus ? definition.appliesStatus : (StatusEffectType?)null,
                definition.statusMagnitude, definition.statusDuration,
                definition.requirements, definition.scalingAxis, definition.queuePushSlots);
        }
    }
}
