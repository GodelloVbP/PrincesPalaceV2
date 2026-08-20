using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The talent screen's tree.
    //
    // This class exists because it DIDN'T. Every other screen got an EditMode
    // audit harness and the constellation never did, so two things nobody could
    // see from a passing suite shipped together: orbs drawn with their own glow
    // sprite (a soft white smudge inside a slightly larger soft white smudge,
    // with no rim for the three state colours to sit on), and a detail plate
    // that opened as an empty 880x150 coloured slab because blanking its labels
    // left the slab behind.
    //
    // Both were visible in the first screenshot and invisible to 1190 passing
    // tests. That gap is the thing being closed here.
    public class ConstellationScreenTests
    {
        private static UiNode Tree() => TalentScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void AnOrbWearsItsMedallionRatherThanItsOwnGlow()
        {
            // The orb read "proc:radial_glow" -- the SAME asset as the glow
            // child sitting behind it. A radial glow has no body and no edge,
            // so an orb had nothing to colour: three carefully graded states
            // rendering as three barely different smudges. It became a flat
            // baked disc, and is now the painted medallion the talent kit was
            // generated for.
            var screen = TalentScreen.Build();
            var byName = Walk(screen.Root).ToDictionary(n => n.Name, n => n);

            var orb = byName["Orb0_0"];
            var glow = byName["Orb0_0Glow"];

            Assert.AreEqual(TalentScreen.OrbUnlitKey, orb.SpriteKey);
            Assert.AreNotEqual(orb.SpriteKey, glow.SpriteKey,
                "an orb and its own halo cannot be the same picture");
        }

        [Test]
        public void EveryOrbWearsTheMedallion()
        {
            // Asserted against the screen's OWN list rather than by matching
            // node names. The first version swept everything called "Orb*" that
            // did not end in "Glow", which quietly grew to include the lit
            // medallion and its mask the moment the reveal was added -- a
            // filter that has to be re-tuned every time the tree gains a part
            // is a filter that will eventually be tuned wrong.
            //
            // UNLIT is the built state: the controller lights what the player
            // owns, so a fresh save draws the tree dark.
            var screen = TalentScreen.Build();

            var offenders = screen.Orbs
                .Where(o => o.Node.SpriteKey != TalentScreen.OrbUnlitKey)
                .Select(o => o.Node.Name)
                .ToList();

            CollectionAssert.IsEmpty(offenders);
            Assert.AreEqual(TalentPage.PathCount * TalentScreen.OrbCount, screen.Orbs.Count,
                "fixture: one orb per slot per path");
        }

        [Test]
        public void TheDetailPlateStartsHidden()
        {
            // Nothing is selected when the screen opens, and a plate with
            // nothing written on it is just a coloured rectangle. The
            // controller shows it the moment an orb is picked.
            var screen = TalentScreen.Build();

            Assert.IsTrue(screen.DetailPlate.Node.StartInactive);
        }

        [Test]
        public void TheDetailTextRidesOnThePlateSoHidingOneHidesBoth()
        {
            // If the labels were siblings of the plate rather than its
            // children, hiding the plate would leave the text floating over the
            // sky -- the same class of bug in the other direction.
            var plate = Walk(TalentScreen.Build().Root).First(n => n.Name == "TalentDetailPlate");
            var names = plate.Children.Select(c => c.Name).ToList();

            CollectionAssert.Contains(names, "TalentDetailName");
            CollectionAssert.Contains(names, "TalentDetailBody");
        }

        [Test]
        public void EveryExemptionStatesARealReason()
        {
            foreach (var node in Walk(TalentScreen.Build().Root))
            {
                if (node.AllowOverlapReason != null)
                {
                    Assert.Greater(node.AllowOverlapReason.Length, 20, $"{node.Name}'s overlap reason is too thin");
                }

                if (node.AllowOverflowReason != null)
                {
                    Assert.Greater(node.AllowOverflowReason.Length, 20, $"{node.Name}'s overflow reason is too thin");
                }
            }
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
        // ---- the edge kit ---------------------------------------------------------
        //
        // The edges were pure decoration: three paths' worth of limbs that
        // never changed whatever the player spent, so a tree with twenty orbs
        // invested looked exactly like an empty one apart from the orbs. v1
        // lights the path behind you, and the climb is what this screen is
        // about.
        [Test]
        public void EveryEdgeHasALitLayerThatStartsDark()
        {
            var screen = TalentScreen.Build();

            Assert.AreEqual(screen.Edges.Count, screen.EdgeGlows.Count,
                "an edge has no lit layer, or a lit layer has no edge");

            foreach (var glow in screen.EdgeGlows)
            {
                Assert.IsTrue(glow.Node.StartInactive,
                    $"'{glow.Node.Name}' is built lit, so an unspent tree draws as though it were " +
                    "already climbed");
            }
        }

        // The controller lights an edge from its CHILD slot alone -- the parent
        // is necessarily invested already, because that is what a prerequisite
        // means. This pins that the screen hands over the slot it claims to.
        [Test]
        public void EachEdgeReportsTheSlotItArrivesAt()
        {
            var screen = TalentScreen.Build();

            Assert.AreEqual(screen.Edges.Count, screen.EdgeChildSlots.Count,
                "the edge list and its slot table are different lengths");

            foreach (int slot in screen.EdgeChildSlots)
            {
                Assert.GreaterOrEqual(slot, 0);
                Assert.Less(slot, TalentSkeleton.SlotCount);
            }

            // One edge per (parent, child) pair the skeleton declares, per path.
            Assert.AreEqual(TalentPage.PathCount * TalentSkeleton.EdgesPerPath, screen.Edges.Count,
                "the tree drew a different number of connections than the skeleton has");
        }

        // The lit layers are wider and brighter than the limb they lie on --
        // that difference IS the light. Built as three separate widths so a
        // change to the base carries them along.
        [Test]
        public void TheLitLayersAreProportionalToTheLimb()
        {
            Assert.Greater(ConstellationLayout.EdgeGlowWidth, ConstellationLayout.EdgeWidth,
                "the halo is no wider than the limb, so there is nothing to see");
            Assert.Less(ConstellationLayout.EdgeCoreWidth, ConstellationLayout.EdgeWidth,
                "the core is not a crack down the middle, it is another limb");
        }

        // ---- the energy ----------------------------------------------------------

        // The core and the spark are CHILDREN OF THE GLOW, and that is what
        // makes an unlit edge dark for free: the controller switches the glow,
        // and a switched-off parent takes its children with it. Built as
        // siblings of the glow instead, an unspent tree would sit there
        // crackling and running sparks along connections the player has not
        // earned.
        [Test]
        public void TheCrackleAndTheSparkRideOnTheLitLayer()
        {
            var screen = TalentScreen.Build();

            for (int i = 0; i < screen.EdgeGlows.Count; i++)
            {
                var glow = screen.EdgeGlows[i].Node;

                CollectionAssert.Contains(glow.Children, screen.EdgeCores[i].Node,
                    $"'{screen.EdgeCores[i].Node.Name}' is not under its own glow, so it keeps " +
                    "crackling on an edge the player has not earned");
                CollectionAssert.Contains(glow.Children, screen.EdgeSparks[i].Node,
                    $"'{screen.EdgeSparks[i].Node.Name}' is not under its own glow, so it keeps " +
                    "running on an edge the player has not earned");
            }
        }

        // The spark is told its travel rather than measuring its parent, so the
        // length handed to it has to be the edge's own. Nothing else can check
        // this: it is a float passed at wiring time.
        [Test]
        public void EverySparkIsToldTheLengthOfItsOwnEdge()
        {
            var screen = TalentScreen.Build();

            Assert.AreEqual(screen.Edges.Count, screen.EdgeLengths.Count,
                "an edge has no length recorded, so its spark would travel nowhere");
            Assert.AreEqual(screen.Edges.Count, screen.EdgeSparks.Count);

            foreach (float length in screen.EdgeLengths)
            {
                Assert.Greater(length, 0f,
                    "an edge of zero length means two orbs share a position");
            }
        }

        // A spark wider than the limb reads as something moving THROUGH the
        // line; one narrower reads as a bright patch of it.
        [Test]
        public void TheSparkIsWiderThanTheLineItRunsAlong()
        {
            Assert.Greater(ConstellationLayout.EdgeSparkSize, ConstellationLayout.EdgeWidth);
        }

    }
}
