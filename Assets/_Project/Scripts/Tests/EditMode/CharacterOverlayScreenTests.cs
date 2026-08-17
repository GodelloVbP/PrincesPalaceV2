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
        public void TheDetailPlateStartsHidden()
        {
            // 1200x170 is the biggest object on the overlay, and
            // nothing-selected is the state it OPENS in -- so the first thing
            // the player saw was the screen's own empty furniture.
            Assert.IsTrue(CharacterOverlayScreen.Build().DetailPlate.Node.StartInactive);
        }

        [Test]
        public void TheEmptyBagHintExistsAndStartsHidden()
        {
            // Every bag cell hides itself when unused, so an empty bag drew a
            // rectangle of nothing with "PAGE 1 OF 1" underneath it -- twenty
            // things that failed to load, rather than a bag you have not
            // filled. Hidden at build because the common case is a full bag.
            var hint = CharacterOverlayScreen.Build().BagEmptyHint;

            Assert.IsTrue(hint.IsValid);
            Assert.IsTrue(hint.Node.StartInactive);
        }

        [Test]
        public void TheSilhouettePointsAtArtThatActuallyExists()
        {
            // It pointed at a painted stand that had not been drawn yet, so
            // LoadSpriteByKey warned, degraded, and left the paperdoll as eight
            // boxes floating in a void -- every slot coordinate is placed
            // against a BODY, and without one the pane reads as broken rather
            // than as unfinished. A generated stand holds the place until the
            // painted one lands.
            // The PAINTED stand now, keyed off its green screen. The
            // procedural placeholder existed only until this art was drawn.
            Assert.AreEqual("UI/CharacterOverlay/Processed/armour_stand.png",
                CharacterOverlayScreen.SilhouetteKey);

            var node = CharacterOverlayScreen.Build().Silhouette.Node;

            // WHITE, because a tint multiplies and this art carries its own
            // colour. The violet that suited the white procedural stand would
            // have crushed the painted one to near-black on a 94% dimmer.
            Assert.AreEqual("#FFFFFF", node.ColorHex,
                "a tint over painted art multiplies it darker");
            Assert.IsTrue(node.Decor, "it must not eat the clicks of the slots laid over it");
        }

        [Test]
        public void TheDimmerIsOpaqueEnoughToSilenceTheHubBehindIt()
        {
            // At the 85% it started on, the hub's own headings came straight
            // through: "DIVINE PRINCIPALITY" read louder than the character
            // name on top of it, because a large light glyph survives a dim
            // that a painted building does not.
            // The colour lives on the dimmer Ui.Modal builds, not on the modal
            // root itself -- the root is a transparent grouping node.
            var dimmer = Walk(CharacterOverlayScreen.Build().Root)
                .First(n => n.Name == "CharacterOverlayPanelDimmer");
            string hex = dimmer.ColorHex;

            Assert.IsNotNull(hex);
            int alpha = System.Convert.ToInt32(hex.Substring(hex.Length - 2), 16);
            Assert.GreaterOrEqual(alpha, 0xE6, $"dimmer alpha {hex} lets the hub compete");
        }

        [Test]
        public void ThePaperdollClearsTheDetailPlate()
        {
            // The plate is .AsDecor(), and decor is EXEMPT from the sibling
            // overlap check -- deliberately, because stacked cell layers need
            // that exemption to exist at all. The cost is that this particular
            // collision is invisible to UiAudit by construction, and it really
            // happened: the boots and the bottom of the Shoes cell sat behind
            // the plate, found only by compositing the layout as an image.
            //
            // So it is asserted directly. An exemption that buys something
            // elsewhere still needs the thing it stops checking checked.
            Assert.Greater(OverlayAnchors.PaperdollBottom, OverlayAnchors.DetailPlateTop,
                $"the paperdoll reaches {OverlayAnchors.PaperdollBottom} and the plate's top edge " +
                $"is {OverlayAnchors.DetailPlateTop} -- the stand is behind it");
        }

        [Test]
        public void EverySlotSitsOnTheStandItIsPositionedAgainst()
        {
            // Each cell must fall inside the silhouette's box, horizontally or
            // vertically, or it is a box floating in a void -- which is exactly
            // how the pane read before the stand existed. The two weapon slots
            // are excluded on purpose: they FLANK the body, which is the whole
            // reason they are out there.
            float top = OverlayAnchors.Silhouette.Y + OverlayAnchors.SilhouetteSize.Y * 0.5f;
            float bottom = OverlayAnchors.Silhouette.Y - OverlayAnchors.SilhouetteSize.Y * 0.5f;

            foreach (var slot in EquipmentSlots.All)
            {
                if (slot == EquipmentSlot.Weapon1 || slot == EquipmentSlot.Weapon2) continue;

                var at = OverlayAnchors.PositionFor(slot);
                Assert.LessOrEqual(at.Y, top, $"{slot} floats above the stand");
                Assert.GreaterOrEqual(at.Y, bottom, $"{slot} floats below the stand");
            }
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

        // No slot cell may sit on the figure.
        //
        // The silhouette is decor, so UiAudit's overlap check ignores it by
        // design -- which is exactly how five opaque cells came to be stacked
        // down the stand's torso with a green build. The paperdoll read as a
        // column of boxes with a purple shape behind them.
        //
        // This is the rule that was only ever written in a comment. Cells live
        // in the gutters either side of the figure; anything that puts one back
        // on the body fails here rather than shipping and being noticed in a
        // screenshot weeks later.
        [Test]
        public void NoSlotCellOverlapsTheSilhouette()
        {
            float figureLeft = OverlayAnchors.Silhouette.X - OverlayAnchors.SilhouetteSize.X * 0.5f;
            float figureRight = OverlayAnchors.Silhouette.X + OverlayAnchors.SilhouetteSize.X * 0.5f;
            float figureBottom = OverlayAnchors.Silhouette.Y - OverlayAnchors.SilhouetteSize.Y * 0.5f;
            float figureTop = OverlayAnchors.Silhouette.Y + OverlayAnchors.SilhouetteSize.Y * 0.5f;

            float half = OverlayAnchors.SlotCell * 0.5f;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var at = OverlayAnchors.PositionFor(slot);

                bool clearsHorizontally =
                    at.X + half <= figureLeft || at.X - half >= figureRight;
                bool clearsVertically =
                    at.Y + half <= figureBottom || at.Y - half >= figureTop;

                Assert.IsTrue(clearsHorizontally || clearsVertically,
                    $"the {slot} cell sits on the silhouette -- cells belong in the "
                    + "gutters beside the figure, not over it");
            }
        }

        // The two columns must stay clear of the figure rather than merely
        // outside it by a pixel: a cell touching the outline reads as attached
        // to the body and reintroduces the same crowding at a smaller scale.
        [Test]
        public void TheSlotColumnsKeepARealGutter()
        {
            float figureLeft = OverlayAnchors.Silhouette.X - OverlayAnchors.SilhouetteSize.X * 0.5f;
            float figureRight = OverlayAnchors.Silhouette.X + OverlayAnchors.SilhouetteSize.X * 0.5f;
            float half = OverlayAnchors.SlotCell * 0.5f;

            Assert.GreaterOrEqual(figureLeft - (OverlayAnchors.SlotColumnLeftX + half), 24f,
                "the left column is crowding the figure");
            Assert.GreaterOrEqual(OverlayAnchors.SlotColumnRightX - half - figureRight, 24f,
                "the right column is crowding the figure");
        }

        // Every slot has somewhere of its own to be. A slot falling through to
        // the default branch would silently stack on another one.
        [Test]
        public void EverySlotHasItsOwnCell()
        {
            var seen = new List<UiVec>();

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var at = OverlayAnchors.PositionFor(slot);
                Assert.IsFalse(seen.Any(p => p.X == at.X && p.Y == at.Y),
                    $"{slot} shares a position with another slot");
                seen.Add(at);
            }
        }
    }
}
