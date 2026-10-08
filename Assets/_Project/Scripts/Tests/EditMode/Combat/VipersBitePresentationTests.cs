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
            var left = vfx.layers.Single(l => l.id == "eye_a");
            var right = vfx.layers.Single(l => l.id == "eye_b");
            foreach (var eye in new[] { left, right })
            {
                Assert.AreEqual(jaws.place, eye.place, eye.id + " sits where the jaws sit");
                Assert.AreEqual(jaws.height, eye.height, 1e-6f, eye.id + " shares the jaws' anchor height");
                Assert.AreEqual(jaws.impactY, eye.impactY, 1e-6f, eye.id + " is centred in the gap between the jaws");
                Assert.AreEqual("#66741a", eye.tint, eye.id + " is a dark sickly green, not lemon");
                Assert.AreEqual(0.15f, eye.glow, 1e-6f, eye.id + " smoulders");
            }

            Assert.AreEqual(-28f, left.dx, 1e-6f);
            Assert.AreEqual(28f, right.dx, 1e-6f);
            Assert.AreEqual(vfx.hitCueSeconds, jaws.offset + jaws.seconds, 0.02f, "the jaws finish drawing together on the cue");
            Assert.Less(jaws.glow, 1.25f, "the wind-up is dimmer than the snap");

            var bite = vfx.layers.Single(l => l.id == "bite");
            Assert.AreEqual("hit", bite.at);
            Assert.AreEqual(0f, bite.offset, 1e-6f);
            Assert.AreEqual("Spells/vipers_bite_ghost_jaws", bite.path, "the frontal ghost jaws, owner 2026-10-08");
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
            Assert.AreEqual("Spells/vipers_bite_burst", pop.path, "the flat venom burst");
            Assert.AreEqual(0.9f, pop.glow, 1e-6f);
            Assert.IsTrue(vfx.layers.Where(l => l.id == "jaws" || l.id == "bite" || l.id == "settle")
                .All(l => string.IsNullOrEmpty(l.whenConsumed)), "the bite itself always plays");
        }
    }
}
