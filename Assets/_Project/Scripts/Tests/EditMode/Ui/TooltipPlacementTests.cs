using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
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

        // anchorHeight defaults to 100, matching the square-ish anchor the
        // original cases were written against -- it is read only by the
        // never-overlap fallback (a box placed under or over its anchor has
        // to know how tall the anchor is), so the cases that resolve beside
        // the anchor are unaffected by it.
        private static UiVec Place(float anchorX, float anchorY, float anchorWidth,
                                   float w = 200f, float h = 200f,
                                   float left = Left, float right = Right,
                                   float bottom = Bottom, float top = Top,
                                   float anchorHeight = 100f) =>
            TooltipPlacement.Beside(anchorX, anchorY, anchorWidth, anchorHeight,
                w, h, left, right, bottom, top);

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

        // ---- and it never lands on its subject (gamepad phase 3b, job 1) -----
        //
        // The three cases above pin "beside", "flipped" and "inside". This
        // group pins the rule added for the focus-driven path, where the
        // player has no cursor to move off a box that covers the thing it
        // describes: no side has room, so it goes UNDER the anchor rather
        // than being clamped on top of it.

        [Test]
        public void WithRoomOnNeitherSide_ItGoesUnderTheAnchorRatherThanOverIt()
        {
            // Same geometry as the case above: a 400-wide box beside a
            // 700-wide anchor needs 0 +/- 564, and the walls are at +/-300.
            // Under it is anchorY - (14 + 50 + 100) = -164, whose bottom edge
            // (-264) still clears the floor at -300.
            var at = Place(anchorX: 0f, anchorY: 0f, anchorWidth: 700f, anchorHeight: 100f,
                           w: 400f, left: -300f, right: 300f);

            Assert.AreEqual(0f, at.X, 0.01f, "centred on the anchor, since neither side has room");
            Assert.AreEqual(-164f, at.Y, 0.01f, "under the anchor: -(14 gap + 50 half-anchor + 100 half-tooltip)");
            Assert.LessOrEqual(at.Y + 100f, -50f, "and its top edge is below the anchor's bottom edge");
        }

        [Test]
        public void WithNoRoomBelowEither_ItGoesOverTheAnchor()
        {
            // The floor is raised to -160, so the under-the-anchor answer
            // (-164, bottom edge -264) no longer fits and over it must be
            // taken: +164, top edge 264, against a ceiling at 300.
            var at = Place(anchorX: 0f, anchorY: 0f, anchorWidth: 700f, anchorHeight: 100f,
                           w: 400f, h: 200f, left: -300f, right: 300f, bottom: -160f, top: 300f);

            Assert.AreEqual(164f, at.Y, 0.01f);
            Assert.GreaterOrEqual(at.Y - 100f, 50f, "its bottom edge is above the anchor's top edge");
        }

        // The whole rule in one assertion, over a grid of anchor positions
        // rather than one sample: for every cell of a 3x2 block in a box only
        // just big enough to hold the tooltip beside it, the box clears the
        // cell it belongs to on one axis or the other.
        [Test]
        public void AcrossAWholeBlockOfAnchors_TheBoxAlwaysClearsItsOwnAnchor()
        {
            const float AnchorW = 130f;
            const float AnchorH = 86f;
            const float TipW = 300f;
            const float TipH = 480f;
            const float L = -792f, R = 792f, B = -394f, T = 394f;

            for (int column = 0; column < 3; column++)
            {
                for (int row = 0; row < 2; row++)
                {
                    float ax = 426f + column * 133f;
                    float ay = 300f - row * AnchorH;

                    var at = TooltipPlacement.Beside(ax, ay, AnchorW, AnchorH,
                        TipW, TipH, L, R, B, T);

                    bool clearsX = System.Math.Abs(at.X - ax) >= (AnchorW + TipW) * 0.5f;
                    bool clearsY = System.Math.Abs(at.Y - ay) >= (AnchorH + TipH) * 0.5f;

                    Assert.IsTrue(clearsX || clearsY,
                        $"the box at column {column}, row {row} overlaps the anchor it describes");
                    Assert.GreaterOrEqual(at.X - TipW * 0.5f, L, "left edge inside");
                    Assert.LessOrEqual(at.X + TipW * 0.5f, R, "right edge inside");
                }
            }
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

        // ReckoningScreen.CardHeight is private to the screen that draws it,
        // and a copy of it would be a second opinion about a number this file
        // has no business owning. It is read only by the under/over fallback,
        // which neither case below reaches -- both assert the tooltip lands
        // BESIDE its card, which is the whole point of them -- so a stand-in
        // is honest here in a way a pinned copy would not be.
        private const float OfferCardHeight = 300f;


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
                    OfferRowLayout.CardX(i, 3), 0f, OfferRowLayout.CardWidth(3), OfferCardHeight,
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
                    cardX, 0f, OfferRowLayout.CardWidth(3), OfferCardHeight,
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

        // ---- against the dossier's real geometry, at the new 420x420 size ---

        // ITEM-MODIFIER PLAN / "roughly square tooltips" (2026-09-22):
        // BuildTooltip moved off its old 300x480 sliver onto
        // ItemComparisonPanel's shared 420x420 shape. Beside's own clamp is
        // general (AcrossAWholeBlockOfAnchors above already proves both axes
        // for arbitrary geometry) but a panel this much wider deserves its
        // own pin against the two anchors furthest from centre: the
        // rightmost pack cell (column A, hugging the left wall) and the
        // lowest equipment slot on EACH file (column B, a left-file and a
        // right-file anchor exercise opposite branches of the right/left
        // check). DossierLayout's own methods, not restated literals, so a
        // future layout change cannot silently invalidate this pin without
        // the numbers here moving with it.
        private const float DossierMargin = 8f;

        private static void AssertInsideDossier(UiVec at, float tooltipHalf)
        {
            float halfW = DossierLayout.HalfWidth;
            float halfH = DossierLayout.HalfHeight;

            Assert.GreaterOrEqual(at.X - tooltipHalf, -halfW + DossierMargin, "left edge inside the dossier");
            Assert.LessOrEqual(at.X + tooltipHalf, halfW - DossierMargin, "right edge inside the dossier");
            Assert.GreaterOrEqual(at.Y - tooltipHalf, -halfH + DossierMargin, "bottom edge inside the dossier");
            Assert.LessOrEqual(at.Y + tooltipHalf, halfH - DossierMargin, "top edge inside the dossier");
        }

        [Test]
        public void TheDossierTooltipAt420_StaysInsideThePanelBesideTheRightmostPackSlot()
        {
            float halfW = DossierLayout.HalfWidth;
            float halfH = DossierLayout.HalfHeight;

            int rightmostColumn = (int)DossierLayout.PackColumns - 1;
            float anchorX = DossierLayout.ColumnACentreX + DossierLayout.PackCellCentreX(rightmostColumn);
            float anchorY = DossierLayout.PackCellCentreY(0);

            var at = TooltipPlacement.Beside(
                anchorX, anchorY, DossierLayout.PackCellWidth, DossierLayout.PackCellHeight,
                420f, 420f,
                -halfW + DossierMargin, halfW - DossierMargin,
                -halfH + DossierMargin, halfH - DossierMargin);

            AssertInsideDossier(at, 210f);
        }

        [Test]
        public void TheDossierTooltipAt420_StaysInsideThePanelBesideTheLowestEquipmentSlot()
        {
            float halfW = DossierLayout.HalfWidth;
            float halfH = DossierLayout.HalfHeight;

            // Legs (left file) and Shoes (right file) share the lowest
            // authored row (SlotGeometry's own top: 452 for both), which is
            // exactly why both are checked here rather than one: a left-file
            // anchor tries "right, into the mannequin" first and a
            // right-file anchor tries "right, off the panel" first, and only
            // running both proves the clamp holds for either starting side.
            foreach (var slot in new[] { EquipmentSlot.Legs, EquipmentSlot.Shoes })
            {
                var anchor = DossierLayout.SlotAt(slot);

                var at = TooltipPlacement.Beside(
                    anchor.X, anchor.Y, DossierLayout.SlotSize, DossierLayout.SlotSize,
                    420f, 420f,
                    -halfW + DossierMargin, halfW - DossierMargin,
                    -halfH + DossierMargin, halfH - DossierMargin);

                AssertInsideDossier(at, 210f);
            }
        }
    }
}
