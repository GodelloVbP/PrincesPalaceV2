using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // GILDED AEGIS'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.8): a
    // mote carries the ward from Shawn to the ally, the four shards close
    // around them and are the last drawing before the cue, and the lock frame
    // lands on the cue with the ward bar. Payoff to clean is 0.4 s.
    public class GildedAegisPresentationTests
    {
        private static SpellPresentation Aegis()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "gilded_aegis");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheMoteLeavesShawnAndTheShardsCloseOnItsArrival()
        {
            var vfx = Aegis();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var mote = vfx.layers.Single(l => l.id == "mote");
            Assert.AreEqual("caster-centre", mote.place);
            Assert.AreEqual(0.2f, mote.travelSeconds, 1e-6f, "a delivery that reads, in the first two thirds of the wind-up");
            Assert.AreEqual("Spells/aegis_mote", mote.path);

            var shards = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.1f, shards.offset, 1e-6f, "the shards open as the mote nears");
            Assert.AreEqual(25f, shards.fps, 1e-6f);
            Assert.AreEqual(0.2f, shards.seconds, 1e-6f, "f0-f4: the last shard drawing ends on the cue");
            Assert.AreEqual(vfx.hitCueSeconds, shards.offset + shards.seconds, 1e-6f);
            Assert.AreEqual(0f, shards.fade, 1e-6f, "the lock frame replaces it, not a dissolve");
            Assert.AreEqual("target-centre", shards.place, "on the ally, not the caster");
        }

        [Test]
        public void TheLockLandsOnTheCueAndEverythingIsCleanFortyHundredthsLater()
        {
            var vfx = Aegis();
            var lock_ = vfx.layers.Single(l => l.id == "lock");
            Assert.AreEqual("hit", lock_.at);
            Assert.AreEqual(0f, lock_.offset, 1e-6f);
            Assert.AreEqual(6, lock_.startFrame, "f5, the shield locking in its halo");
            Assert.AreEqual(1.6f, lock_.glow, 1e-6f);
            Assert.AreEqual(0.32f, lock_.seconds, 1e-6f, "7 frames at 22 fps");
            Assert.AreEqual(0.08f, lock_.fade, 1e-6f);
            Assert.AreEqual(0.4f, lock_.seconds + lock_.fade, 1e-6f, "payoff to clean");

            Assert.AreEqual(0.4f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }

        [Test]
        public void TheTrailRidesTheMoteAndNoLayerIsNeedlesslyLong()
        {
            var vfx = Aegis();
            var trail = vfx.layers.Single(l => l.id == "trail");
            Assert.AreEqual("layer:mote", trail.place);
            Assert.AreEqual(0.2f, trail.emitter.window, 1e-6f, "the length of the flight");
            Assert.AreEqual(5, vfx.layers.Length);
        }
    }
}
