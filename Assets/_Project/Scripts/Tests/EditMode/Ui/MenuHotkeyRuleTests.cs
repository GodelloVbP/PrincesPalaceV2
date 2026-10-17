using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // What a menu hotkey does, row by row from plan C6. The rule is pure: it
    // sees whether the menu is open, which tab is up, which tab and target the
    // key names, and whether that target is already on screen.
    public class MenuHotkeyRuleTests
    {
        // Rows read: menu open, tab showing, tab the key names, whether the key
        // names the pack as a target, whether that target is on screen,
        // expected outcome.
        [TestCase(false, SystemMenuTab.Character, SystemMenuTab.Party, false, false, MenuHotkeyOutcome.GoTo,
            TestName = "APlainHotkeyOpensAClosedMenu")]
        [TestCase(true, SystemMenuTab.Options, SystemMenuTab.Party, false, false, MenuHotkeyOutcome.GoTo,
            TestName = "APlainHotkeySwitchesWhenAnotherTabIsShowing")]
        [TestCase(true, SystemMenuTab.Party, SystemMenuTab.Party, false, false, MenuHotkeyOutcome.Close,
            TestName = "APlainHotkeyClosesWhenItsOwnTabIsShowing")]
        [TestCase(false, SystemMenuTab.Party, SystemMenuTab.Party, false, false, MenuHotkeyOutcome.GoTo,
            TestName = "TheClosedMenuIsNeverClosedWhateverTheStaleTabSays")]
        [TestCase(false, SystemMenuTab.Character, SystemMenuTab.Character, true, false, MenuHotkeyOutcome.GoTo,
            TestName = "ATargetHotkeyOpensAClosedMenu")]
        [TestCase(true, SystemMenuTab.Party, SystemMenuTab.Character, true, false, MenuHotkeyOutcome.GoTo,
            TestName = "ATargetHotkeySwitchesFromAnotherTab")]
        // The case the plan singles out: I with the Character tab up goes to
        // the pack rather than closing the menu.
        [TestCase(true, SystemMenuTab.Character, SystemMenuTab.Character, true, false, MenuHotkeyOutcome.GoTo,
            TestName = "ATargetHotkeyOnItsOwnTabWithoutTheTargetNavigatesInsteadOfClosing")]
        // Once the pack is up there is nowhere further to navigate. Closing is
        // the choice made here (the plan says a target hotkey never closes),
        // because without it the keyboard has no key that closes the sheet
        // after I.
        [TestCase(true, SystemMenuTab.Character, SystemMenuTab.Character, true, true, MenuHotkeyOutcome.Close,
            TestName = "ATargetHotkeyWithItsTargetAlreadyShowingCloses")]
        [TestCase(true, SystemMenuTab.Party, SystemMenuTab.Character, true, true, MenuHotkeyOutcome.GoTo,
            TestName = "AShowingTargetOnAnotherTabIsNotClosedOver")]
        public void TheRuleResolvesEachRowOfPlanC6(bool open, SystemMenuTab shown, SystemMenuTab key,
            bool pack, bool targetShowing, MenuHotkeyOutcome expected)
        {
            Assert.AreEqual(expected, MenuHotkeyRule.Resolve(open, shown, key, pack ? MenuTarget.Pack : MenuTarget.None, targetShowing));
        }
    }
}
