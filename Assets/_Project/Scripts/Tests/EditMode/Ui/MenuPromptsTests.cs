using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The footer's content (plan C12, spec sections 2 and 8): the glyph table,
    // the frame's default row, and what a prompt reads as in a context. All
    // literals are pinned, not recomputed from the code under test.
    public class MenuPromptsTests
    {
        private static string Labels(System.Collections.Generic.IReadOnlyList<MenuPromptView> views) =>
            string.Join(" | ", views.Select(v => v.GlyphText + " " + v.Label));

        // ---- the glyph table ------------------------------------------------------

        [Test]
        public void TheXboxFaceButtonsWearTheirColours()
        {
            var a = PromptGlyphs.For(PromptDevice.Xbox, PromptButton.Confirm);
            var b = PromptGlyphs.For(PromptDevice.Xbox, PromptButton.Cancel);
            var x = PromptGlyphs.For(PromptDevice.Xbox, PromptButton.West);
            var y = PromptGlyphs.For(PromptDevice.Xbox, PromptButton.North);

            Assert.AreEqual(("A", "#6FD08C"), (a.Text, a.TintHex), "A is green");
            Assert.AreEqual(("B", "#F0716A"), (b.Text, b.TintHex), "B is red");
            Assert.AreEqual(("X", "#66AEF0"), (x.Text, x.TintHex), "X is blue");
            Assert.AreEqual(("Y", "#F0C955"), (y.Text, y.TintHex), "Y is yellow");
        }

        [Test]
        public void TheOtherXboxButtonsArePlain()
        {
            var expected = new[]
            {
                (PromptButton.PageBack, "LB"), (PromptButton.PageForward, "RB"),
                (PromptButton.TabBack, "LT"), (PromptButton.TabForward, "RT"),
                (PromptButton.Pages, "LB/RB"), (PromptButton.Tabs, "LT/RT"),
                (PromptButton.View, "View"), (PromptButton.Start, "Start"),
            };

            foreach (var (button, text) in expected)
            {
                var glyph = PromptGlyphs.For(PromptDevice.Xbox, button);
                Assert.AreEqual(text, glyph.Text, button.ToString());
                Assert.AreEqual(PromptGlyphs.Plain, glyph.TintHex, button.ToString());
            }
        }

        [Test]
        public void TheKeyboardNamesItsKeys()
        {
            var expected = new[]
            {
                (PromptButton.Confirm, "Enter"), (PromptButton.Cancel, "Esc"),
                (PromptButton.PageBack, "Q"), (PromptButton.PageForward, "E"),
                (PromptButton.Pages, "Q/E"), (PromptButton.Tabs, "Click"), (PromptButton.Start, "Esc"),
            };

            foreach (var (button, text) in expected)
            {
                Assert.AreEqual(text, PromptGlyphs.For(PromptDevice.Keyboard, button).Text, button.ToString());
            }
        }

        [Test]
        public void AButtonTheKeyboardLacksHasNoGlyph()
        {
            foreach (var button in new[] { PromptButton.West, PromptButton.North, PromptButton.View,
                                           PromptButton.TabBack, PromptButton.TabForward })
            {
                Assert.IsFalse(PromptGlyphs.For(PromptDevice.Keyboard, button).IsSet, button.ToString());
            }
        }

        [Test]
        public void EveryButtonHasAnXboxGlyph()
        {
            foreach (PromptButton button in System.Enum.GetValues(typeof(PromptButton)))
            {
                Assert.IsTrue(PromptGlyphs.For(PromptDevice.Xbox, button).IsSet, button.ToString());
            }
        }

        [Test]
        public void TheLastInputDecidesTheDevice()
        {
            Assert.AreEqual(PromptDevice.Xbox, PromptGlyphs.DeviceFor(lastInputWasPad: true));
            Assert.AreEqual(PromptDevice.Keyboard, PromptGlyphs.DeviceFor(lastInputWasPad: false));
        }

        // ---- the frame's default row -----------------------------------------------

        [Test]
        public void TheCharacterTabOffersSelectCloseTabAndTheCharacterPager()
        {
            var row = MenuPrompts.Defaults(SystemMenuTabs.Def(SystemMenuTab.Character));

            Assert.AreEqual("A Select | B Close | LT/RT Tab | LB/RB Character",
                Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Xbox)));
            Assert.AreEqual("Enter Select | Esc Close | Click Tab | Q/E Character",
                Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Keyboard)));
        }

        // Plan C8: PARTY, OPTIONS and EXIT page nothing, and no prompt is shown.
        [TestCase(SystemMenuTab.Party)]
        [TestCase(SystemMenuTab.RunStats)]
        [TestCase(SystemMenuTab.Options)]
        [TestCase(SystemMenuTab.Exit)]
        public void ATabThatPagesNothingShowsNoPagerPrompt(SystemMenuTab tab)
        {
            var row = MenuPrompts.Defaults(SystemMenuTabs.Def(tab));

            Assert.AreEqual("A Select | B Close | LT/RT Tab",
                Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Xbox)));
        }

        // ---- a prompt in a context --------------------------------------------------

        private static readonly MenuPrompt Equip =
            new MenuPrompt(PromptButton.Confirm, UiStrings.PromptEquip, MenuAction.EquipGear);

        [Test]
        public void AMutatingPromptIsLockedInAFightAndOnlyThere()
        {
            var row = new[] { Equip };

            Assert.AreEqual("A Equip (locked)", Labels(MenuPrompts.Resolve(row, MenuContext.Fight, PromptDevice.Xbox)));
            Assert.AreEqual("A Equip", Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Xbox)));
            Assert.AreEqual("A Equip", Labels(MenuPrompts.Resolve(row, MenuContext.Descent, PromptDevice.Xbox)));
        }

        // The suffix comes from the table, so it follows the table into the
        // contexts other than the fight that refuse an action.
        [Test]
        public void TheSuffixFollowsThePermissionTableOutsideTheFightToo()
        {
            var refund = new MenuPrompt(PromptButton.West, UiStrings.PromptSpendPoint, MenuAction.RefundAttribute);
            var book = new MenuPrompt(PromptButton.Confirm, UiStrings.PromptPlaceBook, MenuAction.PlaceBook);

            Assert.AreEqual("X Spend point (locked)",
                Labels(MenuPrompts.Resolve(new[] { refund }, MenuContext.Descent, PromptDevice.Xbox)));
            Assert.AreEqual("A Place book (locked)",
                Labels(MenuPrompts.Resolve(new[] { book }, MenuContext.Camp, PromptDevice.Xbox)));
            Assert.AreEqual("A Place book",
                Labels(MenuPrompts.Resolve(new[] { book }, MenuContext.Descent, PromptDevice.Xbox)));
        }

        [Test]
        public void EveryActionThePermissionTableRefusesInAFightWearsTheSuffix()
        {
            foreach (MenuAction action in System.Enum.GetValues(typeof(MenuAction)))
            {
                var row = new[] { new MenuPrompt(PromptButton.Confirm, UiStrings.PromptSelect, action) };
                string label = MenuPrompts.Resolve(row, MenuContext.Fight, PromptDevice.Xbox)[0].Label;

                bool locked = label.EndsWith("(locked)");
                Assert.AreEqual(!MenuPermissions.Allows(action, MenuContext.Fight), locked, action.ToString());
            }
        }

        [Test]
        public void APromptWithNoActionIsNeverLocked()
        {
            var row = new[] { new MenuPrompt(PromptButton.Cancel, UiStrings.PromptClose) };

            Assert.AreEqual("B Close", Labels(MenuPrompts.Resolve(row, MenuContext.Fight, PromptDevice.Xbox)));
        }

        [Test]
        public void APromptTheDeviceHasNoButtonForIsLeftOut()
        {
            var row = new[]
            {
                new MenuPrompt(PromptButton.Confirm, UiStrings.PromptSelect),
                new MenuPrompt(PromptButton.West, UiStrings.PromptOpen),
            };

            Assert.AreEqual("A Select | X Open", Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Xbox)));
            Assert.AreEqual("Enter Select", Labels(MenuPrompts.Resolve(row, MenuContext.Camp, PromptDevice.Keyboard)));
        }

        // ---- the mode pill -------------------------------------------------------------

        [Test]
        public void ThePillNamesEachContext()
        {
            Assert.AreEqual("AT CAMP", MenuModePill.For(MenuContext.Camp).Format());
            Assert.AreEqual("IN A DESCENT", MenuModePill.For(MenuContext.Descent).Format());
            Assert.AreEqual("IN A FIGHT — VIEW ONLY", MenuModePill.For(MenuContext.Fight).Format());
        }

        [Test]
        public void ThePillFitsItsWordsAndNeverPassesItsMaximum()
        {
            Assert.AreEqual(132f, SystemMenuLayout.PillWidth(100f), "100 + 2 x 16 padding");
            Assert.AreEqual(SystemMenuLayout.PillMaxWidth, SystemMenuLayout.PillWidth(900f));
            Assert.AreEqual(-100f + 66f, SystemMenuLayout.PillCentreX(132f), "the left edge stays put");
        }
    }
}
