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

        // BOTH KEYS LAND ON ONE TAB now. The design pass merged Character and
        // Inventory into a single tab, because the dossier was always both --
        // it carries its own pack -- and a second tab onto the same pane was a
        // second door onto one room.
        //
        // So the switch-vs-close rule below has nothing left to switch between
        // and collapses to a plain toggle. The `inventory` argument stays in the
        // signature: three controllers pass it, the dossier could yet open with
        // its pack already up, and removing it would be a rewiring of every
        // caller to say the same thing.
        private static SystemMenuTab TabFor(bool inventory) =>
            SystemMenuTab.CharacterInventory;

        // A PLAIN TOGGLE now that both keys mean one tab.
        //
        // This used to carry a switch-vs-close rule: pressing the other pane's
        // key while open switched panes rather than closing. There is nothing
        // left to switch between, and leaving the comment would have described
        // behaviour the code no longer has.
        public static void Toggle(GameObject panel, bool inventory)
        {
            if (panel == null) return;

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

                // THE PACK, now that `inventory` has somewhere real to go.
                // I opens with it up, C opens without -- Select has already
                // activated the pane synchronously (SetShown runs inline, not
                // deferred to next frame the way the menu's own Start() is),
                // so the dossier and its pack panel are live by the time this
                // runs. Found rather than wired: SheetPanel only ever knows
                // the outer system-menu panel, one field shared by three
                // callers, not the dossier underneath it.
                var dossier = panel.GetComponentInChildren<CharacterDossierController>(includeInactive: true);
                dossier?.ShowPack(inventory);
                return;
            }

            panel.SetActive(true);
        }
    }
}
