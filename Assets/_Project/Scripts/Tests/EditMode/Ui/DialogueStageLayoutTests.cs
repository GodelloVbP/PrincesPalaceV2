using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The dialogue stage's geometry (docs/PLAN_DIALOGUE_STAGE.md contracts
    // 7-9), pinned as literals worked out by hand -- never recomputed from
    // the formula under test.
    public class DialogueStageLayoutTests
    {
        private const float Tolerance = 0.01f;

        // Dungeon.png, the default backdrop, is 1672x941.
        private static readonly UiVec Dungeon = new UiVec(1672f, 941f);

        [Test]
        public void CoverOnAWideCanvasFitsTheWidthAndOverhangsTheHeight()
        {
            // 2580/1672 = 1.54306 beats 1080/941 = 1.14772.
            var cover = DialogueStageLayout.CoverSize(Dungeon, new UiVec(2580f, 1080f));

            Assert.AreEqual(2580f, cover.X, Tolerance);
            Assert.AreEqual(1452.02f, cover.Y, Tolerance);
        }

        [Test]
        public void CoverOnATallCanvasFitsTheHeightAndOverhangsTheWidth()
        {
            // 1440/941 = 1.53029 beats 1920/1672 = 1.14833.
            var cover = DialogueStageLayout.CoverSize(Dungeon, new UiVec(1920f, 1440f));

            Assert.AreEqual(2558.64f, cover.X, Tolerance);
            Assert.AreEqual(1440f, cover.Y, Tolerance);
        }

        [Test]
        public void CoverNeverLeavesAGapOnEitherAxis()
        {
            // 1672x941 is a hair wider than 16:9, so the reference canvas
            // fits its width and overhangs by half a pixel of height.
            var cover = DialogueStageLayout.CoverSize(Dungeon, new UiVec(1920f, 1080f));

            Assert.AreEqual(1920f, cover.X, Tolerance);
            Assert.AreEqual(1080.57f, cover.Y, Tolerance);
        }

        [Test]
        public void CoverOfNoSpriteIsTheCanvasItself()
        {
            var cover = DialogueStageLayout.CoverSize(new UiVec(0f, 0f), new UiVec(1920f, 1440f));

            Assert.AreEqual(1920f, cover.X);
            Assert.AreEqual(1440f, cover.Y);
        }

        // The owl's canvas is 1122x1402, the sheep's 1408x1402.
        [Test]
        public void TheBustIs840TallAtItsOwnAspect()
        {
            var owl = DialogueStageLayout.BustSize(new UiVec(1122f, 1402f));
            var sheep = DialogueStageLayout.BustSize(new UiVec(1408f, 1402f));

            Assert.AreEqual(840f, owl.Y);
            Assert.AreEqual(672.24f, owl.X, Tolerance);
            Assert.AreEqual(840f, sheep.Y);
            Assert.AreEqual(843.59f, sheep.X, Tolerance);
        }

        // Painted facing right: as painted on the left, mirrored on the right.
        [Test]
        public void OnlyARightSideBustIsMirrored()
        {
            Assert.AreEqual(1f, DialogueStageLayout.BustMirrorX(DialogueSide.Left));
            Assert.AreEqual(-1f, DialogueStageLayout.BustMirrorX(DialogueSide.Right));
        }

        // The pivot is the bust's centre so the mirror flips it in place.
        [Test]
        public void TheBustStandsAgainstItsOwnEdgeAboutItsCentre()
        {
            var left = DialogueStageLayout.BustPin(DialogueSide.Left, 600f);
            var right = DialogueStageLayout.BustPin(DialogueSide.Right, 600f);

            Assert.AreEqual((0f, 0.5f, 300f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 0.5f, -300f), (right.AnchorX, right.PivotX, right.OffsetX));
        }

        // ---- framing -----------------------------------------------------------

        [Test]
        public void NarrationFramesAsNarrationWhateverItsSideOrBust()
        {
            Assert.AreEqual(StageFraming.Narration, DialogueStageLayout.FramingFor(true, true, DialogueSide.Left));
            Assert.AreEqual(StageFraming.Narration, DialogueStageLayout.FramingFor(true, false, DialogueSide.Right));
        }

        [Test]
        public void ASpeakerWithABustFramesOnTheirOwnSide()
        {
            Assert.AreEqual(StageFraming.LeftSpeaker, DialogueStageLayout.FramingFor(false, true, DialogueSide.Left));
            Assert.AreEqual(StageFraming.RightSpeaker, DialogueStageLayout.FramingFor(false, true, DialogueSide.Right));
        }

        // Owner call 2026-09-25: no bust, no empty bust slot.
        [Test]
        public void ASpeakerWhoseBustIsMissingFramesCentredOnEitherSide()
        {
            Assert.AreEqual(StageFraming.CentredSpeaker, DialogueStageLayout.FramingFor(false, false, DialogueSide.Left));
            Assert.AreEqual(StageFraming.CentredSpeaker, DialogueStageLayout.FramingFor(false, false, DialogueSide.Right));
        }

        // ---- box and plate -------------------------------------------------------

        [Test]
        public void TheBoxSitsOppositeTheBustAndCentresWithoutOne()
        {
            var left = DialogueStageLayout.BoxPin(StageFraming.LeftSpeaker);
            var right = DialogueStageLayout.BoxPin(StageFraming.RightSpeaker);
            var narration = DialogueStageLayout.BoxPin(StageFraming.Narration);
            var unseen = DialogueStageLayout.BoxPin(StageFraming.CentredSpeaker);

            // Near edge 608 in from the speaker's edge: at 1920 wide the box
            // spans 608..1888 for a left speaker, 32..1312 for a right one.
            Assert.AreEqual((0f, 0f, 608f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 1f, -608f), (right.AnchorX, right.PivotX, right.OffsetX));
            Assert.AreEqual((0.5f, 0.5f, 0f), (narration.AnchorX, narration.PivotX, narration.OffsetX));
            Assert.AreEqual((0.5f, 0.5f, 0f), (unseen.AnchorX, unseen.PivotX, unseen.OffsetX));
        }

        [Test]
        public void ThePlateSitsAtTheSpeakersEndOfTheBox()
        {
            var left = DialogueStageLayout.PlatePin(StageFraming.LeftSpeaker);
            var right = DialogueStageLayout.PlatePin(StageFraming.RightSpeaker);

            Assert.AreEqual((0f, 0f, 648f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 1f, -648f), (right.AnchorX, right.PivotX, right.OffsetX));
            Assert.AreEqual(304f, DialogueStageLayout.PlateCentreY);
        }

        // Contract 15: no bust, but the name still shows -- at the centred
        // box's left end, 40 in, as it sits 40 in from a speaker's end.
        [Test]
        public void AMissingBustKeepsThePlateAtTheCentredBoxsLeftEnd()
        {
            var plate = DialogueStageLayout.PlatePin(StageFraming.CentredSpeaker);
            var box = DialogueStageLayout.BoxPin(StageFraming.CentredSpeaker);

            Assert.AreEqual((0.5f, 0f, -600f), (plate.AnchorX, plate.PivotX, plate.OffsetX));

            // 1920 wide: the box spans 320..1600, the plate starts at 360.
            Assert.AreEqual(320f, box.LeftEdge(1920f, 1280f), Tolerance);
            Assert.AreEqual(360f, plate.LeftEdge(1920f, 480f), Tolerance);

            // 21:9 (2580 wide): the same, 330 further right.
            Assert.AreEqual(650f, box.LeftEdge(2580f, 1280f), Tolerance);
            Assert.AreEqual(690f, plate.LeftEdge(2580f, 480f), Tolerance);
        }

        // ---- choices ---------------------------------------------------------------

        // Owner call 2026-09-25: a small fixed gap above the box, not above
        // the plate's top.
        [Test]
        public void TheChoicesStandTwentyAboveTheBoxTop()
        {
            Assert.AreEqual(288f, DialogueStageLayout.BoxTop);
            Assert.AreEqual(308f, DialogueStageLayout.ChoicesBottom);
        }

        // 24 margin + 10 gap + 26 marker + 3 bob.
        [Test]
        public void TheRowsNeverStartCloserThan63ToTheLeftEdge()
        {
            Assert.AreEqual(63f, DialogueStageLayout.ChoicesMinLeft);
        }

        [Test]
        public void ForALeftSpeakerTheRowsAreFlushWithTheBoxsFarEnd()
        {
            foreach (float width in new[] { 1920f, 2580f })
            {
                var pin = DialogueStageLayout.ChoicesPin(StageFraming.LeftSpeaker, width, 704f);
                Assert.AreEqual((0f, 1f, 1888f), (pin.AnchorX, pin.PivotX, pin.OffsetX), $"at {width}");
            }
        }

        // At 1920 the box's far end is 32 from the left edge, which put the
        // marker 4-7px from it; the rows move in to 63. At 21:9 the far end
        // is 692 in and the rows stay flush with it.
        [Test]
        public void ForARightSpeakerTheRowsMoveInOnlyWhereTheMarkerNeedsIt()
        {
            var narrow = DialogueStageLayout.ChoicesPin(StageFraming.RightSpeaker, 1920f, 704f);
            var wide = DialogueStageLayout.ChoicesPin(StageFraming.RightSpeaker, 2580f, 704f);

            Assert.AreEqual((0f, 0f, 63f), (narrow.AnchorX, narrow.PivotX, narrow.OffsetX));
            Assert.AreEqual((1f, 0f, -1888f), (wide.AnchorX, wide.PivotX, wide.OffsetX));
        }

        [Test]
        public void ForAMissingBustTheRowsAreFlushWithTheCentredBoxsRightEnd()
        {
            var pin = DialogueStageLayout.ChoicesPin(StageFraming.CentredSpeaker, 1920f, 704f);

            Assert.AreEqual((0.5f, 1f, 640f), (pin.AnchorX, pin.PivotX, pin.OffsetX));
            Assert.AreEqual(896f, pin.LeftEdge(1920f, 704f), Tolerance);
        }

        [Test]
        public void ForNarrationTheRowsAreCentred()
        {
            var pin = DialogueStageLayout.ChoicesPin(StageFraming.Narration, 1920f, 704f);

            Assert.AreEqual((0.5f, 0.5f, 0f), (pin.AnchorX, pin.PivotX, pin.OffsetX));
        }

        // The panel is four rows (356) tall; with one row shown its bottom
        // drops 276 so that row still ends at 308.
        [Test]
        public void ThePanelDropsByTheRowsItDoesNotShow()
        {
            Assert.AreEqual(32f, DialogueStageLayout.ChoicesPanelBottom(356f, 80f), Tolerance);
            Assert.AreEqual(124f, DialogueStageLayout.ChoicesPanelBottom(356f, 172f), Tolerance);
            Assert.AreEqual(308f, DialogueStageLayout.ChoicesPanelBottom(356f, 356f), Tolerance);
        }

        [Test]
        public void ShownRowsStandEightyTallWithTwelveBetween()
        {
            Assert.AreEqual(0f, EventScreen.ChoicesHeightFor(0));
            Assert.AreEqual(80f, EventScreen.ChoicesHeightFor(1));
            Assert.AreEqual(172f, EventScreen.ChoicesHeightFor(2));
            Assert.AreEqual(356f, EventScreen.ChoicesHeightFor(4));
            Assert.AreEqual(356f, EventScreen.ChoicesHeight);
            Assert.AreEqual(704f, EventScreen.ChoiceRowWidth);
        }

        // ---- the whole arrangement, at every audit frame ------------------------------
        //
        // Every framing, every row count, every frame: the rows sit 16-24
        // above the box, clear of the plate; the focus marker beside any row
        // (at rest and at both ends of its bob) stays at least 24 from every
        // screen edge and off the plate. Positions come from the pins through
        // StagePin.LeftEdge, and the marker from FocusMarkerPlacement itself,
        // so this checks the pieces against each other rather than restating
        // a formula.

        private static readonly StageFraming[] AllFramings =
        {
            StageFraming.LeftSpeaker, StageFraming.RightSpeaker, StageFraming.CentredSpeaker, StageFraming.Narration,
        };

        private const float RowHeight = 80f;
        private const float RowPitch = 92f;

        // Stage space: origin bottom-left, +y up.
        private static UiRect FromEdges(float left, float bottom, float width, float height) =>
            new UiRect(new UiVec(left + width * 0.5f, bottom + height * 0.5f), new UiVec(width, height));

        private static bool Overlaps(UiRect a, UiRect b) =>
            a.Left < b.Right && b.Left < a.Right && a.Bottom < b.Top && b.Bottom < a.Top;

        private static IEnumerable<(string Where, UiRect Row, UiRect Marker, UiVec Frame, StageFraming Framing)> EveryRow()
        {
            float width = EventScreen.ChoiceRowWidth;
            foreach (var frame in UiFrames.All)
            {
                // FocusMarkerPlacement works in canvas space (centre origin).
                var canvas = new UiRect(UiVec.Zero, frame);
                var toCanvas = new UiVec(-frame.X * 0.5f, -frame.Y * 0.5f);

                foreach (var framing in AllFramings)
                {
                    float left = DialogueStageLayout.ChoicesPin(framing, frame.X, width).LeftEdge(frame.X, width);

                    for (int shown = 1; shown <= EventScreen.ChoiceRowCount; shown++)
                    {
                        float panelBottom = DialogueStageLayout.ChoicesPanelBottom(
                            EventScreen.ChoicesHeight, EventScreen.ChoicesHeightFor(shown));

                        for (int i = 0; i < shown; i++)
                        {
                            // Row i from the panel's top, as the panel lays them out.
                            float bottom = panelBottom + EventScreen.ChoicesHeight - RowHeight - i * RowPitch;
                            var row = FromEdges(left, bottom, width, RowHeight);

                            var rowOnCanvas = new UiRect(
                                new UiVec(row.Centre.X + toCanvas.X, row.Centre.Y + toCanvas.Y), row.Size);
                            var at = FocusMarkerPlacement.Place(rowOnCanvas, canvas);
                            var marker = new UiRect(
                                new UiVec(at.X - toCanvas.X, at.Y - toCanvas.Y),
                                new UiVec(FocusMarkerPlacement.Size + 2f * FocusMarkerPlacement.BobAmplitude,
                                    FocusMarkerPlacement.Size));

                            yield return ($"{UiFrames.Describe(frame)} {framing} {shown} shown, row {i}", row, marker, frame, framing);
                        }
                    }
                }
            }
        }

        [Test]
        public void TheLastShownRowSitsSixteenToTwentyFourAboveTheBox()
        {
            foreach (var frame in UiFrames.All)
            {
                for (int shown = 1; shown <= EventScreen.ChoiceRowCount; shown++)
                {
                    float panelBottom = DialogueStageLayout.ChoicesPanelBottom(
                        EventScreen.ChoicesHeight, EventScreen.ChoicesHeightFor(shown));
                    float lastRowBottom = panelBottom + EventScreen.ChoicesHeight - RowHeight - (shown - 1) * RowPitch;
                    float gap = lastRowBottom - DialogueStageLayout.BoxTop;

                    Assert.That(gap, Is.InRange(16f, 24f), $"{shown} shown at {UiFrames.Describe(frame)}");
                }
            }
        }

        [Test]
        public void TheFocusMarkerKeeps24FromEveryScreenEdge()
        {
            foreach (var (where, _, marker, frame, _) in EveryRow())
            {
                Assert.GreaterOrEqual(marker.Left, 24f, $"left edge, {where}");
                Assert.LessOrEqual(marker.Right, frame.X - 24f, $"right edge, {where}");
                Assert.GreaterOrEqual(marker.Bottom, 24f, $"bottom edge, {where}");
                Assert.LessOrEqual(marker.Top, frame.Y - 24f, $"top edge, {where}");
            }
        }

        [Test]
        public void NoRowAndNoMarkerLandsOnTheNamePlate()
        {
            foreach (var (where, row, marker, frame, framing) in EveryRow())
            {
                if (framing == StageFraming.Narration) continue; // no plate

                var plate = FromEdges(
                    DialogueStageLayout.PlatePin(framing).LeftEdge(frame.X, DialogueStageLayout.PlateWidth),
                    DialogueStageLayout.PlateCentreY - DialogueStageLayout.PlateHeight * 0.5f,
                    DialogueStageLayout.PlateWidth, DialogueStageLayout.PlateHeight);

                Assert.IsFalse(Overlaps(row, plate), $"the row covers the plate, {where}");
                Assert.IsFalse(Overlaps(marker, plate), $"the marker covers the plate, {where}");
            }
        }

        [Test]
        public void EveryRowStaysOnScreenAndOffTheBox()
        {
            foreach (var (where, row, _, frame, _) in EveryRow())
            {
                Assert.GreaterOrEqual(row.Left, 0f, where);
                Assert.LessOrEqual(row.Right, frame.X, where);
                Assert.LessOrEqual(row.Top, frame.Y, where);
                Assert.Greater(row.Bottom, DialogueStageLayout.BoxTop, where);
            }
        }
    }
}
