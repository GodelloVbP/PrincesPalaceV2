using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The defeat screen's tree.
    public class DefeatScreenTests
    {
        private static UiNode Tree() => DefeatScreen.Build().Root;

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
        public void ItIsTheSameSizeAsTheReckoning()
        {
            // Deliberately its twin. A death screen shaped differently from the
            // victory screen would make the two feel like different games, and
            // the player is reading the same kinds of fact in both.
            Assert.AreEqual(ReckoningScreen.PanelWidth, DefeatScreen.PanelWidth);
            Assert.AreEqual(ReckoningScreen.PanelHeight, DefeatScreen.PanelHeight);
            Assert.AreEqual(ReckoningScreen.RowCount, DefeatScreen.RowCount);
        }

        [Test]
        public void TheScreenStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void EveryRowStartsHiddenSoAnEmptyPartyDrawsNothing()
        {
            foreach (var row in DefeatScreen.Build().RowGroups)
            {
                Assert.IsTrue(row.Node.StartInactive, $"{row.Node.Name} is visible before anything filled it");
            }
        }

        [Test]
        public void ItDimsHarderThanTheReckoningDoes()
        {
            // The stage behind a victory is worth keeping. The stage behind a
            // defeat is your own party lying on it.
            var defeat = Walk(Tree()).First(n => n.Name == "DefeatPanelDimmer");
            var reckoning = Walk(ReckoningScreen.Build().Root).First(n => n.Name == "ReckoningPanelDimmer");

            Assert.Greater(Alpha(defeat.ColorHex), Alpha(reckoning.ColorHex));
        }

        [Test]
        public void BothExitsWearSilverThereIsNoRetryToMakeGold()
        {
            // Both are ordinary navigation off a screen with no recommended
            // continuation to reserve Gold for -- see the comment at their
            // construction site.
            var screen = DefeatScreen.Build();

            Assert.AreEqual(ButtonTheme.Silver, screen.ReturnButton.Node.Theme);
            Assert.AreEqual(ButtonTheme.Silver, screen.InspectButton.Node.Theme);
        }

        [Test]
        public void ThereAreTwoWaysOutAndOneOfThemIsTheRoster()
        {
            // The roster is the ONE thing that survived a defeat, so "what did
            // I actually keep" is the question this screen provokes. Sending
            // the player to the hub to go and look is a worse answer than a
            // button.
            var screen = DefeatScreen.Build();

            Assert.IsTrue(screen.ReturnButton.IsValid);
            Assert.IsTrue(screen.InspectButton.IsValid);
        }

        [Test]
        public void TheFrameIsNotTheDimmerSoScalingOneDoesNotScaleTheOther()
        {
            var screen = DefeatScreen.Build();

            Assert.AreEqual("DefeatFrame", screen.Frame.Node.Name);
            Assert.AreNotEqual(screen.Root.Name, screen.Frame.Node.Name);
        }

        // The frame's container theme/ratio and content inset are covered by
        // KitContainerPlacementTests; only the screen-specific fact -- the
        // old flat #2A1230F5 fill is gone now that the painted 3:2 container
        // art is the only frame -- stays here.
        [Test]
        public void TheFrameHasNoLeftoverFlatFill()
        {
            Assert.IsNull(DefeatScreen.Build().Frame.Node.ColorHex,
                "the old flat fill must be gone -- the art is the only frame now");
        }

        [Test]
        public void TheFrameIsPinnedWidthAndTheHeightMatchesTheMeasuredAspect()
        {
            var rect = RectOf("DefeatFrame");

            // 1.49 -- ContainerArt.ContainerAspect3x2 is internal and this
            // assembly carries no InternalsVisibleTo grant to it (only
            // Editor gets one), so the measured aspect is restated here as a
            // literal, same as ContainerTests' own SpriteKey literals.
            Assert.AreEqual(1344f, rect.Width, 0.01f);
            Assert.AreEqual(1344f / 1.49f, rect.Height, 0.01f, "902.01, was 896");
        }

        [Test]
        public void TheFrameChildrenRideInsideTheFrame()
        {
            // If the title, rows or exit buttons were siblings of the frame
            // rather than descendants, moving or hiding it would leave them
            // stranded next to it instead of with it.
            var frame = DefeatScreen.Build().Frame.Node;
            var names = Walk(frame).Select(n => n.Name).ToList();

            CollectionAssert.Contains(names, "DefeatTitle");
            CollectionAssert.Contains(names, "DefeatReturnButton");
            CollectionAssert.Contains(names, "DefeatInspectButton");
        }

        private static UiRect RectOf(string name)
        {
            var solved = UiSolver.Solve(Tree(), UiFrames.Reference);
            var node = solved.Name == name ? solved : solved.Descendants().FirstOrDefault(n => n.Name == name);
            Assert.IsNotNull(node, $"no node named '{name}' in the defeat tree");
            return node.Rect;
        }

        [Test]
        public void NoChildCollidesWithAButtonsGeneratedCaption()
        {
            // UiTreeTestHelpers.UnthemedButtons narrows past a THEMED
            // button's own "<Name>Label" child (Ui.ApplyTheme's real,
            // intended output -- see UiNode.Themed). This guard is only for
            // a button that generates NO label of its own (untethered),
            // where a same-named child would still be the emitter-name
            // clash it was written to catch.
            var offenders = UiTreeTestHelpers.UnthemedButtons(Tree())
                .SelectMany(b => b.Children.Where(c => c.Name == b.Name + "Label").Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        private static int Alpha(string hex) =>
            System.Convert.ToInt32(hex.Substring(hex.Length - 2), 16);

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
