using UnityEngine;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The character sheet, wherever it is mounted: a thin caller onto the
    // system menu's one entry (SystemMenuController.Open) and its one hotkey
    // rule (MenuHotkeys).
    //
    // The hub, the fight and the map each carry a copy of the menu and each
    // exposes ToggleCharacterSheet / SetCharacterSheet for the tests and the
    // hub's CHARACTER SHEET building. Those are the C and I keys' actions
    // without the key, so they go through the same Press the dispatcher calls:
    // the building and the key cannot drift apart.
    //
    // Static and panel-taking rather than a base class: the three owners have
    // nothing else in common, and inheritance for one shared behaviour would
    // be the wrong shape.
    internal static class SheetPanel
    {
        public static bool IsOpen(GameObject panel) => panel != null && panel.activeSelf;

        // C and I, without the key. `inventory` is I: the sheet with the pack
        // up.
        public static void Toggle(GameObject panel, bool inventory)
        {
            if (panel == null) return;

            var menu = MenuOf(panel);
            if (menu == null)
            {
                // A panel mounted without a controller above it: a headless
                // fixture, or a future screen that reuses this seam.
                panel.SetActive(!panel.activeSelf);
                return;
            }

            MenuHotkeys.Press(menu, SystemMenuTabs.HotkeyFor(inventory ? SystemMenuTabs.PackKey : 'C'));
        }

        // The building's way in, and the tests': open on the Character tab
        // (with the pack for I), or close. Idempotent, so a held key cannot
        // flicker it.
        public static void Set(GameObject panel, bool open, bool inventory = false)
        {
            if (panel == null) return;

            var menu = MenuOf(panel);

            if (!open)
            {
                // Through the menu when there is one, so the pause it took is
                // the pause it gives back: closing by deactivating the panel
                // never reached Close(), and every open-then-close left the
                // clock stopped. SetActive stays as the fallback for a panel
                // with no controller above it.
                if (menu != null) menu.Close();
                else panel.SetActive(false);
                return;
            }

            if (menu != null)
            {
                menu.Open(SystemMenuTab.Character, inventory ? MenuTarget.Pack : default);
                return;
            }

            panel.SetActive(true);
        }

        // In a PARENT, not on `panel` itself. `panel` is what every caller
        // passes as "the sheet" -- the menu's visual root, which is what
        // IsOpen has to read. The controller lives one level up, on the scene
        // root, so it keeps listening while the panel is inactive.
        private static SystemMenuController MenuOf(GameObject panel) =>
            panel.GetComponentInParent<SystemMenuController>();
    }
}
