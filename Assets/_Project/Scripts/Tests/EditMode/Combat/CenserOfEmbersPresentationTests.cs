using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // CENSER OF EMBERS' BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.10):
    // the censer swings for 0.3 s, the embers catch on the cue with the Burn badge,
    // and the sheet's smoke is gone, so the spell is clean 0.37 s after its payoff
    // (it kept the stage for about 0.7 s).
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
        public void TheCenserSwingsForTheCueAndTheEmbersCatchOnIt()
        {
            var vfx = Censer();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var swing = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(1, swing.startFrame);
            Assert.AreEqual(16.667f, swing.fps, 1e-3f);
            Assert.AreEqual(0.3f, swing.seconds, 1e-6f, "f0-f4 and no more");
            Assert.AreEqual(0f, swing.fade, 1e-6f, "the flame replaces it on the cue");

            var flame = vfx.layers.Single(l => l.id == "catch");
            Assert.AreEqual("hit", flame.at);
            Assert.AreEqual(0f, flame.offset, 1e-6f);
            Assert.AreEqual(6, flame.startFrame, "f5, the embers catching");
            Assert.AreEqual(1.6f, flame.glow, 1e-6f);
            Assert.AreEqual(0.14f, flame.seconds, 1e-6f);
            Assert.AreEqual(0.08f, flame.fade, 1e-6f);
        }

        [Test]
        public void NoSmokeIsDrawnAndEverythingIsCleanThirtySevenHundredthsAfterTheCue()
        {
            var vfx = Censer();

            // The sheet's f6-f9 are smoke and the particle sheet's f2 is a wisp of it.
            Assert.IsFalse(vfx.layers.Any(l => l.render == "sprite" && l.startFrame >= 6),
                "no sprite layer plays the smoke frames");
            CollectionAssert.AreEqual(new[] { 1f, 1f, 0f, 1f }, vfx.layers.Single(l => l.id == "embers").emitter.weights);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 1f }, vfx.layers.Single(l => l.id == "lick").emitter.weights);

            var embers = vfx.layers.Single(l => l.id == "embers").emitter;
            Assert.AreEqual(0.36f, embers.lifeMax, 1e-6f);

            var lick = vfx.layers.Single(l => l.id == "lick");
            Assert.AreEqual(0.05f, lick.offset, 1e-6f, "the flames take hold just after the catch");
            Assert.AreEqual(0.32f, lick.emitter.lifeMax, 1e-6f);
            Assert.AreEqual(0.37f, lick.offset + lick.emitter.lifeMax, 1e-6f, "the last thing on screen");
        }
    }
}
