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
            Assert.AreEqual(0.17f, glob.travelDelay, 1e-6f, "held while the gather plays");
            Assert.AreEqual(0.155f, glob.travelSeconds, 1e-6f, "long enough for the arc to read");
            Assert.AreEqual(0.6f, glob.drift.arcY, 1e-6f, "lobbed, not shot");
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
            Assert.AreEqual("Spells/mud_splat", splat.path, "the painted splat, one still");
            Assert.AreEqual("hold", splat.until);
            Assert.AreEqual(0.3f, splat.seconds, 1e-6f);
            Assert.AreEqual(0.22f, splat.fade, 1e-6f);

            var drops = vfx.layers.Single(l => l.id == "drops");
            Assert.AreEqual("hit", drops.at);
            Assert.AreEqual(0.55f, drops.emitter.lifeMax, 1e-6f);
        }

        [Test]
        public void NothingGlowsAndOnlyTheGatherIsTinted()
        {
            var vfx = Mud();
            Assert.IsTrue(vfx.layers.All(l => l.glow == 0f), "matte: a hot core is what read as fire");
            Assert.AreEqual("#7A5A40", vfx.layers.Single(l => l.id == "gather").tint, "the old gold sheet reads as fire untinted");
            foreach (string id in new[] { "glob", "splat", "puddle" })
            {
                Assert.IsTrue(string.IsNullOrEmpty(vfx.layers.Single(l => l.id == id).tint), id + ": painted brown already");
            }

            Assert.AreEqual(7, vfx.layers.Length, "gather, glob, splat, drops, two drip emitters, puddle");
        }

        // THE SLIDE RUNS OVER THE FLIGHT: still in the hand while held, an
        // apex of arcY * 4 * u * (1 - u) with u the eased flight fraction (the
        // quarter of the way across at mid-time), and level again on arrival.
        [Test]
        public void TheGlobStaysLevelInTheHandAndArcsOnlyInFlight()
        {
            var performance = SpellPerformance.Resolve(Mud(), 1, path => 8);
            var glob = performance.Instances.Single(i => i.Layer.id == "glob");

            Assert.AreEqual(0f, SpellFrameCursor.DriftAt(glob, 0.05f).y, 1e-6f, "held in the hand");
            Assert.AreEqual(0f, SpellFrameCursor.DriftAt(glob, 0.17f).y, 1e-6f, "just leaving");
            Assert.AreEqual(0.45f, SpellFrameCursor.DriftAt(glob, 0.2475f).y, 1e-4f,
                "mid-time is a quarter of the way across: 0.6 * 4 * 0.25 * 0.75");
            Assert.AreEqual(0f, SpellFrameCursor.DriftAt(glob, 0.325f).y, 1e-6f, "down on the body");
            Assert.AreEqual(0f, SpellFrameCursor.DriftAt(glob, 0.6f).y, 1e-6f, "held at the end after arrival");
        }

        [Test]
        public void AnArcIsAParabolaOnTopOfTheLegs()
        {
            var drift = new SpellDrift { arcY = 0.4f, toY = 0.2f };
            Assert.IsTrue(drift.IsAuthored);
            Assert.AreEqual(0.4f, drift.At(0.5f, 1f).y - (0.2f * 0.75f), 1e-6f, "apex is arcY above the eased line");
            Assert.AreEqual(0.2f, drift.At(1f, 1f).y, 1e-6f);
            Assert.AreEqual(0.4f, drift.Copy().arcY, 1e-6f);
        }
    }
}
