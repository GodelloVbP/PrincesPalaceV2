using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // VIPER'S BITE'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.6):
    // jaws drop in for 0.3 s, the bite lands on the cue, and a bite that spent
    // Poison earns a second payoff 0.18 s later with its own number and sound.
    public class VipersBitePresentationTests
    {
        private static SpellPresentation Viper()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "vipers_bite");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheSnakeLungesInForTheCueAndTheBiteLandsOnIt()
        {
            var vfx = Viper();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var lunge = vfx.layers.Single(l => l.id == "lunge");
            Assert.AreEqual("Spells/vipers_bite_lunge", lunge.path, "the painted head; its cut neck is feathered in the recipe");
            Assert.AreEqual("release", lunge.at);
            Assert.AreEqual(0.12f, lunge.offset, 1e-6f);
            Assert.AreEqual(0.4f, lunge.seconds, 1e-6f);
            Assert.AreEqual(-1.5f, lunge.drift.fromX, 1e-6f, "starts a box and a half out, between the caster and the body");
            Assert.AreEqual(0f, lunge.drift.viaX, 1e-6f, "on the body at the waypoint");
            Assert.AreEqual(-0.6f, lunge.drift.toX, 1e-6f, "and drawn back out");
            Assert.AreEqual(vfx.hitCueSeconds, lunge.offset + lunge.seconds * lunge.drift.viaAt, 1e-3f, "the head arrives on the cue");
            Assert.AreEqual(0f, lunge.glow, 1e-6f);

            var bite = vfx.layers.Single(l => l.id == "bite");
            Assert.AreEqual("hit", bite.at);
            Assert.AreEqual(0f, bite.offset, 1e-6f);
            Assert.AreEqual(6, bite.startFrame, "f5, the snap");
            Assert.AreEqual(1.25f, bite.glow, 1e-6f);
            Assert.AreEqual(0.2f, vfx.gloom.strength, 1e-6f, "owner O1");
        }

        [Test]
        public void TheDetonationIsAGatedSecondPayoffWithItsOwnSoundAndNumber()
        {
            var vfx = Viper();
            Assert.AreEqual(0.18f, vfx.detonationSeconds, 1e-6f);
            Assert.AreEqual("Audio/Sfx/spore_cloud_impact", vfx.secondSfxPath);
            Assert.AreNotEqual(vfx.sfxPath, vfx.secondSfxPath);

            foreach (string id in new[] { "pop", "detonate" })
            {
                var layer = vfx.layers.Single(l => l.id == id);
                Assert.AreEqual("hit", layer.at, id);
                Assert.AreEqual(vfx.detonationSeconds, layer.offset, 1e-6f, id + " opens with the second number");
                Assert.AreEqual("Poison", layer.whenConsumed, id);
            }

            var pop = vfx.layers.Single(l => l.id == "pop");
            Assert.AreEqual("Spells/vipers_bite_burst", pop.path, "the painted venom burst");
            Assert.AreEqual(0.9f, pop.glow, 1e-6f);
            Assert.IsTrue(vfx.layers.Where(l => l.id == "lunge" || l.id == "bite" || l.id == "settle")
                .All(l => string.IsNullOrEmpty(l.whenConsumed)), "the bite itself always plays");
        }
    }
}
