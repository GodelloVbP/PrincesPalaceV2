using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // A monster's ability, before the skill behind it has been looked up. See
    // ResolvedEnemy.Abilities for why the id survives this far.
    public readonly struct EnemyAbilityRef
    {
        public readonly string SkillId;
        public readonly float Weight;

        public EnemyAbilityRef(string skillId, float weight)
        {
            SkillId = (skillId ?? "").Trim();
            Weight = weight;
        }
    }

    // A monster after every omitted field in its RawEnemyEntry has been
    // filled in and every provided field has been validated — the shape
    // ContentBuilder actually needs to create an EnemyDefinition asset from.
    public readonly struct ResolvedEnemy
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly StatBlock BaseStats;
        public readonly int ExpReward;
        public readonly int CurrencyReward;
        public readonly bool IsBoss;

        // Every element this monster takes badly and every one it shrugs off.
        // Two DamageType fields until multi-element affinities landed -- see
        // ElementalAffinity for why the pair became one value rather than two
        // arrays.
        public readonly ElementalAffinity Affinity;
        public readonly int SortOrder;

        // 0 means this enemy carries no stagger meter and cannot be broken.
        public readonly int BreakShieldPoints;

        // See RawEnemyEntry.stageScale and .slotSpan. Both default to 1, so a
        // monster that says nothing stands exactly as it always has.
        public readonly float StageScale;
        public readonly int SlotSpan;

        // Resources-relative FOLDER holding this monster's stance
        // sprites (e.g. "Enemies/golem", which contains idle.png,
        // attack.png and so on). Empty for the many monsters with no art
        // yet — the fight stage falls back to a plain nameplate rather
        // than showing a broken sprite, same graceful-missing-art rule as
        // CharacterDefinition.portraitPath.
        public readonly string SpritePath;

        // WHAT THIS MONSTER CAN DO, as skill ids and relative weights.
        //
        // IDS RATHER THAN RESOLVED SKILLS, deliberately. Enemies and skills are
        // two catalogues resolved independently and in no guaranteed order, so
        // holding a ResolvedSkill here would make one resolver depend on the
        // other having finished. The lookup happens where player kits are
        // already built from content -- FightEncounterAdapter -- which is the
        // one place that legitimately sees both.
        //
        // Empty means this monster uses the legacy SkillName trio below.
        public readonly IReadOnlyList<EnemyAbilityRef> Abilities;

        // The basic attack's own weight in that pool. See RawEnemyEntry.
        public readonly float AttackWeight;

        // Empty SkillName means this monster has no second action at all.
        public readonly string SkillName;
        public readonly float SkillPower;
        public readonly float SkillChance;

        // Which way SpritePath's art is drawn. The stage mirrors from this
        // rather than assuming a house convention — see StageFacing.
        public readonly PrincesPalace.Domain.Stage.SpriteFacing Facing;

        // False means benched: the entry is still authored and validated,
        // but ContentBuilder builds no asset for it, so it never spawns.
        public readonly bool Active;

        // A skill's VFX, shown over the TARGET when it lands — see
        // RawEnemyEntry's own comment. Empty VfxPath means no prop.
        // ONE VALUE, like a skill's. See SpellPresentation for the
        // measurement that collapsed the four fields that were here.
        public readonly SpellPresentation Vfx;

        // The status this monster's attacks apply on a hit, regardless of
        // whether that hit was the skill or a plain attack. Null means it
        // applies nothing — see RawEnemyEntry's own comment on why this
        // isn't skill-gated the way SkillDefinition's own status is.
        public readonly StatusEffectType? AppliesStatus;
        public readonly int StatusMagnitude;
        public readonly int StatusDuration;

        // See RawEnemyEntry.avoidsFrontSlot.
        public readonly bool AvoidsFrontSlot;

        // See RawEnemyEntry.attackHoldsPosition.
        public readonly bool AttackHoldsPosition;

        // See RawEnemyEntry.attackApproach. How the plain attack travels when it
        // is not holding position. Lunge is the default and the old behaviour.
        public readonly PrincesPalace.Domain.Combat.Session.StageApproach AttackApproach;

        // See RawEnemyEntry.minFloor.
        public readonly int MinFloor;

        // The damage type this monster's own attacks and abilities carry —
        // see RawEnemyEntry.attackType. Defaults to Physical, same as an
        // unauthored player character's attackType (CharacterEntryResolver).
        // Enemies have no CombatantKit the way players' PlayerKit carries
        // one, so this is what FightSession.ActorAttackType/AttackTypeOf
        // fall through to for a combatant with no player kit registered —
        // without it, MagicalDefense was a dead stat against every enemy in
        // the game, since nothing an enemy did was ever typed.
        public readonly DamageType AttackType;

        // THE SINGLE-ELEMENT CONVENIENCE, kept because it is how nearly every
        // fixture reads. NOT because the roster is authored through it -- both
        // production callers (EnemyEntryResolver and FightEncounterAdapter)
        // build an ElementalAffinity and use the other constructor, so nothing
        // shipped reaches this. It is one line of delegation, not a second
        // definition of anything: both forms end up in the same Affinity field,
        // so there is no shape here that can drift out of step with the other.
        public ResolvedEnemy(string id, string displayName, StatBlock baseStats, int expReward, int currencyReward,
            bool isBoss, DamageType weakness, DamageType resistance, int sortOrder, string spritePath = "",
            PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right,
            bool active = true, string skillName = "", float skillPower = 1.5f, float skillChance = 0f,
            int breakShieldPoints = 0, SpellPresentation presentation = null,
            IReadOnlyList<EnemyAbilityRef> abilities = null, float attackWeight = 1f,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0,
            int statusDuration = 0, bool avoidsFrontSlot = false, bool attackHoldsPosition = false,
            int minFloor = 1, float stageScale = 1f, int slotSpan = 1,
            PrincesPalace.Domain.Combat.Session.StageApproach attackApproach =
                PrincesPalace.Domain.Combat.Session.StageApproach.Lunge,
            DamageType attackType = DamageType.Physical)
            : this(id, displayName, baseStats, expReward, currencyReward, isBoss,
                   ElementalAffinity.Of(weakness, resistance), sortOrder, spritePath, facing, active,
                   skillName, skillPower, skillChance, breakShieldPoints, presentation, abilities,
                   attackWeight, appliesStatus, statusMagnitude, statusDuration, avoidsFrontSlot,
                   attackHoldsPosition, minFloor, stageScale, slotSpan, attackApproach, attackType)
        {
        }

        public ResolvedEnemy(string id, string displayName, StatBlock baseStats, int expReward, int currencyReward,
            bool isBoss, ElementalAffinity affinity, int sortOrder, string spritePath = "",
            PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right,
            bool active = true, string skillName = "", float skillPower = 1.5f, float skillChance = 0f,
            int breakShieldPoints = 0, SpellPresentation presentation = null,
            IReadOnlyList<EnemyAbilityRef> abilities = null, float attackWeight = 1f,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0,
            int statusDuration = 0, bool avoidsFrontSlot = false, bool attackHoldsPosition = false,
            int minFloor = 1, float stageScale = 1f, int slotSpan = 1,
            PrincesPalace.Domain.Combat.Session.StageApproach attackApproach =
                PrincesPalace.Domain.Combat.Session.StageApproach.Lunge,
            DamageType attackType = DamageType.Physical)
        {
            // CLAMPED RATHER THAN TRUSTED, both of them. A zero or negative
            // scale is an invisible monster and a zero span is a room that
            // never fills, and neither is worth a crash or a mystery.
            StageScale = stageScale > 0f ? stageScale : 1f;
            SlotSpan = slotSpan > 0 ? slotSpan : 1;
            SkillName = skillName ?? string.Empty;
            SkillPower = skillPower;
            SkillChance = skillChance;
            Facing = facing;
            Active = active;
            Id = id;
            DisplayName = displayName;
            BaseStats = baseStats;
            ExpReward = expReward;
            CurrencyReward = currencyReward;
            IsBoss = isBoss;

            // Clamped up, not trusted: 0 from an unbanded entry means the
            // first floor, and a negative would let a filter of the form
            // `minFloor <= floor` pass everything forever.
            MinFloor = minFloor < 1 ? 1 : minFloor;
            Affinity = affinity;
            SortOrder = sortOrder;
            SpritePath = spritePath ?? "";
            BreakShieldPoints = breakShieldPoints;
            Vfx = (presentation ?? SpellPresentation.None).Copy();
            Abilities = abilities ?? Array.Empty<EnemyAbilityRef>();
            AttackWeight = attackWeight < 0f ? 0f : attackWeight;
            AppliesStatus = appliesStatus;
            StatusMagnitude = statusMagnitude;
            StatusDuration = statusDuration;
            AvoidsFrontSlot = avoidsFrontSlot;
            AttackHoldsPosition = attackHoldsPosition;
            AttackApproach = attackApproach;
            AttackType = attackType;
        }

        // "Authored a skill" is a name, not a chance: an enemy with a named
        // skill and skillChance 0 still HAS one (a future rule could raise the
        // chance), while a blank name with a chance of 1 is nothing to use.
        public bool HasSkill => !string.IsNullOrWhiteSpace(SkillName);

        // Ported from v1's EnemyDefinition, which derived both the same way at
        // the same place. They live here rather than at the call site so the
        // enemy turn does not re-decide what "has a skill" means each time.
        public bool HasStatus => AppliesStatus.HasValue && StatusDuration > 0;
    }
}
