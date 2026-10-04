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
            Assert.AreEqual("still", lock_.render);
            Assert.AreEqual("Spells/gilded_aegis_lock", lock_.path, "the painted closed shield, centre open");;
            Assert.AreEqual(1.7f, lock_.glow, 1e-6f);
            Assert.AreEqual(0.22f, lock_.seconds, 1e-6f);
            Assert.AreEqual(0.18f, lock_.fade, 1e-6f);
            Assert.AreEqual(0.4f, lock_.seconds + lock_.fade, 1e-6f, "payoff to clean");

            Assert.AreEqual(0.4f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }

        // spell_feel's contact check: the biggest rise after release must be the lock's frame
        // (docs/SPELL_FEEL.md). The shards and the mote are dim so the close is a build, and
        // the bloom ring flares for 0.08 s on the cue around the lock, clear of the ally's face.
        [Test]
        public void TheCloseIsDimAndTheLocksBloomIsTheCastsPeak()
        {
            var vfx = Aegis();
            var shards = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.5f, shards.opacity, 1e-6f);
            Assert.AreEqual(0f, shards.glow, 1e-6f);
            Assert.AreEqual(0.8f, vfx.layers.Single(l => l.id == "mote").glow, 1e-6f);

            var flash = vfx.layers.Single(l => l.id == "flash");
            Assert.AreEqual("hit", flash.at);
            Assert.AreEqual(0f, flash.offset, 1e-6f, "on the lock's frame");
            Assert.AreEqual("Spells/aegis_bloom", flash.path);
            Assert.AreEqual(3.0f, flash.glow, 1e-6f);
            Assert.AreEqual(0.08f, flash.seconds, 1e-6f);
            Assert.AreEqual(0.12f, flash.fade, 1e-6f);
            Assert.Less(flash.seconds + flash.fade, vfx.layers.Single(l => l.id == "lock").seconds,
                "the flash is gone before the lock's hold ends");
        }

        [Test]
        public void TheTrailRidesTheMoteAndNoLayerIsNeedlesslyLong()
        {
            var vfx = Aegis();
            var trail = vfx.layers.Single(l => l.id == "trail");
            Assert.AreEqual("layer:mote", trail.place);
            Assert.AreEqual(0.2f, trail.emitter.window, 1e-6f, "the length of the flight");
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 1f }, trail.emitter.weights, "leaves and glints, not the gem or the crescent");
            Assert.AreEqual(6, vfx.layers.Length);
        }
    }
}
