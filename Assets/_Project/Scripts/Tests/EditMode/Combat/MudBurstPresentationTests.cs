using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // MUD BURST'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.3): a
    // gather in the hand, a thrown glob that lands on the cue, and a matte
    // splat. Matte is the identity ("mud", not "fire"), so no layer glows.
    public class MudBurstPresentationTests
    {
        private static SpellPresentation Mud()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "mud_burst");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheGlobIsHeldThenThrownAndLandsOnTheCue()
        {
            var vfx = Mud();
            Assert.AreEqual(0.325f, vfx.hitCueSeconds, 1e-6f);

            var gather = vfx.layers.Single(l => l.id == "gather");
            Assert.AreEqual(0.2f, gather.seconds, 1e-6f);
            Assert.AreEqual(40f, gather.fps, 1e-6f, "the sheet's first eight frames fill the 0.2 s hold");
            Assert.AreEqual("caster-centre", gather.place);

            var glob = vfx.layers.Single(l => l.id == "glob");
            Assert.AreEqual(0.2f, glob.travelDelay, 1e-6f, "held while the gather plays");
            Assert.AreEqual(0.125f, glob.travelSeconds, 1e-6f);
            Assert.AreEqual(vfx.hitCueSeconds, glob.travelDelay + glob.travelSeconds, 1e-6f, "arrives on the cue");
            Assert.AreEqual("Spells/mud_glob", glob.path);
        }

        [Test]
        public void TheSplatLandsOnTheCueAndTheClotsFallBackBeforeItClears()
        {
            var vfx = Mud();
            var splat = vfx.layers.Single(l => l.id == "splat");
            Assert.AreEqual("hit", splat.at);
            Assert.AreEqual(0f, splat.offset, 1e-6f);
            Assert.AreEqual("Spells/mud_burst_splat", splat.path);
            Assert.AreEqual(28f, splat.fps, 1e-6f);
            Assert.AreEqual(0.5f, splat.seconds, 1e-6f, "14 frames at 28 fps");

            var drops = vfx.layers.Single(l => l.id == "drops");
            Assert.AreEqual("hit", drops.at);
            Assert.AreEqual(0.55f, drops.emitter.lifeMax, 1e-6f);
        }

        [Test]
        public void NothingGlowsAndTheSheetsAreTintedBrown()
        {
            var vfx = Mud();
            Assert.IsTrue(vfx.layers.All(l => l.glow == 0f), "matte: a hot core is what read as fire");
            foreach (string id in new[] { "gather", "splat" })
            {
                Assert.AreEqual("#7A5A40", vfx.layers.Single(l => l.id == id).tint, id);
            }

            Assert.AreEqual(4, vfx.layers.Length);
        }
    }
}
