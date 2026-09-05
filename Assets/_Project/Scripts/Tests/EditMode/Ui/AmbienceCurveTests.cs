using System;
using NUnit.Framework;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace.Domain.Tests
{
    // The five remaining ambient curves.
    //
    // FIRST COVERAGE, all of them. Every one was written as a pure static with a
    // comment saying a test could pin it, and every one sat in Core where the
    // Domain-only EditMode suite could not see it. They have been running in
    // front of the player since the menu shipped.
    public class AmbienceCurveTests
    {
        // ---- motes -----------------------------------------------------------

        [Test]
        public void AMoteFadesInFastAndOutSlow()
        {
            // The asymmetry is the whole point: airborne light appears quickly
            // and lingers. A symmetric fade reads as a dot being cross-faded.
            float fadeIn = AmbienceCurves.MoteFadeInEnds;
            float fadeOut = 1f - AmbienceCurves.MoteFadeOutBegins;

            Assert.Less(fadeIn, fadeOut, "the fade-in must be shorter than the fade-out");
        }

        [Test]
        public void AMoteIsInvisibleAtBothEndsOfItsLife()
        {
            Assert.AreEqual(0f, AmbienceCurves.MoteAlphaAt(0f), 0.0001f);
            Assert.AreEqual(0f, AmbienceCurves.MoteAlphaAt(1f), 0.0001f);
        }

        [Test]
        public void AMoteIsFullyOpaqueThroughItsMiddle()
        {
            for (float p = AmbienceCurves.MoteFadeInEnds; p <= AmbienceCurves.MoteFadeOutBegins; p += 0.01f)
            {
                Assert.AreEqual(1f, AmbienceCurves.MoteAlphaAt(p), 0.0001f, $"dipped at p={p}");
            }
        }

        [Test]
        public void AMoteOutsideItsLifeIsClampedRatherThanNegative()
        {
            // Progress is elapsed/life and nothing guarantees a caller stops at
            // 1. A negative alpha silently inverts a colour.
            Assert.AreEqual(0f, AmbienceCurves.MoteAlphaAt(-0.5f), 0.0001f);
            Assert.AreEqual(0f, AmbienceCurves.MoteAlphaAt(1.5f), 0.0001f);
        }

        // ---- slow drift ------------------------------------------------------

        [Test]
        public void DriftTracesAnEllipse_NotALine()
        {
            // Sine on x against cosine on y. If both used sine the layer would
            // slide along a diagonal, which reads as a slipping texture rather
            // than as air.
            var amplitude = new Drift2(24f, 8f);

            var quarter = AmbienceCurves.DriftOffsetAt(10f, amplitude, 40f, 0f);
            var start = AmbienceCurves.DriftOffsetAt(0f, amplitude, 40f, 0f);

            Assert.AreEqual(0f, start.X, 0.001f, "x starts at the centre");
            Assert.AreEqual(8f, start.Y, 0.001f, "y starts at its extreme");
            Assert.AreEqual(24f, quarter.X, 0.001f, "a quarter turn later they have swapped");
            Assert.AreEqual(0f, quarter.Y, 0.001f);
        }

        [Test]
        public void DriftNeverLeavesItsAmplitude()
        {
            var amplitude = new Drift2(24f, 8f);

            for (float t = 0f; t < 200f; t += 0.05f)
            {
                var offset = AmbienceCurves.DriftOffsetAt(t, amplitude, 40f, 0f);
                Assert.LessOrEqual(Math.Abs(offset.X), 24.001f);
                Assert.LessOrEqual(Math.Abs(offset.Y), 8.001f);
            }
        }

        [Test]
        public void AZeroPeriodDegradesToNoDriftRatherThanNaN()
        {
            // It would divide into NaN and strand the rect off-screen
            // permanently -- a layout destroyed by a field left at its default.
            var offset = AmbienceCurves.DriftOffsetAt(5f, new Drift2(24f, 8f), 0f, 0f);

            Assert.AreEqual(0f, offset.X);
            Assert.AreEqual(0f, offset.Y);
        }

        [Test]
        public void DriftRepeatsExactlyOnItsPeriod()
        {
            var amplitude = new Drift2(24f, 8f);

            var now = AmbienceCurves.DriftOffsetAt(7f, amplitude, 40f, 0f);
            var later = AmbienceCurves.DriftOffsetAt(47f, amplitude, 40f, 0f);

            Assert.AreEqual(now.X, later.X, 0.001f);
            Assert.AreEqual(now.Y, later.Y, 0.001f);
        }

        // ---- Ken Burns -------------------------------------------------------

        [Test]
        public void TheBackgroundNeverScalesBelowOne()
        {
            // THE ONE THAT MATTERS. The layer is full-bleed, so any scale under
            // 1 pulls its edges inside the canvas and shows bare camera colour
            // down the sides.
            for (float t = 0f; t < 300f; t += 0.05f)
            {
                Assert.GreaterOrEqual(AmbienceCurves.KenBurnsScaleAt(t, 1.055f, 54f), 1f, $"went under at t={t}");
            }
        }

        [Test]
        public void ItTouchesOneAtItsTroughAndTheMaximumAtItsPeak()
        {
            Assert.AreEqual(1f, AmbienceCurves.KenBurnsScaleAt(0f, 1.055f, 54f), 0.0001f);
            Assert.AreEqual(1.055f, AmbienceCurves.KenBurnsScaleAt(27f, 1.055f, 54f), 0.0001f);
        }

        [Test]
        public void AMaxScaleBelowOneIsRefused()
        {
            // Same failure as the trough, reached by typing a number instead of
            // by the curve: it would expose the canvas edges for the whole cycle.
            for (float t = 0f; t < 60f; t += 0.1f)
            {
                Assert.GreaterOrEqual(AmbienceCurves.KenBurnsScaleAt(t, 0.8f, 54f), 1f);
            }
        }

        [Test]
        public void AZeroPeriodHoldsStillRatherThanNaN()
        {
            Assert.AreEqual(1f, AmbienceCurves.KenBurnsScaleAt(5f, 1.055f, 0f), 0.0001f);
        }

        // ---- stars -----------------------------------------------------------

        [Test]
        public void StarsGetSpreadPeriodsSoTheFieldNeverPulsesTogether()
        {
            float first = AmbienceCurves.StarPeriodFor(0, 8);
            float last = AmbienceCurves.StarPeriodFor(7, 8);

            Assert.AreEqual(AmbienceCurves.StarMinPeriod, first, 0.0001f);
            Assert.AreEqual(AmbienceCurves.StarMaxPeriod, last, 0.0001f);
            Assert.AreNotEqual(first, last);
        }

        [Test]
        public void ALoneStarStillGetsAValidPeriod()
        {
            // count == 1 divides by (count - 1). Zero.
            Assert.AreEqual(AmbienceCurves.StarMinPeriod, AmbienceCurves.StarPeriodFor(0, 1), 0.0001f);
        }

        [Test]
        public void AStarDimsButNeverGoesOut()
        {
            // It scales the star's authored alpha, so reaching 0 would make a
            // painted star vanish entirely rather than twinkle.
            for (float t = 0f; t < 60f; t += 0.01f)
            {
                float scale = AmbienceCurves.StarAlphaScaleAt(t, 2.4f, 0f);
                Assert.GreaterOrEqual(scale, AmbienceCurves.StarMinAlphaScale - 0.0001f);
                Assert.LessOrEqual(scale, 1.0001f);
            }
        }

        // ---- beacon ----------------------------------------------------------

        [Test]
        public void TheBeaconIsOneCleanCycle()
        {
            // Deliberately the thing FlickerCurve exists NOT to be: a magic
            // light under a map room should read as a steady pulse.
            Assert.AreEqual(0f, AmbienceCurves.BeaconProgressAt(0f, 2.4f, 0f), 0.0001f);
            Assert.AreEqual(1f, AmbienceCurves.BeaconProgressAt(1.2f, 2.4f, 0f), 0.0001f);
            Assert.AreEqual(0f, AmbienceCurves.BeaconProgressAt(2.4f, 2.4f, 0f), 0.0001f);
        }

        [Test]
        public void TheBeaconStaysInsideItsRange()
        {
            for (float t = 0f; t < 60f; t += 0.01f)
            {
                float value = AmbienceCurves.BeaconProgressAt(t, 2.4f, 0f);
                Assert.GreaterOrEqual(value, -0.0001f);
                Assert.LessOrEqual(value, 1.0001f);
            }
        }

        [Test]
        public void AZeroPeriodBeaconHoldsRatherThanBlanking()
        {
            // PeriodSeconds is public, so a zero left in it would divide to NaN
            // and blank the Image entirely.
            Assert.AreEqual(0f, AmbienceCurves.BeaconProgressAt(5f, 0f, 0f), 0.0001f);
        }
    }
}
