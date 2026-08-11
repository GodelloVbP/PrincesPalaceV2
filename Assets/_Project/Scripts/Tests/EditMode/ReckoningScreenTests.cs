using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The Reckoning's tree.
    //
    // Doubles as the tuning harness for a layout with a hard external
    // constraint: 70% of the frame, centred, is 1344x756, and four party rows
    // plus three offers plus chrome does not fit in one column at that height.
    // Each attempt costs a second here instead of a scene rebuild.
    public class ReckoningScreenTests
    {
        private static UiNode Tree() => ReckoningScreen.Build().Root;

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
        public void ItAuditsCleanInsideTheFightItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(FightScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TheFrameIsSeventyPercentOfTheReferenceFrame()
        {
            // The design's number, stated once and asserted rather than
            // trusted -- everything in the layout is budgeted against it.
            Assert.AreEqual(1920f * 0.7f, ReckoningScreen.PanelWidth, 0.5f);
            Assert.AreEqual(1080f * 0.7f, ReckoningScreen.PanelHeight, 0.5f);
        }

        [Test]
        public void TheScreenStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void EveryRowStartsHiddenSoAnEmptyPartyDrawsNothing()
        {
            // Four rows are built and a party is one or two. Rows left visible
            // would be four empty boxes under an EXPERIENCE heading.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual(ReckoningScreen.RowCount, screen.RowGroups.Count);
            foreach (var row in screen.RowGroups)
            {
                Assert.IsTrue(row.Node.StartInactive, $"{row.Node.Name} is visible before anything filled it");
            }
        }

        [Test]
        public void ThereIsOneOfferNodePerOfferTheTableProduces()
        {
            // ItemOfferTable.OfferCount is the contract. A tree with fewer
            // buttons than the roll returns would silently drop an offer the
            // player was supposed to be choosing between.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual(ItemOfferTable.OfferCount, screen.OfferButtons.Count);
            Assert.AreEqual(ItemOfferTable.OfferCount, screen.OfferNames.Count);
            Assert.AreEqual(ItemOfferTable.OfferCount, screen.OfferMetas.Count);
        }

        [Test]
        public void TheDimmerIsLightEnoughToLeaveTheFightVisible()
        {
            // The whole argument for an overlay rather than a screen: the stage
            // is still there and the expand plays against it. Dim it to
            // near-black and it becomes a screen with extra steps. Deliberately
            // LIGHTER than the character overlay's, which has the opposite job.
            var dimmer = Walk(Tree()).First(n => n.Name == "ReckoningPanelDimmer");
            int alpha = System.Convert.ToInt32(dimmer.ColorHex.Substring(dimmer.ColorHex.Length - 2), 16);

            Assert.Less(alpha, 0xC0, $"dimmer alpha {dimmer.ColorHex} hides the fight it is supposed to sit over");
            Assert.Greater(alpha, 0x60, "and it still has to make the panel readable");
        }

        [Test]
        public void TheFrameIsNotTheDimmerSoScalingOneDoesNotScaleTheOther()
        {
            // The controller expands the FRAME. If the dimmer were the same
            // node, it would grow from a point too and the fight would flash at
            // full brightness for the first frames of the animation.
            var screen = ReckoningScreen.Build();

            Assert.AreEqual("ReckoningFrame", screen.Frame.Node.Name);
            Assert.AreNotEqual(screen.Root.Name, screen.Frame.Node.Name);
        }

        [Test]
        public void NoOfferChildCollidesWithItsButtonsGeneratedCaption()
        {
            // UiEmitter names a button's caption "<button>Label" and it never
            // appears in the tree, so a child by that name is invisible to
            // every by-name lookup. Three screens had shipped that bug.
            var offenders = Walk(Tree())
                .Where(n => n.Kind == UiNodeKind.Button)
                .SelectMany(b => b.Children.Where(c => c.Name == b.Name + "Label").Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void EveryExemptionStatesARealReason()
        {
            foreach (var node in Walk(Tree()))
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
    }
}
