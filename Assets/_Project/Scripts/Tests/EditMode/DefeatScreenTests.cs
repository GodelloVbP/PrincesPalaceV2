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

        [Test]
        public void NoChildCollidesWithAButtonsGeneratedCaption()
        {
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
