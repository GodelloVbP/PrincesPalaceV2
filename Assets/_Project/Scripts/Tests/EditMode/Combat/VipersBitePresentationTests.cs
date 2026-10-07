using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // VIPER'S BITE'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.6):
    // eyes, then jaws forming over 0.4 s, the bite lands on the cue, and a bite that spent
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
        public void TheJawsFormOnTheBodyAndSnapOnTheCueWithNoSnakeHead()
        {
            var vfx = Viper();
            Assert.AreEqual(0.4f, vfx.hitCueSeconds, 1e-6f);
            Assert.IsFalse(vfx.layers.Any(l => l.path != null && l.path.Contains("lunge")), "owner: the snake head looks bad");

            foreach (string id in new[] { "eye_a", "eye_b" })
            {
                var eye = vfx.layers.Single(l => l.id == id);
                Assert.AreEqual("release", eye.at, id);
                Assert.Less(eye.offset, vfx.hitCueSeconds - 0.2f, id + " opens in the dark well before the jaws");
                Assert.LessOrEqual(eye.glow, 1f, id + " stays under the snap's glow");
            }

            var jaws = vfx.layers.Single(l => l.id == "jaws");
            Assert.AreEqual(vfx.hitCueSeconds, jaws.offset + jaws.seconds, 0.02f, "the jaws finish drawing together on the cue");
            Assert.Less(jaws.glow, 1.25f, "the wind-up is dimmer than the snap");

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
            Assert.IsTrue(vfx.layers.Where(l => l.id == "jaws" || l.id == "bite" || l.id == "settle")
                .All(l => string.IsNullOrEmpty(l.whenConsumed)), "the bite itself always plays");
        }
    }
}
