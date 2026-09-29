namespace PrincesPalace.Domain.UiKit
{
    // One key or button that opens the menu somewhere. The table of them is
    // SystemMenuTabs.Hotkeys; the dispatcher reads each Axis once a frame and
    // hands a pressed one to the top context.
    public readonly struct MenuHotkeyDef
    {
        // The Input Manager button, so a test double can script it and a
        // rebind touches ProjectSettings and nothing else.
        public readonly string Axis;

        // Null means "the context's view tab" (the pad's View button), which
        // depends on where the menu is standing.
        public readonly SystemMenuTab? Tab;

        public readonly MenuTarget Target;

        public MenuHotkeyDef(string axis, SystemMenuTab? tab, MenuTarget target = default)
        {
            Axis = axis;
            Tab = tab;
            Target = target;
        }

        public SystemMenuTab TabIn(MenuContext context) => Tab ?? SystemMenuTabs.ViewTabFor(context);
    }

    public enum MenuHotkeyOutcome
    {
        // Open the menu on the tab, or switch to it, and go to the target.
        GoTo,
        Close,
    }

    // What a hotkey does, given what the menu is showing (plan C6).
    public static class MenuHotkeyRule
    {
        // A plain hotkey toggles: it closes the menu only when its own tab is
        // already up. A hotkey with a target navigates instead, and closes
        // only when the target itself is already showing -- I with the pack
        // up. Closing there is a choice: without it the keyboard has no key
        // that closes the sheet after I, only C and Escape.
        public static MenuHotkeyOutcome Resolve(
            bool menuOpen, SystemMenuTab shownTab, SystemMenuTab hotkeyTab,
            MenuTarget target, bool targetShowing)
        {
            if (!menuOpen || shownTab != hotkeyTab) return MenuHotkeyOutcome.GoTo;
            if (target.IsNone) return MenuHotkeyOutcome.Close;
            return targetShowing ? MenuHotkeyOutcome.Close : MenuHotkeyOutcome.GoTo;
        }
    }
}
