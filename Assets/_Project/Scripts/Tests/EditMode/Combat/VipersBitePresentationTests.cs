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
        public void TheJawsDropInForTheCueAndTheBiteLandsOnIt()
        {
            var vfx = Viper();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var jaws = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.3f, jaws.seconds, 1e-6f, "f0-f4 at 0.06 s");
            Assert.AreEqual(0f, jaws.fade, 1e-6f);
            Assert.AreEqual(0.5f, jaws.drift.fromY, 1e-6f, "from half a box above");
            Assert.AreEqual(0f, jaws.drift.toY, 1e-6f);

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

            Assert.AreEqual(1.6f, vfx.layers.Single(l => l.id == "pop").glow, 1e-6f);
            Assert.IsTrue(vfx.layers.Where(l => l.id == "ritual" || l.id == "bite" || l.id == "settle")
                .All(l => string.IsNullOrEmpty(l.whenConsumed)), "the bite itself always plays");
        }
    }
}
