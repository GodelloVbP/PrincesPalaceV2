using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // VELVET SHACKLES' BEATS as literal seconds (docs/PLAN_SPELL_ANIMATION.md 3):
    // a ribbon is lashed from Shawn's hand, two bands pull tight round the legs,
    // the cuff closes on the cue with the sound and the Rooted badge, then all
    // of it goes slack and fades.
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
        public void TheLashLeavesTheHandAndLandsBeforeTheCue()
        {
            var vfx = Shackles();
            Assert.AreEqual(0.4f, vfx.hitCueSeconds, 1e-6f, "the cast riser is cut to 0.40 s");

            var lash = vfx.layers.Single(l => l.id == "lash");
            Assert.AreEqual("caster", lash.place, "on a flight, caster aims at the ground line under the body: the legs, not the chest");
            Assert.AreEqual("Spells/velvet_shackles_lash", lash.path);
            Assert.AreEqual(0.02f, lash.offset, 1e-6f);
            Assert.AreEqual(0.2f, lash.travelSeconds, 1e-6f);
            Assert.AreEqual("path", lash.orient);
            Assert.AreEqual(475f, lash.size, 1e-6f, "the drawn lash is 170 px long in a 475 px cell");
            Assert.IsTrue(string.IsNullOrEmpty(lash.fit) || lash.fit == "none", "a constant size");
            Assert.AreEqual(0.04f, lash.fadeIn, 1e-6f, "its first frames are not the loudest rise");
        }

        [Test]
        public void TheWrapIsPulledTightOnTheCueFrameAndSlackAfterIt()
        {
            var vfx = Shackles();
            foreach (var id in new[] { "back", "front" })
            {
                var wrap = vfx.layers.Single(l => l.id == id);
                Assert.AreEqual("Spells/velvet_shackles_wrap_" + id, wrap.path);
                Assert.AreEqual(10f, wrap.fps, 1e-6f);
                Assert.AreEqual(vfx.hitCueSeconds, wrap.offset + 2 / wrap.fps, 1e-6f, "the third cell, closed tight with the ends crossed, is on the cue");
            }
            Assert.AreEqual("ground", vfx.layers.Single(l => l.id == "back").sort, "single target: the back wrap is the one ground layer");
            Assert.AreEqual("effects", vfx.layers.Single(l => l.id == "front").sort);
        }

        [Test]
        public void TheCuffSnapsShutAndLandsSqueezedOnTheCue()
        {
            var vfx = Shackles();
            var close = vfx.layers.Single(l => l.id == "cuff_close");
            Assert.AreEqual("Spells/velvet_shackles_cuff", close.path);
            Assert.AreEqual(30f, close.fps, 1e-6f);
            Assert.AreEqual(vfx.hitCueSeconds, close.offset + 3 / close.fps, 1e-6f, "cells 1-3 snap shut and end as the landing cell opens");
            Assert.AreEqual(0f, close.glow, 1e-6f, "no extra glow");

            var cuff = vfx.layers.Single(l => l.id == "cuff");
            Assert.AreEqual("Spells/velvet_shackles_cuff", cuff.path);
            Assert.AreEqual(vfx.hitCueSeconds, cuff.offset, 1e-6f, "the squeezed cell lands on the cue");
            Assert.AreEqual(4, cuff.startFrame);
            Assert.AreEqual(0.2f, cuff.punch, 1e-6f, "a small overshoot so the cue is the biggest rise");
            Assert.AreEqual(0f, cuff.glow, 1e-6f, "no extra glow");
            Assert.AreEqual("hold", cuff.until);
            Assert.LessOrEqual(cuff.offset + cuff.seconds + cuff.fade - vfx.hitCueSeconds, 0.5f, "residue after the cue");
            Assert.IsNull(vfx.layers.FirstOrDefault(l => l.id == "wind"), "the old ribbon spiral is gone");
        }

        [Test]
        public void TheStageIsDimmedByTwoTenths()
        {
            var vfx = Shackles();
            Assert.AreEqual(0.2f, vfx.gloom.strength, 1e-6f, "owner O1");
            Assert.AreEqual(SpellGloomEnd.Hit, vfx.gloom.Until);
            Assert.AreEqual(0.45f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }
    }
}
