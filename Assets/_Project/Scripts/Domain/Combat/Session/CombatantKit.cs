using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Everything the session needs to know about a combatant that is NOT part
    // of its mutable state.
    //
    // A deliberately thin record. The plan expected a "flattening layer" to
    // strip Unity types out of the content definitions -- it turned out not to
    // be needed, because Domain/Content already holds ResolvedCharacter /
    // ResolvedEnemy / ResolvedSkill with complete field sets and no mirror
    // enums. So a kit is a small bundle over structs that already exist, and
    // the only real conversion left is Core's SkillDefinition -> ResolvedSkill
    // in the adapter.
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

        // Only player characters carry an attack type; only enemies carry a
        // weakness/resistance. A rudimentary, one-directional system by design.
        public readonly DamageType? AttackType;

        // The character's basic spell, if their level grants one.
        public readonly ResolvedSpellTier? BasicSpell;

        public PlayerKit(string id, CharacterRole role,
                         IReadOnlyList<ResolvedSkill> skills,
                         IReadOnlyList<ResolvedRelic> relics,
                         DamageType? attackType,
                         ResolvedSpellTier? basicSpell = null,
                         int level = 1)
        {
            Level = level;
            Id = id;
            Role = role;
            Skills = skills ?? new List<ResolvedSkill>();
            Relics = relics ?? new List<ResolvedRelic>();
            AttackType = attackType;
            BasicSpell = basicSpell;
        }
    }

    public sealed class EnemyKit
    {
        public readonly ResolvedEnemy Source;

        // Elite is a per-encounter decision, not a property of the monster, so
        // it rides the kit rather than the definition.
        public readonly bool IsElite;

        public EnemyKit(ResolvedEnemy source, bool isElite)
        {
            Source = source;
            IsElite = isElite;
        }

        public DamageType Weakness => Source.Weakness;
        public DamageType Resistance => Source.Resistance;
    }
}
