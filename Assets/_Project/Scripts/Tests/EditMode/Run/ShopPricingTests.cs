using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.EditModeTests
{
    // THE SHOP'S PRICE TABLE, PINNED AS LITERALS.
    //
    // Every expected value below is written out, never recomputed from
    // ShopPricing's own formulas: an expectation that recomputes the
    // implementation agrees with any implementation, including a broken one
    // (CLAUDE.md gotcha 5, AUDIT #18). If one of these fails the question is
    // whether the price was MEANT to move, not what the new number should be.
    //
    // Every price is authored in step-0 gold and charged through ScalePrice,
    // which is DifficultyCurve.ScaleReward: 1.075 a step, floored. The scaled
    // literals were hand-derived from that multiplier (1.075^8 = 1.7835,
    // 1.075^16 = 3.1808) and floored, not read off the code.
    public class ShopPricingTests
    {
        // ScalePrice rides fight gold's curve (DifficultyCurve.ScaleReward,
        // the health rate): the same literals ScaleHealth_LandsWhereTheCurveSays
        // pins, so a price and a fight's gold move together at every depth.
        [TestCase(21, 0, 21)]
        [TestCase(100, 8, 178)]
        [TestCase(350, 80, 113958)]
        public void ScalePrice_RidesTheFightGoldCurve(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.ScalePrice(amount, step));
        }

        // tier, affixes, authored (step-0) price. Hand-computed from GearBase
        // 21, GearPerTier 5 and AffixStep 0.5, rounded to even: 21 x 1.5 =
        // 31.5 reads 32 and 31 x 1.5 = 46.5 reads 46.
        [TestCase(0, 0, 21)]
        [TestCase(1, 0, 26)]
        [TestCase(2, 0, 31)]
        [TestCase(12, 0, 81)]
        [TestCase(0, 1, 32)]
        [TestCase(1, 1, 39)]
        [TestCase(2, 1, 46)]
        [TestCase(0, 2, 42)]
        [TestCase(1, 2, 52)]
        [TestCase(2, 2, 62)]
        public void GearAuthored_IsItsPinnedValue(int tier, int affixes, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.GearAuthored(tier, affixes));
        }

        // tier, affixes, step, charged price. Step 8 is the end of floor 1,
        // step 16 the end of floor 2.
        [TestCase(0, 0, 0, 21)]
        [TestCase(0, 0, 8, 37)]
        [TestCase(0, 0, 16, 66)]
        [TestCase(1, 1, 0, 39)]
        [TestCase(1, 1, 8, 69)]
        [TestCase(1, 1, 16, 124)]
        [TestCase(12, 0, 0, 81)]
        [TestCase(12, 0, 8, 144)]
        [TestCase(12, 0, 16, 257)]
        public void GearPrice_IsTheAuthoredPriceThroughScalePrice(int tier, int affixes, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.GearPrice(tier, affixes, step));
        }

        // A hone on a slot already at +plus: 21 x (1 + 0.35 x plus), rounded
        // to even.
        [TestCase(0, 21)]
        [TestCase(1, 28)]
        [TestCase(2, 36)]
        [TestCase(5, 58)]
        public void HoneAuthored_IsItsPinnedValue(int plus, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.HoneAuthored(plus));
        }

        [TestCase(0, 0, 21)]
        [TestCase(1, 0, 28)]
        [TestCase(0, 8, 37)]
        [TestCase(1, 8, 49)]
        [TestCase(5, 8, 103)]
        [TestCase(5, 16, 184)]
        public void HonePrice_IsTheAuthoredPriceThroughScalePrice(int plus, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.HonePrice(plus, step));
        }

        [TestCase(RelicRarity.Common, 60)]
        [TestCase(RelicRarity.Uncommon, 110)]
        [TestCase(RelicRarity.Rare, 190)]
        [TestCase(RelicRarity.UltraRare, 300)]
        [TestCase(RelicRarity.Mythic, 460)]
        [TestCase(RelicRarity.Godlike, 700)]
        public void RelicAuthored_IsItsPinnedValue(RelicRarity rarity, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RelicAuthored(rarity));
        }

        [TestCase(RelicRarity.Common, 0, 60)]
        [TestCase(RelicRarity.Common, 8, 107)]
        [TestCase(RelicRarity.Rare, 8, 338)]
        [TestCase(RelicRarity.Rare, 16, 604)]
        [TestCase(RelicRarity.Godlike, 8, 1248)]
        public void RelicPrice_IsTheAuthoredPriceThroughScalePrice(RelicRarity rarity, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RelicPrice(rarity, step));
        }

        [TestCase(1, 70)]
        [TestCase(2, 95)]
        [TestCase(3, 120)]
        [TestCase(4, 145)]
        public void BookAuthored_IsItsPinnedValue(int bookTier, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.BookAuthored(bookTier));
        }

        [TestCase(1, 0, 70)]
        [TestCase(1, 8, 124)]
        [TestCase(4, 8, 258)]
        [TestCase(4, 16, 461)]
        public void BookPrice_IsTheAuthoredPriceThroughScalePrice(int bookTier, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.BookPrice(bookTier, step));
        }

        // WHICH WAY 34.5 BREAKS, ASKED RATHER THAN ASSUMED.
        //
        // §2c predicted 35 for the +3/two-affix piece at tier 2 and it is
        // 34. 0.30 * 115 is exactly 34.5 in double, and C#'s
        // Math.Round(double) rounds a midpoint TO EVEN -- so it goes down,
        // and so does the health potion's 4.5. Pinned here because a later
        // switch to MidpointRounding.AwayFromZero would move two real sell
        // prices and nothing else in the suite would notice.
        [TestCase(20, 6)]
        [TestCase(24, 7)]
        [TestCase(28, 8)]
        [TestCase(82, 25)]
        [TestCase(98, 29)]
        [TestCase(115, 34)]
        // A health potion, the one price read from authored content (F6),
        // and the second exact midpoint in the table -- 4.5, also rounded
        // down by the to-even rule.
        [TestCase(15, 4)]
        [TestCase(460, 138)]
        public void SellPrice_AtStepZero_IsItsPinnedValue(int authoredBuyPrice, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.SellPrice(authoredBuyPrice, 0));
        }

        // The sale is charged through ScalePrice from its own step-0 amount:
        // 31 sells for 9 at step 0 and floor(9 x 1.7835) = 16 at step 8; a
        // 15-gold potion sells for 4 and then floor(4 x 1.7835) = 7.
        [TestCase(31, 8, 16)]
        [TestCase(15, 8, 7)]
        [TestCase(1, 16, 3)]
        public void SellPrice_IsScaledFromTheStepZeroSale(int authoredBuyPrice, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.SellPrice(authoredBuyPrice, step));
        }

        // Selling is not supposed to make anyone rich. The catalogue-wide
        // version of this (every offerable item at every tier and affix count)
        // needs ContentDatabase and lives in ShopMutationTests; this is the
        // arithmetic half, over the whole range a price can take.
        //
        // FROM 2, not from 1: a 1-gold item would sell for the 1 gold it
        // cost, because the floor is 1 and 30% of 1 rounds to 0. Nothing in
        // the game prices below 15 (a health potion), so the degenerate case
        // is unreachable rather than fixed -- and it is pinned below so that
        // "unreachable" stays a statement about content rather than a hope.
        [Test]
        public void SellingIsAlwaysWorseThanBuying()
        {
            foreach (int step in new[] { 0, 8, 40 })
            {
                for (int price = 2; price <= 10000; price++)
                {
                    Assert.Less(ShopPricing.SellPrice(price, step), ShopPricing.ScalePrice(price, step),
                        $"Selling something bought at {price} (step {step}) returned at least what it cost.");
                }
            }
        }

        [Test]
        public void TheCheapestPossiblePriceIsTheOneDegenerateSellCase()
        {
            Assert.AreEqual(1, ShopPricing.SellPrice(1, 0));
            Assert.AreEqual(1, ShopPricing.SellPrice(2, 0),
                "The floor is what keeps a cheap sale from paying nothing at all.");
        }

        // PER SECTION, doubling from 15, saturating at four digits so the
        // button's label can still print it at step 0 (UiTextFitAudit sizes
        // for four digits plus a suffix).
        [TestCase(0, 15)]
        [TestCase(1, 30)]
        [TestCase(2, 60)]
        [TestCase(3, 120)]
        [TestCase(4, 240)]
        [TestCase(5, 480)]
        [TestCase(6, 960)]
        [TestCase(7, 1920)]
        [TestCase(8, 3840)]
        [TestCase(9, 7680)]
        [TestCase(10, 9999)]
        [TestCase(11, 9999)]
        [TestCase(40, 9999)]
        [TestCase(int.MaxValue, 9999)]
        public void RerollAuthored_IsItsPinnedValue(int rerollsUsed, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RerollAuthored(rerollsUsed));
        }

        [TestCase(0, 0, 15)]
        [TestCase(0, 8, 26)]
        [TestCase(0, 16, 47)]
        [TestCase(3, 8, 214)]
        [TestCase(3, 16, 381)]
        public void RerollPrice_IsTheAuthoredPriceThroughScalePrice(int rerollsUsed, int step, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RerollPrice(rerollsUsed, step));
        }

        // The ceiling is a DISPLAY fact first: 15 * 2^n overflows a signed
        // int at n = 28, and nothing in the run stops a player pressing the
        // button. A negative reroll price would be a free reroll.
        [Test]
        public void RerollPriceNeverOverflowsOrGoesBackwards()
        {
            foreach (int step in new[] { 0, 8, 80, 200 })
            {
                int previous = 0;
                for (int n = 0; n < 200; n++)
                {
                    int price = ShopPricing.RerollPrice(n, step);
                    Assert.GreaterOrEqual(price, previous, $"The reroll price fell at n = {n}, step {step}.");
                    previous = price;
                }
            }
        }

        // Price is a function of tier, affixes and the step. Stated as
        // monotonicity in each axis, which is the observable half of that
        // rule.
        [Test]
        public void EveryAxisMakesAThingMoreExpensive()
        {
            foreach (int step in new[] { 0, 8, 40 })
            {
                foreach (int tier in Enumerable.Range(0, 13))
                {
                    for (int affixes = 0; affixes < 3; affixes++)
                    {
                        Assert.Less(ShopPricing.GearPrice(tier, affixes, step), ShopPricing.GearPrice(tier, affixes + 1, step), "affixes");
                        Assert.Less(ShopPricing.GearPrice(tier, affixes, step), ShopPricing.GearPrice(tier + 1, affixes, step), "tier");
                    }
                }
            }

            Assert.Less(ShopPricing.GearPrice(3, 1, 8), ShopPricing.GearPrice(3, 1, 9), "step");
        }
    }
}
