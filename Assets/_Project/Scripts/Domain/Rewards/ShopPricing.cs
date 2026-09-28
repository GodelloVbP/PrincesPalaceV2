using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // WHAT THE SHOP CHARGES. One table, pure, engine-free.
    //
    // PRICE IS A FUNCTION OF TIER, PLUS AND RIFT -- NEVER OF DEPTH.
    // docs/PLAN_SHOP.md §2a/assumption 2: depth already enters the shop on
    // the STOCK side, through RarityTable.FloorTier deciding which tiers get
    // offered at all. A depth term here would count the same climb twice, and
    // the two rules would then have to be moved together forever.
    //
    // Every number below is pinned by ShopPricingTests as a LITERAL, never
    // recomputed from these formulas -- a test that recomputes the formula
    // agrees with any implementation, including a broken one (CLAUDE.md
    // gotcha 5).
    public static class ShopPricing
    {
        // ---- gear ---------------------------------------------------------

        // Linear in tier, the SHAPE taken from ItemDefinition.cost and
        // nothing else: that field is priced for a permanent-Gold hub store
        // that does not exist in v2 and runs about 5x too expensive against
        // measured run income (docs/PLAN_SHOP.md F6). Left alone rather than
        // rewritten across 646 assets.
        //
        // ShopStock.RollGear targets one tier above the map's floor tier and
        // refuses to hand back a bare common (ShopStock.ApplyQualityFloor),
        // so the cheapest gear a floor-1 shop can ever show is already a
        // tier-1, +1 piece rather than a tier-0, +0 one -- these two
        // constants combine with that to push a floor-1 shelf's floor to 28
        // (see GearPrice(0, 1, 0) below and NormalFightPayoutAnchor's own
        // comment). Pinned as literals in ShopPricingTests; see that file for
        // the full before/after table.
        public const int GearBase = 21;
        public const int GearPerTier = 5;

        // How much one point of +N is worth, as a multiplier. Plus is the
        // long tail rather than the progression (RarityTable's own header),
        // so it multiplies rather than adding a flat band.
        public const double PlusStep = 0.35;

        // And one filled affix slot. Steeper than a plus because a rift tier
        // carries a whole rolled modifier, not a stat nudge.
        public const double RiftStep = 0.50;

        public static int BaseFor(int tier) => GearBase + GearPerTier * (tier < 0 ? 0 : tier);

        public static double PlusFactor(int plus) => 1.00 + PlusStep * (plus < 0 ? 0 : plus);

        public static double RiftFactor(int riftTier) => 1.00 + RiftStep * (riftTier < 0 ? 0 : riftTier);

        public static double RiftFactor(RiftTier riftTier) => RiftFactor((int)riftTier);

        // Rounded with Math.Round's DEFAULT midpoint rule (to-even), stated
        // rather than assumed: the one price in §2c's table that lands on an
        // exact .5 is a sell price (see SellPrice), and to-even takes it
        // down. Nothing here picks a rounding mode for a reason -- what
        // matters is that the choice is pinned, not which one it is.
        public static int GearPrice(int tier, int plus, int riftTier)
        {
            return (int)Math.Round(BaseFor(tier) * PlusFactor(plus) * RiftFactor(riftTier));
        }

        public static int GearPrice(int tier, int plus, RiftTier riftTier) =>
            GearPrice(tier, plus, (int)riftTier);

        // ---- relics -------------------------------------------------------

        // FLAT, NOT DEPTH-SCALED, deliberately: a relic bought at step 8 has
        // thirty-two steps left to pay off and one bought at step 40 has
        // none, so a flat price makes the early buy the good buy without a
        // second curve to tune (§2b).
        //
        // Godlike is NOT in the plan's table and is priced here as an
        // extrapolation: RelicPool.WeightOf gives it the default weight 1,
        // rarer than Mythic's 2, so it prices above Mythic. A number to move
        // in the balance pass, not a measured one -- like every other row.
        public static int RelicPrice(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 60;
                case RelicRarity.Uncommon: return 110;
                case RelicRarity.Rare: return 190;
                case RelicRarity.UltraRare: return 300;
                case RelicRarity.Mythic: return 460;
                case RelicRarity.Godlike: return 700;
                default: return 60;
            }
        }

        // ---- books --------------------------------------------------------

        // Four bands rather than a formula, because four points is not
        // enough data to fit a curve to and a lookup is honest about that
        // (§2b). WHICH band a spell is in is authored content (bookTier), not
        // a switch over skill ids here -- so a sixth spell never needs this
        // file edited. Gate 1 sells no books; the table is here because the
        // price is a pricing fact, and pricing lives in one place.
        //
        // MaxBookTier is the top of that table, and SkillEntryResolver
        // refuses a bookTier above it (AUDIT #145 B3): the default arm below
        // would otherwise price a `bookTier: 9` as a tier-2 book with nothing
        // saying so.
        public const int MaxBookTier = 4;

        public static int BookPrice(int bookTier)
        {
            switch (bookTier)
            {
                case 1: return 70;
                case 2: return 95;
                case 3: return 120;
                case 4: return 145;
                default: return 95;
            }
        }

        // ---- selling ------------------------------------------------------

        // Thirty percent, floored at 1. "Not supposed to make you rich":
        // selling the whole bag never funds a relic, which is the property
        // SellPriceIsAlwaysBelowBuyPrice pins across the whole offerable
        // catalogue rather than at the three depths §2c happened to tabulate.
        public const double SellFraction = 0.30;

        public static int SellPrice(int buyPrice)
        {
            if (buyPrice <= 0) return 1;

            int sell = (int)Math.Round(SellFraction * buyPrice);
            return sell < 1 ? 1 : sell;
        }

        // ---- rerolling ----------------------------------------------------

        // PER SECTION, and `n` is that section's own count at this node
        // (§7.1 point 7). Doubling is the limit on rerolling, not a cap:
        // rerolls stay unlimited while the player can pay.
        public const int RerollBase = 15;

        // A DISPLAY FACT BEFORE IT IS AN ECONOMY FACT. 15 * 2^n passes four
        // digits at n = 10 and overflows a signed int at n = 28; the reroll
        // button is sized for four digits plus a suffix, so a five-digit
        // price fails UiTextFitAudit rather than merely looking wrong.
        // Saturating costs nothing in play -- 9999 is more gold than a
        // 40-step run earns in total (§2a).
        public const int RerollCeiling = 9999;

        // Computed in long and clamped, so the saturation is arithmetic
        // rather than a promise about how far a player can press a button.
        public static int RerollPrice(int rerollsUsed)
        {
            if (rerollsUsed <= 0) return RerollBase;
            if (rerollsUsed >= 31) return RerollCeiling;

            long price = (long)RerollBase << rerollsUsed;
            return price >= RerollCeiling ? RerollCeiling : (int)price;
        }

        // ---- the affordability floor ---------------------------------------

        // WHAT "AT LEAST ONE CARD THEY CAN AFFORD" MEANS, IN GOLD.
        //
        // Assumption 11: a shop that presents nothing a player has a chance
        // of buying is a room that cost them a fight and returned a reroll
        // button. §2a measures a normal fight's payout at 16-24 across every
        // profile and depth, and the anchor is the TOP of that band, not the
        // bottom: the cheapest gear that exists at all is a tier-0 +0 with no
        // affixes at 20, so a 16 anchor could not be satisfied at any depth
        // and the bounded re-draw would exhaust every time -- a guarantee
        // that never fires reads as one that does.
        //
        // Enforced by re-drawing the cheapest slot (ShopStock.RollGear), not
        // by discounting, so the price stays a pure function of tier/plus/
        // rift.
        //
        // 28, not lower, as a consequence of the "no bare commons" rule
        // (ShopStock.ApplyQualityFloor): a tier-0/+0/no-affix piece is not a
        // legal roll, so GearPrice(0, 0, 0) is not the cheapest gear that can
        // exist -- GearPrice(0, 1, 0) = 28 is, and an anchor below that would
        // be a guarantee
        // (TheAffordabilityAnchorIsAboveTheCheapestGearThatCanExist) that
        // could never fire. Left at the floor exactly rather than padded
        // above it, so a normal fight's own measured payout (16-24, see the
        // header above) still buys nothing outright at floor 1 -- that is
        // the price increase working as intended, not a bug to paper over.
        public const int NormalFightPayoutAnchor = 28;
    }
}
