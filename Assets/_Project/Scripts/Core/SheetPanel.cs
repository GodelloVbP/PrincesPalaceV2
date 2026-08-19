using UnityEngine;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Opening and closing the character sheet, wherever it is mounted.
    //
    // The hub, the fight and the map all carry a copy of the same overlay and
    // all three need the same three-line dance: toggle, switch pane rather than
    // close when the other key is pressed, and open onto a named pane. Written
    // out three times it would be three chances to get the switch-vs-close rule
    // subtly different, and the third copy is where that starts.
    //
    // Static and panel-taking rather than a base class: the three owners are a
    // hub controller, a fight controller and a map controller with nothing else
    // in common, and inheritance for one shared behaviour would be the wrong
    // shape entirely.
    //
    // It opens the system menu's dossier, which replaced the paperdoll this
    // used to raise. That swap was the whole of the migration: the three owners
    // still call the same two methods with the same arguments, and only what
    // their panel field points at changed. Rewiring C, I, Escape and the hub's
    // building to address the menu directly was the first attempt, and it was
    // strictly more work for the same result -- it dragged three controllers
    // and their tests along to reach a screen this seam already reached.
    internal static class SheetPanel
    {
        public static bool IsOpen(GameObject panel) => panel != null && panel.activeSelf;

        // C means Character and I means Inventory, which on the dossier are two
        // tabs onto ONE pane rather than two panes. The tab still has to follow
        // the key that was pressed, so this maps the key's meaning and lets
        // SystemMenuTabs decide which pane that tab shows.
        private static SystemMenuTab TabFor(bool inventory) =>
            inventory ? SystemMenuTab.Inventory : SystemMenuTab.Character;

        private static bool ShowingInventory(SystemMenuController menu) =>
            menu.SelectedIndex == SystemMenuTabs.IndexOf(SystemMenuTab.Inventory);

        // Pressing the key for the pane already showing closes the sheet.
        // Pressing the OTHER pane's key while it is open switches to that pane
        // rather than closing -- a reader reaching for the other half wants the
        // other half, not the screen behind it.
        public static void Toggle(GameObject panel, bool inventory)
        {
            if (panel == null) return;

            var menu = panel.GetComponent<SystemMenuController>();

            if (panel.activeSelf && menu != null && ShowingInventory(menu) != inventory)
            {
                menu.Select(TabFor(inventory));
                return;
            }

            Set(panel, !panel.activeSelf, inventory);
        }

        public static void Set(GameObject panel, bool open, bool inventory = false)
        {
            if (panel == null) return;

            if (!open)
            {
                panel.SetActive(false);
                return;
            }

            var menu = panel.GetComponent<SystemMenuController>();
            if (menu != null)
            {
                // Open THEN Select, and the order matters: Select records a
                // deliberate choice that the menu's own Start() must not
                // overwrite, and Start() does not run until the frame after
                // Open() activates the object.
                menu.Open();
                menu.Select(TabFor(inventory));
                return;
            }

            panel.SetActive(true);
        }
    }
}
