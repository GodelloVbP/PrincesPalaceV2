using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The main menu's tree.
    //
    // MISSING UNTIL NOW, despite this screen's own class comment claiming it
    // for as long as the screen has existed ("an EditMode test builds and
    // audits this entire screen -- ambience and all, at four canvas frames --
    // in about a millisecond"). Nothing in the repository ever called
    // MainMenuScreen.Build from an EditMode test, so that sentence described a
    // test that did not exist -- the only thing that ever audited this screen
    // was a full scene build, and every layout change to it (the wordmark, the
    // left-anchored composition, Continue, the delete hold) went
    // in without the fast feedback loop every sibling screen already has. This
    // is that test, the same shape DebugMenuScreenTests and a dozen others use.
    public class MainMenuScreenTests
    {
        private const int RealSlotCount = 5;

        private static UiNode Tree(int slotCount = RealSlotCount) =>
            MainMenuScreen.Build(new MainMenuInputs(slotCount)).Root;

        // Both item styles, so the one not shipping cannot rot
        // (MainMenuScreen.ItemStyle is the switch between them).
        [TestCase(MainMenuItemStyle.Plated)]
        [TestCase(MainMenuItemStyle.Text)]
        public void TheMenuAuditsCleanAtEveryFrame(MainMenuItemStyle style)
        {
            var errors = UiAudit.RunAllFrames(
                MainMenuScreen.Build(new MainMenuInputs(RealSlotCount, style)).Root);

            Assert.IsEmpty(errors,
                $"{style}, first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        // Slot count is an INPUT (Domain cannot see SaveSystem.SlotCount), and
        // the screen's own header names the real reason to test it at more
        // than the shipped number: a roster the game does not currently field
        // still has to lay out cleanly, or the audit is only ever proving
        // today's constant rather than the screen's own arithmetic.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(8)]
        public void TheMenuAuditsCleanAtOtherSlotCounts(int slotCount)
        {
            var errors = UiAudit.RunAllFrames(Tree(slotCount));

            Assert.IsEmpty(errors,
                $"at {slotCount} slots, first 5 of {errors.Count}: " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        // Text style: an item lights from parchment to the title gold on
        // hover, focus and press alike. Literals, not the constants under test.
        [Test]
        public void ATextItemLightsFromIdleToTheTitleGold()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount, MainMenuItemStyle.Text));

            CollectionAssert.AreEquivalent(
                new[] { "ContinueButton", "NewRunButton", "LoadButton", "SettingsButton", "ExitButton" },
                screen.LitItems.Select(i => i.Button.Node.Name).ToArray());

            foreach (var item in screen.LitItems)
            {
                string name = item.Button.Node.Name;
                Assert.AreEqual("#E3C166", item.LitHex, name);
                Assert.AreNotEqual(item.LitHex, item.IdleHex, name + " idle must differ from lit");
                Assert.AreEqual(item.IdleHex, item.Caption.Node.ColorHex, name + " caption is authored in its idle colour");
                Assert.IsTrue(item.Button.Node.Chromeless, name + " wears no plate");
                Assert.IsNull(Find(item.Button.Node, "HoverRim"), name + " draws no rim box");
                Assert.AreEqual(460f, item.Button.Node.Size.X, 0.01f, name + " hit area is the whole row");
            }

            Assert.AreEqual("#FFFFFF", screen.LitItems.First(i => i.Button.Node.Name == "NewRunButton").IdleHex);
            Assert.AreEqual("#B8B4AC", screen.LitItems.First(i => i.Button.Node.Name == "ExitButton").IdleHex,
                "Quit rests dimmer");
        }

        [Test]
        public void PlatedStyleLightsNothing()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount, MainMenuItemStyle.Plated));

            Assert.IsEmpty(screen.LitItems);
        }

        [Test]
        public void EveryModalStartsHidden()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            Assert.IsTrue(screen.SaveSlotPanel.Node.StartInactive);
            Assert.IsTrue(screen.ResetConfirmPanel.Node.StartInactive);
            Assert.IsTrue(screen.SettingsPanel.Node.StartInactive);
        }

        // C7: the Options pane is hosted as built, and the modal gives it its
        // own way out.
        [Test]
        public void TheSettingsModalHostsTheOptionsPaneAndAClose()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            Assert.AreSame(screen.Options.Root, Find(screen.SettingsPanel.Node, "OptionsPane"));
            Assert.AreEqual(UiClickCue.Close, screen.CloseSettingsButton.Node.Click);
        }

        // The parallel arrays UiCountAudit (E4) checks against at build
        // time -- pinned here too, so a mismatch shows up in the same ~1ms
        // pass as the layout errors rather than only in a full scene build.
        // ChooseCards is not E4-bound itself (see MainMenuScreen.SlotCardRefs's
        // own comment on why), but it is built off SlotButtons in the same
        // loop iteration, so pinning its count here catches the same class of
        // drift.
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        public void EverySlotHasAButtonACardAndADeleteButton(int slotCount)
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(slotCount));

            Assert.AreEqual(slotCount, screen.SlotButtons.Count);
            Assert.AreEqual(slotCount, screen.ChooseCards.Count);
            Assert.AreEqual(slotCount, screen.DeleteButtons.Count);
        }

        // C5: a card and its Delete are SIBLINGS in one row, never a Button
        // nested in a Button, and the Manage Saves surface is gone.
        [Test]
        public void EachSlotIsARowHoldingItsCardAndItsDelete()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            var row = Find(screen.Root, "Slot2Row");
            Assert.IsNotNull(row);
            CollectionAssert.AreEqual(new[] { "Slot2Button", "Slot2DeleteButton" },
                row.Children.Select(c => c.Name).ToArray());
            foreach (var name in new[] { "ManageSavesPanel", "ManageSavesButton", "CloseManageSavesButton", "ResetSlot0Row" })
            {
                Assert.IsNull(Find(screen.Root, name), name + " was removed with Manage Saves");
            }
        }

        // 560x112 is exactly 5:1, the FiveByOne plate's own aspect: the card
        // plate is not stretched. Literals, not the constants under test.
        [Test]
        public void TheSlotCardIsAnUnstretchedFiveByOnePlate()
        {
            Assert.AreEqual(560f, MainMenuScreen.CardSize.X, 0.01f);
            Assert.AreEqual(112f, MainMenuScreen.CardSize.Y, 0.01f);
            Assert.AreEqual(0f, Ui.PlateAspectError(MainMenuScreen.CardSize.X, MainMenuScreen.CardSize.Y), 0.0001f);
        }

        // Continue is BUILT UNCONDITIONALLY -- see its own field comment on
        // why it cannot be a child the runtime simply chooses not to add.
        // Asserted as a node search rather than trusting the field alone,
        // since a NodeRef can point at a node that never made it into Root.
        [Test]
        public void ContinueExistsInTheTreeEvenThoughItIsHiddenUntilRuntime()
        {
            var root = Tree();

            bool found = Find(root, "ContinueButton") != null;
            Assert.IsTrue(found, "ContinueButton must exist in the built tree for the controller to toggle");
        }

        // The hold's fill has to be authored at its REAL size -- UiAudit
        // refuses a zero-sized graphic, which is exactly the trap a fill
        // meant to start empty falls into if it is authored at zero instead
        // of authored full and shrunk at runtime. Passing
        // TheMenuAuditsCleanAtEveryFrame already proves this holds; this
        // names WHY, so a future edit that authors the fill at zero fails
        // with an explanation rather than a bare audit error.
        [Test]
        public void TheHoldFillIsAuthoredAtItsFullSize()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            Assert.AreEqual(MainMenuScreen.ResetHoldWidth, screen.ResetConfirmYesFill.Node.Size.X);
            Assert.AreEqual(MainMenuScreen.ResetHoldHeight, screen.ResetConfirmYesFill.Node.Size.Y);
        }

        // C2: one column, top to bottom, no item positioned outside it.
        [Test]
        public void TheMenuIsOneColumnInReadingOrder()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));

            var names = screen.MenuColumn.Node.Children.Select(c => c.Name).ToArray();

            CollectionAssert.AreEqual(
                new[] { "ContinueButton", "NewRunButton", "LoadButton", "SettingsButton", "ExitButton" }, names);
            Assert.IsNull(Find(screen.Root, "PlayButton"), "Play is now New Run and Load");
            Assert.IsNull(Find(screen.Root, "TextScrim"), "the gold container behind the text is gone");
        }

        // C1: the wash stays left of the palace (canvas x -100) and above the
        // terrace lantern glow at canvas y -272. Literals: the lantern and the
        // palace are painted, not derived.
        [Test]
        public void TheLeftScrimClearsThePalaceAndTheLantern()
        {
            var screen = MainMenuScreen.Build(new MainMenuInputs(RealSlotCount));
            var scrim = screen.LeftScrim.Node;

            float right = scrim.Place.Offset.X + scrim.Size.X * 0.5f;
            float bottom = scrim.Place.Offset.Y - scrim.Size.Y * 0.5f;

            Assert.AreEqual(-200f, right, 0.01f);
            Assert.AreEqual(-200f, bottom, 0.01f);
            Assert.IsTrue(scrim.Decor, "a wash must never take a click");
        }

        private static UiNode Find(UiNode node, string name)
        {
            if (node == null) return null;
            if (node.Name == name) return node;

            foreach (var child in node.Children)
            {
                var hit = Find(child, name);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
