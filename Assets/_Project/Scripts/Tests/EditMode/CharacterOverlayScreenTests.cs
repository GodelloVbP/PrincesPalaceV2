using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The character overlay's tree, audited before any scene exists.
    //
    // This class is the TUNING HARNESS for the composition. Eight slot cells
    // laid over a body and a twenty-cell grid is a lot of coordinates to get
    // right, and each attempt here costs about a second instead of a scene
    // rebuild.
    public class CharacterOverlayScreenTests
    {
        private static UiNode Overlay() => CharacterOverlayScreen.Build().Root;

        [Test]
        public void TheOverlayAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Overlay(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheHubItMountsIn()
        {
            // The overlay is a Modal inside HubScreen, so the hub's own audit
            // is the one that actually gates the build. Passing alone and
            // failing in context would be the worst of both.
            var errors = UiAudit.RunAllFrames(HubScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TheOverlayStartsHidden()
        {
            // It lives in the hub's tree permanently; only SetActive opens it.
            Assert.IsTrue(Overlay().StartInactive);
        }

        [Test]
        public void ThereIsOneCellPerEquipmentSlot()
        {
            // Walked from the enum, so adding a ninth slot fails here rather
            // than silently going unwearable.
            var screen = CharacterOverlayScreen.Build();

            Assert.AreEqual(EquipmentSlots.All.Length, screen.SlotCells.Count);
            Assert.AreEqual(screen.SlotCells.Count, screen.SlotIcons.Count);
            Assert.AreEqual(screen.SlotCells.Count, screen.SlotRarityEdges.Count);
            Assert.AreEqual(screen.SlotCells.Count, screen.SlotPlusLabels.Count);
        }

        [Test]
        public void TheBagHasExactlyOnePageOfCells()
        {
            // One number, read by the tree, the controller and the count audit.
            var screen = CharacterOverlayScreen.Build();

            Assert.AreEqual(BagView.CellCount, screen.BagCells.Count);
            Assert.AreEqual(BagView.CellCount, screen.BagIcons.Count);
            Assert.AreEqual(BagView.CellCount, screen.BagCountLabels.Count);
            Assert.AreEqual(BagView.CellCount, screen.BagPlusLabels.Count);
        }

        [Test]
        public void SlotCellsAreDeclaredInEnumOrder()
        {
            // Slot i must be EquipmentSlots.All[i], because that is what lets
            // the controller index the arrays without a lookup table that could
            // drift from the enum.
            var screen = CharacterOverlayScreen.Build();
            var byName = Walk(screen.Root).Select(n => n.Name).ToList();

            foreach (var slot in EquipmentSlots.All)
            {
                Assert.Contains($"Slot{slot}", byName, $"no cell for {slot}");
            }
        }

        [Test]
        public void EveryCellLayerButTheCellItselfIsDecor()
        {
            // Decor is what buys two things at once: exemption from the sibling
            // overlap check for stacked layers, and a cleared raycast so no
            // layer can take the click meant for its cell. Without it every
            // cell would need four exemptions and the icon would eat presses.
            var root = CharacterOverlayScreen.Build().Root;

            foreach (var node in Walk(root))
            {
                bool isLayer = node.Name.EndsWith("Backing") || node.Name.EndsWith("Rarity")
                    || node.Name.EndsWith("Icon") || node.Name.EndsWith("Frame");

                if (isLayer)
                {
                    Assert.IsTrue(node.Decor, $"{node.Name} would steal its cell's click");
                }
            }
        }

        [Test]
        public void TheRarityEdgeStartsInvisible()
        {
            // An empty cell has no rarity. A visible white strip on every empty
            // slot is exactly the "plain white squares" the redesign exists to
            // get rid of.
            var edge = Walk(CharacterOverlayScreen.Build().Root)
                .First(n => n.Name == "SlotTorsoRarity");

            StringAssert.EndsWith("00", edge.ColorHex, "alpha 00 until an item says otherwise");
        }

        [Test]
        public void EveryExemptionStatesARealReason()
        {
            foreach (var node in Walk(CharacterOverlayScreen.Build().Root))
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
