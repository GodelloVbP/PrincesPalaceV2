using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // WHAT A MENU HOTKEY DOES, in one place.
    //
    // The hub, the map and the fight each used to poll C and I in their own
    // Update, and each had to decide again what a second press meant. Now the
    // dispatcher (NavigationInputModule) reads every hotkey once a frame and
    // hands it to whichever context is top, and only a scene root or the menu
    // itself answers -- the same rule Start follows -- by calling Press. The
    // decision is Domain (MenuHotkeyRule); this only reads the menu's state
    // and acts on the answer.
    internal static class MenuHotkeys
    {
        public static void Press(SystemMenuController menu, MenuHotkeyDef hotkey)
        {
            if (menu == null) return;

            var tab = hotkey.TabIn(menu.Context);

            // A key for a tab this context does not have does nothing: R at
            // camp has no run to show, and opening the menu on some other tab
            // in answer to it would be a menu the player did not ask for.
            if (!menu.HasTab(tab)) return;

            bool open = menu.IsOpen;

            var outcome = MenuHotkeyRule.Resolve(
                open, open ? menu.SelectedTab : tab, tab, hotkey.Target,
                targetShowing: menu.TargetIsShowing(hotkey.Target));

            if (outcome == MenuHotkeyOutcome.Close)
            {
                // A pending state never commits on close, so the pane lets go
                // of it first.
                menu.CancelPending();
                menu.Close();
                return;
            }

            menu.Open(tab, hotkey.Target);
        }
    }
}
