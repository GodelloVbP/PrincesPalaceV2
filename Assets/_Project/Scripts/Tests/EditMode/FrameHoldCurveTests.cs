using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // The shape of a stance's timing.
    //
    // The one property that is not a matter of taste is the TOTAL: the beat
    // player subtracts SecondsPerFrame x FrameCount from its budget to work out
    // how long to pause afterwards, so a stance that quietly ran long would eat
    // that pause and drag every following beat out of step. Everything else
    // here is the curve's character; that one is a contract.
    public class FrameHoldCurveTests
    {
        private const float Pace = 0.08f;

        [TestCase(6, 3)]
        [TestCase(6, 1)]
        [TestCase(6, 6)]
        [TestCase(4, 2)]
        [TestCase(12, 5)]
        [TestCase(2, 1)]
        public void TheHoldsAlwaysSumToTheSheetsOwnDuration(int frames, int impact)
        {
            float total = 0f;
            for (int i = 0; i < frames; i++)
            {
                total += FrameHoldCurve.HoldFor(i, frames, impact, Pace);
            }

            Assert.AreEqual(Pace * frames, total, 0.0001f,
                "the stance no longer takes as long as the sheet says it does");
        }

        [Test]
        public void TheBlowIsTheFastestThingOnScreen()
        {
            // Frame 2 is the last of the wind-up on a 6-frame, impact-3 stance,
            // and frame 3 the first of the follow-through. Those two are the
            // snap; nothing either side of them should be quicker.
            float[] holds = new float[6];
            for (int i = 0; i < 6; i++) holds[i] = FrameHoldCurve.HoldFor(i, 6, 3, Pace);

            Assert.Less(holds[2], holds[0], "the wind-up should not be the fast part");
            Assert.Less(holds[2], holds[5], "and neither should the settle");
            Assert.Less(holds[3], holds[5], "the follow-through should open fast and end slow");
        }

        [Test]
        public void TheOpeningPoseAndTheFinalOneAreTheLongestHelds()
        {
            float[] holds = new float[6];
            for (int i = 0; i < 6; i++) holds[i] = FrameHoldCurve.HoldFor(i, 6, 3, Pace);

            Assert.Greater(holds[0], Pace, "the opening pose should hold longer than a flat frame");
            Assert.Greater(holds[5], Pace, "and so should the last");
        }

        [Test]
        public void TheCurveActuallyDoesSomething()
        {
            // A guard against tuning the constants until they are all 1.0 and
            // leaving a class that costs a call per frame to change nothing.
            float shortest = float.MaxValue;
            float longest = 0f;
            for (int i = 0; i < 6; i++)
            {
                float hold = FrameHoldCurve.HoldFor(i, 6, 3, Pace);
                if (hold < shortest) shortest = hold;
                if (hold > longest) longest = hold;
            }

            Assert.Greater(longest / shortest, 2f,
                "the slowest frame should hold at least twice the fastest, or this is a slideshow again");
        }

        // ---- the degenerate cases ------------------------------------------

        [Test]
        public void ASingleFrameStanceIsUnchanged()
        {
            // Every combatant with flat-file art. The beat player skips these
            // entirely, but a curve that divided by (frameCount - 1) would
            // still have to not explode on the way past.
            Assert.AreEqual(Pace, FrameHoldCurve.HoldFor(0, 1, 1, Pace), 0.0001f);
        }

        [Test]
        public void AnImpactFrameOutsideTheSheetIsClamped()
        {
            // impactFrame is authored per stance and nothing validates it
            // against the number of PNGs actually on disk, so a sheet that
            // loses a frame must not produce a negative or infinite hold.
            for (int i = 0; i < 4; i++)
            {
                Assert.Greater(FrameHoldCurve.HoldFor(i, 4, 99, Pace), 0f);
                Assert.Greater(FrameHoldCurve.HoldFor(i, 4, -3, Pace), 0f);
            }
        }

        [Test]
        public void AZeroPaceStaysZeroRatherThanDividingByIt()
        {
            Assert.AreEqual(0f, FrameHoldCurve.HoldFor(0, 6, 3, 0f), 0.0001f);
        }
    }
}
