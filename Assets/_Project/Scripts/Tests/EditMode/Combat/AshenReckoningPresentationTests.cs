using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // ASHEN RECKONING'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.7):
    // the venom converges for 0.3 s, the Poison packet lands on the cue, and the
    // Fire packet, one stagger step (3 frames, 0.05 s) behind it, is the ignition.
    // Each packet's number has its own accent and its own sound.
    public class AshenReckoningPresentationTests
    {
        private static SpellPresentation Ashen()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "ashen_reckoning");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheVenomConvergesForTheCueAndEachPacketHasItsOwnAccent()
        {
            var vfx = Ashen();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var wind = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.3f, wind.seconds, 1e-6f, "f0-f4 at 0.06 s");
            Assert.AreEqual(0f, wind.fade, 1e-6f);

            var venom = vfx.layers.Single(l => l.id == "venom");
            Assert.AreEqual("hit", venom.at);
            Assert.AreEqual(0f, venom.offset, 1e-6f, "the Poison packet's number is on the cue");
            Assert.AreEqual(1.6f, venom.glow, 1e-6f);

            var ignite = vfx.layers.Single(l => l.id == "ignite");
            Assert.AreEqual("hit", ignite.at);
            Assert.AreEqual(0.05f, ignite.offset, 1e-6f, "3 frames at 60 fps, StageHitFlash.StaggerSeconds");
            Assert.AreEqual(6, ignite.startFrame, "f5, the coal bursting into flame");
            Assert.AreEqual(1.9f, ignite.glow, 1e-6f);
        }

        [Test]
        public void TheSecondPacketHasItsOwnSoundAndItsEmbersLandWithIt()
        {
            var vfx = Ashen();
            Assert.AreEqual("Audio/Sfx/censer_of_embers_impact", vfx.secondSfxPath);
            Assert.AreNotEqual(vfx.sfxPath, vfx.secondSfxPath);

            Assert.AreEqual(0f, vfx.layers.Single(l => l.id == "drops").offset, 1e-6f);
            Assert.AreEqual(0.05f, vfx.layers.Single(l => l.id == "settle").offset, 1e-6f);
            Assert.AreEqual(0.62f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }
    }
}
