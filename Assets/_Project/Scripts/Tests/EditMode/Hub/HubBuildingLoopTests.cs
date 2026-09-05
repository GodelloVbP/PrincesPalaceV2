using NUnit.Framework;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace.Domain.Tests
{
    // The frame loop, pinned without a scene.
    //
    // The maths lives in Domain for exactly this reason -- the first draft put
    // it on the Core component and the EditMode suite could not see it, which is
    // the same reachability gap that left six ambience curves unpinned. Every
    // branch below is a way a loop stops being a loop.
    public class HubBuildingLoopTests
    {
        [Test]
        public void ASingleFrameBuildingNeverAnimates()
        {
            // Four of the five buildings have exactly one frame, and the looper
            // is attached to all of them on purpose -- a per-building list is
            // what left the tree's f1/f2 dead in the first place.
            for (float t = 0f; t < 10f; t += 0.1f)
            {
                Assert.AreEqual(0, AmbienceCurves.LoopFrameAt(t, 1, 0.55f));
            }
        }

        [Test]
        public void ItWalksTheFramesInOrderAndWrapsBackToTheStart()
        {
            Assert.AreEqual(0, AmbienceCurves.LoopFrameAt(0f, 3, 1f));
            Assert.AreEqual(1, AmbienceCurves.LoopFrameAt(1f, 3, 1f));
            Assert.AreEqual(2, AmbienceCurves.LoopFrameAt(2f, 3, 1f));
            Assert.AreEqual(0, AmbienceCurves.LoopFrameAt(3f, 3, 1f), "the loop has to close");
        }

        [Test]
        public void ItNeverLeavesTheSheet()
        {
            // An out-of-range index throws on the array lookup, which would take
            // the whole hub down rather than showing one wrong frame.
            for (float t = 0f; t < 200f; t += 0.13f)
            {
                int frame = AmbienceCurves.LoopFrameAt(t, 3, 0.55f);
                Assert.GreaterOrEqual(frame, 0);
                Assert.Less(frame, 3);
            }
        }

        [Test]
        public void AZeroFrameTimeHoldsRatherThanDividingByIt()
        {
            // SecondsPerFrame is public, so a zero left in it would divide into
            // infinity and index the array with garbage.
            Assert.AreEqual(0, AmbienceCurves.LoopFrameAt(5f, 3, 0f));
        }

        [Test]
        public void ANegativeClockStillLandsOnARealFrame()
        {
            // Phase offsets are subtracted as well as added, so a small negative
            // elapsed is reachable in the first frames of a scene.
            Assert.GreaterOrEqual(AmbienceCurves.LoopFrameAt(-0.4f, 3, 0.55f), 0);
        }
    }
}
