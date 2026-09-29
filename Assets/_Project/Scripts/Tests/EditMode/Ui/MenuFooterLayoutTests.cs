using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Where the footer's parts sit (spec section 2), pinned as literals worked
    // out by hand from the spec's numbers.
    public class MenuFooterLayoutTests
    {
        // The footer is 48 tall, 4 below the panel: the panel's bottom edge is
        // at screen y 990 on the 1080 stage, so the footer spans 994..1042 and
        // its centre is 1018, that is -478 from the stage's centre.
        [Test]
        public void TheFooterSitsFourPixelsBelowThePanel()
        {
            Assert.AreEqual(990f, MenuFooterLayout.PanelBottomScreenY, 0.01f);
            Assert.AreEqual(-478f, MenuFooterLayout.CentreY, 0.01f);
            Assert.AreEqual(1600f, MenuFooterLayout.Width);
            Assert.AreEqual(48f, MenuFooterLayout.Height);
        }

        // The panes keep their height: the footer is outside the panel.
        [Test]
        public void TheFooterDoesNotTakeHeightFromThePanes()
        {
            Assert.AreEqual(804f, SystemMenuLayout.ContentHeight, 0.01f);
            Assert.AreEqual(900f, SystemMenuLayout.PanelHeight);
        }

        [Test]
        public void TheLintelFooterAndPanelFitTheStage()
        {
            float footerBottom = MenuFooterLayout.PanelBottomScreenY + MenuFooterLayout.GapBelowPanel
                                 + MenuFooterLayout.Height;

            Assert.AreEqual(1042f, footerBottom, 0.01f);
            Assert.LessOrEqual(footerBottom, 1080f);
        }

        [Test]
        public void TheNoticeAndThePromptAreaDoNotShareRoom()
        {
            float noticeRight = MenuFooterLayout.NoticeCentreX + MenuFooterLayout.NoticeWidth * 0.5f;

            Assert.AreEqual(-426f, MenuFooterLayout.NoticeCentreX, 0.01f);
            Assert.AreEqual(-76f, noticeRight, 0.01f);
            Assert.AreEqual(-44f, MenuFooterLayout.PromptsLeftX, 0.01f);
            Assert.AreEqual(776f, MenuFooterLayout.PromptsRightX, 0.01f);
            Assert.AreEqual(820f, MenuFooterLayout.PromptsWidth, 0.01f);
        }

        [Test]
        public void AGlyphPlateIsAtLeast32WideAndFitsItsLetters()
        {
            Assert.AreEqual(32f, MenuFooterLayout.PlateWidthFor(10f));
            Assert.AreEqual(32f, MenuFooterLayout.PlateWidthFor(16f));
            Assert.AreEqual(59f, MenuFooterLayout.PlateWidthFor(42.2f), "42.2 + 16, up to the pixel");
        }

        // One pair: glyph 32, 8 to the label of 60, so 100 wide, flush right at
        // 776: the plate spans 676..708 (centre 692), the label 716..776
        // (centre 746).
        [Test]
        public void OnePairIsFlushAgainstTheRightEdge()
        {
            var row = MenuFooterLayout.Arrange(new[] { 10f }, new[] { 60f });

            Assert.AreEqual(1, row.Count);
            Assert.AreEqual(32f, row[0].PlateWidth);
            Assert.AreEqual(692f, row[0].PlateCentreX, 0.01f);
            Assert.AreEqual(60f, row[0].LabelWidth);
            Assert.AreEqual(746f, row[0].LabelCentreX, 0.01f);
        }

        // Two pairs of 100 with 24 between: 224 wide, from 552. The first plate
        // spans 552..584 (568), its label 592..652 (622); the second plate
        // 676..708 (692), its label 716..776 (746).
        [Test]
        public void PairsAreTwentyFourApartInOrder()
        {
            var row = MenuFooterLayout.Arrange(new[] { 10f, 10f }, new[] { 60f, 60f });

            Assert.AreEqual(2, row.Count);
            Assert.AreEqual(568f, row[0].PlateCentreX, 0.01f);
            Assert.AreEqual(622f, row[0].LabelCentreX, 0.01f);
            Assert.AreEqual(692f, row[1].PlateCentreX, 0.01f);
            Assert.AreEqual(746f, row[1].LabelCentreX, 0.01f);
        }

        // A key name is a wider plate: "Enter" measures about 62, so 78.
        [Test]
        public void AWideGlyphWidensItsPlateAndPushesItsLabel()
        {
            var row = MenuFooterLayout.Arrange(new[] { 62f }, new[] { 60f });

            Assert.AreEqual(78f, row[0].PlateWidth);
            Assert.AreEqual(776f - 60f - 8f - 39f, row[0].PlateCentreX, 0.01f);
        }

        // Pairs are kept from the head until one would cross into the notice's
        // room: the tail goes, because the head is Select and Close.
        [Test]
        public void ARowTooLongLosesItsTail()
        {
            var glyphs = Enumerable.Repeat(10f, 5).ToArray();
            var labels = new[] { 60f, 60f, 300f, 300f, 300f };

            var row = MenuFooterLayout.Arrange(glyphs, labels);

            // 100 + 24 + 100 + 24 + 340 = 588 fits 820; the next 24 + 340 makes 952.
            Assert.AreEqual(3, row.Count);
            Assert.AreEqual(776f, row[2].LabelCentreX + row[2].LabelWidth * 0.5f, 0.01f,
                "what stays is still flush right");
        }

        [Test]
        public void NoMoreThanTheAuthoredSlotsAreEverPlaced()
        {
            var glyphs = Enumerable.Repeat(10f, 9).ToArray();
            var labels = Enumerable.Repeat(20f, 9).ToArray();

            Assert.AreEqual(MenuFooterLayout.PromptSlots, MenuFooterLayout.Arrange(glyphs, labels).Count);
        }

        [Test]
        public void AnEmptyRowPlacesNothing()
        {
            Assert.AreEqual(0, MenuFooterLayout.Arrange(new float[0], new float[0]).Count);
        }

        [Test]
        public void TheDefaultRowFitsAtTypicalWidthsOnEveryDevice()
        {
            // The longest default: Character's four. Keyboard names run wider
            // than the pad's letters, so it is the one to fit.
            var glyphs = new[] { 62f, 34f, 56f, 32f };
            var labels = new[] { 66f, 52f, 34f, 92f };

            Assert.AreEqual(4, MenuFooterLayout.Arrange(glyphs, labels).Count);
        }
    }
}
