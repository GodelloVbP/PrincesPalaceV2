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
    // The three tier columns are docs/PLAN_SHOP.md §2c's own table: tier 0 is
    // what a step-8 shop stocks, tier 1 a step-24 shop and tier 2 a step-40
    // shop, under RarityTable.FloorTier's current /16 divisor.
    //
    // Every literal below is ShopPricing's own formula output, recomputed
    // and pinned fresh, per this file's own header rule.
    public class ShopPricingTests
    {
        // tier, plus, riftTier, price
        [TestCase(0, 0, 0, 21)]
        [TestCase(0, 1, 0, 28)]
        [TestCase(0, 2, 0, 36)]
        [TestCase(1, 0, 0, 26)]
        [TestCase(1, 1, 0, 35)]
        [TestCase(1, 2, 0, 44)]
        [TestCase(2, 0, 0, 31)]
        [TestCase(2, 1, 0, 42)]
        [TestCase(2, 2, 0, 53)]

        // "Rare": tier-matched, +2, one affix slot.
        [TestCase(0, 2, 1, 54)]
        [TestCase(1, 2, 1, 66)]
        [TestCase(2, 2, 1, 79)]

        // "Very rare": +3, two affix slots.
        [TestCase(0, 3, 2, 86)]
        [TestCase(1, 3, 2, 107)]
        [TestCase(2, 3, 2, 127)]
        public void GearPrice_IsItsPinnedValue(int tier, int plus, int riftTier, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.GearPrice(tier, plus, riftTier));
        }

        [TestCase(RelicRarity.Common, 60)]
        [TestCase(RelicRarity.Uncommon, 110)]
        [TestCase(RelicRarity.Rare, 190)]
        [TestCase(RelicRarity.UltraRare, 300)]
        [TestCase(RelicRarity.Mythic, 460)]
        [TestCase(RelicRarity.Godlike, 700)]
        public void RelicPrice_IsItsPinnedValue(RelicRarity rarity, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RelicPrice(rarity));
        }

        [TestCase(1, 70)]
        [TestCase(2, 95)]
        [TestCase(3, 120)]
        [TestCase(4, 145)]
        public void BookPrice_IsItsPinnedValue(int bookTier, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.BookPrice(bookTier));
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
        public void SellPrice_IsItsPinnedValue(int buyPrice, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.SellPrice(buyPrice));
        }

        // Selling is not supposed to make anyone rich. The catalogue-wide
        // version of this (every offerable item at every plus and rift tier)
        // needs ContentDatabase and lives in ShopSweepTests; this is the
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
            for (int price = 2; price <= 10000; price++)
            {
                Assert.Less(ShopPricing.SellPrice(price), price,
                    $"Selling something bought at {price} returned at least what it cost.");
            }
        }

        [Test]
        public void TheCheapestPossiblePriceIsTheOneDegenerateSellCase()
        {
            Assert.AreEqual(1, ShopPricing.SellPrice(1));
            Assert.AreEqual(1, ShopPricing.SellPrice(2),
                "The floor is what keeps a cheap sale from paying nothing at all.");
        }

        // PER SECTION, doubling from 15, saturating at four digits so the
        // button's label can still print it (UiTextFitAudit sizes for four
        // digits plus a suffix).
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
        public void RerollPrice_IsItsPinnedValue(int rerollsUsed, int expected)
        {
            Assert.AreEqual(expected, ShopPricing.RerollPrice(rerollsUsed));
        }

        // The ceiling is a DISPLAY fact first: 15 * 2^n overflows a signed
        // int at n = 28, and nothing in the run stops a player pressing the
        // button. A negative reroll price would be a free reroll.
        [Test]
        public void RerollPriceNeverOverflowsOrGoesBackwards()
        {
            int previous = 0;
            for (int n = 0; n < 200; n++)
            {
                int price = ShopPricing.RerollPrice(n);
                Assert.GreaterOrEqual(price, previous, $"The reroll price fell at n = {n}.");
                Assert.LessOrEqual(price, ShopPricing.RerollCeiling);
                previous = price;
            }
        }

        // Assumption 11's anchor has to be reachable, or the floor it
        // guarantees is a guarantee that never fires. A tier-0, +0, no-affix
        // piece is not a legal roll at all
        // (ShopStock.ApplyQualityFloor, "no bare commons") -- the cheapest
        // gear that CAN exist is a tier-0, +1 piece, and the anchor sits
        // exactly on that floor (see
        // ShopPricing.NormalFightPayoutAnchor's own comment).
        [Test]
        public void TheAffordabilityAnchorIsAboveTheCheapestGearThatCanExist()
        {
            Assert.GreaterOrEqual(ShopPricing.NormalFightPayoutAnchor, ShopPricing.GearPrice(0, 1, 0));
        }

        // THE BEFORE/AFTER TABLE, pinned. depthStep 8/24/40 are this file's
        // own tier-0/1/2 shop steps (see header); ShopStock.GearTierBoost is
        // what a live shop actually rolls at each of them, and plus 1 is the
        // cheapest a card can now be (ShopStock's "no bare commons" floor --
        // a point of plus is the CHEAPEST way to clear that bar, PlusStep <
        // RiftStep). BEFORE is a literal from the retired §2c table (a bare
        // tier-N common under the OLD GearBase/GearPerTier, 20/4) and is not
        // recomputable from this file's own formula any more -- the whole
        // point of pinning it here is that the code that produced it is
        // gone. Only AFTER is asserted against live code.
        [TestCase(0, 20, 35)]
        [TestCase(1, 24, 42)]
        [TestCase(2, 28, 49)]
        public void TheShelfFloorAtEachTierIsItsPinnedBeforeAndAfterPrice(
            int unboostedTier, int beforePriceForTheRecord, int afterPrice)
        {
            int boostedTier = unboostedTier + ShopStock.GearTierBoost;
            Assert.AreEqual(afterPrice, ShopPricing.GearPrice(boostedTier, 1, 0),
                "the NEW floor: one tier up, +1 minimum. beforePriceForTheRecord " +
                $"({beforePriceForTheRecord}) is the retired formula's own value, kept here as documentation.");
        }

        // Price is a function of tier, plus and rift and nothing else -- no
        // depth term, ever. Stated as monotonicity in each axis, which is the
        // observable half of that rule.
        [Test]
        public void EveryAxisMakesAThingMoreExpensive()
        {
            foreach (int tier in Enumerable.Range(0, 11))
            {
                for (int plus = 0; plus < 5; plus++)
                {
                    for (int rift = 0; rift < 3; rift++)
                    {
                        Assert.Less(ShopPricing.GearPrice(tier, plus, rift),
                            ShopPricing.GearPrice(tier, plus + 1, rift), "plus");
                        Assert.Less(ShopPricing.GearPrice(tier, plus, rift),
                            ShopPricing.GearPrice(tier, plus, rift + 1), "rift");
                        Assert.Less(ShopPricing.GearPrice(tier, plus, rift),
                            ShopPricing.GearPrice(tier + 1, plus, rift), "tier");
                    }
                }
            }
        }
    }
}
