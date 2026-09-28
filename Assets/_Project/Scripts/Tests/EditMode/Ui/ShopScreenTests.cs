using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The shop's tree, audited before any scene exists -- same posture as
    // RelicDraftScreenTests, the other nested-panel screen.
    public class ShopScreenTests
    {
        private static UiNode Tree() => ShopScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheMapItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(MapScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void BuySitsLeftOfPackWhilePackShows_AndCentresAlone()
        {
            // Pair 300 wide with a 16 gap: BUY's centre is (300 + 16) / 2 left.
            Assert.AreEqual(-158f, ShopScreen.BuyButtonX(packShown: true), 0.001f);
            Assert.AreEqual(0f, ShopScreen.BuyButtonX(packShown: false), 0.001f);
        }

        [Test]
        public void TheBuiltBuyButtonIsAtTheRoomShopsSlot()
        {
            var screen = ShopScreen.Build();
            Assert.AreEqual(-158f, screen.BuyButton.Node.Place.Offset.X, 0.001f);
        }

        [Test]
        public void TheMerchantBustStandsOnThePanelsBottomEdge()
        {
            // Row height (892 - 24) / 2 = 434, so the panel's bottom edge is
            // -217 and the bust stands one hairline above it; the content top
            // is 217 - 28 - 48 - 16 = 125, 341 above the bust's foot.
            Assert.AreEqual(-216f, ShopScreen.MerchantBustBottomY, 0.001f);
            Assert.AreEqual(341f, ShopScreen.MerchantBustMaxHeight, 0.001f);

            var bust = ShopScreen.Build().MerchantBust.Node;
            Assert.AreEqual(-216f, bust.Place.Offset.Y, 0.001f);
            Assert.AreEqual(0f, bust.Place.Pivot.Y, 0.001f, "the bust is bottom-pivoted, so its cut edge is its anchor");
        }

        [Test]
        public void TheMerchantBustIsSizedToItsArt()
        {
            // The rat merchant's 1408x1402: 200 wide, 200 * 1402 / 1408 tall.
            var rat = ShopScreen.MerchantBustSize(new UiVec(1408f, 1402f));
            Assert.AreEqual(200f, rat.X, 0.01f);
            Assert.AreEqual(199.15f, rat.Y, 0.01f);

            // A sprite taller than the room shrinks to 341 tall, width with it.
            var tall = ShopScreen.MerchantBustSize(new UiVec(500f, 1000f));
            Assert.AreEqual(170.5f, tall.X, 0.01f);
            Assert.AreEqual(341f, tall.Y, 0.01f);

            var none = ShopScreen.MerchantBustSize(new UiVec(0f, 0f));
            Assert.AreEqual(200f, none.X, 0.01f);
            Assert.AreEqual(200f, none.Y, 0.01f);
        }

        // The shelf sizes are ShopStock's own constants, decided from gate
        // 1's batch (docs/handoffs/shop_v2/GAP_AUDIT.md, "Gate exit checks
        // -> Gate 1"). Pinned against the constant, never a literal, so a
        // future re-decision moves one number and this test still passes.
        [Test]
        public void CardCountsMatchShopStocksOwnConstants()
        {
            var screen = ShopScreen.Build();

            Assert.AreEqual(ShopStock.GearCount, screen.GearCards.Count);
            Assert.AreEqual(ShopStock.BookCount, screen.BookCards.Count);
            Assert.AreEqual(ShopStock.RelicCount, screen.RelicCards.Count);
            Assert.AreEqual(ShopScreen.PackRowCount, screen.PackRows.Count);
        }

        [Test]
        public void EveryOfferCardHasAllThreeLabelsDeclared()
        {
            var screen = ShopScreen.Build();

            foreach (var card in screen.GearCards.Concat(screen.BookCards).Concat(screen.RelicCards))
            {
                Assert.IsTrue(card.Button.IsValid);
                Assert.IsTrue(card.Name.IsValid);
                Assert.IsTrue(card.Meta.IsValid);
                Assert.IsTrue(card.Price.IsValid);
            }
        }

        [Test]
        public void EveryPackRowHasBothSellButtonsDeclared()
        {
            var screen = ShopScreen.Build();

            foreach (var row in screen.PackRows)
            {
                Assert.IsTrue(row.Row.IsValid);
                Assert.IsTrue(row.SellOne.IsValid);
                Assert.IsTrue(row.SellAll.IsValid);
            }
        }
    }
}
