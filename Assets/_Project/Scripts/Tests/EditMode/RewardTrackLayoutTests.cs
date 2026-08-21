using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    public class RewardTrackLayoutTests
    {
        [Test]
        public void ThereIsOneNodeForEveryLevelThatPays()
        {
            // Level 1 is where a character starts, not somewhere they arrive,
            // so the rail runs 2..100.
            Assert.AreEqual(2, RewardTrackLayout.FirstLevel);
            Assert.AreEqual(RewardTrack.MaxLevel - 1, RewardTrackLayout.NodeCount);
        }

        [Test]
        public void NodesAreEvenlySpacedAlongTheRail()
        {
            for (int level = RewardTrackLayout.FirstLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.AreEqual(RewardTrackLayout.NodePitch,
                    RewardTrackLayout.NodeX(level) - RewardTrackLayout.NodeX(level - 1), 0.001f,
                    $"level {level} is not one pitch from level {level - 1}");
            }
        }

        // Every node sits inside the rect it is drawn in, with the end padding
        // keeping the first and last off the edge.
        [Test]
        public void EveryNodeSitsInsideTheContent()
        {
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                float half = RewardTrackLayout.CaptionWidth * 0.5f;

                Assert.GreaterOrEqual(RewardTrackLayout.NodeX(level) - half, 0f,
                    $"level {level}'s caption starts left of the content rect");
                Assert.LessOrEqual(RewardTrackLayout.NodeX(level) + half, RewardTrackLayout.ContentWidth,
                    $"level {level}'s caption runs past the content rect");
            }
        }

        // THE MISTAKE THIS PAIR EXISTS TO PREVENT. NodeX is measured from the
        // content's LEFT EDGE because that is how a rail is easy to reason
        // about; Ui.Place.At measures from a parent's CENTRE. Placing NodeX
        // directly put every node half a content-width too far right, and
        // UiAudit refused the build with "TrackDot100 escapes its parent:
        // 9,330px past the right".
        [Test]
        public void ThePlacementOffsetIsCentreRelativeAndNodeXIsNot()
        {
            int middle = RewardTrackLayout.FirstLevel + RewardTrackLayout.NodeCount / 2;

            Assert.AreEqual(RewardTrackLayout.NodeX(middle) - RewardTrackLayout.ContentWidth * 0.5f,
                RewardTrackLayout.NodeOffsetX(middle), 0.001f);

            Assert.Less(RewardTrackLayout.NodeOffsetX(RewardTrackLayout.FirstLevel), 0f,
                "the first node should sit LEFT of the content's centre");
            Assert.Greater(RewardTrackLayout.NodeOffsetX(RewardTrack.MaxLevel), 0f,
                "the last node should sit RIGHT of the content's centre");
        }

        // ---- scrolling ------------------------------------------------------------

        private const float Window = 1520f;

        [Test]
        public void ScrollingCentresTheLevelAskedFor()
        {
            int middle = RewardTrackLayout.FirstLevel + RewardTrackLayout.NodeCount / 2;
            float left = RewardTrackLayout.ScrollFor(middle, Window);

            // The node's position within the window, once the content has slid.
            float onScreen = left + RewardTrackLayout.NodeX(middle);

            Assert.AreEqual(0f, onScreen, 0.001f,
                "a level in the middle of the track should land in the middle of the window");
        }

        // CLAMPED AT BOTH ENDS. Without this, opening on level 2 scrolls the
        // rail off the right of the window and opening on level 100 scrolls it
        // off the left -- in both cases the player is looking at empty space
        // with their own progress just out of frame.
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(99)]
        [TestCase(100)]
        public void TheRailAlwaysFillsTheWindow(int level)
        {
            float left = RewardTrackLayout.ScrollFor(level, Window);

            Assert.LessOrEqual(left, -Window * 0.5f + 0.001f,
                $"at level {level} the content's left edge is inside the window, showing dead space");
            Assert.GreaterOrEqual(left + RewardTrackLayout.ContentWidth, Window * 0.5f - 0.001f,
                $"at level {level} the content's right edge is inside the window, showing dead space");
        }

        // ---- milestones -----------------------------------------------------------

        [TestCase(10)]
        [TestCase(25)]
        [TestCase(50)]
        [TestCase(90)]
        [TestCase(100)]
        public void AMilestoneLevelDrawsLarge(int level)
        {
            Assert.IsTrue(RewardTrackLayout.IsMilestone(level));
        }

        [Test]
        public void FillerDoesNotDrawLarge()
        {
            int filler = 0;
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (!RewardTrackLayout.IsMilestone(level)) filler++;
            }

            // 87 filler levels against 12 milestones -- if this ever inverts,
            // the rail has lost its landmarks.
            Assert.AreEqual(87, filler);
            Assert.Greater(RewardTrackLayout.MilestoneDiameter, RewardTrackLayout.NodeDiameter);
        }
    }
}
