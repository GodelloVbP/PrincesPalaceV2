using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // WHAT THE SHOP CHARGES. One table, pure, engine-free.
    //
    // EVERY PRICE IS AUTHORED IN STEP-0 GOLD AND CHARGED THROUGH ScalePrice.
    // docs/PLAN_ITEMS.md 1.6: gold income rides DifficultyCurve.ScaleReward,
    // so a price that did not would shrink against it with every step. The
    // authored amounts (a tier, its affixes, a rarity, a book band, a
    // reroll count, a hone) are depth-free; the step enters once, at the
    // charge. Depth also enters the STOCK side (RarityTable.FloorTier decides
    // which tiers a shelf offers), and the two do not double count: one
    // chooses WHICH item, the other keeps what it costs in step with what a
    // fight pays.
    //
    // Every number below is pinned by ShopPricingTests as a LITERAL, never
    // recomputed from these formulas -- a test that recomputes the formula
    // agrees with any implementation, including a broken one (CLAUDE.md
    // gotcha 5).
    public static class ShopPricing
    {
        // ---- depth ----------------------------------------------------------

        // THE price curve: step-0 gold in, the gold that is asked at `step`
        // out, on the curve fight gold rides (DifficultyCurve.ScaleReward), so
        // what a fight pays and what the shop asks keep one ratio at every
        // depth.
        public static int ScalePrice(int amount, int step) => Dungeon.DifficultyCurve.ScaleReward(amount, step);

        // ---- gear ---------------------------------------------------------

        // Linear in tier, the SHAPE taken from ItemDefinition.cost and
        // nothing else: that field is priced for a permanent-Gold hub store
        // that does not exist in v2 and runs about 5x too expensive against
        // measured run income (docs/PLAN_SHOP.md F6). Left alone rather than
        // rewritten across 646 assets.
        //
        // ShopStock.RollGear targets one tier above the map's floor tier.
        // Pinned as literals in ShopPricingTests.
        public const int GearBase = 21;
        public const int GearPerTier = 5;

        // How much one point of honing is worth, as a multiplier.
        public const double PlusStep = 0.35;

        // And one affix on a copy.
        public const double AffixStep = 0.50;

        // A trait is priced as this many affixes (docs/PLAN_ITEMS.md 2.3),
        // not as the one modifier it is on the copy.
        public const int TraitAsAffixes = 2;

        public static int BaseFor(int tier) => GearBase + GearPerTier * (tier < 0 ? 0 : tier);

        public static double AffixFactor(int affixes) => 1.00 + AffixStep * (affixes < 0 ? 0 : affixes);

        // Rounded with Math.Round's DEFAULT midpoint rule (to-even), stated
        // rather than assumed: the one price in §2c's table that lands on an
        // exact .5 is a sell price (see SellPrice), and to-even takes it
        // down. Nothing here picks a rounding mode for a reason -- what
        // matters is that the choice is pinned, not which one it is.
        //
        // `affixes` excludes traits; each trait adds TraitAsAffixes.
        public static int GearAuthored(int tier, int affixes = 0, int traits = 0)
        {
            int weighted = (affixes < 0 ? 0 : affixes) + TraitAsAffixes * (traits < 0 ? 0 : traits);
            return (int)Math.Round(BaseFor(tier) * AffixFactor(weighted));
        }

        public static int GearPrice(int tier, int affixes, int step, int traits = 0) =>
            ScalePrice(GearAuthored(tier, affixes, traits), step);

        // One more point of honing on a slot already honed to `plus`: the
        // gear base, climbing 35% of itself per point.
        public static int HoneAuthored(int plus)
        {
            return (int)Math.Round(GearBase * (1.00 + PlusStep * (plus < 0 ? 0 : plus)));
        }

        public static int HonePrice(int plus, int step) => ScalePrice(HoneAuthored(plus), step);

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
        public static int RelicAuthored(RelicRarity rarity)
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

        public static int RelicPrice(RelicRarity rarity, int step) => ScalePrice(RelicAuthored(rarity), step);

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

        public static int BookAuthored(int bookTier)
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

        public static int BookPrice(int bookTier, int step) => ScalePrice(BookAuthored(bookTier), step);

        // ---- selling ------------------------------------------------------

        // Thirty percent, floored at 1. "Not supposed to make you rich":
        // selling the whole bag never funds a relic, which is the property
        // SellPriceIsAlwaysBelowBuyPrice pins across the whole offerable
        // catalogue rather than at the three depths §2c happened to tabulate.
        public const double SellFraction = 0.30;

        // `authoredBuyPrice` is the step-0 gold the thing costs to buy (the
        // *Authored functions above, or a consumable's authored cost); the
        // sale is charged through ScalePrice like every other price.
        public static int SellPrice(int authoredBuyPrice, int step)
        {
            int sell = authoredBuyPrice <= 0 ? 1 : (int)Math.Round(SellFraction * authoredBuyPrice);
            return ScalePrice(sell < 1 ? 1 : sell, step);
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
        public static int RerollAuthored(int rerollsUsed)
        {
            if (rerollsUsed <= 0) return RerollBase;
            if (rerollsUsed >= 31) return RerollCeiling;

            long price = (long)RerollBase << rerollsUsed;
            return price >= RerollCeiling ? RerollCeiling : (int)price;
        }

        public static int RerollPrice(int rerollsUsed, int step) => ScalePrice(RerollAuthored(rerollsUsed), step);
    }
}
