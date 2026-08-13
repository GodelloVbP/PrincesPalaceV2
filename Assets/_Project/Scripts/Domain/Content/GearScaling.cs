using System;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Content
{
    // What a piece of armour is worth, as one formula.
    //
    //     value = Base x SlotWeight[slot] x StyleWeight[style][stat] x TierCurve(tier)
    //
    // Everything an armour set grants comes out of here. Before this, every
    // piece authored its own baseStats and topStats by hand, which meant a set
    // was balanced against the other sets only by whoever typed it -- and
    // rebalancing "torso should matter less" was a hundred and ten edits
    // spread across a JSON file.
    //
    // Now itemsets.json names a STYLE WEIGHT per set (what that material is
    // for) and the three global knobs below decide the rest. Retuning the
    // whole economy is editing one number here.
    //
    // A piece may still author baseStats/topStats explicitly, and that wins:
    // the derivation covers the ability scores that make a style a style, and
    // hand-authoring is what a piece uses to say something the formula cannot
    // (steel is slow, silk resists magic).
    public static class GearScaling
    {
        // The value one average slot of a pure single-stat style grants at
        // tier 0. Every other number here is a multiplier on it, so this is
        // the volume knob for gear as a whole.
        public const double Base = 4.0;

        // How much stronger each tier is than the one below it.
        //
        // GEOMETRIC, not linear, and that is the design rather than a detail:
        // 1.25 per tier makes tier 10 worth 9.3x tier 0, so the last floor
        // feels wild. A straight line cannot do that -- it only gets steeper,
        // it never accelerates. The cost is that the first floors are flat in
        // absolute terms (tier 0 to 1 on a torso is under a point), which is
        // what the plus axis exists to cover.
        public const double TierGrowth = 1.25;

        // A slot's share of a full set. These sum to 5.0 across the five
        // armour slots, so a complete set is always worth Base x 5 whatever
        // the mix, and the numbers read directly as percentages: torso 25%,
        // gloves 17%.
        //
        // DELIBERATELY FLAT. A steeper spread (40/20/15/13/12) makes the chest
        // the event and everything else filler -- and with three offers per
        // fight, two of every three would be a consolation prize.
        public static double SlotWeight(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Torso: return 1.25;
                case EquipmentSlot.Legs: return 1.05;
                case EquipmentSlot.Head: return 0.95;
                case EquipmentSlot.Shoes: return 0.90;
                case EquipmentSlot.Gloves: return 0.85;

                // Not armour slots. A necklace is not part of a material set
                // and a weapon comes from weapons.json, which has its own
                // scaling; both return zero so a set that names one by mistake
                // grants nothing and trips the resolver's "grants nothing"
                // check rather than silently inventing a value.
                default: return 0.0;
            }
        }

        // What tier `maxTier` multiplies tier 0 by. 9.31 at the default 10.
        public static double TopMultiplier(int maxTier)
        {
            return maxTier <= 0 ? 1.0 : Math.Pow(TierGrowth, maxTier);
        }

        // Where a tier sits between the two authored ends, 0 at tier 0 and 1
        // at maxTier.
        //
        // Expressed as a FRACTION of the span rather than as a multiplier on
        // the value, which is what lets it work for a stat that starts at zero
        // or goes negative -- steel's speed penalty runs 0 to -2, and no
        // geometric curve on the value itself can express that.
        //
        //     f(t) = (g^t - 1) / (g^maxTier - 1)
        //
        // so both ends stay exactly where they were authored and only the
        // shape between them changes. Raising maxTier still stretches the
        // curve instead of inflating the tiers that already exist, which is
        // the property the two-ended authoring was chosen for.
        public static double CurveFraction(int tier, int maxTier)
        {
            if (maxTier <= 0 || tier <= 0) return 0.0;
            if (tier >= maxTier) return 1.0;

            double top = Math.Pow(TierGrowth, maxTier) - 1.0;
            if (top <= 0.0) return 0.0;

            return (Math.Pow(TierGrowth, tier) - 1.0) / top;
        }

        // One stat's value at tier 0, before any hand-authored line overrides
        // it. `weight` is the style's share for that stat.
        public static int AtBaseTier(EquipmentSlot slot, double weight)
        {
            return Round(Base * SlotWeight(slot) * weight);
        }

        // The same stat at maxTier.
        //
        // Scaled from the UNROUNDED tier-0 value on purpose. Rounding first
        // and multiplying second turns a 2.88 into a 2 and then into an 18.6
        // rather than a 26.8, so a third of the top end would be lost to a
        // rounding step nobody could see.
        public static int AtTopTier(EquipmentSlot slot, double weight, int maxTier)
        {
            return Round(Base * SlotWeight(slot) * weight * TopMultiplier(maxTier));
        }

        // What a piece DEMANDS, as against what it grants.
        //
        // A flat baseline distributed by the style's own weights, so a
        // material gates on exactly the scores it is for: a pure style asks 10
        // of its one stat, and a dual asks 4 and 7 rather than 10 and 10. The
        // set that makes you strong is the set that expects you to be.
        //
        // NO SLOT WEIGHT, deliberately, where the granted stats have one. Gate
        // on the MATERIAL and every piece of a set becomes wearable at the same
        // moment; gate per piece and gloves would be free while the torso stays
        // locked, which fragments a set into five separate decisions and makes
        // the lightest slot the one nobody thinks about.
        public const double RequirementBaseline = 10.0;

        public static int RequirementAtBaseTier(double weight)
        {
            return Round(RequirementBaseline * weight);
        }

        // Up the same curve as everything else, so the gate keeps pace with
        // what the gear is worth rather than falling behind it.
        //
        // Reachable by construction: at tier 10 a pure style asks 93 of its own
        // stat, and three other pieces of that same style supply about 100
        // before base scores are counted. What it refuses is wearing one piece
        // of a material you have not otherwise committed to -- which is the
        // whole point of a requirement, and it is what makes the cascade in
        // ItemComparison something a player can actually meet.
        public static int RequirementAtTopTier(double weight, int maxTier)
        {
            return Round(RequirementBaseline * weight * TopMultiplier(maxTier));
        }

        // Away from zero, explicitly.
        //
        // Math.Round defaults to banker's rounding and Mathf.RoundToInt is the
        // same, so 2.5 goes to 2 and 3.5 goes to 4 -- an asymmetry that would
        // land differently on two stats that happen to sit either side of a
        // half. CLAUDE.md records a real test flake from exactly this.
        //
        // Double rather than the integer arithmetic ItemUpgrade insists on:
        // this runs once at content-build time on numbers under a hundred, not
        // per-hit at runtime, and the values are pinned by literal-valued
        // tests rather than by re-deriving this formula.
        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
