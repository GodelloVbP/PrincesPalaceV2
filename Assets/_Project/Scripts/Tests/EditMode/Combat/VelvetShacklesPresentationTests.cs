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
        public void TheCuffIsPulledTightOverTheTenthBeforeTheCueAndHeldAfterIt()
        {
            var vfx = Shackles();
            var cinch = vfx.layers.Single(l => l.id == "cinch");
            Assert.AreEqual("Spells/velvet_shackles_cinch", cinch.path, "the painted four-frame cinch");
            Assert.AreEqual("release", cinch.at);
            Assert.AreEqual(0.2f, cinch.offset, 1e-6f);
            Assert.AreEqual(20f, cinch.fps, 1e-6f);
            Assert.AreEqual(vfx.hitCueSeconds, cinch.offset + 2 / cinch.fps, 1e-6f, "f2, closed and creased, opens on the cue");
            Assert.AreEqual("hold", cinch.until);
            Assert.AreEqual(0.52f, cinch.seconds, 1e-6f);
            Assert.AreEqual(0.22f, cinch.fade, 1e-6f);
            Assert.IsNull(vfx.layers.FirstOrDefault(l => l.id == "accent" || l.id == "held"), "the ring and the held copy are the cuff's own frames now");
        }

        [Test]
        public void TheStageIsDimmedByTwoTenths()
        {
            var vfx = Shackles();
            Assert.AreEqual(0.2f, vfx.gloom.strength, 1e-6f, "owner O1");
            Assert.AreEqual(SpellGloomEnd.Hit, vfx.gloom.Until);
            Assert.AreEqual(0.5f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }
    }
}
