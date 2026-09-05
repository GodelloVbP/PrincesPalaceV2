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
