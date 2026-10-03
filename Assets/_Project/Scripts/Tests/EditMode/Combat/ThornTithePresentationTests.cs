using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THORN TITHE'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.2):
    // the vines sprout over 0.3 s, the Thorned status lands on the cue with a
    // green accent, and the cage has sunk back into the floor 0.72 s in.
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
        public void TheVinesSproutFromTheFloorAndCloseOnTheCue()
        {
            var vfx = Thorn();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f, "sound, number and Thorned badge on one frame; the cast riser is 0.3 s");

            var front = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual(0.72f, front.seconds, 1e-6f, "12 frames at 0.06 s: f5, the sap burst, opens at 0.30 s");
            Assert.AreEqual(-0.1f, front.drift.fromY, 1e-6f, "rises out of the soil a tenth of its height");
            Assert.AreEqual(0f, front.drift.toY, 1e-6f);
            Assert.AreEqual("effects", front.sort);

            var back = vfx.layers.Single(l => l.id == "back");
            Assert.AreEqual("ground", back.sort, "behind the body, so the cage wraps instead of lying over it");
            Assert.AreEqual("reverse", back.facing);
            Assert.AreEqual(0.5f, back.opacity, 1e-6f);
            Assert.AreEqual(front.seconds, back.seconds, 1e-6f, "the two arcs close together");
        }

        [Test]
        public void TheSapBurstIsTheOneHotFrameOnTheCue()
        {
            var accent = Thorn().layers.Single(l => l.id == "accent");
            Assert.AreEqual("hit", accent.at);
            Assert.AreEqual(0f, accent.offset, 1e-6f);
            Assert.AreEqual(6, accent.startFrame, "f5, the green-white burst");
            Assert.AreEqual(1.9f, accent.glow, 1e-6f);
            Assert.AreEqual(0.1f, accent.seconds, 1e-6f);
            Assert.AreEqual(0.12f, accent.fade, 1e-6f);
        }

        [Test]
        public void TheLeavesSettleOnTheCueAndAreGoneBeforeTheCageIs()
        {
            var settle = Thorn().layers.Single(l => l.id == "settle");
            Assert.AreEqual("hit", settle.at);
            Assert.AreEqual(0.42f, settle.emitter.lifeMax, 1e-6f, "0.3 + 0.42 = 0.72, as the cage clears");
        }
    }
}
