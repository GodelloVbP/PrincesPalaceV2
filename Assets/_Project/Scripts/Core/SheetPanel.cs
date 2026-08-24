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

        // THE SWITCH-VS-CLOSE RULE IS BACK, retargeted at the pack rather than
        // at a tab. It genuinely had nothing left to switch between for the
        // stretch between the tab merge and the pack argument being wired to
        // anything -- the comment above used to say so correctly. Now that I
        // and C each mean a real state (pack up / pack down), the rule they
        // were written for applies again: pressing the SAME key a second time
        // closes the sheet, pressing the OTHER key while it is open switches
        // to that key's pack state instead of closing it. A plain
        // `!panel.activeSelf` toggle got this wrong in exactly the way that
        // rule exists to prevent -- opening with I, then pressing C to see
        // the pack go down, closed the whole sheet instead, because closed-
        // vs-open was the only state it was checking.
        public static void Toggle(GameObject panel, bool inventory)
        {
            if (panel == null) return;

            bool alreadyShowingThis = panel.activeSelf && CurrentPackState(panel) == inventory;
            Set(panel, !alreadyShowingThis, inventory);
        }

        private static bool CurrentPackState(GameObject panel)
        {
            var dossier = panel.GetComponentInChildren<CharacterDossierController>(includeInactive: true);
            return dossier != null && dossier.IsPackShown;
        }

        public static void Set(GameObject panel, bool open, bool inventory = false)
        {
            if (panel == null) return;

            // FOUND BEFORE THE BRANCH, because CLOSING needs it too, and that
            // asymmetry is the bug this shape exists to prevent.
            //
            // Opening went through SystemMenuController.Open(), which PAUSES
            // the game -- Time.timeScale = 0, restored by its Close(). Closing
            // went straight to SetActive(false) and never reached Close(), so
            // every C-then-C left the clock stopped with nothing on screen to
            // say so. The controller sits on the scene root rather than on
            // `panel`, so deactivating the panel does not even fire its
            // OnDisable safety net.
            //
            // What that cost, as reported: win a fight, press C or I over the
            // Reckoning, press it again, and the run cannot be continued. The
            // reward screen itself runs on unscaled time and looks fine, so
            // there is no visible symptom until the map loads and the walk to
            // the next room -- Time.deltaTime, like the rest of the map --
            // never advances a single step.
            //
            // Escape was never affected and does not rescue it either: the
            // Reckoning is one of the fight's escapeConsumers, so with it up
            // Escape is declined rather than handled.
            var menu = panel.GetComponentInParent<SystemMenuController>();

            if (!open)
            {
                // Through the menu when there is one, so the pause it took is
                // the pause it gives back. SetActive stays as the fallback for
                // a panel mounted without a controller above it -- a headless
                // fixture, or a future screen that reuses this seam.
                if (menu != null) menu.Close();
                else panel.SetActive(false);
                return;
            }

            // IN A PARENT, not on `panel` itself. `panel` is what every caller
            // passes as "the sheet" -- menu.Root, the visual panel that
            // actually toggles active/inactive, which is what IsOpen has to
            // read. SystemMenuController lives one level up, on the SCENE
            // ROOT, deliberately (see WireSystemMenu's own comment: attached
            // there so its Update() keeps listening for Escape while the menu
            // itself is closed and inactive). GetComponent<SystemMenuController>
            // on `panel` was always null, silently -- Open(), Select() and the
            // ShowPack(inventory) call below never ran, for either key, in any
            // scene; C and I only ever worked by accident, through the plain
            // SetActive(true) fallback at the bottom of this method, which is
            // why the sheet opened and closed but never actually landed on
            // the right tab or the right pack state.
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
