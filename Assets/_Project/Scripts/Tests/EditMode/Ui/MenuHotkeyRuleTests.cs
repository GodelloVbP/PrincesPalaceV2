using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // What a menu hotkey does, row by row from plan C6. The rule is pure: it
    // sees whether the menu is open, which tab is up, which tab and target the
    // key names, and whether that target is already on screen.
    public class MenuHotkeyRuleTests
    {
        private static MenuHotkeyOutcome Plain(bool open, SystemMenuTab shown, SystemMenuTab key) =>
            MenuHotkeyRule.Resolve(open, shown, key, MenuTarget.None, targetShowing: false);

        private static MenuHotkeyOutcome Pack(bool open, SystemMenuTab shown, bool packUp) =>
            MenuHotkeyRule.Resolve(open, shown, SystemMenuTab.Character, MenuTarget.Pack, targetShowing: packUp);

        [Test]
        public void APlainHotkeyOpensAClosedMenu()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Plain(open: false, SystemMenuTab.Character, SystemMenuTab.Party));
        }

        [Test]
        public void APlainHotkeySwitchesWhenAnotherTabIsShowing()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Plain(open: true, SystemMenuTab.Options, SystemMenuTab.Party));
        }

        [Test]
        public void APlainHotkeyClosesWhenItsOwnTabIsShowing()
        {
            Assert.AreEqual(MenuHotkeyOutcome.Close, Plain(open: true, SystemMenuTab.Party, SystemMenuTab.Party));
        }

        [Test]
        public void TheClosedMenuIsNeverClosedWhateverTheStaleTabSays()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Plain(open: false, SystemMenuTab.Party, SystemMenuTab.Party));
        }

        [Test]
        public void ATargetHotkeyOpensAClosedMenu()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Pack(open: false, SystemMenuTab.Character, packUp: false));
        }

        [Test]
        public void ATargetHotkeySwitchesFromAnotherTab()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Pack(open: true, SystemMenuTab.Party, packUp: false));
        }

        // The case the plan singles out: I with the Character tab up goes to
        // the pack rather than closing the menu.
        [Test]
        public void ATargetHotkeyOnItsOwnTabWithoutTheTargetNavigatesInsteadOfClosing()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Pack(open: true, SystemMenuTab.Character, packUp: false));
        }

        // Once the pack is up there is nowhere further to navigate. Closing is
        // the choice made here (the plan says a target hotkey never closes),
        // because without it the keyboard has no key that closes the sheet
        // after I.
        [Test]
        public void ATargetHotkeyWithItsTargetAlreadyShowingCloses()
        {
            Assert.AreEqual(MenuHotkeyOutcome.Close, Pack(open: true, SystemMenuTab.Character, packUp: true));
        }

        [Test]
        public void AShowingTargetOnAnotherTabIsNotClosedOver()
        {
            Assert.AreEqual(MenuHotkeyOutcome.GoTo, Pack(open: true, SystemMenuTab.Party, packUp: true));
        }
    }
}
