using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE OVERSHOOT A LAYER OPENS AT, and the two things it must not do:
    // outlive its own window, or move a layer that authored none.
    //
    // Cinderfault "feels more like a POP": halving the
    // cast buys the tempo; the punch is what makes the opening frame land
    // rather than merely arrive early.
    //
    // EVERY EXPECTED NUMBER IS A LITERAL with its arithmetic written out
    // (CLAUDE.md gotcha 5): the curve is 1 + punch x (1 - t)^2 over
    // SpellFrameCursor.PunchFraction of the lifetime, and each assertion below
    // states that evaluation rather than calling it.
    public class SpellPunchCurveTests
    {
        // Cinderfault's own numbers after the halving: a 0.39s layer with a
        // 0.22 punch, so the window is 0.39 x 0.2 = 0.078s.
        private const float Lifetime = 0.39f;
        private const float Punch = 0.22f;
        private const float Window = 0.078f;

        private const float Tolerance = 1e-4f;

        [Test]
        public void TheLayerOpensOversizedByExactlyItsAuthoredPunch()
        {
            // t = 0, so (1 - t)^2 = 1 and the scale is 1 + 0.22.
            Assert.AreEqual(1.22f, ScaleAt(0f), Tolerance);
        }

        // EASED OUT, NOT LINEAR, and this is the assertion that tells the two
        // apart. Halfway through the window a linear settle would sit at 1.11;
        // a quadratic ease-out has already shed three quarters of the
        // overshoot.
        [Test]
        public void MostOfTheOvershootIsGoneByTheMiddleOfTheWindow()
        {
            // t = 0.5, (1 - t)^2 = 0.25, so 1 + 0.22 x 0.25 = 1.055.
            Assert.AreEqual(1.055f, ScaleAt(Window * 0.5f), Tolerance);
            Assert.AreNotEqual(1.11f, ScaleAt(Window * 0.5f), "a linear settle would sit here");
        }

        [Test]
        public void TheOvershootIsFullyGoneAtTheEndOfItsWindowAndStaysGone()
        {
            Assert.AreEqual(1f, ScaleAt(Window), Tolerance);

            // WELL BEFORE THE PEAK, which is the point of deriving the window
            // from the lifetime rather than authoring it. Cinderfault ruptures
            // at 5/9 of 0.39s = 0.2167s, nearly three windows in, so the sheet
            // is at its true size long before the blow lands.
            Assert.AreEqual(1f, ScaleAt(0.21666667f), Tolerance,
                "the rupture frame must be drawn at the size the box was measured for");
            Assert.AreEqual(1f, ScaleAt(Lifetime), Tolerance);
        }

        // THE INERTNESS CLAIM, and it is the one worth a test of its own: a
        // field was added to a type every spell in the game carries, and the
        // claim that an unauthored one multiplies the box by a number that
        // cannot round it is a claim, not a certainty.
        [Test]
        public void ALayerThatAuthorsNoPunchScalesByExactlyOne()
        {
            var instance = Instance(punch: 0f);

            Assert.AreEqual(1f, SpellFrameCursor.SampleOf(instance, 9, 0f).Scale);
            Assert.AreEqual(1f, SpellFrameCursor.SampleOf(instance, 9, 0.05f).Scale);
            Assert.AreEqual(1f, SpellFrameCursor.SampleOf(instance, 9, Lifetime).Scale);
        }

        // A STILL IS PUNCHED TOO. It takes the early-return path out of
        // SampleOf -- one frame, held -- and a scale threaded onto only the
        // animated branch would be a field that works for four of the five
        // exits. Cheap to assert, and the kind of hole that is invisible until
        // somebody authors the one layer that lands in it.
        [Test]
        public void AHeldStillCarriesThePunchAsWell()
        {
            var instance = Instance(Punch);
            instance.RenderKind = SpellRender.Still;

            Assert.AreEqual(1.22f, SpellFrameCursor.SampleOf(instance, 9, 0f).Scale, Tolerance);
        }

        private static float ScaleAt(float seconds) =>
            SpellFrameCursor.SampleOf(Instance(Punch), 9, seconds).Scale;

        private static SpellLayerInstance Instance(float punch) => new SpellLayerInstance
        {
            Layer = new SpellLayer
            {
                id = "erupt",
                render = "sprite",
                place = "target",
                path = "Spells/cinderfault_eruption",
                seconds = Lifetime,
                punch = punch,
            },
            RenderKind = SpellRender.Sprite,
            UntilKind = SpellEnd.Once,
            StartSeconds = 0f,
            EndSeconds = Lifetime,
        };
    }
}
