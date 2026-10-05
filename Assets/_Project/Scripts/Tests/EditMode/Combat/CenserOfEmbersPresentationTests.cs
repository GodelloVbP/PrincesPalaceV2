using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // CENSER OF EMBERS' BEATS as literal seconds (docs/PLAN_SPELL_ANIMATION.md
    // section 3): a 1.0 s wind-up (the owner's call, over contract 10's cap) in
    // which the censer hangs from off-stage and swings left, right, left about
    // its chain top, breaks into an ember shower on the third extreme with the
    // sound and the Burn badge, and leaves a living fire at the feet that dies
    // down over 0.5 s.
    public class CenserOfEmbersPresentationTests
    {
        private static SpellPresentation Censer()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "censer_of_embers");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheCenserHangsOnALongChainAndSwingsLeftRightLeftEachSwingLargerThenBreaks()
        {
            var vfx = Censer();
            Assert.AreEqual(1.0f, vfx.hitCueSeconds, 1e-6f);

            var censer = vfx.layers.Single(l => l.id == "censer");
            Assert.AreEqual("above", censer.place);
            Assert.AreEqual(3f, censer.height, 1e-6f, "a cap only: the hanging box is held on the stage");
            Assert.AreEqual(300f, censer.size, 1e-6f);
            Assert.AreEqual("", censer.fit, "one size on every body");
            Assert.AreEqual("release", censer.at);
            Assert.AreEqual(1.0f, censer.seconds, 1e-6f, "it lasts to the cue and no longer");
            Assert.AreEqual(0f, censer.fade, 1e-6f, "the break replaces it");
            Assert.AreEqual("Spells/censer_hanging", censer.path);
            var r = censer.rotate;
            Assert.AreEqual(-6.7f, r.amplitude, 1e-6f, "clockwise first: left");
            Assert.AreEqual(0.8f, r.period, 1e-6f);
            Assert.AreEqual(-0.8f, r.damping, 1e-6f, "negative: the swing builds");
            Assert.AreEqual(0.5f, r.pivotX, 1e-6f, "the chain leaves the top edge at the centre (opaque columns 596-657 of 1254)");
            Assert.AreEqual(2.5f, r.pivotY, 1e-6f, "2.5 box heights up: a 650 px chain, so a wide arc and a small tilt");

            // Left extreme 0.2, right 0.6, left 1.0; the last ends on the cue. A plain
            // sine: still through the vertical at 0.4 and 0.8, fastest there.
            Assert.AreEqual(-7.8625f, r.AngleAt(0.2f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.4f), 1e-3f);
            Assert.AreEqual(10.8277f, r.AngleAt(0.6f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.8f), 1e-3f);
            Assert.AreEqual(-14.9111f, r.AngleAt(1.0f), 1e-3f);
        }

        // THE LANTERN'S PATH (lantern centre 50 below the box centre, 300 box, pivot
        // 600 over the centre): (-88.9, -43.9) at the first extreme, (0, -50) at the
        // bottom, (122.1, -38.4) at the second, (-167.3, -28.1) at the third. It
        // rises 22 px over the bottom at the last extreme and sweeps 290 px across.
        [Test]
        public void TheLanternTravelsAWideArcRatherThanTiltingInPlace()
        {
            var r = Censer().layers.Single(l => l.id == "censer").rotate;
            var expected = new[]
            {
                (0.2f, -88.9, -43.9), (0.4f, 0.0, -50.0), (0.6f, 122.1, -38.4), (0.8f, 0.0, -50.0), (1.0f, -167.3, -28.1),
            };
            foreach (var (t, ex, ey) in expected)
            {
                double rad = r.AngleAt(t) * System.Math.PI / 180.0;
                double px = (r.pivotX - 0.5) * 300, py = (r.pivotY - 0.5) * 300;
                double ox = 0, oy = -50;
                double x = px + System.Math.Cos(rad) * (ox - px) - System.Math.Sin(rad) * (oy - py);
                double y = py + System.Math.Sin(rad) * (ox - px) + System.Math.Cos(rad) * (oy - py);
                Assert.AreEqual(ex, x, 0.1, "x at " + t);
                Assert.AreEqual(ey, y, 0.1, "y at " + t);
            }
        }

        [Test]
        public void TheSmokeTrailIsBornOnTheLanternAsTheCenserSwingsAndStaysWhereItWasBorn()
        {
            var trail = Censer().layers.Single(l => l.id == "trail");
            Assert.AreEqual("layer:censer", trail.place);
            Assert.AreEqual("release", trail.at);
            Assert.AreEqual(10f, trail.emitter.rate, 1e-6f);
            Assert.AreEqual(0.95f, trail.emitter.window, 1e-6f);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 1f, 0f }, trail.emitter.weights, "only the smoke wisp");
            Assert.AreEqual(0f, trail.emitter.inherit, 1e-6f, "it does not follow the censer");
            Assert.AreEqual(20f, trail.emitter.gravity, 1e-6f, "barely up: it hangs in the air");
            Assert.AreEqual(-10f, trail.emitter.sourceDy, 1e-6f, "the lantern's body");
        }

        [Test]
        public void TheCenserBreaksOnTheCueIntoAPunchedStillThenAShowerOfEmbersAndCoals()
        {
            var vfx = Censer();
            var censer = vfx.layers.Single(l => l.id == "censer");
            var crack = vfx.layers.Single(l => l.id == "crack");
            Assert.AreEqual("hit", crack.at);
            Assert.AreEqual("Spells/censer_hanging", crack.path, "the censer's own still");
            Assert.AreEqual(0.04f, crack.seconds, 1e-6f);
            Assert.AreEqual(0.05f, crack.fade, 1e-6f);
            Assert.AreEqual(0.4f, crack.punch, 1e-6f);
            Assert.AreEqual(censer.rotate.AngleAt(1.0f), crack.rotate.degrees, 0.01f, "held at the third extreme");
            Assert.AreEqual(0f, crack.rotate.amplitude, 1e-6f);
            Assert.AreEqual(2.5f, crack.rotate.pivotY, 1e-6f);

            var burst = vfx.layers.Single(l => l.id == "burst");
            Assert.AreEqual("hit", burst.at);
            Assert.AreEqual("layer:censer", burst.place, "born off the censer's own art");
            Assert.AreEqual(42, burst.emitter.burst, "the whole spell asks for 63 of the pool's 64 particles");
            Assert.AreEqual(360f, burst.emitter.spreadDegrees, 1e-6f, "every direction");
            Assert.AreEqual(-760f, burst.emitter.gravity, 1e-6f, "negative: pulled down");
            CollectionAssert.AreEqual(new[] { 2f, 1f, 0f, 1f }, burst.emitter.weights, "coals and flecks, never the smoke wisp");
            Assert.AreEqual(1.2f, burst.emitter.lifeMax, 1e-6f);
            Assert.AreEqual(0.9f, burst.emitter.sizeMax, 1e-6f);
            Assert.AreEqual(0f, burst.emitter.sourceDx, 1e-6f);
            Assert.AreEqual(-50f, burst.emitter.sourceDy, 1e-6f, "the lantern's centre on the art (rows 265-670 of 700)");

            var coals = vfx.layers.Single(l => l.id == "coals");
            Assert.AreEqual("hit", coals.at);
            Assert.AreEqual(5, coals.emitter.burst);
            Assert.AreEqual(2.2f, coals.emitter.sizeMax, 1e-6f, "big next to the embers");
            Assert.AreEqual(540f, coals.emitter.spinMax, 1e-6f, "they tumble");
            Assert.AreEqual(-50f, coals.emitter.sourceDy, 1e-6f);
        }

        [Test]
        public void TheStageDarkensDuringTheSwingsAndLiftsOnTheBreak()
        {
            var gloom = Censer().gloom;
            Assert.AreEqual(0.25f, gloom.strength, 1e-6f);
            Assert.AreEqual(0.4f, gloom.fadeIn, 1e-6f);
            Assert.AreEqual("hit", gloom.until);
            Assert.AreEqual(0.3f, gloom.fadeOut, 1e-6f);
        }

        [Test]
        public void ThePaintedArtCarriesItsOwnSmokeAndFlameSoNoStandInLayersRemain()
        {
            var ids = Censer().layers.Select(l => l.id).ToArray();
            CollectionAssert.AreEqual(new[] { "censer", "trail", "crack", "burst", "coals", "fire", "rise" }, ids);
        }

        [Test]
        public void TheFireAtTheFeetLoopsBreathesAndDiesDownIntoTheCoalGlowOverHalfASecond()
        {
            var vfx = Censer();
            var fire = vfx.layers.Single(l => l.id == "fire");
            Assert.AreEqual("hit", fire.at);
            Assert.AreEqual("target", fire.place, "at the feet, not a column in front of the face");
            Assert.AreEqual("Spells/censer_flame_loop", fire.path);
            Assert.AreEqual("loop", fire.until, "four painted frames, the fourth leading back to the first");
            Assert.AreEqual(8f, fire.fps, 1e-6f);
            Assert.AreEqual(0.45f, fire.offset, 1e-6f, "it catches as the embers land, not on the cue");
            Assert.AreEqual(0.25f, fire.fadeIn, 1e-6f);
            Assert.AreEqual(0.5f, fire.seconds, 1e-6f);
            Assert.AreEqual(0.5f, fire.fade, 1e-6f);
            Assert.AreEqual(0.125f, fire.impactY, 1e-6f, "the coal line, row 390 of 444");
            Assert.AreEqual(0.35f, fire.breathe.alpha, 1e-6f);
            Assert.AreEqual(0.07f, fire.breathe.scale, 1e-6f);
            Assert.AreEqual(4f, fire.breathe.rate, 1e-6f);

            var rise = vfx.layers.Single(l => l.id == "rise");
            Assert.AreEqual(0.45f, rise.offset, 1e-6f);
            Assert.AreEqual(140f, rise.emitter.gravity, 1e-6f, "positive: they float up");
            Assert.AreEqual(0.06f, rise.emitter.sizeMin, 1e-6f, "the last thing on screen is small");
            Assert.AreEqual(0.9f, rise.emitter.lifeMax, 1e-6f);
        }
    }
}
