using UnityEngine;

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
    internal static class SheetPanel
    {
        public static bool IsOpen(GameObject panel) => panel != null && panel.activeSelf;

        // Pressing the key for the pane already showing closes the sheet.
        // Pressing the OTHER pane's key while it is open switches to that pane
        // rather than closing -- a reader reaching for the other half wants the
        // other half, not the screen behind it.
        public static void Toggle(GameObject panel, bool inventory)
        {
            if (panel == null) return;

            var overlay = panel.GetComponent<CharacterOverlayController>();

            if (panel.activeSelf && overlay != null && overlay.ShowingInventory != inventory)
            {
                overlay.OpenOn(inventory);
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

            // Through OpenOn so the pane is chosen BEFORE the object activates:
            // OnEnable refreshes, and refreshing into the pane being left would
            // paint one frame of the wrong half.
            var overlay = panel.GetComponent<CharacterOverlayController>();
            if (overlay != null) overlay.OpenOn(inventory);
            else panel.SetActive(true);
        }
    }
}
