using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;
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
            Assert.AreEqual(0.155f, glob.travelSeconds, 1e-6f, "nine frames in the air at 60");
            Assert.AreEqual(0.6f, glob.drift.arcY, 1e-6f, "lobbed: one parabola");
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

            Assert.AreEqual(9, vfx.layers.Length, "gather, glob_trail, glob, splat, drops, three drip strands, puddle");
        }

        // ONE PARABOLA IN TIME. The glob leaves at 0.17 s and lands at 0.325 s;
        // the sideways speed is constant (a flight that authors arcY is a
        // throw), and the height is the line down to the body plus 0.6 box
        // heights of arc: y = 308u - 408u^2 for a 310 x -100 line and a 170 px
        // box, so it peaks (58) at u = 0.377, before the middle, and falls
        // faster than it rose.
        [Test]
        public void TheGlobFliesOneParabolaAtConstantSidewaysSpeed()
        {
            var performance = SpellPerformance.Resolve(Mud(), 1, path => 8);
            var glob = performance.Instances.Single(i => i.Layer.id == "glob");
            glob.From = new UiVec(0f, 0f);
            glob.To = new UiVec(310f, -100f);
            glob.Box = new UiVec(170f, 170f);

            Assert.IsTrue(SpellPerformance.Ballistic(glob));
            (float t, float x, float y)[] along =
            {
                (0.05f, 0f, 0f),          // held in the hand
                (0.17f, 0f, 0f),          // leaves
                (0.20875f, 77.5f, 51.5f), // u 0.25
                (0.228125f, 116.25f, 58.125f), // u 0.375: the apex
                (0.2475f, 155f, 52f),     // u 0.5
                (0.28625f, 232.5f, 1.5f), // u 0.75
                (0.325f, 310f, -100f),    // on the body's middle
                (0.6f, 310f, -100f),      // held there
            };
            foreach (var step in along)
            {
                var at = SpellPerformance.PositionOf(glob, step.t);
                Assert.AreEqual(step.x, at.X, 0.05f, "x at " + step.t);
                Assert.AreEqual(step.y, at.Y, 0.05f, "y at " + step.t);
            }

            var early = SpellPerformance.VelocityOf(glob, 0.2f);
            var late = SpellPerformance.VelocityOf(glob, 0.3f);
            Assert.AreEqual(early.X, late.X, 1e-3f, "constant speed along the line");
            Assert.AreEqual(310f / 0.155f, early.X, 0.1f);

            Assert.AreEqual(540f, Mud().layers.Single(l => l.id == "glob").rotate.spin, 1e-6f, "it tumbles");
            var trail = Mud().layers.Single(l => l.id == "glob_trail");
            Assert.AreEqual("layer:glob", trail.place, "the drips ride the glob");
            Assert.AreEqual(0.012f, Mud().kick.strength, 1e-6f, "the arrival kicks the stage");
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
