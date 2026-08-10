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

        // 4% per plus, so a fully honed item is worth 1.40x its tier.
        //
        // Chosen against the tier curve rather than in the abstract: one tier
        // is worth roughly 10% of a piece's total, so ten plus levels being
        // worth 40% means plus is a real but secondary axis. Tuning this
        // above ~10 would make plus dominate tier and turn the rarity bands
        // into decoration.
        //
        // Held as an INTEGER PERCENT because Apply must not go through
        // float32. 1.4f is really 1.39999997615814208984375, so a float
        // 20 * 1.4f is 27.999999... and floors to 27 rather than 28 — the
        // same precision trap CLAUDE.md gotcha 5 records, and one that would
        // have silently cost a point off honed gear at exactly the round
        // numbers a player is most likely to check.
        public const int PercentPerPlus = 4;

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
        // Floored in BOTH directions rather than truncated toward zero: C#
        // integer division rounds a negative the opposite way from a
        // positive, which would make honing a steel platebody quietly reduce
        // its own speed penalty. Same asymmetry ItemSetEntryResolver.ValueAt
        // and AbilityDerivation.FloorDiv2 exist to avoid.
        public static int Apply(int amount, int plus)
        {
            if (amount == 0 || plus <= 0)
            {
                return amount;
            }

            int scaled = amount * (100 + PercentPerPlus * Clamp(plus));
            return scaled >= 0 ? scaled / 100 : (scaled - 99) / 100;
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
