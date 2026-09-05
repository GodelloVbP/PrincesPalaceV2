using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // The transform breath every idle figure carries, and the three properties
    // that decide whether it reads as breathing or as a fault.
    //
    // ALL OF THEM ARE ARITHMETIC, which is the reason the curve is in Domain at
    // all rather than inside the animator that wears it: a discontinuity, a
    // negative amplitude or a figure that never returns to its authored size
    // are each visible on a stage and none of them needs a stage to find.
    public class BreathCurveTests
    {
        // ---- it never shrinks the figure ---------------------------------------

        // WHY THIS MATTERS AND IS NOT MERELY TIDY. A slot's base scale is what
        // content authored for the creature (RawEnemyEntry.stageScale) times
        // its depth in the formation. A breath centred on that would leave
        // every actor spending half its life smaller than the size somebody
        // chose, and "the authored size" would stop meaning anything you could
        // point at on screen.
        [Test]
        public void ABreathOnlyEverGrowsFromTheAuthoredSize()
        {
            for (int i = 0; i <= 400; i++)
            {
                float t = i * BreathCurve.PeriodSeconds / 400f;
                float amount = BreathCurve.At(t, 1f);

                Assert.GreaterOrEqual(amount, 0f,
                    $"at {t:F3}s the breath is {amount:F4} - a negative amount makes the figure " +
                    "smaller than the size content authored for it");
                Assert.LessOrEqual(amount, BreathCurve.FullAmplitude + 0.0001f,
                    $"at {t:F3}s the breath is {amount:F4}, past the {BreathCurve.FullAmplitude:F4} ceiling");
            }
        }

        // Both ends of the cycle are the bottom of the breath, which is what
        // makes the loop joinable at all.
        [Test]
        public void TheCycleStartsAndEndsFullyExhaled()
        {
            Assert.AreEqual(0f, BreathCurve.At(0f, 1f), 0.0001f);
            Assert.AreEqual(0f, BreathCurve.At(BreathCurve.PeriodSeconds, 1f), 0.0001f);
            Assert.AreEqual(0f, BreathCurve.At(BreathCurve.PeriodSeconds * 7f, 1f), 0.0001f);
        }

        // ---- no corner anywhere, including where the two halves meet ----------

        // THE BUG THIS EXISTS FOR, in the shape it would take. The inhale is
        // deliberately quicker than the exhale, and the obvious way to write
        // that -- one cosine over a warped phase -- puts a kink exactly at the
        // skew point, where the fast half hands over to the slow one. A kink in
        // a scale curve is a visible tick in the figure's height, once every
        // 2.8 seconds, forever. Two raised cosines meeting at their own flat
        // tops cannot produce one, and this is what says so.
        //
        // Sampled at a step far finer than a display frame, and compared
        // against the largest step ANYWHERE on the curve rather than against a
        // fixed number: a kink is a local spike in the rate of change, so the
        // honest test is that no step is much bigger than its neighbours.
        [Test]
        public void TheCurveHasNoKinkWhereTheInhaleHandsOverToTheExhale()
        {
            const int samples = 2000;
            float step = BreathCurve.PeriodSeconds / samples;

            float biggest = 0f;
            float total = 0f;
            float at = 0f;

            float previous = BreathCurve.At(0f, 1f);
            for (int i = 1; i <= samples; i++)
            {
                float now = BreathCurve.At(i * step, 1f);
                float delta = Mathf.Abs(now - previous);

                total += delta;
                if (delta > biggest)
                {
                    biggest = delta;
                    at = i * step;
                }

                previous = now;
            }

            float mean = total / samples;

            // A smooth curve's biggest step is a small multiple of its average
            // one -- the ends are flat and the middle is fast, and that spread
            // is the whole of the difference. A corner is an order of magnitude.
            Assert.Less(biggest, mean * 4f,
                $"the steepest step on the curve is {biggest:F6} at {at:F3}s against a mean of " +
                $"{mean:F6} - that is a corner, and it will read as a tick in the figure's " +
                "height once every cycle");
        }

        // The wrap is the other place a corner can hide, and it is not covered
        // above because the sampling stops at the period rather than crossing
        // it.
        [Test]
        public void TheWrapIsAsSmoothAsAnywhereElseOnTheCurve()
        {
            float step = BreathCurve.PeriodSeconds / 2000f;

            float before = BreathCurve.At(BreathCurve.PeriodSeconds - step, 1f);
            float after = BreathCurve.At(step, 1f);

            // Both sit one step from the bottom on either side, so they should
            // be within a rounding error of each other rather than merely close.
            Assert.AreEqual(before, after, 0.00002f,
                "the last step of a breath and the first step of the next are different sizes, " +
                "so the loop has a seam in it");
        }

        // ---- the amplitude multiplier ------------------------------------------

        [Test]
        public void TheScaleMultipliesTheAmplitudeAndNothingElse()
        {
            float full = BreathCurve.At(0.9f, 1f);
            float third = BreathCurve.At(0.9f, 0.35f);

            Assert.Greater(full, 0f, "the sample time was chosen to be mid-breath and is not");
            Assert.AreEqual(full * 0.35f, third, 0.00001f);
        }

        // Zero and below are how "this actor does not breathe" is said, so the
        // caller needs no branch. Negative is what StanceManifest hands back
        // when a sheet authors none -- it must not breathe the figure inside out.
        [Test]
        public void NoAmplitudeMeansNoBreathRatherThanAnInvertedOne()
        {
            for (int i = 0; i <= 40; i++)
            {
                float t = i * BreathCurve.PeriodSeconds / 40f;
                Assert.AreEqual(0f, BreathCurve.At(t, 0f), 0.0001f);
                Assert.AreEqual(0f, BreathCurve.At(t, -1f), 0.0001f);
            }
        }

        // ---- two figures do not breathe in lockstep ---------------------------

        // The same argument AnchorStageSlots makes about two rats overlapping
        // into "one monster with a spare tail": two of anything moving in
        // perfect step read as one animation drawn twice.
        [Test]
        public void FiguresInDifferentSlotsAreAtDifferentPointsOfTheirBreath()
        {
            float first = BreathCurve.At(1.4f + BreathCurve.PhaseFor(0), 1f);
            float second = BreathCurve.At(1.4f + BreathCurve.PhaseFor(1), 1f);
            float third = BreathCurve.At(1.4f + BreathCurve.PhaseFor(2), 1f);

            // Greater-than a tolerance rather than AreNotEqual with one:
            // NUnit's AreNotEqual has no tolerance overload, so the version
            // that compiles compares floats exactly and would pass on two
            // breaths a millionth apart.
            Assert.Greater(Mathf.Abs(first - second), 0.001f, "slots 0 and 1 breathe in lockstep");
            Assert.Greater(Mathf.Abs(second - third), 0.001f, "slots 1 and 2 breathe in lockstep");
            Assert.Greater(Mathf.Abs(first - third), 0.001f, "slots 0 and 2 breathe in lockstep");
        }

        // A negative index is not a real case -- slots are 0-based and counted
        // -- but it must not produce a phase that runs the curve backwards.
        [Test]
        public void ANegativeSlotIndexIsTreatedAsTheFirstOne()
        {
            Assert.AreEqual(0f, BreathCurve.PhaseFor(-3), 0.0001f);
            Assert.AreEqual(0f, BreathCurve.PhaseFor(0), 0.0001f);
        }

        // Local rather than UnityEngine.Mathf: this file is testing Domain,
        // which is engine-free by construction, and pulling the engine in for
        // an absolute value would be the first crack in that.
        private static class Mathf
        {
            public static float Abs(float value) => value < 0f ? -value : value;
        }
    }
}
