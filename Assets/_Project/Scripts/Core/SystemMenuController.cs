using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Drives the overarching menu: open, close, which tabs exist, and which one
    // is showing.
    //
    // IT LAYS THE BAR OUT AT RUNTIME, which reads like a violation of this
    // project's generated-scenes rule and is not one. The tab SET depends on
    // context -- five tabs at camp, six in a descent -- and a scene is
    // generated once, so the build authors every tab at its six-tab position
    // and this re-applies SystemMenuLayout for whichever set is showing. The
    // arithmetic is still in exactly one place. See SystemMenuScreen's header.
    public class SystemMenuController : MonoBehaviour, INavTabStrip
    {
        [SerializeField] internal GameObject panel;
        [SerializeField] internal Button[] tabButtons;
        [SerializeField] internal GameObject[] tabHovers;
        [SerializeField] internal GameObject[] tabUnderlines;
        [SerializeField] internal GameObject[] tabDividers;
        [SerializeField] internal GameObject[] panes;

        // The lintel, above the panel.
        [SerializeField] internal TMP_Text runTitle;
        [SerializeField] internal TMP_Text contextLine;
        [SerializeField] internal TMP_Text goldValue;
        [SerializeField] internal TMP_Text embersValue;
        [SerializeField] internal Button closeButton;

        // The mode pill (plan C10): the plate, moved and sized to its words,
        // and the label inside it.
        [SerializeField] internal RectTransform modePill;
        [SerializeField] internal TMP_Text modePillLabel;

        // The footer below the panel: the one notice line (plan C11) and the
        // prompt row (plan C12). Both are reached through this controller, so
        // no pane holds a reference to either.
        [SerializeField] internal MenuNotice notice;
        [SerializeField] internal MenuPromptFooter footer;

        // Which part of the game this scene is (MenuContext). Set at build time
        // by ScreenRegistry.WireSystemMenu, and every pane reads it here rather
        // than keeping a flag of its own; MenuPermissions turns it into what a
        // control may change.
        //
        // Not RunManager.HasRun. A run exists from the moment the relic draft
        // is rolled, and that draft is offered IN THE HUB and deliberately
        // survives leaving and coming back -- RelicDraftTests pins that. So
        // HasRun is true while the player stands in the hub with no descent
        // under way, and the run tabs appeared on a screen with no floor and no
        // run to describe. Which scene you are in cannot be got wrong at
        // runtime.
        //
        // Nor is it RunManager.DescentIsUnderWay, the question EndRun asks
        // before it strips gear: on the map at the entry node this scene is part
        // of a descent while nothing has moved yet, so DescentIsUnderWay is
        // still false and the run tabs must still show.
        [SerializeField] internal MenuContext context;

        private bool _wired;
        private int _selected;

        // Whether anything has chosen a tab yet.
        //
        // Start() runs on the frame AFTER the object is activated, so a caller
        // that did Open() then Select(2) in one breath had its choice silently
        // reset a frame later -- which is how the first screenshot of this menu
        // came out showing the Options pane with the Character tab underlined.
        // The default is a fallback, not an override.
        private bool _chosen;

        // What the game was running at before this menu paused it.
        private float _resumeTimeScale = 1f;
        private bool _paused;

        private List<int> _visible = new List<int>();

        // Pushed in Open(), popped in Close() -- never OnEnable/OnDisable's
        // own timing, since content is populated AFTER activation (plan
        // section 2) and a context pushed before that would declare
        // Selectables that do not exist yet. The nested-modal shape plan
        // section 3/4 describes: while this is open it is TOP, so Hub's own
        // Cancel (which is what opened it) cannot also fire the same frame,
        // and this context's own Cancel (Close) is what a player's next
        // Cancel press reaches.
        private NavContext _navContext;

        public bool IsOpen => panel != null && panel.activeSelf;

        public int SelectedIndex => _selected;

        public MenuContext Context => context;

        // Whether this scene is part of a descent (the map and a fight): the
        // tab set and the Exit pane's abandon card key on it.
        public bool InDescent => context != MenuContext.Camp;

        // Whether this context has the tab at all: a run-only tab is absent at
        // camp. Answers from the context, not from a layout that has not run.
        public bool HasTab(SystemMenuTab tab) => !SystemMenuTabs.Def(tab).RunOnly || InDescent;

        // The tabs on show, as indices into SystemMenuTabs.All. Exposed because
        // "which tabs does this context have" is the single most testable claim
        // the context rule makes.
        public IReadOnlyList<int> VisibleTabs => _visible;

        private void Start()
        {
            Wire();
            if (!_chosen) SelectCore(SystemMenuTabs.IndexOf(MenuMemory.TabFor(context)), audible: false);
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (tabButtons == null) return;

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int index = i;
                tabButtons[i].onClick.AddListener(() => Select(index));

                // Hover is a PLATE, not a scale. The skeleton popped the tab to
                // 1.03, which wobbles a bar whose positions are arithmetic and
                // makes a tab harder to hit as you approach it.
                var hover = tabButtons[i].gameObject.AddComponent<HoverIndex>();
                hover.Index = index;
                hover.Changed = OnTabHover;
            }
        }

        private void OnTabHover(int index, bool entered)
        {
            if (tabHovers == null || index < 0 || index >= tabHovers.Length) return;

            // Never on a tab this context does not have, and never lit on the
            // selected tab -- the underline already says which one that is, and
            // two markers on one tab reads as a stuck hover.
            bool show = entered && _visible.Contains(index) && index != _selected;
            tabHovers[index].SetShown(show);
        }

        // THE ONE PATH TO THE OVERARCHING MENU FROM A ROOT CONTEXT, from
        // the hub, the map and the fight alike. Each root hangs it on its
        // context's `systemMenu` handler, so Start and Escape reach it and
        // Cancel does not.
        //
        // Static and null-tolerant on purpose. Its three callers each hold
        // this controller as a [SerializeField] a fixture scene may never
        // wire, and each would otherwise write its own null check and its
        // own already-open guard -- three copies of one rule. Returns
        // whether it opened, for a caller that has something else to do
        // when it did not. Opens on the remembered tab (MenuMemory).
        public static bool OpenFromRoot(SystemMenuController menu)
        {
            if (menu == null || menu.IsOpen) return false;

            menu.Open();
            return true;
        }

        // On the tab the player last used here, else the context's default.
        public void Open() => Open(MenuMemory.TabFor(context));

        // THE ONE WAY IN. Every caller -- the roots' Start, the hotkeys, the
        // pad's View button, the hub's buildings and corner button, the sheet
        // seam -- ends up here, so what opening means (pause, nav context,
        // memory) is written once.
        //
        // Already open is a switch, not a second open: the pending state of
        // the tab being left is cancelled first (plan C6) and the pause and
        // the nav context, both idempotent, are left alone. A tab this
        // context does not have falls back to the reopen rule rather than
        // showing a pane no tab button leads to.
        public void Open(SystemMenuTab tab, MenuTarget target = default)
        {
            Wire();

            bool wasOpen = IsOpen;
            if (wasOpen) CancelPending();

            ApplyContext();

            int index = SystemMenuTabs.IndexOf(tab);
            if (!_visible.Contains(index))
            {
                index = SystemMenuTabs.IndexOf(MenuMemory.TabFor(context));
                if (!_visible.Contains(index)) index = _visible[0];
            }

            // Silent when it is the open itself that lands on a tab: only a
            // switch the player made is a swipe.
            SelectCore(index, audible: wasOpen);
            MenuMemory.Remember(context, SystemMenuTabs.All[index].Tab);

            panel.SetShown(true);

            // AGAIN, NOW THE PANEL IS ACTUALLY ACTIVE. ApplyContext's own
            // RefreshNavLinks call ran one line above this, while `panel` was
            // still hidden -- and ActivePaneEntry resolves through
            // GetComponentInChildren, which sees nothing under an inactive
            // hierarchy. So the selected tab's Down-into-its-pane link was
            // silently null on every first open, and only appeared once the
            // player clicked a different tab (Select's own RefreshNavLinks,
            // which does run with the panel up). The rail itself was never
            // affected -- tabButtons are passed in by reference, not found by
            // a component walk -- which is why this went unnoticed until a
            // pane declared an entry worth reaching. Apply is authoritative,
            // so a second call costs one rewrite and cannot conflict.
            RefreshNavLinks();

            Pause();
            PushNavContext();

            ApplyTarget(SystemMenuTabs.All[index].Tab, target);
        }

        // Any close drops the active pane's pending state first: a pending
        // state never commits on close (plan C6).
        public void Close()
        {
            CancelPending();
            panel.SetShown(false);
            Resume();
            PopNavContext();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // The tab showing, as a value rather than an index into the table.
        public SystemMenuTab SelectedTab => SystemMenuTabs.All[_selected].Tab;

        // DROPS THE PENDING STATE THE ACTIVE PANE HOLDS, with no change to game
        // state (plan C5, C6): the dossier's SlotArmed or BookPicked, Party's
        // Carry. The ONE SEAM for "let go before anything else happens" --
        // Close, a tab switch, a hotkey and a deep link all come through here
        // (IMenuPending), so no path can leave a pending state behind or let
        // it commit on its own.
        public void CancelPending()
        {
            var activePane = ActivePane();
            if (activePane == null) return;

            foreach (var owner in activePane.GetComponentsInChildren<IMenuPending>(includeInactive: true))
            {
                if (owner.HasPending) owner.CancelPending();
            }
        }

        // Whether the thing a target names is already on screen, so a hotkey
        // can tell "go there" from "already there" (MenuHotkeyRule).
        public bool TargetIsShowing(MenuTarget target)
        {
            if (!IsOpen || SelectedTab != SystemMenuTab.Character) return false;
            if (!target.Is(MenuTargetKind.Section, MenuSections.Pack)) return false;

            var dossier = Dossier();
            return dossier != null && dossier.IsPackFocused;
        }

        // Points the tab at its target. A kind the tab has no place for yet is
        // ignored, so a target can name something a later phase builds.
        private void ApplyTarget(SystemMenuTab tab, MenuTarget target)
        {
            if (target.IsNone) return;

            // A relic: page the RUN pane to it and put focus on its row.
            if (tab == SystemMenuTab.Run)
            {
                if (target.Kind == MenuTargetKind.Relic) PaneOf<RunController>(tab)?.ShowRelic(target.Id);
                return;
            }

            // A glossary entry: its category, its page, its row.
            if (tab == SystemMenuTab.Glossary)
            {
                if (target.Kind == MenuTargetKind.GlossaryEntry) PaneOf<GlossaryController>(tab)?.ShowEntry(target.Id);
                return;
            }

            if (tab != SystemMenuTab.Character) return;

            var dossier = Dossier();
            if (dossier == null) return;

            // The character first: choosing one repaints the pane, and the pack
            // that follows should be the character asked for's.
            if (target.Kind == MenuTargetKind.CharacterId) dossier.SelectCharacter(target.Id);
            if (target.Is(MenuTargetKind.Section, MenuSections.Pack)) dossier.FocusPack();
        }

        // The controller a tab's pane hosts. Found rather than wired: the menu
        // only knows its panes, and each pane's controller is its own.
        private T PaneOf<T>(SystemMenuTab tab) where T : Component
        {
            int pane = SystemMenuTabs.PaneIndexFor(SystemMenuTabs.IndexOf(tab));
            return panes != null && pane < panes.Length && panes[pane] != null
                ? panes[pane].GetComponentInChildren<T>(includeInactive: true)
                : null;
        }

        private CharacterDossierController Dossier() => PaneOf<CharacterDossierController>(SystemMenuTab.Character);

        // Restores the clock even if the object is torn down while open -- a
        // scene change with the menu up would otherwise leave the next scene
        // running at timeScale 0, which looks like a hang and is not one.
        // Also the OnDisable/OnDestroy safety net plan section 4 calls for:
        // an external deactivation (a parent hidden, a scene unload) must
        // still remove this context wherever it sits.
        private void OnDisable()
        {
            Resume();
            PopNavContext();
        }

        // ---- navigation stack ----------------------------------------------

        // Idempotent: Open() can in principle run again against an
        // already-open menu (nothing currently guards it), and pushing a
        // second context for the same controller would leave a stale entry
        // under the stack no Close() will ever reach.
        private void PushNavContext()
        {
            if (_navContext == null)
            {
                // Cancel is Close -- UNLESS the active pane claims the press
                // first (INavCancelClaim, plan section 8): Party carrying a
                // character means Cancel puts them back down, and only a
                // pane with nothing open lets this Close run. Options and
                // RewardTrack simply do not implement the interface, so they
                // are the unclaimed case by construction rather than by a
                // check somebody has to remember to write.
                // tabStrip: () => this, not a claimant-shaped lookup -- this
                // controller IS the one INavTabStrip this context ever has,
                // unlike ActivePaneCancelClaim's "ask whichever pane is
                // active right now" (NavContext's own header on why the
                // shape still stays a function).
                // systemMenu: the same press that opened this menu closes it
                // again. It goes through the claimant check too, and not
                // as a nicety: Start over a Party carry has to put the
                // character back rather than shut the menu on an open
                // transaction, which is the identical hazard INavCancelClaim
                // was written for -- so it is the identical check, not a
                // second one that could answer differently.
                _navContext = new NavContext(entry: null, selectables: null, cancel: Close,
                    claimant: ActivePaneCancelClaim, tabStrip: () => this,
                    systemMenu: CloseUnlessTheActivePaneClaimsIt,
                    sectionStrip: ActivePaneSectionStrip,
                    // The menu answers its own hotkeys, so C, I, P and View
                    // switch tabs or close it from inside (plan C6).
                    menuHotkey: hotkey => MenuHotkeys.Press(this, hotkey));
            }

            // PushIfAbsent, not Push: this context outlives its time on the
            // stack now (see PopNavContext), so re-opening the menu puts the
            // SAME instance -- and the focus memory it carries -- back on the
            // stack rather than building a fresh one that could only ever
            // land on its entry.
            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);

            RefreshSelectables();

            // Push selects remembered ?? entry IMMEDIATELY (plan section
            // 4) -- not left for the dispatcher's own next-frame
            // reselection rule to pick up, which would leave the menu
            // showing no selection for the one frame between Open() and
            // the next Process() call. Through the dispatcher's own
            // SelectionFor, so this push resolves memory by exactly the rule
            // the reselection step would have applied a frame later -- the
            // hidden/destroyed fallback included, which matters here because
            // a pane can hide the very control the last visit left focused
            // (RewardTrack's collect button).
            EventSystem.current?.SetSelectedGameObject(NavigationInputModule.SelectionFor(_navContext));
        }

        private void PopNavContext()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);

            // _navContext is NOT nulled. It carries this screen's focus
            // memory (plan section 4: "remembered focus by stable id"), and
            // the menu is re-opened constantly -- a context recreated on the
            // next Open() would land on its entry every single time, which
            // is the whole of AUDIT.md #163's second half. What leaves is its
            // place on the stack; what stays is where the player was.
            //
            // Nothing to reselect-away-from here: NavigationInputModule's
            // own post-dispatch rule now reads the CURRENT top (this fix
            // landed alongside this file, see that method's own comment for
            // why it has to), so whether Close() ran from Cancel or from a
            // mouse click on the lintel's own button, the very next
            // resolution step already asks Hub (or whatever is left on the
            // stack) what it wants selected -- never this now-removed
            // context's stale entry.
        }

        // Rebuilds this context's declared Selectable set and entry from
        // whatever is CURRENTLY active -- the tab bar plus every Selectable
        // under the currently-shown pane, walked generically rather than
        // enumerated per pane. A pane this phase has not given its own
        // navigation groups (Dossier, Main menu) still
        // needs its buttons counted here, or the dispatcher's
        // own reselection rule (NavigationInputModule.
        // ReselectIfOutsideDeclaredSet) would force a mouse click on one of
        // them straight back to the tab bar every single frame -- an
        // incomplete declared set is not a smaller feature, it is a bug that
        // fights the player's own mouse.
        //
        // Called from PushNavContext (on Open) and from Select (a tab
        // change swaps which pane is active) -- never from ApplyContext
        // alone, since that runs on every Open() even when the selected tab
        // has not changed and Select() already covers that path.
        private void RefreshSelectables()
        {
            if (_navContext == null) return;

            var selectables = new Dictionary<string, object>();
            var all = panel == null
                ? System.Array.Empty<Selectable>()
                : panel.GetComponentsInChildren<Selectable>(includeInactive: true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) selectables[$"s{i}"] = all[i].gameObject;
            }

            // WHERE OPENING LANDS: the pane's entry (spec section 9), the same
            // control LT/RT land on, else the selected tab.
            var paneEntry = ActivePaneEntry();
            var entry = paneEntry != null && paneEntry.gameObject.activeInHierarchy
                ? paneEntry.gameObject
                : _selected >= 0 && tabButtons != null && _selected < tabButtons.Length
                    ? TabObject(_selected)
                    : null;

            _navContext.Reconfigure(entry, selectables);
        }

        // THE WHOLE TAB SURFACE, in one authoritative pass: the visible
        // tabs as a Rail group, wrap by default, plus the selected tab's
        // Down link into its pane's own
        // first Selectable ("each tab pane's entry as the Down link from the
        // tab"), found generically in hierarchy order rather than reached
        // for by name, so Options' rows today and whatever a not-yet-migrated
        // pane grows tomorrow both get it for free.
        //
        // ONE CALL FOR BOTH, because RuntimeNavWiring.Apply writes all four
        // directions of every node it resolves -- the rail and the link are
        // two halves of the same declaration, not two passes that have to
        // trust what the other left behind.
        //
        // Rewired here rather than declared once at build time because WHICH
        // tabs are visible is a runtime fact ApplyContext has just
        // recomputed, and the six-tab authored layout on disk is not what a
        // five-tab context shows (SystemMenuScreen's own header explains why
        // the scene cannot know that). Only VISIBLE tabs are chained; a
        // hidden tab's Explicit links are left whatever they were, which does
        // not matter -- it is inactive, and Unity never routes a Move onto an
        // inactive Selectable.
        //
        // Internal so a pane whose entry moves under it (the glossary's is the
        // category being read) can re-point the tab's Down link when it does.
        internal void RefreshNavLinks()
        {
            if (tabButtons == null) return;

            var visibleButtons = new List<Selectable>(_visible.Count);
            foreach (int index in _visible)
            {
                if (index >= 0 && index < tabButtons.Length && tabButtons[index] != null)
                {
                    visibleButtons.Add(tabButtons[index]);
                }
            }

            RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("systemMenuTabs", UiNavGroupKind.Rail, visibleButtons),
                new[] { RuntimeNavWiring.Link(SelectedTabButton(), UiNavDirection.Down, ActivePaneEntry()) });
        }

        // The selected tab's button, for a pane that links Up to it.
        internal Selectable SelectedTabSelectable => SelectedTabButton();

        private Selectable SelectedTabButton() =>
            tabButtons != null && _selected >= 0 && _selected < tabButtons.Length ? tabButtons[_selected] : null;

        // THE PANE'S OWN ANSWER FIRST (INavPaneEntry), then the generic one.
        // The generic one -- first active Selectable in hierarchy order -- is
        // the tree's order rather than the screen's subject, which is right
        // for a pane this phase has not reached (it still gets reached) and
        // wrong for one that knows what it is about: the dossier's first
        // Selectable is the roster pager, not the loadout. See
        // INavPaneEntry's own header.
        private Selectable ActivePaneEntry()
        {
            var activePane = ActivePane();
            if (activePane == null) return null;

            var declared = activePane.GetComponentInChildren<INavPaneEntry>();
            var entry = declared?.NavEntry;
            if (entry != null) return entry;

            return activePane.GetComponentsInChildren<Selectable>(includeInactive: false).FirstOrDefault();
        }

        private GameObject ActivePane()
        {
            int paneIndex = SystemMenuTabs.PaneIndexFor(_selected);
            return paneIndex >= 0 && panes != null && paneIndex < panes.Length ? panes[paneIndex] : null;
        }

        // Asked at the moment of the press, never cached: which pane is
        // active changes under this one context every time a tab is selected
        // (NavContext's own comment on why the claimant is a function).
        private INavCancelClaim ActivePaneCancelClaim()
        {
            var activePane = ActivePane();
            return activePane == null ? null : activePane.GetComponentInChildren<INavCancelClaim>();
        }

        private INavSectionStrip ActivePaneSectionStrip()
        {
            var activePane = ActivePane();
            return activePane == null ? null : activePane.GetComponentInChildren<INavSectionStrip>();
        }

        // WHAT START DOES WHILE THIS MENU IS TOP. It is NavContext.RaiseCancel's
        // precedence restated for the other button, and it is written out here
        // rather than reusing RaiseCancel because the dispatcher must be able
        // to tell "this context answered Start" from "this context answered
        // Cancel" -- one keyboard key (escape) drives both axes, so the two
        // presses are indistinguishable at the source and only the handler
        // that ran can say which one was spent.
        private void CloseUnlessTheActivePaneClaimsIt()
        {
            var claimant = ActivePaneCancelClaim();
            if (claimant != null && claimant.ClaimCancel())
            {
                SoundController.Play(Sound.MenuClose);
                return;
            }

            Close();
            SoundController.Play(Sound.MenuClose);
        }

        private void Pause()
        {
            if (_paused) return;
            _paused = true;

            // NEVER CAPTURE A STOPPED CLOCK.
            //
            // Close() puts back whatever this took, so opening the menu while
            // timeScale is already 0 makes the freeze PERMANENT -- and a frozen
            // clock is not a visible bug. WaitForSeconds is scaled, and the
            // whole fight is built on it: every beat coroutine simply never
            // finishes, the HUD stays locked behind IsPlaying, and it reads as
            // "the game hung and I cannot do anything".
            //
            // There should be no way to get here at zero, which is exactly why
            // this guard is worth having: the failure mode of being wrong about
            // that is unrecoverable without restarting, and the cost of being
            // right is one comparison.
            _resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            if (!_paused) return;
            _paused = false;

            // Back to what it WAS, not to 1. Anything else here would quietly
            // undo a slow-motion or fast-forward the game had set for its own
            // reasons, and battle speed is a stated Options control.
            Time.timeScale = _resumeTimeScale;
        }

        // ---- context ------------------------------------------------------------

        // Which tabs this context has, and where they sit.
        //
        // Runs on every open rather than once, because the answer changes: a
        // player who starts a run and reopens the menu must get the six-tab
        // bar without the scene being rebuilt.
        public void ApplyContext()
        {
            bool inRun = InDescent;
            _visible = new List<int>(SystemMenuTabs.VisibleIndices(inRun));

            var defs = SystemMenuTabs.Visible(inRun);

            // MEASURED, not read off a table.
            //
            // The bar is arithmetic over label widths. They were once authored
            // by hand and were wrong for months -- taken from a design
            // prototype at .14em while the emitter drew at zero tracking --
            // and the only reason that was ever found was measuring the
            // running scene on purpose. Asking the labels themselves removes
            // the whole class: change a tab's wording, font or tracking and
            // the bar re-lays around it with nothing to keep in step by hand.
            MeasureLabels(defs, out var fullWidths, out var shortWidths);
            var bar = SystemMenuLayout.Arrange(defs, fullWidths, shortWidths);

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                int slot = _visible.IndexOf(i);
                bool shown = slot >= 0;

                TabObject(i).SetShown(shown);
                if (!shown)
                {
                    Element(tabHovers, i).SetShown(false);
                    Element(tabUnderlines, i).SetShown(false);
                    continue;
                }

                // Every visible label is the full or the short form together,
                // as the arrangement decided.
                var def = SystemMenuTabs.All[i];
                LabelOf(i)?.Set(bar.ShortLabels ? def.ShortLabel : def.Label);

                Move(TabRect(i), bar.CentreX(slot), bar.Widths[slot], SystemMenuLayout.TabHeight);
                Move(Rect(Element(tabHovers, i)), bar.CentreX(slot), bar.Widths[slot], SystemMenuLayout.TabHeight);

                // RESIZED AS WELL AS MOVED. The underline is the label's width
                // plus a little, not the box's, so a re-measured or shortened
                // label needs the rule under it to follow.
                var underline = Rect(Element(tabUnderlines, i));
                if (underline != null)
                {
                    underline.anchoredPosition =
                        new Vector2(bar.CentreX(slot), SystemMenuLayout.UnderlineOffsetY);

                    underline.sizeDelta = new Vector2(
                        SystemMenuLayout.UnderlineWidth(bar.LabelWidths[slot]),
                        SystemMenuLayout.UnderlineHeight);
                }
            }

            // One divider per gap between adjacent tabs of a group, and only
            // for the gaps this set has: never across the group gap.
            if (tabDividers != null)
            {
                for (int i = 0; i < tabDividers.Length; i++)
                {
                    bool shown = SystemMenuLayout.DividerFollows(defs, i);
                    tabDividers[i].SetShown(shown);
                    if (!shown) continue;

                    var rect = Rect(tabDividers[i]);
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(bar.DividerCentreX(i), 0f);
                    }
                }
            }

            // A tab that just vanished cannot stay selected. Falls back to the
            // reopen rule rather than to index 0, so reordering the table
            // cannot change where the menu lands.
            if (!_visible.Contains(_selected))
            {
                SelectCore(SystemMenuTabs.IndexOf(MenuMemory.TabFor(context)), audible: false);
            }

            PaintTabLabels();
            RefreshNavLinks();
            RefreshLintel(inRun);
        }

        // What each visible tab's label ACTUALLY draws at, in both forms, in
        // slot order. The label's own font, size and tracking measure both, so
        // the short form is measured before it is ever shown.
        //
        // Falls back to the Domain estimate per tab rather than in bulk: a
        // headless fixture with no label still lays out, and a real bar with
        // one missing label still measures the others.
        private void MeasureLabels(
            IReadOnlyList<SystemMenuTabDef> defs, out float[] full, out float[] brief)
        {
            full = new float[defs.Count];
            brief = new float[defs.Count];
            var estimated = SystemMenuLabelWidths.Estimated();

            for (int slot = 0; slot < defs.Count; slot++)
            {
                var label = LabelOf(_visible[slot]);

                full[slot] = label == null
                    ? estimated.Full[_visible[slot]]
                    : PreferredWidth(label, defs[slot].Label);
                brief[slot] = label == null
                    ? estimated.Short[_visible[slot]]
                    : PreferredWidth(label, defs[slot].ShortLabel);
            }
        }

        private static float PreferredWidth(TMP_Text label, UiString text) =>
            label.GetPreferredValues(text.Format().ToUpperInvariant(), Mathf.Infinity, Mathf.Infinity).x;

        private TMP_Text LabelOf(int index)
        {
            var go = TabObject(index);
            return go == null ? null : go.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        private void RefreshLintel(bool inRun)
        {
            var save = SaveSlotManager.CurrentSave;

            if (runTitle != null) runTitle.SetContent(UiStrings.SystemMenuTitle.Format());

            if (contextLine != null)
            {
                var run = RunManager.Run;
                if (inRun && run != null) contextLine.Set(UiStrings.SystemFloorRoom, run.floor, run.step);
                else contextLine.Set(UiStrings.SystemBetweenDescents);
            }

            if (goldValue != null) goldValue.Set(UiStrings.SystemGold, LintelGold(context, save, RunManager.Run));
            if (embersValue != null) embersValue.Set(UiStrings.SystemEmbers, save?.EmberTotal() ?? 0);

            RefreshModePill();
        }

        // THE LINTEL'S GOLD HAS ONE SOURCE PER CONTEXT (plan C4). Gold picked up
        // on a descent lives on the run until it ends, and only then reaches the
        // save, so the save's figure in a descent is the wallet the player left
        // at camp, and it disagreed with the RUN pane's own gold line (which
        // reads the run) by the whole of what was picked up since.
        internal static int LintelGold(MenuContext context, SaveData save, RunSnapshot run)
        {
            if (context == MenuContext.Camp) return save?.Gold ?? 0;
            return run != null && run.hasRun ? run.gold : 0;
        }

        // THE PILL FITS ITS WORDS. Its left edge is fixed and its width follows
        // the measured text for this context, so "AT CAMP" is not a plate built
        // for "IN A FIGHT — VIEW ONLY". The rim and fill stretch with it.
        private void RefreshModePill()
        {
            if (modePillLabel == null) return;

            var text = MenuModePill.For(context).Format();
            modePillLabel.SetContent(text);

            if (modePill == null) return;

            float width = SystemMenuLayout.PillWidth(modePillLabel.GetPreferredValues(text).x);
            modePill.sizeDelta = new Vector2(width, SystemMenuLayout.PillHeight);
            modePill.anchoredPosition = new Vector2(SystemMenuLayout.PillCentreX(width), modePill.anchoredPosition.y);
        }

        // ---- the notice and the prompts ------------------------------------------------
        //
        // Every message the menu's panes have for the player goes through here
        // (plan C11), so it always lands in the same place on the same clock.
        // Null-tolerant: a fixture scene may carry no footer.
        public void ShowNotice(UiString text, float seconds = MenuNotice.DefaultSeconds) =>
            notice?.Show(text, seconds);

        public void ShowNotice(UiString text, object[] args, float seconds = MenuNotice.DefaultSeconds) =>
            notice?.Show(text, args, seconds);

        // A line that stays until cleared, under whatever notice is up.
        public void SetStateLine(UiString text) => notice?.SetState(text);

        public void SetStateLine(UiString text, params object[] args) => notice?.SetState(text, args);

        public void ClearStateLine() => notice?.ClearState();

        // THE REFUSAL EVERY PANE SHARES: true when MenuPermissions refuses the
        // action in this menu's context, after telling the player why. A handler
        // reads `if (SystemMenuController.Refuses(menu, action)) return;`, so a
        // refused press is never silent and the words come from the one table
        // that decided it. Static and null-tolerant for the panes' sake: a
        // fixture pane with no menu reads as camp and has nowhere to say it.
        public static bool Refuses(SystemMenuController menu, MenuAction action)
        {
            var where = menu != null ? menu.Context : MenuContext.Camp;
            if (MenuPermissions.Allows(action, where)) return false;

            menu?.ShowNotice(MenuPermissions.Reason(action, where));
            return true;
        }

        // What the footer says for the focused control: the first active pane
        // source with an answer, else the frame's own row. Asked at the moment
        // of drawing, never cached, for the reason INavCancelClaim is: which
        // pane is active changes under this one component.
        public IReadOnlyList<MenuPrompt> CurrentPrompts()
        {
            Selectable focused = null;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null) focused = selected.GetComponent<Selectable>();

            IReadOnlyList<MenuPrompt> row = null;
            var activePane = ActivePane();
            if (activePane != null)
            {
                foreach (var source in activePane.GetComponentsInChildren<IMenuPromptSource>(includeInactive: false))
                {
                    row = source.PromptsFor(focused);
                    if (row != null) break;
                }
            }

            row = row ?? MenuPrompts.Defaults(SystemMenuTabs.All[_selected]);

            // A on the tab that is already showing selects nothing, and the menu
            // opens with focus there, so without this the footer of a pane whose
            // rows are read-only offered "A Select" the moment it opened. On any
            // other tab A does switch to it.
            if (focused != null && focused == SelectedTabButton())
            {
                row = row.Where(p => p.Button != PromptButton.Confirm).ToList();
            }

            return row;
        }

        // THE TRIGGER SHORTCUT (INavTabStrip): LT/RT step tabs, freeing
        // LB/RB for section/character paging (see ActivePaneSectionStrip
        // below). Wraps through the SAME _visible
        // list RefreshNavLinks' own Rail
        // wires and the SAME Select(index) each tab Button's onClick calls,
        // never a second "what is the next tab" computation (this
        // interface's own header on why). _visible is INDICES into
        // SystemMenuTabs.All, not slot positions, so the step is taken in
        // slot space (0..VisibleTabs.Count-1) and translated back --
        // stepping _selected directly by +-1 would walk the hidden Run
        // tab, between two visible ones at camp.
        public void StepTab(int direction)
        {
            if (_visible.Count == 0) return;

            int slot = _visible.IndexOf(_selected);
            if (slot < 0) slot = 0;

            int next = ((slot + direction) % _visible.Count + _visible.Count) % _visible.Count;
            Select(_visible[next]);

            // LANDS INSIDE THE NEW PANE, not merely on its tab -- the
            // shoulder is a shortcut for the Move-to-tab + Submit +
            // Move-down-into-pane dance those three inputs would otherwise
            // take (plan section 7), so one press should leave the player
            // ready to act on the new pane's own content. Same lookup
            // ApplyContext's own tab-Down link uses (INavPaneEntry first,
            // else the first active Selectable in tree order); falls back
            // to the tab button itself when the pane declares nothing
            // selectable at all (none today), so selection is never
            // left dangling on a pane with nothing to enter.
            var entry = ActivePaneEntry();
            var target = entry != null ? entry.gameObject : SelectedTabButton()?.gameObject;
            if (target != null) EventSystem.current?.SetSelectedGameObject(target);
        }

        public void Select(SystemMenuTab tab) => Select(SystemMenuTabs.IndexOf(tab));

        // A tab the player chose, by click, trigger or caller: it is a swipe,
        // and while the menu is up it is the tab to come back to (MenuMemory
        // ignores the system group).
        public void Select(int index)
        {
            // A switch drops the pending state of the tab being left (plan C6);
            // the same tab again is not a switch.
            if (IsOpen && index != _selected) CancelPending();

            SelectCore(index, audible: true);
            if (IsOpen) MenuMemory.Remember(context, SystemMenuTabs.All[_selected].Tab);
        }

        // Exactly one pane and one underline live at a time. `audible` is off
        // for the tab picked by opening the menu or by a fallback: only a
        // switch the player made is a swipe.
        private void SelectCore(int index, bool audible)
        {
            if (panes == null || panes.Length == 0) return;

            int previous = _selected;
            bool wasChosen = _chosen;
            _chosen = true;
            _selected = index < 0 ? 0 : index >= SystemMenuTabs.Count ? SystemMenuTabs.Count - 1 : index;

            if (audible && wasChosen && _selected != previous) SoundController.Play(TabSwipe.Next());

            int pane = SystemMenuTabs.PaneIndexFor(_selected);

            for (int i = 0; i < panes.Length; i++)
            {
                panes[i].SetShown(i == pane);
            }

            if (tabUnderlines != null)
            {
                for (int i = 0; i < tabUnderlines.Length; i++)
                {
                    tabUnderlines[i].SetShown(i == _selected);
                }
            }

            // The hover plate under the tab just selected comes down with it,
            // so a click does not leave both markers lit.
            if (tabHovers != null && _selected < tabHovers.Length)
            {
                tabHovers[_selected].SetShown(false);
            }

            PaintTabLabels();
            RefreshNavLinks();
            RefreshSelectables();
        }

        // The system group's labels are quiet until selected; the game group
        // keeps the emitter's own colour.
        private void PaintTabLabels()
        {
            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                if (SystemMenuTabs.All[i].Group != SystemMenuTabGroup.System) continue;

                var label = LabelOf(i);
                if (label == null) continue;

                label.color = i == _selected ? SystemGroupSelected : SystemGroupQuiet;
            }
        }

        private static readonly Color SystemGroupQuiet = Hex(FightHudPalette.TextMuted);
        private static readonly Color SystemGroupSelected = Hex(FightHudPalette.GoldLight);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;

        // ---- small helpers ------------------------------------------------------

        private GameObject TabObject(int i) =>
            tabButtons != null && i < tabButtons.Length && tabButtons[i] != null
                ? tabButtons[i].gameObject
                : null;

        private RectTransform TabRect(int i) => Rect(TabObject(i));

        private static GameObject Element(GameObject[] all, int i) =>
            all != null && i >= 0 && i < all.Length ? all[i] : null;

        private static RectTransform Rect(GameObject go) =>
            go == null ? null : go.transform as RectTransform;

        private static void Move(RectTransform rect, float x, float width, float height)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

            }
}
