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
            //
            // "...Frame"/"...FrameContent" are excluded: the Violet 3:4
            // container wrapper each card grew (balance-bot, 2026-09-02) is a
            // non-Decor holder wrapping a Decor frame sprite (see Ui.
            // BuildFrameHolder's own comment on why the holder must stay
            // non-Decor) -- content beneath a Decor node is exempt from
            // sibling overlap entirely, which is wrong for real content. The
            // screen's own top-level DraftFrame stopped being a Container on
            // 2026-09-23 (see its build site) and is not one of these --
            // this exclusion is for the per-card frames only.
            foreach (var node in Walk(Tree()))
            {
                if (!node.Name.StartsWith("DraftCard")) continue;
                if (node.Kind == UiNodeKind.Button) continue;
                if (node.Name.EndsWith("Frame") || node.Name.EndsWith("FrameContent")) continue;

                Assert.IsTrue(node.Decor, $"{node.Name} would steal its card's click");
            }
        }

        // The frame ITSELF (the Panel that wraps DraftFrameFill/the cards/
        // Descend/the Rim) must stay a bare grouping node with no ColorHex of
        // its own -- the fill is a separate DraftFrameFill child (2026-09-23,
        // the system-menu frame idiom replacing the old Violet Container),
        // exactly as SystemMenuFrame's own ColorHex-less wrapper sits over
        // SystemMenuFill. A colour landing on the wrapper directly would
        // double-paint under the Rim's own edges.
        [Test]
        public void TheFrameHasNoFillOfItsOwn()
        {
            Assert.IsNull(RelicDraftScreen.Build().Frame.Node.ColorHex,
                "the frame wrapper must stay bare -- DraftFrameFill is the only thing that paints the ground");
        }

        // Pins the actual shape of the 2026-09-23 replacement: a near-black
        // fill and a four-edge hairline Rim, not the Violet Container art it
        // used to wear. If DraftFrameFill's own colour ever drifted back
        // toward a lighter/tinted violet, this is the test that would say so
        // rather than a screenshot someone had to notice was wrong.
        [Test]
        public void TheFrameIsANearBlackFillWithAHairlineRim()
        {
            var frame = RelicDraftScreen.Build().Frame.Node;

            var fill = frame.Children.Single(c => c.Name == "DraftFrameFill");
            Assert.AreEqual("#1A1024F5", fill.ColorHex,
                "the interior must read as near-black, matching the system menu's own frame -- not the old leather container");
            Assert.IsTrue(fill.Decor, "the fill must not steal clicks meant for the cards/pager/Descend drawn over it");

            var rimNames = new[] { "DraftFrameRimTop", "DraftFrameRimBottom", "DraftFrameRimLeft", "DraftFrameRimRight" };
            foreach (var name in rimNames)
            {
                var edge = frame.Children.SingleOrDefault(c => c.Name == name);
                Assert.IsNotNull(edge, $"'{name}' is missing -- Ui.Rim should have produced all four edges");
                Assert.IsTrue(edge.Decor, $"'{name}' must be Decor, same as every other kit rim edge");
            }
        }

        [Test]
        public void EachCardIsAVioletThreeByFourContainerNestedInsideItsButton()
        {
            // The authored 380x460 (0.826) moved to 270x460 (0.587) against
            // the spliced kit's measured 0.588, and to 345x460 (0.75 exactly)
            // at the 2026-09-07 regeneration -- see CardWidth's own comment.
            // The card stays a Button (it still takes Choose()'s click); the
            // container art is a nested, non-Button holder inside it, same
            // shape Ui.Container always returns.
            var card = RelicDraftScreen.Build().Cards[0].Node;
            var frame = card.Children.Single(c => c.Name == "DraftCard0Frame");

            Assert.IsFalse(frame.Decor, "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = frame.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("UI/Buttons/Processed/container_violet_3x4.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);

            var content = frame.Children.Single(c => c.Name == "DraftCard0FrameContent");
            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);

            // LITERAL, worked by hand rather than recomputed from
            // Ui.ContainerContentInset -- multiplying the production inset
            // back out here would only assert that multiplication works
            // (CLAUDE.md gotcha 5). The kit's Container/3:4 inset is
            // left/right 0.069 and top 0.052, so on a 345x460 card:
            //   Left = 345 * 0.069 = 23.805
            //   Top  = 460 * 0.052 = 23.92
            Assert.AreEqual(23.805f, content.Place.Left, 0.01f);
            Assert.AreEqual(23.92f, content.Place.Top, 0.01f);
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
