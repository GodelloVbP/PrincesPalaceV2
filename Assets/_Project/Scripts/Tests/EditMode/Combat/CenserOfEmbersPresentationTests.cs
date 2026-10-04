using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // CENSER OF EMBERS' BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.10):
    // the censer swings for 0.3 s, the embers catch on the cue with the Burn badge,
    // and the sheet's smoke is gone, so the crescent is gone 0.45 s after its payoff.
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
            Assert.AreEqual("still", flame.render);
            Assert.AreEqual("Spells/censer_of_embers_catch", flame.path, "the crescent of embers, centre open");
            Assert.AreEqual("target", flame.place, "at the target's feet, not a column in front of the face");
            Assert.AreEqual(1.0f, flame.glow, 1e-6f);
            Assert.AreEqual(0.25f, flame.seconds, 1e-6f);
            Assert.AreEqual(0.2f, flame.fade, 1e-6f);
        }

        [Test]
        public void NoSmokeIsDrawnAndTheLastFlameIsGoneHalfASecondAfterTheCue()
        {
            var vfx = Censer();

            // The sheet's f6-f9 are smoke and the particle sheet's f2 is a wisp of it.
            Assert.IsFalse(vfx.layers.Any(l => l.render == "sprite" && l.startFrame >= 6),
                "no sprite layer plays the smoke frames");
            var flame = vfx.layers.Single(l => l.id == "catch");
            Assert.AreEqual(0.45f, flame.seconds + flame.fade, 1e-6f, "payoff to clean, the crescent is the last thing on screen");
            CollectionAssert.AreEqual(new[] { 1f, 1f, 0f, 1f }, vfx.layers.Single(l => l.id == "embers").emitter.weights);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 1f }, vfx.layers.Single(l => l.id == "lick").emitter.weights);

            var embers = vfx.layers.Single(l => l.id == "embers").emitter;
            Assert.AreEqual(0.36f, embers.lifeMax, 1e-6f);

            var lick = vfx.layers.Single(l => l.id == "lick");
            Assert.AreEqual(0.05f, lick.offset, 1e-6f, "the flames take hold just after the catch");
            Assert.AreEqual(0.32f, lick.emitter.lifeMax, 1e-6f);
            Assert.AreEqual(0.37f, lick.offset + lick.emitter.lifeMax, 1e-6f, "clears before the crescent does");
        }
    }
}
