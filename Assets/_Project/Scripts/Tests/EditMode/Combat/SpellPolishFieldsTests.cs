using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE FIELDS THE CENSER'S POLISH PASS ADDED, as models: a glow that moves over
    // a layer's life (glowCurve), afterimages of a moving layer (echo), the body's
    // coal glow following a fire (stoke), extra emitter bursts (burstTimes) and the
    // stage's push on the cue (kick). Literals throughout; nothing here recomputes
    // the cosine or the smoothstep it pins.
    public class SpellPolishFieldsTests
    {
        private const float Tol = 1e-3f;

        private static SpellLayerInstance Instance(SpellLayer layer)
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
            instance.Facing = 1f;
            return instance;
        }

        // ---- glowCurve -----------------------------------------------------

        [Test]
        public void AnUnauthoredGlowCurveLeavesTheGlowAlone()
        {
            Assert.IsFalse(new SpellGlowCurve().IsAuthored);
            var sample = SpellFrameCursor.SampleOf(Instance(new SpellLayer { glow = 0.7f }), 1, 0.4f);
            Assert.AreEqual(0.7f, sample.Glow, Tol);
        }

        [Test]
        public void TheGlowRampsInAStraightLineFromGlowToTheEndValue()
        {
            var curve = new SpellGlowCurve { to = 1.0f };
            Assert.AreEqual(0.2f, curve.At(0.2f, 0f, 2f), Tol);
            Assert.AreEqual(0.6f, curve.At(0.2f, 1f, 2f), Tol);
            Assert.AreEqual(1.0f, curve.At(0.2f, 2f, 2f), Tol);
            Assert.AreEqual(1.0f, curve.At(0.2f, 3f, 2f), Tol, "held at the end value through a fade");
        }

        [Test]
        public void ThePulseCrestsEveryPeriodAndNeverBeforeHalfAPeriodAheadOfTheFirst()
        {
            var curve = new SpellGlowCurve { pulse = 0.5f, period = 0.4f, crest = 0.4f };
            Assert.AreEqual(0.3f, curve.At(0.3f, 0.1f, 1f), Tol, "before the first pulse begins to rise");
            Assert.AreEqual(0.3f + 0.25f, curve.At(0.3f, 0.3f, 1f), Tol, "a quarter period out is halfway up: 0.5 x 0.5");
            Assert.AreEqual(0.3f + 0.5f, curve.At(0.3f, 0.4f, 1f), Tol, "the crest");
            Assert.AreEqual(0.3f, curve.At(0.3f, 0.6f, 1f), Tol, "the trough between crests");
            Assert.AreEqual(0.3f + 0.5f, curve.At(0.3f, 0.8f, 1f), Tol, "the next crest");
        }

        [Test]
        public void TheCurveCarriesItsKneeAndDefaultsToTheShadersOwn()
        {
            Assert.AreEqual(0.72f, new SpellGlowCurve().KneeOrDefault, 0f);
            Assert.AreEqual(0.4f, new SpellGlowCurve { knee = 0.4f }.KneeOrDefault, 0f);
            Assert.IsTrue(new SpellGlowCurve { knee = 0.4f }.IsAuthored);

            var plain = SpellFrameCursor.SampleOf(Instance(new SpellLayer { glow = 0.7f }), 1, 0.4f);
            Assert.AreEqual(0.72f, plain.Knee, Tol);

            var lowered = SpellFrameCursor.SampleOf(
                Instance(new SpellLayer { glow = 0.7f, glowCurve = new SpellGlowCurve { knee = 0.4f } }), 1, 0.4f);
            Assert.AreEqual(0.4f, lowered.Knee, Tol);
            Assert.AreEqual(0.7f, lowered.Glow, Tol, "a knee alone leaves the glow where it was");
        }

        [Test]
        public void TheSampleCarriesTheCurvesGlow()
        {
            var layer = new SpellLayer { glow = 0.3f, glowCurve = new SpellGlowCurve { to = 1f, pulse = 0.5f, period = 0.4f, crest = 0.4f } };
            var sample = SpellFrameCursor.SampleOf(Instance(layer), 1, 0.4f);
            Assert.AreEqual(0.3f + 0.7f * 0.4f + 0.5f, sample.Glow, Tol);
        }

        // ---- echo ----------------------------------------------------------

        [Test]
        public void AnEchoCopyIsFaintNearTheLayerAndFullStrengthAtTheSpread()
        {
            var echo = new SpellEcho { count = 3, lagSeconds = 0.05f, alpha = 0.4f, spread = 100f };
            Assert.AreEqual(0.2f, echo.StrengthOf(1, 50f), Tol, "nearest copy, half the spread");
            Assert.AreEqual(0.4f * 2f / 3f, echo.StrengthOf(2, 100f), Tol, "the next one is a third weaker");
            Assert.AreEqual(0.4f / 3f, echo.StrengthOf(3, 300f), Tol, "capped at the spread");
            Assert.AreEqual(0f, echo.StrengthOf(1, 0f), 0f, "a layer that has not moved leaves nothing");
            Assert.AreEqual(0f, echo.StrengthOf(4, 100f), 0f, "there is no fourth copy");
            Assert.AreEqual(SpellEcho.DefaultSpread, new SpellEcho().SpreadOrDefault, 0f);
        }

        // A 100 x 200 box hooked at its top centre swinging 10 degrees a second-period
        // sine: at 0.5 s it is on the vertical and fastest; 0.1 s earlier it was at 5.878
        // degrees, which moved its centre 10.2408 across and 0.5258 up.
        [Test]
        public void AnEchoSamplesTheLayersOwnMotionAtTheLaggedInstant()
        {
            var layer = new SpellLayer
            {
                rotate = new SpellRotate { amplitude = 10f, period = 1f, pivotX = 0.5f, pivotY = 1f },
                echo = new SpellEcho { count = 2, lagSeconds = 0.1f, alpha = 0.5f, spread = 20f },
            };
            var instance = Instance(layer);

            var first = SpellFrameCursor.EchoOf(instance, 1, 0.5f, 1, out var at);
            Assert.IsTrue(first.Visible);
            Assert.AreEqual(5.8779f, first.Degrees, Tol, "the angle then, not now");
            Assert.AreEqual(10.2408f, at.X, Tol);
            Assert.AreEqual(0.5258f, at.Y, Tol);
            Assert.AreEqual(0.5f * 1f * (10.2538f / 20f), first.Alpha, 2e-3f, "alpha x step x distance over spread");

            var second = SpellFrameCursor.EchoOf(instance, 1, 0.5f, 2, out var farther);
            Assert.AreEqual(9.5106f, second.Degrees, Tol);
            Assert.AreEqual(16.5229f, farther.X, Tol);
            Assert.AreEqual(0.5f * 0.5f * (16.5784f / 20f), second.Alpha, 2e-3f, "further from the layer but a step weaker");
        }

        [Test]
        public void AHeldLayerLeavesNoAfterimageAndNoneShowsBeforeTheLayerOpened()
        {
            var held = Instance(new SpellLayer
            {
                rotate = new SpellRotate { degrees = 5f },
                echo = new SpellEcho { count = 1, lagSeconds = 0.1f, alpha = 0.5f },
            });
            Assert.IsFalse(SpellFrameCursor.EchoOf(held, 1, 0.5f, 1, out _).Visible, "a still layer has no echo");

            var moving = Instance(new SpellLayer
            {
                rotate = new SpellRotate { amplitude = 10f, period = 1f, pivotY = 1f },
                echo = new SpellEcho { count = 1, lagSeconds = 0.1f, alpha = 0.5f },
            });
            Assert.IsFalse(SpellFrameCursor.EchoOf(moving, 1, 0.05f, 1, out _).Visible, "0.05 s in, 0.1 s ago it was not open");
            Assert.IsFalse(SpellFrameCursor.EchoOf(moving, 1, 0.5f, 2, out _).Visible, "no second copy was authored");
        }

        // ---- stoke ---------------------------------------------------------

        [Test]
        public void AFireLayerStokesInProportionToItsOwnFadeInAndNothingOutsideItsLife()
        {
            var fire = Instance(new SpellLayer { stoke = 0.8f, fadeIn = 0.2f, opacity = 0.5f });
            Assert.AreEqual(0.8f * 0.5f, SpellFrameCursor.StokeOf(fire, 0.1f), Tol, "half faded in");
            Assert.AreEqual(0.8f, SpellFrameCursor.StokeOf(fire, 0.5f), Tol, "full strength once faded in");
            Assert.AreEqual(0f, SpellFrameCursor.StokeOf(fire, 1.5f), Tol, "past its life, no fade: gone");

            var none = Instance(new SpellLayer());
            Assert.AreEqual(0f, SpellFrameCursor.StokeOf(none, 0.5f), 0f);
        }

        [Test]
        public void AStokingFireFlickersWithItsOwnBreathing()
        {
            var fire = Instance(new SpellLayer { stoke = 1f, breathe = new SpellBreathe { alpha = 0.35f, rate = 4f } });
            float low = 1f, high = 0f;
            for (float t = 0.05f; t < 1f; t += 0.01f)
            {
                float s = SpellFrameCursor.StokeOf(fire, t);
                low = System.Math.Min(low, s);
                high = System.Math.Max(high, s);
            }

            Assert.LessOrEqual(high, 1f + 1e-5f, "never brighter than the layer's authored opacity");
            Assert.GreaterOrEqual(low, 0.65f - 1e-4f, "the flutter dips by at most its authored 0.35");
            Assert.Less(low, 0.95f, "and does dip");
        }

        // ---- emitter burstTimes ---------------------------------------------

        private static SpellEmitter Bursting() => new SpellEmitter
        {
            burst = 2, burstTimes = new[] { 0.2f, 0.6f }, burstEach = 3, rate = 10f, window = 0.5f, lifeMin = 0.3f, lifeMax = 0.4f,
        };

        [Test]
        public void ExtraBurstsSitBetweenTheOpeningBurstAndTheStream()
        {
            var spec = Bursting();
            Assert.AreEqual(2 + 6 + 5, SpellEmitterSim.CountOf(spec));
            Assert.AreEqual(6, SpellEmitterSim.PulsedOf(spec));

            var births = Enumerable.Range(0, 13).Select(i => SpellEmitterSim.BirthOf(spec, i, 1f)).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                1f, 1f,
                1.2f, 1.2f, 1.2f, 1.6f, 1.6f, 1.6f,
                1.0f, 1.1f, 1.2f, 1.3f, 1.4f,
            }, births.Select(b => (float)System.Math.Round(b, 4)).ToArray());
        }

        [Test]
        public void AnEmitterLivesToItsLastBurstPlusTheLongestParticleLife()
        {
            var spec = Bursting();
            Assert.AreEqual(0.6f, SpellEmitterSim.LastBirthOffsetOf(spec), 1e-6f, "the 0.6 s burst is later than the 0.5 s window");

            var layer = new SpellLayer { id = "sparks", render = "emitter", place = "target", at = "release", emitter = spec };
            var vfx = new SpellPresentation { layerFormat = 1, layers = new[] { layer } };
            var instance = SpellPerformance.Resolve(vfx, 1, p => 1).Instances.Single();
            Assert.AreEqual(0.6f + 0.4f, instance.EndSeconds, 1e-6f);

            spec.burstTimes = null;
            Assert.AreEqual(0.5f, SpellEmitterSim.LastBirthOffsetOf(spec), 1e-6f);
            Assert.AreEqual(2 + 5, SpellEmitterSim.CountOf(spec), "no times, no extra bursts");
        }

        // ---- kick ----------------------------------------------------------

        [Test]
        public void TheKickRisesToItsPeakThenEasesBackAndIsZeroOutsideThem()
        {
            var kick = new SpellKick { strength = 0.025f, rise = 0.03f, fall = 0.18f };
            Assert.AreEqual(0f, kick.At(-0.01f), 0f, "nothing before the cue");
            Assert.AreEqual(0f, kick.At(0f), 0f);
            Assert.AreEqual(0.0125f, kick.At(0.015f), 1e-5f, "half way up the rise");
            Assert.AreEqual(0.025f, kick.At(0.03f), 1e-5f, "the peak");
            Assert.AreEqual(0.00625f, kick.At(0.12f), 1e-5f, "half way down: a quarter of the peak");
            Assert.AreEqual(0f, kick.At(0.21f), 1e-6f);
            Assert.AreEqual(0f, kick.At(5f), 0f);
            Assert.AreEqual(1f + 0.03f + 0.18f, kick.EndSeconds(1f), 1e-6f);
            Assert.AreEqual(0f, new SpellKick().EndSeconds(1f), 0f, "no kick, nothing to wait for");
        }

        [Test]
        public void APerformanceReadsTheKickFromItsCue()
        {
            var layer = new SpellLayer { id = "core", render = "still", place = "target", path = "Spells/test", seconds = 2f };
            var vfx = new SpellPresentation
            {
                layerFormat = 1, hitCueSeconds = 1f, layers = new[] { layer },
                kick = new SpellKick { strength = 0.025f, rise = 0.03f, fall = 0.18f },
            };
            var performance = SpellPerformance.Resolve(vfx, 1, p => 1);
            Assert.AreEqual(0f, performance.KickAt(0.99f), 0f);
            Assert.AreEqual(0.025f, performance.KickAt(1.03f), 1e-5f);
            Assert.AreEqual(1.21f, performance.KickEndSeconds, 1e-5f);
        }

        // ---- the rules -----------------------------------------------------

        private static SpellLayer Still() => new SpellLayer
        {
            id = "core", render = "still", place = "target", path = "Spells/x", seconds = 0.5f,
        };

        private static SpellLayer Emitter(SpellEmitter spec) => new SpellLayer
        {
            id = "drops", render = "emitter", place = "target", emitter = spec,
        };

        private static SpellEmitter Spec() => new SpellEmitter
        {
            path = "Spells/x", burst = 4, lifeMin = 0.2f, lifeMax = 0.3f, speedMin = 10f, speedMax = 20f,
        };

        private static System.Collections.Generic.List<string> Check(SpellLayer layer, SpellKick kick = null) =>
            SpellLayerRules.Check("skill 't'", new SpellPresentation
            {
                layerFormat = 1, layers = new[] { layer }, kick = kick ?? new SpellKick(),
            });

        [Test]
        public void TheRulesAcceptAWellFormedPolish()
        {
            var layer = Still();
            layer.glowCurve = new SpellGlowCurve { to = 1f, pulse = 0.4f, period = 0.4f, crest = 0.4f };
            layer.echo = new SpellEcho { count = 3, lagSeconds = 0.04f, alpha = 0.35f, spread = 140f };
            layer.stoke = 1f;
            Assert.IsEmpty(Check(layer, new SpellKick { strength = 0.025f, rise = 0.03f, fall = 0.18f }));

            var spec = Spec();
            spec.burstTimes = new[] { 0.2f, 0.6f };
            spec.burstEach = 5;
            Assert.IsEmpty(Check(Emitter(spec)));
        }

        [Test]
        public void TheRulesRefuseAnEchoThatCannotWork()
        {
            var tooMany = Still();
            tooMany.echo = new SpellEcho { count = 5, lagSeconds = 0.04f, alpha = 0.3f };
            StringAssert.Contains("echo.count 5 is outside 0..4", Check(tooMany).Single());

            var noLag = Still();
            noLag.echo = new SpellEcho { count = 2, alpha = 0.3f };
            StringAssert.Contains("echo.lagSeconds 0 must be above 0", Check(noLag).Single());

            var orphan = Still();
            orphan.echo = new SpellEcho { alpha = 0.3f };
            StringAssert.Contains("authors lagSeconds, alpha or spread with no count", Check(orphan).Single());

            var onEmitter = Emitter(Spec());
            onEmitter.echo = new SpellEcho { count = 1, lagSeconds = 0.04f, alpha = 0.3f };
            StringAssert.Contains("emitter and authors echo", Check(onEmitter).Single());
        }

        [Test]
        public void TheRulesRefuseAGlowCurveWithoutAPeriodAndAStokeOutOfRange()
        {
            var layer = Still();
            layer.glowCurve = new SpellGlowCurve { pulse = 0.4f };
            StringAssert.Contains("glowCurve.pulse 0.4 needs a period above 0", Check(layer).Single());

            var knee = Still();
            knee.glowCurve = new SpellGlowCurve { knee = 1.5f };
            StringAssert.Contains("glowCurve.knee 1.5 is outside 0..1", Check(knee).Single());

            var stoked = Still();
            stoked.stoke = 1.5f;
            StringAssert.Contains("stoke 1.5 is outside 0..1", Check(stoked).Single());

            var onEmitter = Emitter(Spec());
            onEmitter.stoke = 0.5f;
            StringAssert.Contains("emitter and authors stoke", Check(onEmitter).Single());
        }

        [Test]
        public void TheRulesRefuseBurstTimesThatDoNotAddUp()
        {
            var unordered = Spec();
            unordered.burstTimes = new[] { 0.6f, 0.2f };
            unordered.burstEach = 3;
            StringAssert.Contains("burstTimes[1] 0.2 must be ascending", Check(Emitter(unordered)).Single());

            var noCount = Spec();
            noCount.burstTimes = new[] { 0.2f };
            StringAssert.Contains("burstEach is 0, so nothing is thrown", Check(Emitter(noCount)).Single());

            var noTimes = Spec();
            noTimes.burstEach = 3;
            StringAssert.Contains("burstEach 3 has no burstTimes", Check(Emitter(noTimes)).Single());
        }

        [Test]
        public void TheRulesRefuseAKickPastTenPercentOfTheStage()
        {
            string message = Check(Still(), new SpellKick { strength = 0.2f }).Single();
            StringAssert.Contains("vfx.kick.strength 0.2 is outside 0..0.1", message);
            StringAssert.Contains("fraction of the stage's width", message);
        }
    }
}
