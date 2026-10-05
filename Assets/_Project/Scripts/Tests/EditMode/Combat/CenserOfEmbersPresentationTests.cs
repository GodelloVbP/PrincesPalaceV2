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
        public void TheCenserHangsAndSwingsLeftRightLeftThenBreaksOnTheThirdExtreme()
        {
            var vfx = Censer();
            Assert.AreEqual(1.0f, vfx.hitCueSeconds, 1e-6f);

            var censer = vfx.layers.Single(l => l.id == "censer");
            Assert.AreEqual("above", censer.place);
            Assert.AreEqual(3f, censer.height, 1e-6f, "a cap only: the hanging box is held at the stage top");
            Assert.AreEqual(270f, censer.size, 1e-6f);
            Assert.AreEqual("", censer.fit, "one size on every body: the lantern must clear a treant and stay on screen");
            Assert.AreEqual("release", censer.at);
            Assert.AreEqual(1.0f, censer.seconds, 1e-6f, "it lasts to the cue and no longer");
            Assert.AreEqual(0f, censer.fade, 1e-6f, "the break replaces it");
            Assert.AreEqual(0.2f, censer.fadeIn, 1e-6f, "no pop-in");
            var r = censer.rotate;
            Assert.AreEqual(-35f, r.amplitude, 1e-6f, "clockwise first: left");
            Assert.AreEqual(0.8f, r.period, 1e-6f);
            Assert.AreEqual(1.5f, r.dwell, 1e-6f);
            Assert.AreEqual(0f, r.damping, 1e-6f);
            Assert.AreEqual("Spells/censer_hanging", censer.path, "the owner's painted censer, not the swing sheet");
            Assert.AreEqual(0.5f, r.pivotX, 1e-6f, "the chain leaves the top edge at the centre (opaque columns 596-657 of 1254)");
            Assert.AreEqual(1.0f, r.pivotY, 1e-6f, "the top edge");

            // Left extreme 0.2, right 0.6, left 1.0: three half-swings, the last ends on the cue.
            Assert.AreEqual(-35f, r.AngleAt(0.2f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.4f), 1e-3f);
            Assert.AreEqual(35f, r.AngleAt(0.6f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.8f), 1e-3f);
            Assert.AreEqual(-35f, r.AngleAt(1.0f), 1e-3f);

            // Heavy at the ends: 0.08 s (a tenth of a period) before the first extreme it is
            // within 0.6 degrees of it, where a plain sine would still be 6.7 degrees short.
            Assert.AreEqual(-34.4421f, r.AngleAt(0.12f), 1e-2f);
        }

        [Test]
        public void TheCenserBreaksOnTheCueIntoAPunchedStillThenAShowerOfEmbersAndCoals()
        {
            var vfx = Censer();
            var crack = vfx.layers.Single(l => l.id == "crack");
            Assert.AreEqual("hit", crack.at);
            Assert.AreEqual("Spells/censer_hanging", crack.path, "the censer's own still");
            Assert.AreEqual(0.04f, crack.seconds, 1e-6f);
            Assert.AreEqual(0.05f, crack.fade, 1e-6f);
            Assert.AreEqual(0.4f, crack.punch, 1e-6f);
            Assert.AreEqual(-35f, crack.rotate.degrees, 1e-6f, "held at the third extreme");
            Assert.AreEqual(0f, crack.rotate.amplitude, 1e-6f);
            Assert.AreEqual(0.5f, crack.rotate.pivotX, 1e-6f);
            Assert.AreEqual(1.0f, crack.rotate.pivotY, 1e-6f);

            var burst = vfx.layers.Single(l => l.id == "burst");
            Assert.AreEqual("hit", burst.at);
            Assert.AreEqual("layer:censer", burst.place, "born off the censer's own box");
            Assert.AreEqual(48, burst.emitter.burst);
            Assert.AreEqual(360f, burst.emitter.spreadDegrees, 1e-6f, "every direction");
            Assert.AreEqual(-760f, burst.emitter.gravity, 1e-6f, "negative: pulled down");
            CollectionAssert.AreEqual(new[] { 2f, 1f, 0f, 1f }, burst.emitter.weights, "coals and flecks, never the smoke wisp");
            Assert.AreEqual(1.2f, burst.emitter.lifeMax, 1e-6f);
            Assert.AreEqual(0.9f, burst.emitter.sizeMax, 1e-6f);
            Assert.AreEqual(-111f, burst.emitter.sourceDx, 1e-6f, "where the lantern hangs at the third extreme");
            Assert.AreEqual(-24f, burst.emitter.sourceDy, 1e-6f);

            var coals = vfx.layers.Single(l => l.id == "coals");
            Assert.AreEqual("hit", coals.at);
            Assert.AreEqual(6, coals.emitter.burst);
            Assert.AreEqual(2.2f, coals.emitter.sizeMax, 1e-6f, "big next to the embers");
            Assert.AreEqual(540f, coals.emitter.spinMax, 1e-6f, "they tumble");
            Assert.AreEqual(-111f, coals.emitter.sourceDx, 1e-6f);
        }

        [Test]
        public void TheLanternAtTheThirdExtremeIsWhereTheBurstIsBorn()
        {
            // censer_hanging.png: the lantern body centres at (0.498, 0.72 of the height from
            // the top), the chain top is the pivot (0.5, 1). Turned -35 degrees about it, the
            // lantern centre lands at (-0.413, -0.090) box sizes from the box centre; at the
            // 270 box that is (-111.5, -24.3), the burst's source offset.
            var r = Censer().layers.Single(l => l.id == "censer").rotate;
            double rad = r.AngleAt(1.0f) * System.Math.PI / 180.0;
            double vx = 0.498 - r.pivotX, vy = 0.28 - r.pivotY;
            double x = (r.pivotX - 0.5) + System.Math.Cos(rad) * vx - System.Math.Sin(rad) * vy;
            double y = (r.pivotY - 0.5) + System.Math.Sin(rad) * vx + System.Math.Cos(rad) * vy;
            Assert.AreEqual(-111.5, x * 270, 1.0);
            Assert.AreEqual(-24.3, y * 270, 1.0);
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
            CollectionAssert.AreEqual(new[] { "censer", "crack", "burst", "coals", "fire", "rise" }, ids);
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
            Assert.AreEqual(0.08f, fire.fadeIn, 1e-6f);
            Assert.AreEqual(0.5f, fire.seconds, 1e-6f);
            Assert.AreEqual(0.5f, fire.fade, 1e-6f);
            Assert.AreEqual(0.125f, fire.impactY, 1e-6f, "the coal line, row 390 of 444");
            Assert.AreEqual(0.35f, fire.breathe.alpha, 1e-6f);
            Assert.AreEqual(0.07f, fire.breathe.scale, 1e-6f);
            Assert.AreEqual(4f, fire.breathe.rate, 1e-6f);

            var rise = vfx.layers.Single(l => l.id == "rise");
            Assert.AreEqual(0.05f, rise.offset, 1e-6f);
            Assert.AreEqual(140f, rise.emitter.gravity, 1e-6f, "positive: they float up");
            Assert.AreEqual(0.06f, rise.emitter.sizeMin, 1e-6f, "the last thing on screen is small");
            Assert.AreEqual(0.9f, rise.emitter.lifeMax, 1e-6f);
        }
    }
}
