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
        public void InvestWearsGoldAndKeepsItsOwnCaption()
        {
            // ThemedPlate(), not Themed(): the panel declares its own custom-
            // coloured caption (InvestButtonCaption) rather than taking the
            // generated one.
            var screen = TalentScreen.Build();

            Assert.AreEqual(ButtonTheme.Gold, screen.InvestButton.Node.Theme);
            Assert.IsTrue(screen.InvestButton.Node.CaptionPreserving);
            CollectionAssert.Contains(
                screen.InvestButton.Node.Children.Select(c => c.Name), "InvestButtonCaption");
        }

        [Test]
        public void RespecWearsVioletAndItsDialogButtonsSplitGoldAndSilver()
        {
            var screen = TalentScreen.Build();

            Assert.AreEqual(ButtonTheme.Violet, screen.RespecButton.Node.Theme);
            Assert.AreEqual(ButtonTheme.Gold, screen.RespecConfirmButton.Node.Theme);
            Assert.AreEqual(ButtonTheme.Silver, screen.RespecCancelButton.Node.Theme);
        }

        [Test]
        public void BackWearsSilverAndThePagingArrowsStayUnthemed()
        {
            var screen = TalentScreen.Build();

            Assert.AreEqual(ButtonTheme.Silver, screen.BackButton.Node.Theme);
            Assert.IsNull(screen.PrevPathButton.Node.Theme, "an icon arrow, not a plate button");
            Assert.IsNull(screen.NextPathButton.Node.Theme, "an icon arrow, not a plate button");
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
            // First(), not ToDictionary(): every themed button on this screen
            // now declares its own "Visuals" child (Ui.ApplyTheme/
            // ApplyThemePlateOnly's Glow+Plate container), so node names are
            // no longer unique screen-wide the moment more than one button is
            // themed -- a dictionary keyed by name throws on the duplicate
            // rather than finding the two orb nodes this test actually wants.
            var all = Walk(screen.Root).ToList();
            var orb = all.First(n => n.Name == "Orb0_0");
            var glow = all.First(n => n.Name == "Orb0_0Glow");

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

        // THE PLATE BECAME A COLUMN, and stopped hiding itself.
        //
        // What it replaced was an 880x150 slab along the bottom that appeared
        // with the selection and vanished with it, which made the screen change
        // shape as the pointer moved. A column that is always there and says
        // what to do with it is furniture; the two tests that used to guard the
        // hiding are replaced by these, which guard the not-hiding.
        [Test]
        public void ThePanelIsAlwaysThere()
        {
            var screen = TalentScreen.Build();

            Assert.IsFalse(screen.Panel.Node.StartInactive,
                "the panel starts hidden, so the screen opens a column short and grows one later");
        }

        [Test]
        public void ThePanelTextRidesOnThePanelSoTheyMoveTogether()
        {
            // If the labels were siblings rather than children, the column
            // could be moved or hidden and leave its own text over the sky --
            // the same class of bug the plate had, in the other direction.
            var panel = Walk(TalentScreen.Build().Root).First(n => n.Name == "TalentPanelColumn");
            var names = panel.Children.Select(c => c.Name).ToList();

            CollectionAssert.Contains(names, "TalentDetailName");
            CollectionAssert.Contains(names, "TalentDetailBody");
            CollectionAssert.Contains(names, "TalentDetailKicker");
            CollectionAssert.Contains(names, "TalentDetailRefusal");
        }

        // The confirmation is the one thing on this screen that MUST start
        // hidden: it is a modal over everything, and a screen that opens asking
        // whether to put out every ember has asked a question nobody posed.
        [Test]
        public void TheRespecDialogStartsHidden()
        {
            Assert.IsTrue(TalentScreen.Build().RespecDialog.Node.StartInactive);
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


        // ---- a child is placed in its PARENT'S frame ------------------------------
        //
        // The edge kit's lit core is a child of the glow and was placed at the
        // edge's absolute midpoint, rotated to the edge's absolute angle --
        // both correct for the sibling it used to be, and both wrong once it
        // moved. A child's transform composes with its parent's, so every lit
        // edge drew a second gold line at twice the offset and twice the angle,
        // scattered across the sky with no stone at either end.
        //
        // UiAudit could not see it: the core declares AllowOverflow, which
        // waives the one check -- child containment -- that measures exactly
        // this. So it is asserted here instead, against the built tree, where
        // "sits on its parent" is a thing that can be stated.
        [Test]
        public void EveryEdgesLitCoreSitsOnTheEdgeItLights()
        {
            var offenders = new List<string>();

            foreach (var node in Walk(TalentScreen.Build().Root))
            {
                if (!node.Name.EndsWith("Glow") || !node.Name.StartsWith("Edge")) continue;

                foreach (var child in node.Children)
                {
                    if (!child.Name.EndsWith("Core")) continue;

                    if (child.Place.Offset.X != 0f || child.Place.Offset.Y != 0f)
                    {
                        offenders.Add($"{child.Name} is offset " +
                                      $"({child.Place.Offset.X}, {child.Place.Offset.Y}) " +
                                      "from the edge it is drawn on");
                    }

                    if (child.Rotation != 0f)
                    {
                        offenders.Add($"{child.Name} carries its own rotation of {child.Rotation}, " +
                                      "which composes with the edge's and doubles it");
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "a lit core is placed in its PARENT'S frame, not the page's - the glow already " +
                "carries the midpoint and the angle");
        }
    }
}
