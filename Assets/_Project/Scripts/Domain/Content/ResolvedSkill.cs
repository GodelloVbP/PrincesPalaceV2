using System;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated character skill — the shape ContentBuilder needs to
    // create a SkillDefinition asset from.
    //
    // No mirror enums: SkillEffect and SkillTargeting live in Domain and are
    // used directly on both sides, the way DamageType and EquipmentSlot
    // already are. ResolvedItemKind exists only because ItemKind is a
    // Content-layer enum, which is a different situation.
    public readonly struct ResolvedSkill
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string CharacterId;
        public readonly int UnlockLevel;
        public readonly SkillEffect Effect;
        public readonly SkillTargeting Targeting;
        public readonly int ManaCost;
        public readonly int ResourceCost;
        public readonly bool SpendsAllResource;
        public readonly int Power;
        public readonly int FlatAmount;
        public readonly bool IgnoresDefense;

        // Empty for a skill that scales off Attack; non-empty for one that
        // deals fixed typed packets instead.
        public readonly DamageInstance[] DamageInstances;
        public readonly string VfxPath;
        public readonly float VfxSeconds;
        public readonly int VfxImpactFrame;
        public readonly string SfxPath;

        public readonly int SortOrder;

        // Null means this skill applies no status. Landed on whoever Effect
        // already resolves against — see RawSkillEntry.appliesStatus.
        public readonly StatusEffectType? AppliesStatus;
        public readonly int StatusMagnitude;
        public readonly int StatusDuration;

        // What a character needs, from base scores/talents/worn gear, before
        // this skill can be cast at all — see RequirementResolver and
        // FightController.Hud's own companion flag beside `affordable`.
        public readonly AbilityScoreBlock Requirements;

        // Which axis this skill's damage rides — see RawSkillEntry.
        // scalingAxis. Auto (the default) derives it from the caster's own
        // attackType at cast time; this only ever carries an explicit
        // override.
        public readonly ScalingAxis ScalingAxis;

        // How many places later in the queue this skill knocks its target; 0
        // for everything that does not touch the turn order.
        public readonly int QueuePushSlots;

        // What a Transform skill turns the caster into. Null for every other
        // skill — the resolver only ever fills this in for the one effect
        // that reads it, so a stray authored block is an error rather than a
        // silently ignored one.
        public readonly TransformGrant Transform;

        public bool HasFixedDamage => DamageInstances != null && DamageInstances.Length > 0;

        public ResolvedSkill(string id, string displayName, string description, string characterId,
            int unlockLevel, SkillEffect effect, SkillTargeting targeting, int manaCost,
            int resourceCost, bool spendsAllResource, int power, int flatAmount, bool ignoresDefense,
            DamageInstance[] damageInstances, string vfxPath, float vfxSeconds, int vfxImpactFrame, string sfxPath, int sortOrder,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0, int statusDuration = 0,
            AbilityScoreBlock requirements = default, ScalingAxis scalingAxis = ScalingAxis.Auto,
            int queuePushSlots = 0, TransformGrant transform = null)
        {
            QueuePushSlots = queuePushSlots;
            Transform = transform;
            DamageInstances = damageInstances ?? Array.Empty<DamageInstance>();
            VfxPath = vfxPath ?? "";
            VfxSeconds = vfxSeconds;
            VfxImpactFrame = vfxImpactFrame;
            SfxPath = sfxPath ?? "";
            Id = id;
            DisplayName = displayName;
            Description = description;
            CharacterId = characterId;
            UnlockLevel = unlockLevel;
            Effect = effect;
            Targeting = targeting;
            ManaCost = manaCost;
            ResourceCost = resourceCost;
            SpendsAllResource = spendsAllResource;
            Power = power;
            FlatAmount = flatAmount;
            IgnoresDefense = ignoresDefense;
            SortOrder = sortOrder;
            AppliesStatus = appliesStatus;
            StatusMagnitude = statusMagnitude;
            StatusDuration = statusDuration;
            Requirements = requirements;
            ScalingAxis = scalingAxis;
        }
    }
}
