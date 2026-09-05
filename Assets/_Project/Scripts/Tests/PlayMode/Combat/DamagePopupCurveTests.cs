using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The three curves a damage number travels: fade, punch and glow.
    //
    // AlphaAt has carried a comment since it was written saying "the curve can
    // be pinned by a test without a scene, a coroutine or a frame" -- and no
    // test existed. Adding two more curves to an untested seam is how a seam
    // stays untested forever, so all three are covered here.
    //
    // Pure statics, so none of this needs a scene. They live in PlayMode only
    // because DamagePopup is a Core class and the EditMode assembly is
    // Domain-only by rule.
    public class DamagePopupCurveTests
    {
        // ---- the fade ----------------------------------------------------------

        [Test]
        public void TheNumberIsFullyOpaqueWhileItMattersMost()
        {
            Assert.AreEqual(1f, DamagePopup.AlphaAt(0f), 1e-4f);
            Assert.AreEqual(1f, DamagePopup.AlphaAt(0.2f), 1e-4f,
                "a number that starts fading immediately is hard to read at the moment it lands");
        }

        [Test]
        public void TheFadeFinishesExactlyAtTheEnd()
        {
            Assert.AreEqual(0f, DamagePopup.AlphaAt(1f), 1e-4f);
        }

        [Test]
        public void TheFadeOnlyEverDecreases()
        {
            float previous = DamagePopup.AlphaAt(0f);

            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                float alpha = DamagePopup.AlphaAt(t);
                Assert.LessOrEqual(alpha, previous + 1e-4f, $"alpha rose again at {t}");
                Assert.That(alpha, Is.InRange(-1e-4f, 1f + 1e-4f), $"out of range at {t}");
                previous = alpha;
            }
        }

        // ---- the punch ----------------------------------------------------------

        // Small, then bigger than full size, then full size. A number that
        // simply appears at its final scale reads as a label rather than a hit.
        [Test]
        public void ThePunchStartsSmallOvershootsAndSettles()
        {
            float start = DamagePopup.ScaleAt(0f);
            float peak = DamagePopup.ScaleAt(0.15f);
            float settled = DamagePopup.ScaleAt(0.5f);

            Assert.Less(start, 1f, "the number does not pop in, it just appears");
            Assert.Greater(peak, 1f, "there is no overshoot, so there is no punch");
            Assert.AreEqual(1f, settled, 1e-4f, "the punch never settles to full size");
        }

        // The motion must be over well before the fade begins, or the number is
        // moving and dimming at once and reads as a wobble.
        [Test]
        public void ThePunchIsFinishedBeforeTheFadeStarts()
        {
            for (float t = 0.35f; t <= 1f; t += 0.05f)
            {
                Assert.AreEqual(1f, DamagePopup.ScaleAt(t), 1e-4f,
                    $"still scaling at {t}, where the number should be still");
            }
        }

        [Test]
        public void TheScaleIsNeverZeroOrNegative()
        {
            for (float t = -0.5f; t <= 1.5f; t += 0.02f)
            {
                Assert.Greater(DamagePopup.ScaleAt(t), 0f, $"non-positive scale at {t}");
            }
        }

        // ---- the glow -----------------------------------------------------------

        // PipelineBuilder puts Bloom at threshold 1.05, so the flash has to
        // exceed that on arrival or there is no glow at all -- only a number
        // that is briefly a slightly different colour.
        [Test]
        public void TheFlashCrossesTheBloomThreshold()
        {
            Assert.Greater(DamagePopup.GlowAt(0f), 1.05f,
                "the arrival flash sits under PipelineBuilder's bloom threshold, so it will not bloom");
        }

        // And it has to stop: a number glowing for its whole life reads as a
        // light source rather than as a hit landing.
        [Test]
        public void TheFlashDecaysToAnUnmultipliedFace()
        {
            Assert.AreEqual(1f, DamagePopup.GlowAt(0.5f), 1e-4f);
            Assert.AreEqual(1f, DamagePopup.GlowAt(1f), 1e-4f);
        }

        [Test]
        public void TheFlashOnlyEverDims()
        {
            float previous = DamagePopup.GlowAt(0f);

            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                float glow = DamagePopup.GlowAt(t);
                Assert.LessOrEqual(glow, previous + 1e-4f, $"the flash brightened again at {t}");
                Assert.GreaterOrEqual(glow, 1f - 1e-4f,
                    $"the face colour dropped below 1 at {t}, which would DARKEN the number");
                previous = glow;
            }
        }

        // The glow is over before the fade starts, like the punch: three things
        // finishing at three different times would read as a mess rather than
        // as one impact.
        [Test]
        public void TheFlashIsFinishedBeforeTheFadeStarts()
        {
            Assert.AreEqual(1f, DamagePopup.GlowAt(0.35f), 1e-4f);
        }

        // Every curve is clamped, so a caller that hands over a progress
        // outside 0..1 gets the endpoint rather than an extrapolation.
        [Test]
        public void EveryCurveClampsItsInput()
        {
            Assert.AreEqual(DamagePopup.AlphaAt(0f), DamagePopup.AlphaAt(-1f), 1e-4f);
            Assert.AreEqual(DamagePopup.AlphaAt(1f), DamagePopup.AlphaAt(2f), 1e-4f);
            Assert.AreEqual(DamagePopup.ScaleAt(0f), DamagePopup.ScaleAt(-1f), 1e-4f);
            Assert.AreEqual(DamagePopup.ScaleAt(1f), DamagePopup.ScaleAt(2f), 1e-4f);
            Assert.AreEqual(DamagePopup.GlowAt(0f), DamagePopup.GlowAt(-1f), 1e-4f);
            Assert.AreEqual(DamagePopup.GlowAt(1f), DamagePopup.GlowAt(2f), 1e-4f);
        }
    }
}
