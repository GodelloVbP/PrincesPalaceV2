using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // WHAT ONE BASE IS WORTH AT ANY TIER (docs/PLAN_ITEMS.md 2.3). A base is a
    // material x piece or a weapon family; the tier lives on the copy
    // (ItemInstance.Tier), and this turns the two into numbers. Every worn
    // stat, name and icon a copy shows comes out of At, so a generated
    // per-tier asset and a base read at that tier cannot disagree: the
    // resolvers build their per-tier assets by calling this same function.
    //
    // Two authored ends, as before: `*AtZero` is tier 0 and `*AtMax` is
    // `maxTier`, the tier the art ladder tops out at. Between them the curve
    // is ItemSetEntryResolver.ValueAt's geometric shape; past `maxTier` it
    // keeps growing on the same shape (the cap is ItemInstance.TierCap, not
    // the art), while the icon stays on the top frame and the scaling grades
    // stay at their authored top -- a grade is a letter ladder with S at the
    // end, not a curve.
    //
    // Serializable and mutable because it rides on ItemDefinition
    // (.claude/rules/content.md). Written once by the resolver.
    [Serializable]
    public struct GearCurve
    {
        // The tier the *AtMax ends sit at. Zero is a flat curve: every tier
        // reads the *AtZero end (a hand-authored item from items.json).
        public int maxTier;

        public StatBlock statsAtZero;
        public StatBlock statsAtMax;

        public AbilityScoreBlock scoresAtZero;
        public AbilityScoreBlock scoresAtMax;

        public AbilityScoreBlock requiresAtZero;
        public AbilityScoreBlock requiresAtMax;

        // Flat weapon Attack. Zero at both ends on armour. Climbs on
        // GearScaling.WeaponTierGrowth; everything else on TierGrowth.
        public int attackAtZero;
        public int attackAtMax;

        // The plain-Attack grade ladder, every score at once (a score no axis
        // names holds the same grade at both ends).
        public ScalingProfile scalingAtZero;
        public ScalingProfile scalingAtMax;

        // Not tiered.
        public ScalingProfile spellScaling;

        // "Leather Coif", "Sword": the name without the tier adjective.
        public string baseName;
        public string[] tierAdjectives;
        public string fallbackAdjective;

        // Per-level art when `iconSheet` is set, else the flat `iconPath`.
        public string iconSheet;
        public int iconLevels;
        public string iconPath;

        // One fixed definition: the same numbers at every tier.
        public static GearCurve Flat(string name, StatBlock stats, int attack,
            AbilityScoreBlock scores, AbilityScoreBlock requires, string iconPath)
        {
            return new GearCurve
            {
                maxTier = 0,
                statsAtZero = stats,
                statsAtMax = stats,
                scoresAtZero = scores,
                scoresAtMax = scores,
                requiresAtZero = requires,
                requiresAtMax = requires,
                attackAtZero = attack,
                attackAtMax = attack,
                baseName = name ?? "",
                tierAdjectives = new string[0],
                fallbackAdjective = "",
                iconSheet = "",
                iconLevels = 0,
                iconPath = iconPath ?? "",
            };
        }

        public GearStats At(int tier)
        {
            int t = tier < 0 ? 0 : tier;
            int gradeTier = maxTier > 0 && t > maxTier ? maxTier : t;

            var stats = StatBlock.Zero;
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                stats = stats + StatBlock.ForStat(stat, Value(statsAtZero[stat], statsAtMax[stat], t));
            }

            var scaling = ScalingProfile.None;
            foreach (AbilityScore score in AbilityScores.All)
            {
                scaling = scaling.With(score,
                    ScalingGrades.FromIndex(Value((int)scalingAtZero[score], (int)scalingAtMax[score], gradeTier)));
            }

            string adjective = ItemNaming.AdjectiveAt(tierAdjectives, t, fallbackAdjective ?? "");
            string icon = !string.IsNullOrWhiteSpace(iconSheet)
                ? ItemSetEntryResolver.IconPathFor(iconSheet, t, maxTier,
                    iconLevels > 0 ? iconLevels : ItemSetEntryResolver.DefaultIconLevels)
                : (iconPath ?? "").Trim();

            return new GearStats(
                t,
                0,
                ItemSetEntryResolver.ValueAt(attackAtZero, attackAtMax, t, maxTier, GearScaling.WeaponTierGrowth),
                stats,
                Scores(scoresAtZero, scoresAtMax, t),
                Scores(requiresAtZero, requiresAtMax, t),
                scaling,
                spellScaling,
                ItemNaming.Compose(adjective, baseName ?? ""),
                icon);
        }

        private int Value(int atZero, int atMax, int tier) =>
            ItemSetEntryResolver.ValueAt(atZero, atMax, tier, maxTier);

        private AbilityScoreBlock Scores(AbilityScoreBlock atZero, AbilityScoreBlock atMax, int tier)
        {
            return new AbilityScoreBlock(
                Value(atZero.strength, atMax.strength, tier),
                Value(atZero.dexterity, atMax.dexterity, tier),
                Value(atZero.constitution, atMax.constitution, tier),
                Value(atZero.wisdom, atMax.wisdom, tier),
                Value(atZero.intelligence, atMax.intelligence, tier),
                Value(atZero.charisma, atMax.charisma, tier));
        }
    }

    // One copy's gear numbers: a base at a tier, worn in a slot honed to
    // `Honing`. ContentDatabase.Effective is the one producer outside tests.
    //
    // Honing scales every combat stat including the flat Attack
    // (ItemUpgrade), and WeaponPower; it never touches ability scores, which
    // feed derived stats that are already scaled (AbilityDerivation), nor
    // requirements, nor grades.
    public readonly struct GearStats
    {
        public readonly int Tier;
        public readonly int Honing;

        // Flat weapon Attack at this tier, before honing.
        public readonly int AttackBonus;

        public readonly AbilityScoreBlock AbilityScoreBonus;
        public readonly AbilityScoreBlock Requirements;
        public readonly ScalingProfile Scaling;
        public readonly ScalingProfile SpellScaling;

        // Tier adjective + base name, no plus.
        public readonly string DisplayName;
        public readonly string IconPath;

        private readonly StatBlock _stats;

        public GearStats(int tier, int honing, int attackBonus, StatBlock stats,
            AbilityScoreBlock abilityScoreBonus, AbilityScoreBlock requirements,
            ScalingProfile scaling, ScalingProfile spellScaling, string displayName, string iconPath)
        {
            Tier = tier;
            Honing = honing < 0 ? 0 : honing;
            AttackBonus = attackBonus;
            _stats = stats;
            AbilityScoreBonus = abilityScoreBonus;
            Requirements = requirements;
            Scaling = scaling;
            SpellScaling = spellScaling;
            DisplayName = displayName ?? "";
            IconPath = iconPath ?? "";
        }

        public GearStats WithHoning(int plus) =>
            new GearStats(Tier, plus, AttackBonus, _stats, AbilityScoreBonus, Requirements,
                Scaling, SpellScaling, DisplayName, IconPath);

        // Every combat stat this copy grants while worn, the flat Attack
        // folded in, honed.
        public StatBlock StatBonus
        {
            get
            {
                var withAttack = _stats + new StatBlock(0, 0, AttackBonus);
                if (Honing <= 0)
                {
                    return withAttack;
                }

                var scaled = StatBlock.Zero;
                foreach (StatType stat in Enum.GetValues(typeof(StatType)))
                {
                    scaled = scaled + StatBlock.ForStat(stat, ItemUpgrade.Apply(withAttack[stat], Honing));
                }

                return scaled;
            }
        }

        public int WeaponPower => Stats.WeaponPower.Compute(AttackBonus, Honing);
    }
}
