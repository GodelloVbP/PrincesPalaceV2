using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // Where a hover tooltip lands, pinned with literals.
    //
    // Worth its own fixture rather than being left to the two screens that use
    // it: the rule is four cases (fits right, does not, fits neither, taller
    // than the box) and only the first of them is ever visible in a casual
    // playtest. The other three are what a capture catches weeks later, if at
    // all -- the strip this replaced on the Reckoning survived for exactly that
    // reason.
    public class TooltipPlacementTests
    {
        // A generous interior, so nothing in these cases is clamped by
        // accident: the numbers below are about the flip, not the walls.
        private const float Left = -500f;
        private const float Right = 500f;
        private const float Bottom = -300f;
        private const float Top = 300f;

        private static UiVec Place(float anchorX, float anchorY, float anchorWidth,
                                   float w = 200f, float h = 200f,
                                   float left = Left, float right = Right,
                                   float bottom = Bottom, float top = Top) =>
            TooltipPlacement.Beside(anchorX, anchorY, anchorWidth, w, h, left, right, bottom, top);

        [Test]
        public void WithRoomOnTheRight_ItOpensToTheRight()
        {
            // anchor centre -300, half a 100-wide anchor is 50, gap 14,
            // half a 200-wide tooltip is 100: -300 + 164 = -136.
            var at = Place(anchorX: -300f, anchorY: 0f, anchorWidth: 100f);

            Assert.AreEqual(-136f, at.X, 0.01f);
            Assert.AreEqual(0f, at.Y, 0.01f, "level with the anchor when nothing pushes it");
        }

        [Test]
        public void WithNoRoomOnTheRight_ItFlipsToTheLeft()
        {
            // Right edge would be 400 + 164 + 100 = 664, past the wall at 500.
            var at = Place(anchorX: 400f, anchorY: 0f, anchorWidth: 100f);

            Assert.AreEqual(236f, at.X, 0.01f, "400 - 164");
            Assert.LessOrEqual(at.X + 100f, Right, "and it is inside the wall it flipped away from");
        }

        // The case the dossier's own copy of this got wrong and nothing caught,
        // because its tooltip is narrow and its cells are small. A wide tooltip
        // on a wide anchor has room on neither side, and the flip alone would
        // have put it further outside than it started.
        [Test]
        public void WithRoomOnNeitherSide_ItIsPushedBackInsideRatherThanHangingOff()
        {
            var at = Place(anchorX: 0f, anchorY: 0f, anchorWidth: 700f,
                           w: 400f, left: -300f, right: 300f);

            Assert.GreaterOrEqual(at.X - 200f, -300f, "left edge inside");
            Assert.LessOrEqual(at.X + 200f, 300f, "right edge inside");
        }

        [Test]
        public void NearTheTop_ItSlidesDownRatherThanLeavingThePanel()
        {
            var at = Place(anchorX: 0f, anchorY: 280f, anchorWidth: 100f);

            Assert.AreEqual(200f, at.Y, 0.01f, "top 300 less half of 200");
        }

        [Test]
        public void NearTheBottom_ItSlidesUp()
        {
            var at = Place(anchorX: 0f, anchorY: -290f, anchorWidth: 100f);

            Assert.AreEqual(-200f, at.Y, 0.01f);
        }

        // An interior smaller than the tooltip makes the two clamp bounds
        // cross. min-then-max in that order would pin it to whichever wall the
        // arithmetic happened to reach last; centring is the only answer that
        // degrades instead of jumping.
        [Test]
        public void ATooltipTallerThanTheInterior_IsCentredRatherThanPinnedToAWall()
        {
            var at = Place(anchorX: 0f, anchorY: 0f, anchorWidth: 100f,
                           h: 800f, bottom: -100f, top: 100f);

            Assert.AreEqual(0f, at.Y, 0.01f);
        }

        // ---- against the Reckoning's real geometry ---------------------------

        // The screen this was written for, at the row every player below level
        // 50 actually sees. All three cards have to land somewhere legal, and
        // the outer two are the ones the old "a lot of arithmetic for no gain"
        // comment said could not be made to work.
        [Test]
        public void EveryOfferCardInAThreeCardRow_PlacesItsTooltipInsideTheFrame()
        {
            const float Margin = 8f;
            float halfW = ReckoningScreen.TooltipWidth * 0.5f;
            float halfH = ReckoningScreen.TooltipHeight * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                var at = TooltipPlacement.Beside(
                    OfferRowLayout.CardX(i, 3), 0f, OfferRowLayout.CardWidth(3),
                    ReckoningScreen.TooltipWidth, ReckoningScreen.TooltipHeight,
                    -ReckoningScreen.ContentHalfWidth + Margin,
                    ReckoningScreen.ContentHalfWidth - Margin,
                    ReckoningScreen.ContentBottom + Margin,
                    ReckoningScreen.ContentTop - Margin);

                Assert.GreaterOrEqual(at.X - halfW, -ReckoningScreen.ContentHalfWidth,
                    $"card {i}'s tooltip runs off the left of the painted interior");
                Assert.LessOrEqual(at.X + halfW, ReckoningScreen.ContentHalfWidth,
                    $"card {i}'s tooltip runs off the right of the painted interior");
                Assert.GreaterOrEqual(at.Y - halfH, ReckoningScreen.ContentBottom,
                    $"card {i}'s tooltip sits on the frame's bottom ornament");
                Assert.LessOrEqual(at.Y + halfH, ReckoningScreen.ContentTop,
                    $"card {i}'s tooltip sits on the frame's crest");
            }
        }

        // The widest row the reward track can grant. Four narrower cards means
        // four more chances for a flip to land badly.
        [Test]
        public void EveryOfferCardInTheWidestRow_PlacesItsTooltipInsideTheFrame()
        {
            const float Margin = 8f;
            float halfW = ReckoningScreen.TooltipWidth * 0.5f;

            for (int i = 0; i < OfferRowLayout.MaxCards; i++)
            {
                var at = TooltipPlacement.Beside(
                    OfferRowLayout.CardX(i, OfferRowLayout.MaxCards), 0f,
                    OfferRowLayout.CardWidth(OfferRowLayout.MaxCards),
                    ReckoningScreen.TooltipWidth, ReckoningScreen.TooltipHeight,
                    -ReckoningScreen.ContentHalfWidth + Margin,
                    ReckoningScreen.ContentHalfWidth - Margin,
                    ReckoningScreen.ContentBottom + Margin,
                    ReckoningScreen.ContentTop - Margin);

                Assert.GreaterOrEqual(at.X - halfW, -ReckoningScreen.ContentHalfWidth, $"card {i}");
                Assert.LessOrEqual(at.X + halfW, ReckoningScreen.ContentHalfWidth, $"card {i}");
            }
        }

        // A tooltip must never sit centred on the card it describes -- covering
        // the item you are weighing is the failure the pack's own placement
        // comment records ("the box answering the question was covering the
        // evidence").
        [Test]
        public void ATooltipNeverLandsOnTopOfTheCardItDescribes()
        {
            const float Margin = 8f;

            for (int i = 0; i < 3; i++)
            {
                float cardX = OfferRowLayout.CardX(i, 3);
                var at = TooltipPlacement.Beside(
                    cardX, 0f, OfferRowLayout.CardWidth(3),
                    ReckoningScreen.TooltipWidth, ReckoningScreen.TooltipHeight,
                    -ReckoningScreen.ContentHalfWidth + Margin,
                    ReckoningScreen.ContentHalfWidth - Margin,
                    ReckoningScreen.ContentBottom + Margin,
                    ReckoningScreen.ContentTop - Margin);

                float clearance = System.Math.Abs(at.X - cardX)
                                  - OfferRowLayout.CardWidth(3) * 0.5f
                                  - ReckoningScreen.TooltipWidth * 0.5f;

                Assert.GreaterOrEqual(clearance, 0f,
                    $"card {i}'s tooltip overlaps the card it is describing");
            }
        }
    }
}
