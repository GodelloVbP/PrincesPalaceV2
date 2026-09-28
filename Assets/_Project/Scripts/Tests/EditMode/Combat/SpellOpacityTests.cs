using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Tests
{
    // A LAYER'S AUTHORED OPACITY: held for its whole life, and multiplying the
    // fade-out rather than replacing it. Earned by the Bellwether's toll ripple
    // (kit-m5 capture), four opaque rings that hid the body they rang from.
    //
    // Literal expectations with the arithmetic written out (CLAUDE.md gotcha 5):
    // a 0.63s layer with a 0.15s fade at opacity 0.5.
    public class SpellOpacityTests
    {
        private const float Lifetime = 0.63f;
        private const float Fade = 0.15f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void AnUnauthoredLayerDrawsAtFullStrength()
        {
            Assert.AreEqual(1f, new SpellLayer().opacity);
            Assert.AreEqual(1f, AlphaAt(Instance(1f), 0.3f), Tolerance);
        }

        [Test]
        public void AHalfOpaqueLayerIsHalfOpaqueForItsWholeLife()
        {
            Assert.AreEqual(0.5f, AlphaAt(Instance(0.5f), 0f), Tolerance);
            Assert.AreEqual(0.5f, AlphaAt(Instance(0.5f), 0.3f), Tolerance);
            Assert.AreEqual(0.5f, AlphaAt(Instance(0.5f), Lifetime), Tolerance);
        }

        [Test]
        public void TheFadeOutScalesFromTheOpacityDownToNothing()
        {
            // Halfway through the fade: (1 - 0.5) x 0.5 = 0.25.
            Assert.AreEqual(0.25f, AlphaAt(Instance(0.5f), Lifetime + Fade * 0.5f), Tolerance);
            Assert.IsFalse(SpellFrameCursor.SampleOf(Instance(0.5f), 7, Lifetime + Fade + 0.01f).Visible);
        }

        [Test]
        public void AHeldStillCarriesTheOpacityToo()
        {
            var instance = Instance(0.4f);
            instance.RenderKind = SpellRender.Still;

            Assert.AreEqual(0.4f, AlphaAt(instance, 0.1f), Tolerance);
        }

        [TestCase(0f)]
        [TestCase(-0.2f)]
        [TestCase(1.3f)]
        public void ContentRefusesAnOpacityOutsideZeroToOne(float opacity)
        {
            var problems = ProblemsFor(opacity);

            Assert.IsTrue(problems.Exists(p => p.Contains("opacity")), string.Join("\n", problems));
        }

        [Test]
        public void ContentAcceptsAHalfOpaqueLayer()
        {
            var problems = ProblemsFor(0.5f);

            Assert.IsFalse(problems.Exists(p => p.Contains("opacity")), string.Join("\n", problems));
        }

        private static List<string> ProblemsFor(float opacity)
        {
            var presentation = new SpellPresentation
            {
                layerFormat = 1,
                hitCueSeconds = 0.2f,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "ripple",
                        render = "sprite",
                        place = "caster-centre",
                        path = "Spells/bellwether_toll_ripple",
                        seconds = Lifetime,
                        fade = Fade,
                        opacity = opacity,
                    },
                },
            };
            return SpellLayerRules.Check("toll", presentation);
        }

        private static float AlphaAt(SpellLayerInstance instance, float seconds) =>
            SpellFrameCursor.SampleOf(instance, 7, seconds).Alpha;

        private static SpellLayerInstance Instance(float opacity) => new SpellLayerInstance
        {
            Layer = new SpellLayer
            {
                id = "ripple",
                render = "sprite",
                place = "caster-centre",
                path = "Spells/bellwether_toll_ripple",
                seconds = Lifetime,
                fade = Fade,
                opacity = opacity,
            },
            RenderKind = SpellRender.Sprite,
            UntilKind = SpellEnd.Once,
            StartSeconds = 0f,
            EndSeconds = Lifetime,
            FadeSeconds = Fade,
        };
    }
}
