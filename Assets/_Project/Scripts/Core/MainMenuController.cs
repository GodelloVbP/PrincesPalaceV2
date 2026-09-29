using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Contract B in practice: UI references are `internal` with [SerializeField]
    // kept.
    //
    // internal, because the Editor assembly assigns them directly (see
    // Core/AssemblyInfo.cs) instead of reflecting over a field NAME the way v1
    // did at 330 sites. [SerializeField] kept, because internal fields do not
    // serialize on their own -- and a field that does not serialize is null in
    // the built player no matter how correctly the builder assigned it.
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] internal Button newRunButton;
        [SerializeField] internal Button loadButton;
        [SerializeField] internal Button settingsButton;
        [SerializeField] internal Button exitButton;
        [SerializeField] internal Button closeSaveSlotButton;
        [SerializeField] internal GameObject saveSlotPanel;

        // The Options pane in a modal. Its controller is the system menu's own
        // OptionsController, so a setting changed here persists through
        // GameSettings exactly as it does in a run. Assigned by ScreenRegistry
        // from WireOptions' return value.
        [SerializeField] internal GameObject settingsPanel;
        [SerializeField] internal Button closeSettingsButton;
        [SerializeField] internal OptionsController optionsController;

        // Shown only when a save exists to continue. Built unconditionally by
        // the screen -- Domain has no SaveSystem to ask at build time -- and
        // toggled here. The column closes the gap it leaves (ReflowMenu).
        [SerializeField] internal Button continueButton;
        [SerializeField] internal TMP_Text continueCaption;
        [SerializeField] internal TMP_Text continueSubline;
        [SerializeField] internal RectTransform menuColumn;

        // Cross-controller: the two controllers this screen splits between
        // (menu column here, the Load list itself on SaveSlotController)
        // share ONE NavContext, owned here (docs/GAMEPAD_NAVIGATION_PLAN.md).
        // Assigned by ScreenRegistry's MainMenu Wire step,
        // not auto-bound: SaveSlotController is not one of the five types
        // UiAutoBind has a typed lookup for.
        [SerializeField] internal SaveSlotController saveSlotController;

        // -1 means nothing to continue into. Set by RefreshContinue, read by
        // the button's own click handler so the two can never disagree about
        // which slot "Continue" means.
        private int _continueSlot = -1;

        // Pushed once in Start(), never popped while this scene is loaded --
        // the main menu is a whole scene, not a modal, the same shape Hub's
        // own base context uses. RECONFIGURED (never re-pushed) every time
        // the save-slot panel opens or closes, since NavContext.Reconfigure
        // is exactly Map's own case: one context whose navigable set changes
        // contents, not a fixed set with some members hidden.
        private NavContext _navContext;

        // The menu column's buttons as authored, in order, with their solved
        // top edges. Captured once: ReflowMenu recomputes from these every
        // time so repeated refreshes never accumulate a shift.
        private RectTransform[] _menuItems;
        private float[] _authoredY;
        private float[] _authoredTop;

        private void Start()
        {
            // New Run takes the lowest empty slot through the same path a
            // slot card does; only when all are full does it hand over to the
            // Load list, which is the one place a full slot can be entered.
            newRunButton.onClick.AddListener(NewRun);
            loadButton.onClick.AddListener(() =>
            {
                Toggle(saveSlotPanel);
                RefreshNavigation();
            });
            settingsButton.onClick.AddListener(() =>
            {
                settingsPanel.SetActive(true);
                RefreshNavigation();
            });
            closeSettingsButton.onClick.AddListener(CloseSettings);
            // Settled BEFORE the process goes away. Quitting is not
            // continuing the run, so it ends it, and ending it is what pays out
            // what the run earned -- quitting straight to the desktop would
            // bank nothing.
            exitButton.onClick.AddListener(() =>
            {
                RunManager.EndRun();
                // Navigation.Quit, not Application.Quit: the system menu grew a
                // second quit and one of the two had to be the door. See Navigation.
                Navigation.Quit();
            });
            closeSaveSlotButton.onClick.AddListener(() =>
            {
                saveSlotPanel.SetActive(false);
                RefreshNavigation();
            });

            continueButton.onClick.AddListener(() =>
            {
                if (_continueSlot < 0) return;
                SaveSlotManager.EnterSlot(_continueSlot);
                Navigation.Go(Navigation.Hub);
            });

            CaptureMenuLayout();
            RefreshContinue();
            RegisterNavContext();
        }

        private void NewRun()
        {
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            {
                if (SaveSystem.SlotExists(slot))
                {
                    continue;
                }

                saveSlotController.Choose(slot);
                return;
            }

            saveSlotPanel.SetActive(true);
            RefreshNavigation();
        }

        // Recomputes whether Continue has anything to continue, and what it
        // says. Called once at Start, and again from SaveSlotController
        // whenever a delete could have removed the slot Continue was pointing
        // at -- see its ConfirmDelete for why a runtime lookup reaches back
        // here instead of a wired event.
        public void RefreshContinue()
        {
            _continueSlot = SaveSystem.MostRecentSlot();
            bool hasSave = _continueSlot >= 0;

            continueButton.gameObject.SetActive(hasSave);

            if (hasSave)
            {
                continueCaption.Set(UiStrings.ContinueSlot, _continueSlot + 1);

                var facts = SaveSlotDetail.For(_continueSlot);
                continueSubline.SetContent(UiStrings.ContinueSubline.Format(
                    facts.CharacterName, facts.Floor, PlaytimeFormat.Describe(facts.PlaySeconds)));
            }

            ReflowMenu();

            // Continue can appear or vanish out from under an already-pushed
            // context (a save deleted from the Load list on top of it) --
            // reconfigure rather than assume Start() already saw the final
            // answer.
            RefreshNavigation();
        }

        private void CaptureMenuLayout()
        {
            var items = new List<RectTransform>();
            foreach (Transform child in menuColumn)
            {
                if (child.GetComponent<Button>() != null) items.Add((RectTransform)child);
            }

            _menuItems = items.ToArray();
            _authoredY = new float[_menuItems.Length];
            _authoredTop = new float[_menuItems.Length];
            for (int i = 0; i < _menuItems.Length; i++)
            {
                var rect = _menuItems[i];
                _authoredY[i] = rect.anchoredPosition.y;
                _authoredTop[i] = rect.anchoredPosition.y + rect.rect.height * (1f - rect.pivot.y);
            }
        }

        // The column is laid out once at build time with every item present;
        // moving the items below a hidden one up by the space it occupied is
        // the runtime half of that layout (ColumnReflow).
        private void ReflowMenu()
        {
            var visible = new bool[_menuItems.Length];
            for (int i = 0; i < visible.Length; i++) visible[i] = _menuItems[i].gameObject.activeSelf;

            var shifts = ColumnReflow.Shifts(_authoredTop, visible);
            for (int i = 0; i < _menuItems.Length; i++)
            {
                var position = _menuItems[i].anchoredPosition;
                position.y = _authoredY[i] + shifts[i];
                _menuItems[i].anchoredPosition = position;
            }
        }

        private static void Toggle(GameObject panel) => panel.SetActive(!panel.activeSelf);

        // ---- gamepad navigation (docs/GAMEPAD_NAVIGATION_PLAN.md) ----------------
        //
        // ONE CONTEXT FOR THE WHOLE SCENE, reconfigured rather than pushed a
        // second time whenever the save-slot modal opens or closes -- exactly
        // NavContext.Reconfigure's own documented case (Map's runtime graph
        // rewrite): the navigable set genuinely changes CONTENTS, it is not a
        // fixed set with some members hidden.
        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            _navContext = new NavContext(entry: null, selectables: null, cancel: HandleCancel);
            NavigationInputModule.Contexts?.Push(_navContext);
            RefreshNavigation();
        }

        private void OnDestroy()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        // No submenu depth and no previous screen to return to -- the main
        // menu is where the game STARTS. Closing whichever modal is
        // topmost is the one thing Cancel has to spend itself on here;
        // with everything closed there really is nowhere else to go, so
        // the press is a deliberate no-op rather than a stand-in for a
        // Back this screen does not have.
        //
        // Checked topmost-first: the confirm dialog can be active WHILE the
        // Load list underneath it still is too (ResetConfirmPanel is a sibling
        // drawn over SaveSlotPanel, not a child that goes with it), so testing
        // list-open before confirm-open would send Cancel to the wrong layer.
        private void HandleCancel()
        {
            if (saveSlotController != null && saveSlotController.IsConfirmOpen)
            {
                // Dismiss(), not a raw SetActive -- it also clears the
                // pending slot, cancels any hold in progress and puts focus
                // back on the Delete that opened the dialog.
                saveSlotController.Dismiss();
                return;
            }

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }

            if (saveSlotPanel != null && saveSlotPanel.activeSelf)
            {
                saveSlotPanel.SetActive(false);
                RefreshNavigation();
            }
        }

        // Focus goes back to the item that opened the panel, not to the
        // column's first item the way closing the Load list does.
        private void CloseSettings()
        {
            settingsPanel.SetActive(false);
            RefreshNavigation();

            if (EventSystem.current != null && settingsButton != null)
            {
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            }
        }

        // Rebuilds the declared set and entry from whichever surface is
        // showing right now -- the base menu column, the Load list, or its
        // confirm dialog -- the same "walk what's ACTUALLY active" shape
        // SystemMenuController.RefreshSelectables uses for its own tab/pane
        // split. Public, not private: SaveSlotController reaches back here
        // through GetComponentInParent after its own toggles, the same
        // cross-controller shape RefreshContinue already uses.
        public void RefreshNavigation()
        {
            if (_navContext == null) return;

            bool confirmOpen = saveSlotController != null && saveSlotController.IsConfirmOpen;
            bool settingsOpen = !confirmOpen && settingsPanel != null && settingsPanel.activeSelf;
            bool slotsOpen = !confirmOpen && !settingsOpen && saveSlotPanel != null && saveSlotPanel.activeSelf;

            Selectable entryButton;
            IEnumerable<Selectable> group;
            if (confirmOpen)
            {
                entryButton = WireConfirmNavigation();
                group = ConfirmSelectables();
            }
            else if (settingsOpen)
            {
                entryButton = WireSettingsNavigation();
                group = SettingsSelectables();
            }
            else if (slotsOpen)
            {
                entryButton = WireSaveSlotNavigation();
                group = SaveSlotSelectables();
            }
            else
            {
                entryButton = WireMainMenuNavigation();
                group = MainMenuSelectables();
            }

            // NavContext.Entry is read back through `as GameObject`
            // (NavigationInputModule.ReselectIfOutsideDeclaredSet) -- a
            // Button reference there would fail that cast silently and
            // leave selection null forever, so it is converted here, once.
            object entry = entryButton != null ? entryButton.gameObject : null;

            var selectables = new Dictionary<string, object>();
            foreach (var selectable in group)
            {
                if (selectable != null) selectables[selectable.name] = selectable.gameObject;
            }

            // No forced SetSelectedGameObject here, deliberately -- unlike
            // PushNavContext's first-open case, an ordinary Reconfigure must
            // not clobber a selection that is STILL declared (a Continue
            // label refresh must not knock focus off of Exit). Whatever the
            // player had selected either survives (still in the new set) or
            // fails NavigationInputModule.ReselectIfOutsideDeclaredSet's own
            // check next frame and falls back to this entry then -- the same
            // one-frame-later resolution SystemMenuController.Select relies
            // on for its own tab-to-pane swap.
            _navContext.Reconfigure(entry, selectables);
        }

        // The menu column top to bottom, Continue only when shown -- a List,
        // clamp, no wrap: there is nothing below Quit or above the first item
        // to step onto.
        private IEnumerable<Button> MainMenuSelectables()
        {
            if (continueButton != null && continueButton.gameObject.activeSelf) yield return continueButton;
            yield return newRunButton;
            yield return loadButton;
            yield return settingsButton;
            yield return exitButton;
        }

        // Every Selectable under the panel, the way SystemMenuController
        // declares its Options pane: a click on a stepper button must not be
        // read as leaving the declared set. Rows Up/Down through the group
        // OptionsController.Wire applied; Left/Right on a row is OptionRow's
        // own OnMove. Restore and Close are chained below the last row.
        private IEnumerable<Selectable> SettingsSelectables()
        {
            if (settingsPanel == null) return System.Array.Empty<Selectable>();
            return settingsPanel.GetComponentsInChildren<Selectable>(includeInactive: false);
        }

        private Selectable WireSettingsNavigation()
        {
            var rows = optionsController != null && optionsController.rows != null
                ? optionsController.rows.Where(r => r != null).ToList()
                : new List<OptionRow>();
            var restore = optionsController != null ? optionsController.restoreDefaults : null;

            var links = new List<UiNavLink<Selectable>?>();
            if (rows.Count > 0)
            {
                links.AddRange(RuntimeNavWiring.LinkBoth(rows[rows.Count - 1], UiNavDirection.Down, restore));
            }

            links.AddRange(RuntimeNavWiring.LinkBoth(restore, UiNavDirection.Down, closeSettingsButton));

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("optionsRows", UiNavGroupKind.List, rows), links);

            return rows.Count > 0 ? rows[0] : closeSettingsButton;
        }

        private Button WireMainMenuNavigation()
        {
            var members = MainMenuSelectables().ToList();
            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("mainMenuButtons", UiNavGroupKind.List, members));
            return members.Count > 0 ? members[0] : null;
        }

        // Each slot is a card and, when the slot is filled, its Delete to the
        // card's right. Cards are a List, top to bottom (matching
        // SaveSlotColumn's own Ui.Column). A Delete is in no group: its
        // links are explicit -- Left back to its card, Up/Down to the
        // neighbouring card, and the last one Down to the footer.
        private IEnumerable<Button> SaveSlotSelectables()
        {
            if (saveSlotController == null) yield break;

            var slots = saveSlotController.slotButtons ?? System.Array.Empty<Button>();
            var deletes = saveSlotController.deleteButtons ?? System.Array.Empty<Button>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) yield return slots[i];
                if (i < deletes.Length && deletes[i] != null && deletes[i].gameObject.activeSelf) yield return deletes[i];
            }

            // closeSaveSlotButton lives on THIS controller, not
            // SaveSlotController -- it is the same button MainMenuController
            // already wires in Start().
            if (closeSaveSlotButton != null) yield return closeSaveSlotButton;
        }

        private Button WireSaveSlotNavigation()
        {
            if (saveSlotController == null) return null;

            var slots = saveSlotController.slotButtons ?? System.Array.Empty<Button>();
            var deletes = saveSlotController.deleteButtons ?? System.Array.Empty<Button>();
            var cards = slots.Where(b => b != null).ToList();

            // Where Down from a Delete lands: the next card, or the footer
            // after the last one.
            var links = new List<UiNavLink<Selectable>?>();
            if (cards.Count > 0 && closeSaveSlotButton != null)
            {
                links.AddRange(RuntimeNavWiring.LinkBoth(cards[cards.Count - 1], UiNavDirection.Down, closeSaveSlotButton));
            }

            for (int i = 0; i < slots.Length && i < deletes.Length; i++)
            {
                if (slots[i] == null || deletes[i] == null || !deletes[i].gameObject.activeSelf) continue;

                links.AddRange(RuntimeNavWiring.LinkBoth(slots[i], UiNavDirection.Right, deletes[i]));
                if (i > 0) links.Add(RuntimeNavWiring.Link(deletes[i], UiNavDirection.Up, slots[i - 1]));
                links.Add(RuntimeNavWiring.Link(deletes[i], UiNavDirection.Down,
                    i + 1 < slots.Length ? slots[i + 1] : closeSaveSlotButton));
            }

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("saveSlots", UiNavGroupKind.List, cards), links);

            return cards.Count > 0 ? cards[0] : closeSaveSlotButton;
        }

        // Yes then No, left to right (ResetConfirmButtons' own Row order) --
        // a Rail, wrap by default. Entry is forced to No rather
        // than following the Rail's own first member: the destructive
        // button needs a HELD press to do anything at all (a tap of Submit on
        // Yes is inert), but the focus marker still points at whatever
        // is selected, and resting that arrow on the delete button the
        // instant this dialog opens is the wrong default regardless.
        private IEnumerable<Button> ConfirmSelectables()
        {
            if (saveSlotController == null) yield break;
            if (saveSlotController.confirmYesButton != null) yield return saveSlotController.confirmYesButton;
            if (saveSlotController.confirmNoButton != null) yield return saveSlotController.confirmNoButton;
        }

        private Button WireConfirmNavigation()
        {
            if (saveSlotController == null) return null;

            var members = ConfirmSelectables().ToList();
            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("resetConfirmButtons", UiNavGroupKind.Rail, members));

            if (saveSlotController.confirmNoButton != null) return saveSlotController.confirmNoButton;
            return members.Count > 0 ? members[0] : null;
        }
    }
}
