namespace PrincesPalace.Domain.Stats
{
    // What PLUS is worth.
    //
    // Two axes describe an item. TIER is which item it is — its icon, its
    // base stats, its scaling grades, its rarity band — and it is baked, one
    // asset per tier. PLUS is how honed this particular copy happens to be,
    // it rides on the item instance, and all it does is multiply what the
    // tier already grants.
    //
    // They multiply rather than add, which is what keeps an early drop
    // interesting: a tier-2 +10 and a tier-6 +0 land close enough together
    // that the piece you have spent on is not automatically discarded the
    // moment something rarer falls out of a chest.
    //
    // ONE function owns the curve. Every consumer that applies plus goes
    // through MultiplierFor — ItemDefinition.StatBonusAt for worn stats,
    // the weapon path for attack — so there is no second place where the
    // rate could quietly disagree.
    public static class ItemUpgrade
    {
        // The highest plus an instance can carry. Matched to the tier ladder
        // deliberately: the two axes are the same length, so "+10 tier 0" and
        // "+0 tier 10" are the two opposite corners of the same square.
        public const int MaxPlus = 10;

        // 15% per plus, so a fully honed item is worth 2.5x its tier. Tier is
        // geometric (GearScaling.TierGrowth, 1.25 a floor, 9.3x across the
        // ladder), and a plus rate has to stay secondary to that without
        // going invisible: at 2.5x a +10 is worth about five tiers, a real
        // prize and still clearly beaten by descending five more floors. The
        // two axes are 2.5x and 9.3x, and closing that gap is what would turn
        // the rarity bands back into decoration.
        //
        // Held as an INTEGER PERCENT because Apply must not go through
        // float32. 1.4f is really 1.39999997615814208984375, so a float
        // 20 * 1.4f is 27.999999... and floors to 27 rather than 28 — the
        // same precision trap CLAUDE.md gotcha 5 records, and one that would
        // have silently cost a point off honed gear at exactly the round
        // numbers a player is most likely to check.
        public const int PercentPerPlus = 15;

        // The same rate as a multiplier, for callers that want the factor
        // rather than a scaled amount. Derived from PercentPerPlus rather
        // than authored separately, so the two cannot drift.
        public const float PerPlus = PercentPerPlus / 100f;

        // Clamped rather than trusted: plus arrives from save data, and a
        // hand-edited or truncated save must not be able to hand a consumer
        // a 900% multiplier.
        public static float MultiplierFor(int plus)
        {
            return 1f + PercentPerPlus * Clamp(plus) / 100f;
        }

        // Applies the curve to one stat amount.
        //
        // Entirely in integers, for the precision reason on PercentPerPlus.
        //
        // Rounded away from zero, in both directions, not floored: flooring
        // would make plus a lie on most of the catalogue, since a stat has
        // to reach 7 before 15% of it is a whole point and armour stats on
        // this game's scale are 1 to 18 -- a floored +1 Etched Runeplate
        // Gauntlets (manaRegen 1, and 1 x 1.15 floors straight back to 1)
        // hands the player an identical item.
        //
        // Away-from-zero fixes the whole class: for every integer >= 1,
        // ceil(n x 1.15) > n, so a +1 is strictly better on every stat its
        // item grants, and the same rounding keeps a penalty growing rather
        // than shrinking. None of the 701 authored items is flat at +1.
        //
        // It over-grants by at most one point per stat against the honest
        // multiplier, which is the price of the guarantee and cheap at these
        // magnitudes: the +10 ceiling still lands within a point of 2.5x.
        //
        // What it does NOT fix, because integers cannot: at amount 1 the whole
        // plus ladder is 1,2,2,2,2,2,2,3,3,3,3. Adjacent pluses still tie on a
        // one-point stat. Making every plus level distinct needs bigger stat
        // numbers, which is a rebalance rather than a rounding rule.
        //
        // The direction symmetry is the same one ItemSetEntryResolver.ValueAt
        // exists to keep: C# integer division truncates toward zero, which
        // would round a penalty the opposite way from a bonus and make honing
        // a steel platebody quietly reduce its own speed penalty. (Note the
        // trade the other direction: AbilityDerivation's own SpeedBonus/
        // SignatureGainBonus deliberately do use plain truncating division --
        // the plan's pinned Shawn example calls for it. The
        // "guard against truncation" rule below is local to hone/gear math,
        // not a rule AbilityDerivation follows everywhere.)
        public static int Apply(int amount, int plus)
        {
            if (amount == 0 || plus <= 0)
            {
                return amount;
            }

            int scaled = amount * (100 + PercentPerPlus * Clamp(plus));
            return scaled >= 0 ? (scaled + 99) / 100 : (scaled - 99) / 100;
        }

        // Plus arrives from save data, which a player can edit and a
        // truncated write can corrupt. Neither may hand a consumer an
        // arbitrary multiplier.
        private static int Clamp(int plus)
        {
            if (plus <= 0)
            {
                return 0;
            }

            return plus > MaxPlus ? MaxPlus : plus;
        }
    }
}
