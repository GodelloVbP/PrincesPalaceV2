using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace.Domain.Tests
{
    // The lantern flame, pinned.
    //
    // FIRST COVERAGE. This curve was always pure and always seam-shaped, and it
    // sat in Core where the EditMode suite -- Domain-only by asmdef -- could
    // never reach it. It shipped in the menu, in front of the player, with no
    // test of any kind.
    public class FlickerCurveTests
    {
        private static readonly FlickerCurve Lantern = FlickerCurve.Lantern;

        [Test]
        public void TheCurveStaysInsideItsOwnRange()
        {
            // Every consumer lerps a colour and a scale by this, so anything
            // outside [0,1] is an inverted alpha or a mirrored rect.
            for (float t = 0f; t < 120f; t += 0.017f)
            {
                float value = Lantern.At(t);
                Assert.GreaterOrEqual(value, 0f, $"below zero at t={t}");
                Assert.LessOrEqual(value, 1f, $"above one at t={t}");
            }
        }

        [Test]
        public void ItActuallyReachesBothEnds()
        {
            // The centre and the amplitudes are chosen so the flame goes fully
            // dark and fully bright rather than hovering in a narrow band -- a
            // lantern that only ever swings between 0.4 and 0.6 reads as a
            // dimmer switch, not as fire.
            float lowest = 1f, highest = 0f;
            for (float t = 0f; t < 300f; t += 0.01f)
            {
                float value = Lantern.At(t);
                if (value < lowest) lowest = value;
                if (value > highest) highest = value;
            }

            Assert.Less(lowest, 0.05f, "it never gets dark");
            Assert.Greater(highest, 0.95f, "it never gets bright");
        }

        [Test]
        public void TheAmplitudesSumToTheHeadroomTheCentreLeaves()
        {
            // 0.5 centre + 0.5 of swing is what makes the assertion above hold.
            // Stated as its own test because adding a fourth term is exactly the
            // kind of edit that quietly breaks it.
            Assert.AreEqual(0.5f, Lantern.Swing, 0.0001f);
            Assert.AreEqual(0.5f, Lantern.Centre, 0.0001f);
        }

        [Test]
        public void ItRestsAtTheCentreBeforeAnyTimeHasPassed()
        {
            // Only the first term has no phase offset, so t=0 is not the centre
            // -- and that is deliberate: a curve whose every term starts at zero
            // starts every lantern at exactly the same brightness.
            Assert.Greater(Math.Abs(Lantern.At(0f) - Lantern.Centre), 0.0001f);
        }

        [Test]
        public void TheRatesShareNoSmallCommonMultiple()
        {
            // The whole reason it reads as fire. If two rates were harmonic the
            // sum would have a short visible period and the flame would loop.
            var rates = Lantern.Terms.Select(term => term.Rate).ToList();

            for (int i = 0; i < rates.Count; i++)
            {
                for (int j = i + 1; j < rates.Count; j++)
                {
                    float ratio = rates[j] / rates[i];
                    Assert.Greater(Math.Abs(ratio - (float)Math.Round(ratio)), 0.05f,
                        $"rates {rates[i]} and {rates[j]} are near-harmonic, so the flame will visibly loop");
                }
            }
        }

        [Test]
        public void NoTwoLanternsFlareTogether()
        {
            // Each instance offsets its own phase, so this asks the curve the
            // question the component asks it: does the same moment look
            // different at a different offset?
            const float moment = 12.5f;

            Assert.Greater(Math.Abs(Lantern.At(moment) - Lantern.At(moment + 37f)), 0.01f);
        }

        [Test]
        public void ADifferentCurveReadsDifferently()
        {
            // The point of making it data. Ember is steadier and slower, and if
            // it were not measurably different there would be no reason for it
            // to exist.
            Assert.Less(FlickerCurve.Ember.Swing, Lantern.Swing, "ember should swing less than fire");

            bool differs = false;
            for (float t = 0f; t < 20f; t += 0.05f)
            {
                if (Math.Abs(FlickerCurve.Ember.At(t) - Lantern.At(t)) > 0.05f) { differs = true; break; }
            }

            Assert.IsTrue(differs);
        }

        [Test]
        public void ACurveWithNoTermsIsJustItsCentre()
        {
            // The degenerate case a tuner will reach by deleting terms one at a
            // time. It has to be a steady light, not a crash.
            var flat = new FlickerCurve(0.7f);

            Assert.AreEqual(0.7f, flat.At(0f), 0.0001f);
            Assert.AreEqual(0.7f, flat.At(99f), 0.0001f);
        }

        [Test]
        public void AnOverdrivenCurveClampsRatherThanWrapping()
        {
            // Someone WILL type a bigger amplitude to make it flare harder. It
            // must saturate at full brightness, not fold back into darkness.
            var overdriven = new FlickerCurve(0.5f, new FlickerTerm(5f, 2.11f));

            for (float t = 0f; t < 20f; t += 0.01f)
            {
                float value = overdriven.At(t);
                Assert.GreaterOrEqual(value, 0f);
                Assert.LessOrEqual(value, 1f);
            }
        }

    }
}
