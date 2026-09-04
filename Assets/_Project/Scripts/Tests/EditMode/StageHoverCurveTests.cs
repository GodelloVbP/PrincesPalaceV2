using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // The altitude a flyer rides at, pinned as literals so a change to the
    // curve's shape has to come here and say so -- the same reason
    // BreathCurveTests exists beside it. Odette is the one actor this
    // describes today; the numbers are hers (StanceManifest.json).
    public class StageHoverCurveTests
    {
        private static readonly HoverSpec Odette = new HoverSpec(height: 70f, bob: 10f, periodSeconds: 2.4f);

        [Test]
        public void AGroundedActorNeverLeavesTheFloor()
        {
            Assert.AreEqual(0f, HoverCurve.At(0f, HoverSpec.Grounded));
            Assert.AreEqual(0f, HoverCurve.At(1.3f, HoverSpec.Grounded));
            Assert.IsFalse(HoverSpec.Grounded.IsAirborne);
        }

        // Height is the RESTING altitude and the bob is the excursion either
        // side of it, so the period starts and ends at the height itself.
        [Test]
        public void AFlyerRestsAtItsHeightAtTheStartAndEndOfAPeriod()
        {
            Assert.AreEqual(70f, HoverCurve.At(0f, Odette), 0.001f);
            Assert.AreEqual(70f, HoverCurve.At(2.4f, Odette), 0.001f);
        }

        // A quarter in is the top of the bob and three quarters in the
        // bottom -- and the bottom is still Height - Bob above the floor,
        // which is what lets a flyer authored to clear the front row keep
        // clearing it.
        [Test]
        public void TheBobPeaksAQuarterInAndTroughsThreeQuartersIn()
        {
            Assert.AreEqual(80f, HoverCurve.At(0.6f, Odette), 0.001f);
            Assert.AreEqual(60f, HoverCurve.At(1.8f, Odette), 0.001f);
        }

        // Zero is JsonUtility's "unset" for a float, so an authored hover
        // block that says nothing about its period gets the default rather
        // than a division by zero.
        [Test]
        public void AnUnsetPeriodTakesTheDefault()
        {
            var spec = new HoverSpec(70f, 10f, 0f);
            Assert.AreEqual(2.4f, spec.PeriodSeconds, 0.0001f);
            Assert.AreEqual(HoverCurve.DefaultPeriodSeconds, spec.PeriodSeconds, 0.0001f);
        }

        // Negative altitude has no meaning on a stage whose floor is the
        // ground line; it is clamped rather than passed through, so a typo
        // cannot bury a figure.
        [Test]
        public void NegativeNumbersReadAsGrounded()
        {
            var spec = new HoverSpec(-5f, -1f, 2f);
            Assert.IsFalse(spec.IsAirborne);
            Assert.AreEqual(0f, HoverCurve.At(0.5f, spec));
        }
    }
}
