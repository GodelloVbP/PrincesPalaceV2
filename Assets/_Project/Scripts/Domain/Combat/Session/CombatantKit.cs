using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Combat.Session
{
    // Everything the session needs to know about a combatant that is NOT part
    // of its mutable state.
    //
    // A deliberately thin record. The plan expected a "flattening layer" to
    // strip Unity types out of the content definitions -- it turned out not to
    // be needed, because Domain/Content already holds ResolvedCharacter /
    // ResolvedEnemy / ResolvedSkill with complete field sets and no mirror
    // enums. So a kit is a small bundle over values that already exist. The
    // last real conversion, Core's SkillDefinition -> ResolvedSkill, is gone
    // too: the asset stores the ResolvedSkill and the adapter hands it back.
    //
    // Kits carry COMBAT facts only. Portraits, sprite folders and voice clip
    // keys are view concerns and live in adapter-built parallel arrays -- v1's
    // `_playerOwners`/`_enemySources` dictionaries did both jobs at once, which
    // is a large part of why combat logic could not leave the controller.
    public sealed class PlayerKit
    {
        // The save-side identity this combatant writes back to, and the key the
        // view uses to find its portrait and voice takes.
        public readonly string Id;

        public readonly CharacterRole Role;

        // Printed on the party plate beside the role. Carried on the kit rather
        // than read back off a save at paint time: the plate describes the
        // character AS THEY ENTERED THIS FIGHT, and a level-up mid-round would
        // otherwise change the plate before the beat that earned it played.
        public readonly int Level;
        public readonly IReadOnlyList<ResolvedSkill> Skills;
        public readonly IReadOnlyList<ResolvedRelic> Relics;

        // Which effects this kit's Relics carry, resolved once here rather
        // than rescanned on every HasRelic call -- DealDamage and the
        // after-swing/cast hooks ask up to ~10 times per hit.
        private readonly HashSet<RelicEffect> _relicEffects;

        // Only player characters carry an attack type; only enemies carry an
        // elemental affinity. A rudimentary, one-directional system by design.
        public readonly DamageType? AttackType;

        // BasicSpell removed (docs/PLAN_SHOP.md §4 Phase E, the flip): the
        // automatic, nameless "Skill" action every character used to cast
        // off spells.json's level-keyed ladder regardless of what they had
        // learned. The ladder itself (SpellTierDefinition, EffectiveSkillScaling,
        // GetSpellTierForLevel) is untouched -- it is what every Spell-axis
        // skill's damage still scales through, this field just is not one
        // any more. A character now has exactly the skills their kit
        // authored plus whatever they learned from a book.

        // NOT PART OF THE BasicSpell REMOVAL -- kept on purpose. The spell
        // tier's own powerMultiplier is a SEPARATE axis from
        // EffectiveSkillScaling's INT/WIS grade (FightSession.Skills.cs'
        // own header calls this out: "the two never stood in for each other
        // and neither replaces the other"), and it is what scales every
        // FIXED-damage-instance skill (frost_flare, lightning_bolt) -- named
        // skills, not the free action that used to carry it. Removing
        // BasicSpell wholesale would have silently dropped this multiplier
        // to 1 for both of them, a real damage nerf nobody asked for and the
        // plan's own F4 did not flag, because it read spells.json as
        // powering only EffectiveSkillScaling. 1f (neutral) for a kit built
        // with no level to ask about, same as the ladder's own "before tier
        // 1" reading.
        public readonly float SkillPowerMultiplier;

        // THE PARTY PLATE'S FRAME, carried on the kit for the same reason
        // Level is: painted off what the character walked into the fight
        // with, not re-read off content at paint time. ResolvedCharacter.
        // PlateTheme (characters.json's plateTheme field), Blue by default --
        // see ResolvedCharacter's own header.
        public readonly ButtonTheme PlateTheme;

        public PlayerKit(string id, CharacterRole role,
                         IReadOnlyList<ResolvedSkill> skills,
                         IReadOnlyList<ResolvedRelic> relics,
                         DamageType? attackType,
                         int level = 1,
                         float skillPowerMultiplier = 1f,
                         ButtonTheme plateTheme = ButtonTheme.Blue)
        {
            Level = level;
            Id = id;
            Role = role;
            Skills = skills ?? new List<ResolvedSkill>();
            Relics = relics ?? new List<ResolvedRelic>();
            AttackType = attackType;
            SkillPowerMultiplier = skillPowerMultiplier;
            PlateTheme = plateTheme;

            _relicEffects = new HashSet<RelicEffect>();
            for (int i = 0; i < Relics.Count; i++)
            {
                _relicEffects.Add(Relics[i].Effect);
            }
        }

        public bool HasRelic(RelicEffect effect) => _relicEffects.Contains(effect);
    }

    public sealed class EnemyKit
    {
        public readonly ResolvedEnemy Source;

        // Elite is a per-encounter decision, not a property of the monster, so
        // it rides the kit rather than the definition.
        public readonly bool IsElite;

        // EVERYTHING THIS MONSTER CAN DO, ready to draw from.
        //
        // Always includes the basic attack, so a monster whose every turn is a
        // special is a thing an author has to ask for (attackWeight 0) rather
        // than the default.
        public readonly IReadOnlyList<EnemyAbility> Abilities;

        public EnemyKit(ResolvedEnemy source, bool isElite,
                        IReadOnlyList<EnemyAbility> abilities = null)
        {
            Source = source;
            IsElite = isElite;
            Abilities = abilities != null && abilities.Count > 0
                ? abilities
                : LegacyPoolFor(source);
        }

        // A MONSTER AUTHORED BEFORE ABILITIES EXISTED, expressed in the same
        // shape as one authored after.
        //
        // The old trio is a two-entry weighted pool and always was: skillChance
        // of the scaled attack, and the remainder of a plain one. Saying it that
        // way means there is ONE list and ONE draw everywhere downstream rather
        // than a branch that has to be remembered at every call site -- and it
        // consumes exactly the draw the old code did, so a seeded run keeps its
        // shape.
        private static IReadOnlyList<EnemyAbility> LegacyPoolFor(ResolvedEnemy source)
        {
            // NULL-TOLERANT since ResolvedEnemy became a class: a kit built
            // without a source still fields a plain attack rather than throwing.
            float chance = source != null && source.HasSkill ? Clamp01(source.SkillChance) : 0f;

            var pool = new List<EnemyAbility>
            {
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f - chance),
            };

            if (chance > 0f)
            {
                pool.Add(EnemyAbility.LegacyAttack(source.SkillName, source.SkillPower, chance));
            }

            return pool;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // What this monster takes badly and what it shrugs off, as one value
        // -- see ElementalAffinity. Two properties for two single elements is
        // what this was, and the pair had to be threaded through every damage
        // call site together anyway.
        public ElementalAffinity Affinity => Source?.Affinity ?? ElementalAffinity.Neutral;
    }
}
