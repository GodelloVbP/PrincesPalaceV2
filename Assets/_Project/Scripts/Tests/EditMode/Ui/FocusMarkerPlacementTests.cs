using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The placement arithmetic behind the one pad-focus marker
    // (Core/FocusMarker.cs, hardware round 1's visual pass), pinned against
    // LITERAL rects and LITERAL expected coordinates.
    //
    // Literal on both sides on purpose (CLAUDE.md gotcha 5): a test that
    // recomputed `target.Left - Gap - Size / 2` would pass for any value of
    // those three constants and prove only that the compiler works. The
    // numbers below were worked out by hand from a 1920x1080 canvas and the
    // kit's own 26-unit marker, and changing Gap or Size is meant to fail
    // here.
    //
    // The canvas throughout is the project's reference frame: 1920x1080,
    // centre origin, +y UP (UiVec's own convention), so its edges are
    // x -960..960 and y -540..540.
    public class FocusMarkerPlacementTests
    {
        private static readonly UiRect Canvas =
            new UiRect(UiVec.Zero, new UiVec(1920f, 1080f));

        private static UiRect Box(float cx, float cy, float w, float h) =>
            new UiRect(new UiVec(cx, cy), new UiVec(w, h));

        // ---- which side ---------------------------------------------------

        [Test]
        public void AWideRowTakesTheMarkerOnItsLeft()
        {
            // A Fight submenu row's shape: far wider than tall.
            Assert.AreEqual(FocusEdge.Left, FocusMarkerPlacement.EdgeFor(new UiVec(400f, 60f)));
        }

        [Test]
        public void ASquareControlTakesTheMarkerAboveIt()
        {
            // A Talent orb, and the Hub's 620x620 gate, are both this shape.
            Assert.AreEqual(FocusEdge.Above, FocusMarkerPlacement.EdgeFor(new UiVec(96f, 96f)));
            Assert.AreEqual(FocusEdge.Above, FocusMarkerPlacement.EdgeFor(new UiVec(620f, 620f)));
        }

        [Test]
        public void ATallCardTakesTheMarkerAboveIt()
        {
            // A Reckoning offer card / a Dossier pack cell: taller than wide.
            Assert.AreEqual(FocusEdge.Above, FocusMarkerPlacement.EdgeFor(new UiVec(220f, 320f)));
        }

        [Test]
        public void TheWideThresholdIsInclusiveAndSitsAt1Point8()
        {
            // Exactly 1.8:1 is wide; a hair under is not. Pinned because the
            // whole left-versus-above rule turns on this one number and
            // nothing else in the project declares it.
            Assert.AreEqual(FocusEdge.Left, FocusMarkerPlacement.EdgeFor(new UiVec(180f, 100f)));
            Assert.AreEqual(FocusEdge.Above, FocusMarkerPlacement.EdgeFor(new UiVec(179f, 100f)));
        }

        [Test]
        public void AZeroHeightControlIsNeverCalledWide()
        {
            // Division by zero would make every degenerate rect "infinitely
            // wide". A rect with no height is a control mid-layout or mid-
            // hide, and putting the marker above it is the harmless answer.
            Assert.AreEqual(FocusEdge.Above, FocusMarkerPlacement.EdgeFor(new UiVec(400f, 0f)));
        }

        // ---- where exactly ------------------------------------------------

        [Test]
        public void TheMarkerSitsLeftOfARowAtTheRowsOwnHeight()
        {
            // Row centred at (0, 100), 400 wide: its left edge is x = -200.
            // Gap 10 then half of the 26-unit marker: -200 - 10 - 13 = -223.
            var at = FocusMarkerPlacement.Place(Box(0f, 100f, 400f, 60f), Canvas);

            Assert.AreEqual(-223f, at.X, 0.001f, "x: left edge, minus the gap, minus half the marker");
            Assert.AreEqual(100f, at.Y, 0.001f, "y: level with the row's own centre");
        }

        [Test]
        public void TheMarkerSitsAboveACardOnItsOwnCentreLine()
        {
            // Card centred at (300, -50), 320 tall: its top edge is y = 110.
            // 110 + 10 + 13 = 133.
            var at = FocusMarkerPlacement.Place(Box(300f, -50f, 220f, 320f), Canvas);

            Assert.AreEqual(300f, at.X, 0.001f, "x: on the card's own centre line");
            Assert.AreEqual(133f, at.Y, 0.001f, "y: top edge, plus the gap, plus half the marker");
        }

        [Test]
        public void AControlFlushWithTheCanvasTopKeepsTheMarkerOnScreen()
        {
            // Card centred at (0, 400), 280 tall: top edge y = 540, which IS
            // the canvas top. Unclamped the marker would want y = 563; the
            // clamp brings its own top edge back onto the canvas top, so its
            // centre lands at 540 - 13 = 527.
            var at = FocusMarkerPlacement.Place(Box(0f, 400f, 200f, 280f), Canvas);

            Assert.AreEqual(0f, at.X, 0.001f);
            Assert.AreEqual(527f, at.Y, 0.001f, "the marker's own top edge stops at the canvas top");
        }

        [Test]
        public void ARowFlushWithTheCanvasLeftKeepsTheMarkerOnScreen()
        {
            // Row centred at (-860, 0), 180 wide: left edge x = -950.
            // Unclamped the marker would want -973, outside the canvas;
            // clamped, its centre lands at -960 + 13 = -947.
            var at = FocusMarkerPlacement.Place(Box(-860f, 0f, 180f, 60f), Canvas);

            Assert.AreEqual(-947f, at.X, 0.001f, "the marker's own left edge stops at the canvas left");
            Assert.AreEqual(0f, at.Y, 0.001f);
        }

        [Test]
        public void TheClampNeverRePicksTheSide()
        {
            // The same flush-left row, asked for explicitly. A clamp that
            // flipped to the other side would put the marker over the row
            // rather than beside it, and the arrow would then point away
            // from the thing it names.
            var at = FocusMarkerPlacement.Place(Box(-860f, 0f, 180f, 60f), Canvas, FocusEdge.Left);
            Assert.AreEqual(0f, at.Y, 0.001f, "still on the row's own height, not above it");
        }

        // ---- a control inside a clipping window --------------------------

        [Test]
        public void ARowInsideItsWindowIsThereToPointAt()
        {
            // The fight submenu's shape: a 240x300 window centred at (0, 0),
            // so y -150..150; a 40-tall row centred at y = 100 is inside it.
            Assert.IsTrue(FocusMarkerPlacement.IsVisibleWithin(Box(0f, 100f, 240f, 40f), Box(0f, 0f, 240f, 300f)));
        }

        [Test]
        public void ARowScrolledAboveItsWindowIsNot()
        {
            // Centre at y = 200, past the window's top edge at 150: the list
            // was wheeled down and this row is drawn nowhere.
            Assert.IsFalse(FocusMarkerPlacement.IsVisibleWithin(Box(0f, 200f, 240f, 40f), Box(0f, 0f, 240f, 300f)));
        }

        [Test]
        public void ARowHalfOutStillCountsWhileItsCentreIsIn()
        {
            // Centre exactly on the top edge (150): half the row still shows,
            // and it is the half the arrow lines up with. Inclusive on purpose.
            Assert.IsTrue(FocusMarkerPlacement.IsVisibleWithin(Box(0f, 150f, 240f, 40f), Box(0f, 0f, 240f, 300f)));
            Assert.IsFalse(FocusMarkerPlacement.IsVisibleWithin(Box(0f, 150.5f, 240f, 40f), Box(0f, 0f, 240f, 300f)));
        }

        [Test]
        public void AControlBesideTheWindowIsNotInIt()
        {
            // Same height, but centred at x = 300, right of the window's
            // right edge at 120.
            Assert.IsFalse(FocusMarkerPlacement.IsVisibleWithin(Box(300f, 0f, 40f, 40f), Box(0f, 0f, 240f, 300f)));
        }

        // ---- which way it points ------------------------------------------

        [Test]
        public void TheArrowPointsRightFromTheLeftAndDownFromAbove()
        {
            // The sprite is baked pointing right (ProceduralSpriteBaker.
            // BakeFocusArrow), so Left is unrotated and Above is a quarter
            // turn clockwise -- negative, in Unity's counter-clockwise-
            // positive convention.
            Assert.AreEqual(0f, FocusMarkerPlacement.RotationFor(FocusEdge.Left), 0.001f);
            Assert.AreEqual(-90f, FocusMarkerPlacement.RotationFor(FocusEdge.Above), 0.001f);
        }

        // ---- the bob ------------------------------------------------------

        [Test]
        public void TheBobRunsAlongThePointingAxisAndNotAcrossIt()
        {
            // t = 0 is the top of the cosine: the full amplitude, toward the
            // control in both cases (+x from the left, -y from above).
            var left = FocusMarkerPlacement.BobOffset(FocusEdge.Left, 0f);
            Assert.AreEqual(3f, left.X, 0.001f);
            Assert.AreEqual(0f, left.Y, 0.001f, "a left-hand marker never moves vertically");

            var above = FocusMarkerPlacement.BobOffset(FocusEdge.Above, 0f);
            Assert.AreEqual(0f, above.X, 0.001f, "an above marker never moves horizontally");
            Assert.AreEqual(-3f, above.Y, 0.001f);
        }

        [Test]
        public void TheBobIsBackWhereItStartedAfterOnePeriod()
        {
            // 1.6s is the period; half of it is the far end of the travel.
            var half = FocusMarkerPlacement.BobOffset(FocusEdge.Left, 0.8f);
            Assert.AreEqual(-3f, half.X, 0.001f);

            var full = FocusMarkerPlacement.BobOffset(FocusEdge.Left, 1.6f);
            Assert.AreEqual(3f, full.X, 0.001f);
        }

        [Test]
        public void TheBobIsSmallEnoughToReadAsBreathing()
        {
            // "A gentle bob of a few pixels", not a control that moves. Pinned
            // so a later tuning pass has to change a test to make it loud.
            Assert.LessOrEqual(FocusMarkerPlacement.BobAmplitude, 4f);
            Assert.LessOrEqual(FocusMarkerPlacement.Size, 30f);
        }
    }
}
