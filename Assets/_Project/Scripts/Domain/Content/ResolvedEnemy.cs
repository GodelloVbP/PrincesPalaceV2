using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
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
        public readonly DamageType Weakness;
        public readonly DamageType Resistance;
        public readonly int SortOrder;

        // 0 means this enemy carries no stagger meter and cannot be broken.
        public readonly int BreakShieldPoints;

        // Resources-relative FOLDER holding this monster's stance
        // sprites (e.g. "Enemies/golem", which contains idle.png,
        // attack.png and so on). Empty for the many monsters with no art
        // yet — the fight stage falls back to a plain nameplate rather
        // than showing a broken sprite, same graceful-missing-art rule as
        // CharacterDefinition.portraitPath.
        public readonly string SpritePath;

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

        // See RawEnemyEntry.minFloor.
        public readonly int MinFloor;

        public ResolvedEnemy(string id, string displayName, StatBlock baseStats, int expReward, int currencyReward,
            bool isBoss, DamageType weakness, DamageType resistance, int sortOrder, string spritePath = "",
            PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right,
            bool active = true, string skillName = "", float skillPower = 1.5f, float skillChance = 0f,
            int breakShieldPoints = 0, SpellPresentation presentation = null,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0,
            int statusDuration = 0, bool avoidsFrontSlot = false, bool attackHoldsPosition = false,
            int minFloor = 1)
        {
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
            Weakness = weakness;
            Resistance = resistance;
            SortOrder = sortOrder;
            SpritePath = spritePath ?? "";
            BreakShieldPoints = breakShieldPoints;
            Vfx = (presentation ?? SpellPresentation.None).Copy();
            AppliesStatus = appliesStatus;
            StatusMagnitude = statusMagnitude;
            StatusDuration = statusDuration;
            AvoidsFrontSlot = avoidsFrontSlot;
            AttackHoldsPosition = attackHoldsPosition;
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
