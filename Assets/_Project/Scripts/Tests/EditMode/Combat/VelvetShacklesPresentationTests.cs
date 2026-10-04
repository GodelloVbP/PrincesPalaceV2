using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // VELVET SHACKLES' BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.5):
    // ribbons wind round the body for 0.3 s, the cuff snaps tight on the cue with
    // the sound and the Rooted badge, and the cuff stays held after it.
    public class VelvetShacklesPresentationTests
    {
        private static SpellPresentation Shackles()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "velvet_shackles");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheRibbonsWindForTheCueAndAreGoneOnIt()
        {
            var vfx = Shackles();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f, "the cast riser is 0.3 s");

            var wind = vfx.layers.Single(l => l.id == "wind");
            Assert.AreEqual("release", wind.at);
            Assert.AreEqual(0.3f, wind.seconds, 1e-6f, "f0-f4 at 0.06 s each");
            Assert.AreEqual(0f, wind.fade, 1e-6f, "no fade: the ribbons are gone on the frame the cuff opens");
        }

        [Test]
        public void TheCuffSnapsTightOnTheCueWithAHotRing()
        {
            var vfx = Shackles();
            var cinch = vfx.layers.Single(l => l.id == "cinch");
            Assert.AreEqual("hit", cinch.at);
            Assert.AreEqual(6, cinch.startFrame, "f5, the cuff");
            Assert.AreEqual(0.5f, cinch.punch, 1e-6f, "opened half oversized, snapped in");

            var accent = vfx.layers.Single(l => l.id == "accent");
            Assert.AreEqual("hit", accent.at);
            Assert.AreEqual(0f, accent.offset, 1e-6f);
            Assert.AreEqual("Spells/shackle_cinch_ring", accent.path);
            Assert.AreEqual(1f, accent.punch, 1e-6f, "twice as wide for two frames, then on the body");
            Assert.AreEqual(1.5f, accent.glow, 1e-6f);
            Assert.AreEqual(0.08f, accent.seconds, 1e-6f);
        }

        [Test]
        public void TheCuffStaysHeldAfterTheSnapAndTheStageIsDimmedByTwoTenths()
        {
            var vfx = Shackles();
            var held = vfx.layers.Single(l => l.id == "held");
            Assert.AreEqual(0.12f, held.offset, 1e-6f, "takes over as the snap finishes");
            Assert.AreEqual(0.3f, held.seconds, 1e-6f);
            Assert.AreEqual(0.22f, held.fade, 1e-6f);

            Assert.AreEqual(0.2f, vfx.gloom.strength, 1e-6f, "owner O1");
            Assert.AreEqual(SpellGloomEnd.Hit, vfx.gloom.Until);
            Assert.AreEqual(0.5f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }
    }
}
