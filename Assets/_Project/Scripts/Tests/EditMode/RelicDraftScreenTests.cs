using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    public class RelicDraftScreenTests
    {
        private static UiNode Tree() => RelicDraftScreen.Build().Root;

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
        public void ItAuditsCleanInsideTheHubItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(HubScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void ThereIsExactlyOneCardPerOffer()
        {
            // Read from RelicPool rather than typed here, so changing the offer
            // count cannot leave a fourth card orphaned in the tree.
            var screen = RelicDraftScreen.Build();

            Assert.AreEqual(RelicPool.OfferCount, screen.Cards.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardNames.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardIcons.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardRarities.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardBodies.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardHalos.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardBursts.Count);
            Assert.AreEqual(screen.Cards.Count, screen.CardSelections.Count);
        }

        [Test]
        public void TheCardRowIsCentred()
        {
            // Laid out from the centre outward, so a thin pool showing two
            // cards stays centred rather than sitting left with a hole where
            // the third would have been.
            var screen = RelicDraftScreen.Build();
            var xs = screen.Cards.Select(c => c.Node.Place.Offset.X).ToList();

            Assert.AreEqual(0f, xs.Sum(), 0.01f, "the card row is not symmetric about the centre");
        }

        [Test]
        public void TheSelectionRingStartsInvisible()
        {
            // Nothing is chosen when the draft opens, and a lit ring on every
            // card would say the opposite.
            foreach (var ring in RelicDraftScreen.Build().CardSelections)
            {
                StringAssert.EndsWith("00", ring.Node.ColorHex);
            }
        }

        [Test]
        public void SelectionAndRarityAreSeparateLayers()
        {
            // Two channels, not one Image.color carrying both. v1's item cells
            // made that mistake and the two states fought.
            var screen = RelicDraftScreen.Build();

            for (int i = 0; i < screen.Cards.Count; i++)
            {
                Assert.AreNotEqual(screen.CardSelections[i].Node.Name, screen.CardRarities[i].Node.Name);
            }
        }

        [Test]
        public void TheScreenStartsHiddenAndTheEmptyHintDoes()
        {
            var screen = RelicDraftScreen.Build();

            Assert.IsTrue(screen.Root.StartInactive);
            Assert.IsTrue(screen.EmptyHint.Node.StartInactive);
        }

        [Test]
        public void ThereIsAlwaysAWayOut()
        {
            // A draft the player cannot leave is worse than one they decline,
            // and an empty pool still has to have an exit.
            Assert.IsTrue(RelicDraftScreen.Build().DescendButton.IsValid);
        }

        [Test]
        public void DescendIsTheOneGoldButtonOnTheScreen()
        {
            // The recommended action: take the offer (or knowingly decline
            // it) and move on. It is the only way out of the draft.
            Assert.AreEqual(ButtonTheme.Gold, RelicDraftScreen.Build().DescendButton.Node.Theme);
        }

        [Test]
        public void EveryCardLayerButTheCardIsDecor()
        {
            // Decor buys overlap exemption for the stacked layers AND clears
            // the raycast, so no layer can eat the click meant for its card.
            foreach (var node in Walk(Tree()))
            {
                if (!node.Name.StartsWith("DraftCard")) continue;
                if (node.Kind == UiNodeKind.Button) continue;

                Assert.IsTrue(node.Decor, $"{node.Name} would steal its card's click");
            }
        }

        [Test]
        public void TheFrameIsAVioletThreeByTwoContainer()
        {
            // Was a flat #241736F5 panel at 1500x820 -- the frame moved to
            // the kit's Violet 3:2 container 2026-09-02, nudged to 1500x1000
            // to clear the aspect band (see the build site's own comment).
            var frame = RelicDraftScreen.Build().Frame.Node;

            Assert.IsFalse(frame.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = frame.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("UI/Buttons/Processed/container_violet_3x2.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);
            Assert.IsNull(frame.ColorHex, "the old flat fill must be gone -- the art is the only frame now");
        }

        [Test]
        public void TheFrameContentSitsInsideTheMeasuredInset()
        {
            var frame = RelicDraftScreen.Build().Frame.Node;
            var content = frame.Children.Single(c => c.Name == "DraftFrameContent");
            var inset = Ui.ContainerContentInset(ContainerRatio.ThreeByTwo);

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);
            Assert.AreEqual(1500f * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(1500f * inset.Right, content.Place.Right, 0.01f);
            Assert.AreEqual(1000f * inset.Top, content.Place.Top, 0.01f);
            Assert.AreEqual(1000f * inset.Bottom, content.Place.Bottom, 0.01f);
        }

        [Test]
        public void TheFrameChildrenRideInsideTheFrame()
        {
            // If the cards or Descend were siblings of the frame rather than
            // descendants, moving or hiding it would leave them stranded next
            // to it instead of with it.
            var frame = RelicDraftScreen.Build().Frame.Node;
            var names = Walk(frame).Select(n => n.Name).ToList();

            CollectionAssert.Contains(names, "DraftCard0");
            CollectionAssert.Contains(names, "DraftDescendButton");
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
