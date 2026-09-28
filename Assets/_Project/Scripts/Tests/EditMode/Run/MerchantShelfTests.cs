using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.EditModeTests
{
    // A MERCHANT SHELF'S RULES WITHOUT A SCENE (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // 1.5, 3.4, M5): the price, the fake count and pick, the lots, the scuffle
    // and the copies a card stamps. The draws are scripted delegates, so
    // every expected value is a literal (CLAUDE.md gotcha 5) -- the run-level
    // half (streams, persistence, the event) is CaravanShelfRunTests.
    public class MerchantShelfTests
    {
        // ---- the fake count: max(1, round(n / 3)) ------------------------------

        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(6, 2)]
        public void FakesAreOneOneTwoTwo_ForThreeFourFiveSixCards_AtAThird(int cards, int fakes)
        {
            Assert.AreEqual(fakes, MerchantShelf.FakeCount(cards, 3));
        }

        [Test]
        public void OneCardAtAThird_IsStillOneFake_TheFloorOfMaxOne()
        {
            Assert.AreEqual(1, MerchantShelf.FakeCount(1, 3));
        }

        [Test]
        public void AFakeShareOfZero_OrNoCards_IsNoFakes()
        {
            Assert.AreEqual(0, MerchantShelf.FakeCount(6, 0));
            Assert.AreEqual(0, MerchantShelf.FakeCount(0, 3));
        }

        // ---- the price: 70% of the shop's, half away from zero, min 1 ----------

        [TestCase(15, 11)] // 10.5 -> 11: away from zero, not to-even (10)
        [TestCase(35, 25)] // 24.5 -> 25, same rule; to-even would say 24
        [TestCase(28, 20)] // 19.6
        [TestCase(21, 15)] // 14.7
        [TestCase(40, 28)] // exact
        [TestCase(1, 1)]   // 0.7 -> 1
        [TestCase(0, 1)]   // the floor
        public void ThePriceIsSeventyPercent_RoundedAwayFromZero_NeverBelowOne(int shopPrice, int expected)
        {
            Assert.AreEqual(expected, MerchantShelf.Price(shopPrice, 70));
        }

        // ---- building the stock ---------------------------------------------------

        private static List<ShopStockEntry> FourGear() => new List<ShopStockEntry>
        {
            ShopStockEntry.Gear(0, "gear_a", 1, null, 0, 28),
            ShopStockEntry.Gear(1, "gear_b", 0, new[] { "fiery" }, 0, 35),
            ShopStockEntry.Gear(2, "gear_c", 2, null, 1, 40),
            ShopStockEntry.Gear(3, "gear_d", 1, null, 0, 21),
        };

        private static readonly MerchantShelf.ConsumableCandidate[] TwoPotions =
        {
            new MerchantShelf.ConsumableCandidate("health_potion", 15),
            new MerchantShelf.ConsumableCandidate("mana_potion", 15),
        };

        private static string Lot(int card) => MerchantShelf.LotFor("caravan", "wares", 3, 7, card);

        [Test]
        public void TheLot_NamesEventShelfStepNodeAndCard()
        {
            Assert.AreEqual("caravan:wares:3:7:5", Lot(5));
        }

        [Test]
        public void SixCards_AreRepricedAtSeventyPercent_LottedInShelfOrder_AndTwoAreFake()
        {
            // Consumables: always the LAST remaining (mana, then health).
            // Fakes: always the first remaining real card (gear_a, then gear_b).
            var stock = MerchantShelf.Build(FourGear(), TwoPotions, 2, 70, 3, Lot,
                bound => bound - 1, bound => 0);

            CollectionAssert.AreEqual(
                new[] { "gear_a", "gear_b", "gear_c", "gear_d", "mana_potion", "health_potion" },
                stock.Select(e => e.contentId).ToArray());
            CollectionAssert.AreEqual(new[] { 20, 25, 28, 15, 11, 11 }, stock.Select(e => e.price).ToArray());
            CollectionAssert.AreEqual(
                new[] { "caravan:wares:3:7:0", "caravan:wares:3:7:1", "caravan:wares:3:7:2", "caravan:wares:3:7:3",
                        "caravan:wares:3:7:4", "caravan:wares:3:7:5" },
                stock.Select(e => e.lot).ToArray());
            CollectionAssert.AreEqual(new[] { true, true, false, false, false, false }, stock.Select(e => e.fake).ToArray());

            // The consumables take their own section, indexed from 0.
            CollectionAssert.AreEqual(new[] { 3, 3 }, stock.Skip(4).Select(e => e.section).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1 }, stock.Skip(4).Select(e => e.index).ToArray());
            Assert.AreEqual(ShopEntryKind.Consumable, stock[4].kind);
        }

        [Test]
        public void AShortConsumablePool_PadsWithNoOffer_WhichIsNeitherLottedNorFake()
        {
            var stock = MerchantShelf.Build(FourGear(), TwoPotions.Take(1).ToList(), 2, 70, 3, Lot,
                bound => 0, bound => bound - 1);

            var padded = stock[5];
            Assert.IsTrue(padded.noOffer);
            Assert.AreEqual("", padded.lot);
            Assert.IsFalse(padded.fake);

            // Five real cards: round(5/3) = 2 fakes, the last two real ones
            // (health_potion, then gear_d).
            CollectionAssert.AreEqual(new[] { false, false, false, true, true, false }, stock.Select(e => e.fake).ToArray());
        }

        [Test]
        public void NoGearSection_StocksOnlyTheConsumables()
        {
            var stock = MerchantShelf.Build(new List<ShopStockEntry>(), TwoPotions, 2, 70, 3, Lot,
                bound => 0, bound => 0);

            CollectionAssert.AreEqual(new[] { "health_potion", "mana_potion" }, stock.Select(e => e.contentId).ToArray());
            CollectionAssert.AreEqual(new[] { true, false }, stock.Select(e => e.fake).ToArray());
        }

        // ---- the scuffle ------------------------------------------------------------

        [Test]
        public void TheScuffle_LosesOneOfTheUnsoldCards_AndHandsOverTheRest()
        {
            var stock = MerchantShelf.Build(FourGear(), TwoPotions, 2, 70, 3, Lot, bound => 0, bound => 0);
            stock[1].sold = true;

            // Remaining unsold: a, c, d, health, mana; index 2 is gear_d.
            var (taken, lost) = MerchantShelf.Scuffle(stock, 1, bound => 2);

            CollectionAssert.AreEqual(new[] { "gear_d" }, lost.Select(e => e.contentId).ToArray());
            CollectionAssert.AreEqual(new[] { "gear_a", "gear_c", "health_potion", "mana_potion" },
                taken.Select(e => e.contentId).ToArray());
        }

        [Test]
        public void TheScuffle_WithNothingLeft_LosesNothing()
        {
            var stock = MerchantShelf.Build(FourGear(), TwoPotions, 2, 70, 3, Lot, bound => 0, bound => 0);
            foreach (var entry in stock) entry.sold = true;

            var (taken, lost) = MerchantShelf.Scuffle(stock, 1, bound => 0);

            Assert.AreEqual(0, taken.Count);
            Assert.AreEqual(0, lost.Count);
        }

        [Test]
        public void TheScuffle_WithOneCardLeft_LosesThatOne()
        {
            var stock = MerchantShelf.Build(FourGear(), TwoPotions, 2, 70, 3, Lot, bound => 0, bound => 0);
            foreach (var entry in stock.Take(5)) entry.sold = true;

            var (taken, lost) = MerchantShelf.Scuffle(stock, 1, bound => 0);

            Assert.AreEqual(0, taken.Count);
            CollectionAssert.AreEqual(new[] { "mana_potion" }, lost.Select(e => e.contentId).ToArray());
        }

        // ---- the copy a card stamps ------------------------------------------------

        [Test]
        public void AGenuineCaravanPotion_CarriesItsLot_AndNeverMergesWithAnOrdinaryOne()
        {
            var card = ShopStockEntry.Consumable(0, "health_potion", 11);
            card.lot = "caravan:wares:3:7:4";

            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, "health_potion", 2);
            InventoryOps.Add(bag, card.Instance(), 1);

            Assert.AreEqual(2, bag.Count, "the caravan potion stacked onto the ordinary ones");
            Assert.AreEqual("caravan:wares:3:7:4", card.Instance().Lot);
            Assert.IsFalse(card.Instance().IsFake);
        }

        [Test]
        public void AFakeGearCard_StampsTheWearCountdown_AFakePotionNone()
        {
            var gear = ShopStockEntry.Gear(0, "gear_a", 1, null, 0, 20);
            gear.lot = "l0";
            gear.fake = true;
            var potion = ShopStockEntry.Consumable(0, "health_potion", 11);
            potion.lot = "l4";
            potion.fake = true;

            Assert.IsTrue(gear.Instance().IsFake);
            Assert.AreEqual(3, gear.Instance().FightsLeft);
            Assert.IsTrue(potion.Instance().IsFake);
            Assert.AreEqual(0, potion.Instance().FightsLeft);
        }

        [Test]
        public void ARoomShopCard_StampsAnOrdinaryCopy_ThatStacks()
        {
            var card = ShopStockEntry.Gear(0, "gear_a", 1, null, 0, 28);

            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, card.GearInstance(), 1);
            InventoryOps.Add(bag, card.GearInstance(), 1);

            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(2, bag[0].count);
            Assert.IsFalse(card.GearInstance().HasLot);
        }
    }
}
