using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THORN TITHE'S BEATS as literal seconds (docs/captures/spell_rework/after/thorn_tithe/NOTES.md):
    // a back and a front cane set rise from one base over 0.3 s, the cage closes on the 0.4 s cue
    // (f6 of both sets), and it has sunk back into the soil before 0.9 s.
    public class ThornTithePresentationTests
    {
        private static SpellPresentation Thorn()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "thorn_tithe");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheVinesRiseForTheWindUpAndTheCageClosesOnTheCue()
        {
            var vfx = Thorn();
            Assert.AreEqual(0.4f, vfx.hitCueSeconds, 1e-6f, "sound, Thorned badge and the closed cage on one frame; the cast riser is 0.4 s");
            Assert.IsNull(vfx.layers.FirstOrDefault(l => l.at == "hit" && l.render == "sprite"),
                "an at: hit sprite is a tail, shrunk to a fraction of the body; the cage is f6 of the vine layers");

            var back = vfx.layers.Single(l => l.id == "back");
            var front = vfx.layers.Single(l => l.id == "front");
            Assert.AreEqual("Spells/thorn_tithe_back", back.path);
            Assert.AreEqual("Spells/thorn_tithe_front", front.path);
            Assert.AreEqual("ground", back.sort, "behind the body, so the cage wraps instead of lying over it; one target, one ground renderer");
            Assert.AreEqual("effects", front.sort);
            Assert.AreEqual("none", front.facing, "both sets hold a bunch on each foot, so neither is mirrored");
            foreach (var l in new[] { back, front })
            {
                Assert.AreEqual("release", l.at);
                Assert.AreEqual(0.1f, l.offset, 1e-6f);
                Assert.AreEqual(20f, l.fps, 1e-6f, "f6 opens at 0.1 + 6 / 20 = 0.4 s, the cue");
                Assert.AreEqual("hold", l.until);
                Assert.AreEqual(341f, l.size, 1e-6f, "the dome is 442 of the 520 px canvas; 341 puts it 290 wide, a rat's body");
                Assert.AreEqual(back.size, l.size, 1e-6f, "one cage: both sets on one canvas and one size");
                Assert.AreEqual(back.impactY, l.impactY, 1e-6f, "one pivot at the base");
                Assert.AreEqual(0.7f, l.seconds, 1e-6f, "6 growth frames, the cage held 4, 4 frames of sinking back: 14 at 20 fps");
            }
        }

        [Test]
        public void EverythingIsGoneHalfASecondAfterTheCue()
        {
            var vfx = Thorn();
            foreach (var l in vfx.layers.Where(l => l.render == "sprite"))
                Assert.LessOrEqual(l.offset + l.seconds + l.fade, vfx.hitCueSeconds + 0.5f + 1e-6f, l.id);
            var crack = vfx.layers.Single(l => l.id == "crack");
            Assert.AreEqual("Spells/thorn_tithe_crack", crack.path);
            Assert.AreEqual(0f, crack.offset, 1e-6f, "the crack opens the anticipation, before the canes");
            var leaves = vfx.layers.Single(l => l.id == "leaves");
            Assert.AreEqual("hit", leaves.at);
            Assert.LessOrEqual(leaves.emitter.lifeMax, 0.5f, "leaves clear inside the 0.5 s residue limit");
        }
    }
}
