using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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
        [SerializeField] internal Button exitButton;
        [SerializeField] internal Button closeSaveSlotButton;
        [SerializeField] internal GameObject saveSlotPanel;

        // Shown only when a save exists to continue. Built unconditionally by
        // the screen -- Domain has no SaveSystem to ask at build time -- and
        // toggled here. The column closes the gap it leaves (ReflowMenu).
        [SerializeField] internal Button continueButton;
        [SerializeField] internal TMP_Text continueCaption;
        [SerializeField] internal TMP_Text continueSubline;
        [SerializeField] internal RectTransform menuColumn;

        // Cross-controller: the two controllers this screen splits between
        // (Play/Exit/Continue here, the slot list itself on SaveSlotController)
        // share ONE NavContext, owned here (docs/GAMEPAD_NAVIGATION_PLAN.md).
        // Assigned by ScreenRegistry's MainMenu Wire step,
        // not auto-bound: SaveSlotController is not one of the five types
        // UiAutoBind has a typed lookup for.
        [SerializeField] internal SaveSlotController saveSlotController;

        // Cross-controller, same reasoning as saveSlotController above: the
        // Manage Saves list and its own reset-confirm dialog (AUDIT.md #159)
        // join the same shared NavContext rather than getting one of their
        // own -- Reconfigure already handles a screen whose navigable set
        // changes shape across more than two states (this makes four:
        // base menu, save-slot list, manage-saves list, confirm dialog).
        [SerializeField] internal ResetProgressController resetProgressController;

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
        // says. Called once at Start, and again from ResetProgressController
        // whenever a delete could have removed the slot Continue was pointing
        // at -- see that class's ConfirmDelete for why a runtime lookup reaches
        // back here instead of a wired event.
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
            // context (a save deleted through Manage Saves while its own
            // panel sits on top) -- reconfigure rather than assume Start()
            // already saw the final answer.
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
        // Checked topmost-first (AUDIT.md #159): the confirm dialog can be
        // active WHILE the manage-saves panel underneath it still is too
        // (ResetConfirmPanel is a sibling drawn over ManageSavesPanel, not
        // a child that goes with it), so testing manage-open before
        // confirm-open would send Cancel to the wrong layer.
        private void HandleCancel()
        {
            if (resetProgressController != null && resetProgressController.IsConfirmOpen)
            {
                // Dismiss(), not a raw SetActive -- it also clears the
                // pending slot and cancels any hold in progress, the same
                // state ConfirmNoButton's own click already resets.
                resetProgressController.Dismiss();
                return;
            }

            if (resetProgressController != null && resetProgressController.gameObject.activeSelf)
            {
                resetProgressController.GoBack();
                return;
            }

            if (saveSlotPanel != null && saveSlotPanel.activeSelf)
            {
                saveSlotPanel.SetActive(false);
                RefreshNavigation();
            }
        }

        // Rebuilds the declared set and entry from whichever surface is
        // showing right now -- the base menu column, the
        // save-slot modal, the manage-saves list, or its own confirm
        // dialog -- the same "walk what's ACTUALLY active" shape
        // SystemMenuController.RefreshSelectables uses for its own
        // tab/pane split. Public, not private: ResetProgressController and
        // SaveSlotController both reach back here through
        // GetComponentInParent after their own panel toggles, the same
        // cross-controller shape RefreshContinue already uses.
        public void RefreshNavigation()
        {
            if (_navContext == null) return;

            bool confirmOpen = resetProgressController != null && resetProgressController.IsConfirmOpen;
            bool manageOpen = !confirmOpen && resetProgressController != null &&
                resetProgressController.gameObject.activeSelf;
            bool slotsOpen = !confirmOpen && !manageOpen && saveSlotPanel != null && saveSlotPanel.activeSelf;

            Button entryButton;
            IEnumerable<Button> group;
            if (confirmOpen)
            {
                entryButton = WireConfirmNavigation();
                group = ConfirmSelectables();
            }
            else if (manageOpen)
            {
                entryButton = WireManageNavigation();
                group = ManageSelectables();
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
            yield return exitButton;
        }

        private Button WireMainMenuNavigation()
        {
            var members = MainMenuSelectables().ToList();
            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("mainMenuButtons", UiNavGroupKind.List, members));
            return members.Count > 0 ? members[0] : null;
        }

        // The slot column (a List, top to bottom, matching SaveSlotColumn's
        // own Ui.Column) plus the footer row (a Rail: ManageSaves then
        // Cancel, matching SaveSlotFooter's own Ui.Row) -- linked together
        // by one explicit Down/Up pair off the last slot, the inter-group
        // jump neither group's own order can express.
        private IEnumerable<Button> SaveSlotSelectables()
        {
            if (saveSlotController == null) yield break;

            if (saveSlotController.slotButtons != null)
            {
                foreach (var slot in saveSlotController.slotButtons)
                {
                    if (slot != null) yield return slot;
                }
            }

            if (saveSlotController.manageSavesButton != null) yield return saveSlotController.manageSavesButton;
            // closeSaveSlotButton lives on THIS controller, not
            // SaveSlotController -- it is the same button MainMenuController
            // already wires in Start() (Cancel(), above, reaches the
            // identical SetActive(false)).
            if (closeSaveSlotButton != null) yield return closeSaveSlotButton;
        }

        private Button WireSaveSlotNavigation()
        {
            if (saveSlotController == null) return null;

            var slots = (saveSlotController.slotButtons ?? System.Array.Empty<Button>())
                .Where(b => b != null).ToList();
            var footer = new List<Button>();
            if (saveSlotController.manageSavesButton != null) footer.Add(saveSlotController.manageSavesButton);
            if (closeSaveSlotButton != null) footer.Add(closeSaveSlotButton);

            var links = new List<UiNavLink<Selectable>?>();
            if (slots.Count > 0 && footer.Count > 0)
            {
                links.AddRange(RuntimeNavWiring.LinkBoth(slots[slots.Count - 1], UiNavDirection.Down, footer[0]));
            }

            RuntimeNavWiring.Apply(
                new[]
                {
                    RuntimeNavWiring.Group("saveSlots", UiNavGroupKind.List, slots),
                    RuntimeNavWiring.Group("saveSlotFooter", UiNavGroupKind.Rail, footer),
                },
                links);

            return slots.Count > 0 ? slots[0] : footer.Count > 0 ? footer[0] : null;
        }

        // The delete column (a List, top to bottom, matching
        // ManageSavesColumn's own layout) plus Back, reached by an explicit
        // Down/Up pair off the last row -- the same cross-surface link
        // WireSaveSlotNavigation uses for its own slots/footer split. Back
        // gets no group of its own: a Rail/List of one has no internal
        // neighbour to link (UiNavLinkBuilder.BuildGroup's own single-member
        // guard), so unlike the save-slot footer (two buttons, a real Rail)
        // a bare group here would do nothing but state the obvious.
        private IEnumerable<Button> ManageSelectables()
        {
            if (resetProgressController == null) yield break;

            if (resetProgressController.deleteButtons != null)
            {
                foreach (var delete in resetProgressController.deleteButtons)
                {
                    if (delete != null) yield return delete;
                }
            }

            if (resetProgressController.backButton != null) yield return resetProgressController.backButton;
        }

        private Button WireManageNavigation()
        {
            if (resetProgressController == null) return null;

            var deletes = (resetProgressController.deleteButtons ?? System.Array.Empty<Button>())
                .Where(b => b != null).ToList();
            var footer = new List<Button>();
            if (resetProgressController.backButton != null) footer.Add(resetProgressController.backButton);

            var links = new List<UiNavLink<Selectable>?>();
            if (deletes.Count > 0 && footer.Count > 0)
            {
                links.AddRange(RuntimeNavWiring.LinkBoth(deletes[deletes.Count - 1], UiNavDirection.Down, footer[0]));
            }

            RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("manageDeletes", UiNavGroupKind.List, deletes),
                links);

            return deletes.Count > 0 ? deletes[0] : footer.Count > 0 ? footer[0] : null;
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
            if (resetProgressController == null) yield break;
            if (resetProgressController.confirmYesButton != null) yield return resetProgressController.confirmYesButton;
            if (resetProgressController.confirmNoButton != null) yield return resetProgressController.confirmNoButton;
        }

        private Button WireConfirmNavigation()
        {
            if (resetProgressController == null) return null;

            var members = ConfirmSelectables().ToList();
            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("resetConfirmButtons", UiNavGroupKind.Rail, members));

            if (resetProgressController.confirmNoButton != null) return resetProgressController.confirmNoButton;
            return members.Count > 0 ? members[0] : null;
        }
    }
}
