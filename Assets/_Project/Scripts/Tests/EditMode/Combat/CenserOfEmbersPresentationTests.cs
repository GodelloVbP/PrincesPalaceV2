using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // CENSER OF EMBERS' BEATS as literal seconds (docs/PLAN_SPELL_ANIMATION.md
    // section 3): a 0.7 s wind-up (contract 10's cap) in which the censer hangs
    // above the target and swings left then right about its chain top, a burst on
    // the cue with the sound and the Burn badge, and a living fire at the feet
    // that dies down over 0.5 s.
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
        public void TheCenserHangsAndSwingsForTheWholeWindUpThenCutsOnTheBurst()
        {
            var vfx = Censer();
            Assert.AreEqual(0.7f, vfx.hitCueSeconds, 1e-6f);

            var censer = vfx.layers.Single(l => l.id == "censer");
            Assert.AreEqual("above", censer.place);
            Assert.AreEqual("release", censer.at);
            Assert.AreEqual(0.7f, censer.seconds, 1e-6f, "it lasts to the cue and no longer");
            Assert.AreEqual(0f, censer.fade, 1e-6f, "the burst replaces it");
            Assert.AreEqual(0.2f, censer.fadeIn, 1e-6f, "no pop-in");
            Assert.AreEqual(-18f, censer.rotate.amplitude, 1e-6f, "clockwise first: left, then right");
            Assert.AreEqual(0.93f, censer.rotate.period, 1e-6f, "left extreme 0.23, home 0.47, right extreme 0.70");
            Assert.AreEqual(0f, censer.rotate.damping, 1e-6f);
            Assert.AreEqual(0.37f, censer.rotate.pivotX, 1e-6f, "the chain's top in the swing sheet's f0");
            Assert.AreEqual(0.88f, censer.rotate.pivotY, 1e-6f);

            // The last swing ends on the cue: the pendulum is at its far extreme there.
            Assert.AreEqual(18f, censer.rotate.AngleAt(0.6975f), 1e-3f);
        }

        [Test]
        public void TheSmokeTrailsTheCenserAndBreathes()
        {
            var smoke = Censer().layers.Single(l => l.id == "smoke");
            Assert.AreEqual(7, smoke.startFrame, "the swing sheet's f6, the smoke column");
            Assert.AreEqual(0.4f, smoke.breathe.alpha, 1e-6f);
            Assert.AreEqual(0.06f, smoke.breathe.scale, 1e-6f);
            Assert.AreEqual(3f, smoke.breathe.rate, 1e-6f);
            Assert.AreEqual(0.3f, smoke.fade, 1e-6f, "it outlives the censer, a curl left after the burst");
        }

        [Test]
        public void TheBurstLandsOnTheCueWithAFlashAndAShowerOfEmbers()
        {
            var vfx = Censer();

            var flash = vfx.layers.Single(l => l.id == "flash");
            Assert.AreEqual("hit", flash.at);
            Assert.AreEqual(0f, flash.offset, 1e-6f);
            Assert.AreEqual(6, flash.startFrame, "the sheet's f5, the ember eruption");
            Assert.AreEqual(0.1f, flash.seconds, 1e-6f);
            Assert.AreEqual(0.15f, flash.fade, 1e-6f);

            var burst = vfx.layers.Single(l => l.id == "burst");
            Assert.AreEqual("hit", burst.at);
            Assert.AreEqual("above", burst.place);
            Assert.AreEqual(28, burst.emitter.burst);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 0f, 1f }, burst.emitter.weights, "coals and tongues, never the smoke wisp");
            Assert.AreEqual(0.7f, burst.emitter.lifeMax, 1e-6f);
        }

        [Test]
        public void TheFireAtTheFeetBreathesAndDiesDownIntoTheCoalGlowOverHalfASecond()
        {
            var vfx = Censer();
            var fire = vfx.layers.Single(l => l.id == "fire");
            Assert.AreEqual("hit", fire.at);
            Assert.AreEqual("target", fire.place, "at the feet, not a column in front of the face");
            Assert.AreEqual("Spells/censer_of_embers_catch", fire.path);
            Assert.AreEqual(0.08f, fire.fadeIn, 1e-6f);
            Assert.AreEqual(0.4f, fire.seconds, 1e-6f);
            Assert.AreEqual(0.5f, fire.fade, 1e-6f);
            Assert.AreEqual(0.35f, fire.breathe.alpha, 1e-6f);
            Assert.AreEqual(0.07f, fire.breathe.scale, 1e-6f);
            Assert.AreEqual(4f, fire.breathe.rate, 1e-6f);

            var tongues = vfx.layers.Single(l => l.id == "tongues");
            Assert.AreEqual(0.4f, tongues.emitter.window, 1e-6f);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 1f }, tongues.emitter.weights);

            var rise = vfx.layers.Single(l => l.id == "rise");
            Assert.AreEqual(0.05f, rise.offset, 1e-6f);
            Assert.AreEqual(140f, rise.emitter.gravity, 1e-6f, "positive: they float up");
            Assert.AreEqual(0.06f, rise.emitter.sizeMin, 1e-6f, "the last thing on screen is small");
            Assert.AreEqual(0.9f, rise.emitter.lifeMax, 1e-6f);
        }
    }
}
