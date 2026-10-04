using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE THREE LAYER FIELDS THE CENSER NEEDED (docs/PLAN_SPELL_ANIMATION.md E1,
    // E2, E4): rotate, fadeIn, breathe. Literals throughout; none of these
    // recompute the sine they are pinning.
    public class SpellRotateBreatheTests
    {
        private const float Tol = 1e-3f;

        private static SpellLayerInstance Instance(SpellLayer layer, float facing = 1f)
        {
            layer.id = "core";
            layer.render = "still";
            layer.place = "target";
            layer.path = "Spells/test";
            layer.seconds = 1f;
            layer.until = "hold";
            var vfx = new SpellPresentation { layerFormat = 1, layers = new[] { layer } };
            var instance = SpellPerformance.Resolve(vfx, 1, p => 1).Instances.Single();
            instance.Box = new UiVec(100f, 200f);
            instance.Facing = facing;
            return instance;
        }

        // ---- E1: one angle -------------------------------------------------

        [Test]
        public void ThePendulumStartsAtRestAndSwingsSineWise()
        {
            var r = new SpellRotate { amplitude = -14f, period = 0.93f };
            Assert.AreEqual(0f, r.AngleAt(0f), Tol);
            Assert.AreEqual(-14f, r.AngleAt(0.2325f), Tol, "first extreme, clockwise for a negative amplitude");
            Assert.AreEqual(0f, r.AngleAt(0.465f), Tol, "home through the middle");
            Assert.AreEqual(14f, r.AngleAt(0.6975f), Tol, "then the other way");
        }

        [Test]
        public void DampingShrinksTheSwingAndTheStaticTiltAndSpinAddOn()
        {
            var damped = new SpellRotate { amplitude = 10f, period = 1f, damping = 1f };
            Assert.AreEqual(7.788f, damped.AngleAt(0.25f), Tol, "10 x e^-0.25");

            var all = new SpellRotate { degrees = 5f, spin = 90f, amplitude = 10f, period = 1f };
            Assert.AreEqual(50f, all.AngleAt(0.5f), Tol, "tilt + half a second of spin + the pendulum home");
        }

        [Test]
        public void SwingsStopThePendulumAtTheVerticalButNotTheTilt()
        {
            var r = new SpellRotate { degrees = 3f, amplitude = 10f, period = 1f, swings = 1.25f };
            Assert.AreEqual(13f, r.AngleAt(1.25f), Tol, "at 1.25 periods it is at the extreme");
            Assert.AreEqual(3f, r.AngleAt(1.3f), Tol, "past them it rests");
        }

        [Test]
        public void AnUnauthoredTurnIsNotAuthoredAndItsPivotAloneDoesNotAuthorIt()
        {
            Assert.IsFalse(new SpellRotate().IsAuthored);
            Assert.IsFalse(new SpellRotate { pivotX = 0.1f, pivotY = 1f }.IsAuthored);
            Assert.AreEqual(0.5f, new SpellRotate().pivotX, 0f);
            Assert.AreEqual(0.5f, new SpellRotate().pivotY, 0f);
        }

        [Test]
        public void TurningAboutTheTopCentreSwingsTheCentreOutOfTheBox()
        {
            // 100x200 box hooked at its top centre, turned 90 degrees clockwise:
            // the centre, 100 below the hook, ends 100 to its left.
            var sample = SpellFrameCursor.SampleOf(Instance(new SpellLayer
            { rotate = new SpellRotate { degrees = -90f, pivotX = 0.5f, pivotY = 1f } }), 1, 0.5f);
            Assert.AreEqual(-90f, sample.Degrees, Tol);
            Assert.AreEqual(-100f, sample.ShiftX, Tol);
            Assert.AreEqual(100f, sample.ShiftY, Tol);
        }

        [Test]
        public void ACentrePivotShiftsNothingAndAMirroredCastMirrorsTheTurn()
        {
            var centred = SpellFrameCursor.SampleOf(Instance(new SpellLayer
            { rotate = new SpellRotate { degrees = 30f } }), 1, 0.5f);
            Assert.AreEqual(30f, centred.Degrees, Tol);
            Assert.AreEqual(0f, centred.ShiftX, Tol);
            Assert.AreEqual(0f, centred.ShiftY, Tol);

            var mirrored = SpellFrameCursor.SampleOf(Instance(new SpellLayer
            { rotate = new SpellRotate { degrees = -90f, pivotX = 0.5f, pivotY = 1f } }, -1f), 1, 0.5f);
            Assert.AreEqual(90f, mirrored.Degrees, Tol);
            Assert.AreEqual(100f, mirrored.ShiftX, Tol);
            Assert.AreEqual(100f, mirrored.ShiftY, Tol);
        }

        [Test]
        public void ALayerWithNoRotateHasNoTurnAndNoShift()
        {
            var s = SpellFrameCursor.SampleOf(Instance(new SpellLayer()), 1, 0.5f);
            Assert.AreEqual(0f, s.Degrees, 0f);
            Assert.AreEqual(0f, s.ShiftX, 0f);
            Assert.AreEqual(0f, s.ShiftY, 0f);
        }

        // ---- E2: fadeIn ----------------------------------------------------

        [Test]
        public void FadeInRampsAlphaLinearlyThenHoldsTheOpacity()
        {
            var instance = Instance(new SpellLayer { fadeIn = 0.2f, opacity = 0.8f });
            Assert.IsFalse(SpellFrameCursor.SampleOf(instance, 1, 0f).Visible, "alpha 0 at the opening instant");
            Assert.AreEqual(0.4f, SpellFrameCursor.SampleOf(instance, 1, 0.1f).Alpha, Tol);
            Assert.AreEqual(0.8f, SpellFrameCursor.SampleOf(instance, 1, 0.2f).Alpha, Tol);
            Assert.AreEqual(0.8f, SpellFrameCursor.SampleOf(instance, 1, 0.5f).Alpha, Tol);
        }

        // ---- E4: breathe ---------------------------------------------------

        [Test]
        public void BreatheMovesAlphaAndScaleAndNeverBrightensPastTheOpacity()
        {
            var instance = Instance(new SpellLayer
            { opacity = 0.9f, breathe = new SpellBreathe { alpha = 0.4f, scale = 0.1f, rate = 4f } });

            float lowA = 9f, highA = 0f, lowS = 9f, highS = 0f;
            for (float t = 0.01f; t < 1f; t += 0.01f)
            {
                var s = SpellFrameCursor.SampleOf(instance, 1, t);
                lowA = System.Math.Min(lowA, s.Alpha);
                highA = System.Math.Max(highA, s.Alpha);
                lowS = System.Math.Min(lowS, s.Scale);
                highS = System.Math.Max(highS, s.Scale);
            }

            Assert.LessOrEqual(highA, 0.9f + 1e-5f, "never past the authored opacity");
            Assert.Less(lowA, 0.9f - 0.2f, "it dips by a visible fraction of 0.4");
            Assert.Greater(highS - lowS, 0.1f, "scale travels more than half its 0.2 span in a second at rate 4");
            Assert.LessOrEqual(highS, 1.1f + 1e-5f);
            Assert.GreaterOrEqual(lowS, 0.9f - 1e-5f);
        }

        [Test]
        public void TheBreathingCurveIsNormalisedToMinusOneAndOne()
        {
            var b = new SpellBreathe { alpha = 1f, scale = 0.5f };
            float lo = 9f, hi = -9f;
            for (float t = 0f; t < 60f; t += 0.01f)
            {
                float w = b.WaveAt(t, 0f);
                lo = System.Math.Min(lo, w);
                hi = System.Math.Max(hi, w);
            }

            Assert.AreEqual(-1f, lo, 0.05f);
            Assert.AreEqual(1f, hi, 0.05f);
        }

        [Test]
        public void AnUnauthoredBreatheIsExactlyOneAndNotAuthored()
        {
            Assert.IsFalse(new SpellBreathe().IsAuthored);
            var s = SpellFrameCursor.SampleOf(Instance(new SpellLayer()), 1, 0.37f);
            Assert.AreEqual(1f, s.Scale, 0f);
            Assert.AreEqual(1f, s.Alpha, 0f);
        }

        // ---- rules ---------------------------------------------------------

        private static List<string> Problems(SpellLayer layer)
        {
            layer.id = "core";
            if (string.IsNullOrEmpty(layer.render)) layer.render = "sprite";
            layer.place = "target";
            layer.path = "Spells/frost_flare";
            layer.seconds = 0.5f;
            return SpellLayerRules.Check("skill 'test_spell'",
                new SpellPresentation { layerFormat = 1, layers = new[] { layer } });
        }

        [Test]
        public void ALegalPendulumAndBreatheAndFadeInPass()
        {
            CollectionAssert.IsEmpty(Problems(new SpellLayer
            {
                fadeIn = 0.2f,
                rotate = new SpellRotate { amplitude = -14f, period = 0.93f, pivotX = 0.37f, pivotY = 0.88f },
                breathe = new SpellBreathe { alpha = 0.4f, scale = 0.06f, rate = 3f },
            }));
        }

        [TestCase(0f, "rotate.period 0 must be 0.1..10")]
        [TestCase(30f, "rotate.period 30 must be 0.1..10")]
        public void APendulumNeedsASaneRealPeriod(float period, string fragment)
        {
            var problems = Problems(new SpellLayer { rotate = new SpellRotate { amplitude = 10f, period = period } });
            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains(fragment, problems[0]);
        }

        [Test]
        public void ARotateOutOfRangeIsRefusedWithItsFieldNamed()
        {
            var problems = Problems(new SpellLayer
            { rotate = new SpellRotate { degrees = 400f, pivotY = 64f } });
            Assert.AreEqual(2, problems.Count, string.Join(" | ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("rotate.pivotY 64 is outside 0..1")));
            Assert.IsTrue(problems.Any(p => p.Contains("rotate.degrees 400 is outside -180..180")));
        }

        [Test]
        public void BreatheAndFadeInOutOfRangeAreRefused()
        {
            var problems = Problems(new SpellLayer
            { fadeIn = 0.9f, breathe = new SpellBreathe { alpha = 2f, scale = 8f, rate = 0f } });
            Assert.AreEqual(4, problems.Count, string.Join(" | ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("breathe.alpha 2 is outside 0..1")));
            Assert.IsTrue(problems.Any(p => p.Contains("breathe.scale 8 is outside 0..0.5")));
            Assert.IsTrue(problems.Any(p => p.Contains("breathe.rate 0 must be above 0")));
            Assert.IsTrue(problems.Any(p => p.Contains("fadeIn 0.9 is longer than the 0.5s")));
        }

        [Test]
        public void AnEmitterRefusesAllThreeAsInert()
        {
            var problems = Problems(new SpellLayer
            {
                render = "emitter",
                fadeIn = 0.1f,
                rotate = new SpellRotate { degrees = 10f },
                breathe = new SpellBreathe { alpha = 0.2f },
                emitter = new SpellEmitter { path = "Spells/x", burst = 3, lifeMin = 0.2f, lifeMax = 0.3f },
            });
            Assert.IsTrue(problems.Any(p => p.Contains("authors rotate, which is inert")), string.Join(" | ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("authors breathe, which is inert")));
            Assert.IsTrue(problems.Any(p => p.Contains("authors fadeIn, which is inert")));
        }
    }
}
