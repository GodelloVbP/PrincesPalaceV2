using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

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

        [Test]
        public void TheBoxSitsOppositeTheBustAndCentresForNarration()
        {
            var left = DialogueStageLayout.BoxPin(false, DialogueSide.Left);
            var right = DialogueStageLayout.BoxPin(false, DialogueSide.Right);
            var narration = DialogueStageLayout.BoxPin(true, DialogueSide.Right);

            // Near edge 608 in from the speaker's edge: at 1920 wide the box
            // spans 608..1888 for a left speaker, 32..1312 for a right one.
            Assert.AreEqual((0f, 0f, 608f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 1f, -608f), (right.AnchorX, right.PivotX, right.OffsetX));
            Assert.AreEqual((0.5f, 0.5f, 0f), (narration.AnchorX, narration.PivotX, narration.OffsetX));
        }

        [Test]
        public void ThePlateSitsAtTheSpeakersEndOfTheBox()
        {
            var left = DialogueStageLayout.PlatePin(DialogueSide.Left);
            var right = DialogueStageLayout.PlatePin(DialogueSide.Right);

            Assert.AreEqual((0f, 0f, 648f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 1f, -648f), (right.AnchorX, right.PivotX, right.OffsetX));
            Assert.AreEqual(304f, DialogueStageLayout.PlateCentreY);
        }

        // Flush with the box's far end, clear of the plate's top.
        [Test]
        public void TheChoicesStandAboveTheBoxAtItsFarEnd()
        {
            var left = DialogueStageLayout.ChoicesPin(false, DialogueSide.Left);
            var right = DialogueStageLayout.ChoicesPin(false, DialogueSide.Right);

            Assert.AreEqual((0f, 1f, 1888f), (left.AnchorX, left.PivotX, left.OffsetX));
            Assert.AreEqual((1f, 0f, -1888f), (right.AnchorX, right.PivotX, right.OffsetX));
            Assert.AreEqual(368f, DialogueStageLayout.ChoicesBottom);
        }
    }
}
