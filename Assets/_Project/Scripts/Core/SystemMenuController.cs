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
    // context -- three tabs between runs, five during one -- and a scene is
    // generated once, so the build authors every tab at its five-tab position
    // and this re-applies SystemMenuLayout for whichever set is showing. The
    // arithmetic is still in exactly one place. See SystemMenuScreen's header.
    public class SystemMenuController : MonoBehaviour
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

        // Whether THIS SCENE is part of a descent. Set at build time: false in
        // the hub, true on the map and in a fight.
        //
        // Not RunManager.HasRun, which was the first answer and the wrong one.
        // A run exists from the moment the relic draft is rolled, and that
        // draft is offered IN THE HUB and deliberately survives leaving and
        // coming back -- RelicDraftTests pins that. So HasRun is true while the
        // player is standing in the hub with no descent under way, and Floor
        // map and Run statistics appeared on a screen with no floor and no run
        // to describe.
        //
        // Which scene you are in cannot be got wrong at runtime, and the hub is
        // never part of a descent by construction.
        //
        // The observation above is now also a model: RunSnapshot's
        // DescentIsUnderWay (forwarded as RunManager.DescentIsUnderWay) is what
        // EndRun asks before it strips gear, because it read hasRun and stripped
        // a roster over a draft that never left the hub. This field is NOT that
        // question and does not become it -- on the map at the entry node this
        // scene is part of a descent while nothing has moved yet, so
        // DescentIsUnderWay is still false and these two tabs must still show.
        [SerializeField] internal bool inDescent;

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

        // Whether this scene is part of a descent, for the panes that need the
        // same answer. Exposed rather than copied: the Main menu pane hides its
        // abandon card between descents, and a second serialized bool over
        // there would be one more thing that can disagree with the bar.
        public bool InDescent => inDescent;

        // The tabs on show, as indices into SystemMenuTabs.All. Exposed because
        // "which tabs does this context have" is the single most testable claim
        // the context rule makes.
        public IReadOnlyList<int> VisibleTabs => _visible;

        private void Start()
        {
            Wire();
            if (!_chosen) Select(SystemMenuTabs.IndexOf(SystemMenuTabs.DefaultFor(inDescent)));
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

        public void Open()
        {
            Wire();
            ApplyContext();
            panel.SetShown(true);
            Pause();
            PushNavContext();
        }

        public void Close()
        {
            panel.SetShown(false);
            Resume();
            PopNavContext();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

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
                _navContext = new NavContext(entry: null, selectables: null, cancel: Close);
                NavigationInputModule.Contexts?.Push(_navContext);
            }

            RefreshSelectables();

            // Push selects remembered ?? entry IMMEDIATELY (plan section
            // 4) -- not left for the dispatcher's own next-frame
            // reselection rule to pick up, which would leave the menu
            // showing no selection for the one frame between Open() and
            // the next Process() call.
            EventSystem.current?.SetSelectedGameObject(_navContext.ResolveSelection() as GameObject);
        }

        private void PopNavContext()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;

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
        // navigation groups (Dossier, Run statistics, Main menu -- phase 3's
        // rollout) still needs its buttons counted here, or the dispatcher's
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

            var entry = _selected >= 0 && tabButtons != null && _selected < tabButtons.Length
                ? TabObject(_selected)
                : null;

            _navContext.Reconfigure(entry, selectables);
        }

        // The Down link from the selected tab into its pane's own first
        // Selectable (plan section 7's "each tab pane's entry as the Down
        // link from the tab") -- found generically, in hierarchy order,
        // rather than reached for by name, so this works the same for
        // Options' rows today as it will for whatever a not-yet-migrated
        // pane grows tomorrow.
        private void RefreshPaneDownLink()
        {
            if (tabButtons == null || _selected < 0 || _selected >= tabButtons.Length) return;

            var tabButton = tabButtons[_selected];
            if (tabButton == null) return;

            int paneIndex = SystemMenuTabs.PaneIndexFor(_selected);
            var activePane = paneIndex >= 0 && panes != null && paneIndex < panes.Length ? panes[paneIndex] : null;
            var firstSelectable = activePane == null
                ? null
                : activePane.GetComponentsInChildren<Selectable>(includeInactive: false).FirstOrDefault();

            RuntimeNavWiring.Link(tabButton, firstSelectable, isDown: true);
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
        // player who starts a run and reopens the menu must get the five-tab
        // bar without the scene being rebuilt.
        public void ApplyContext()
        {
            bool inRun = inDescent;
            _visible = new List<int>(SystemMenuTabs.VisibleIndices(inRun));

            var defs = SystemMenuTabs.Visible(inRun);

            // MEASURED, not read off the table.
            //
            // The bar is arithmetic over label widths, and the authored ones
            // are a hand-measured approximation the build needs before any text
            // exists. They were wrong for months -- taken from a design
            // prototype at .14em while the emitter drew at zero tracking -- and
            // the only reason that was ever found was measuring the running
            // scene on purpose.
            //
            // Asking the labels themselves removes the whole class: change a
            // tab's wording, or its font, or its tracking, and the bar re-lays
            // around it with nothing to keep in step by hand.
            var labelWidths = MeasuredLabelWidths(_visible, defs);

            var centres = SystemMenuLayout.TabCentresX(labelWidths);
            var widths = SystemMenuLayout.TabWidths(labelWidths);

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

                Move(TabRect(i), centres[slot], widths[slot], SystemMenuLayout.TabHeight);
                Move(Rect(Element(tabHovers, i)), centres[slot], widths[slot], SystemMenuLayout.TabHeight);

                // RESIZED AS WELL AS MOVED. The underline is the label's width
                // plus a little, not the box's -- so once the label is measured
                // rather than assumed, the rule under it has to follow. It kept
                // its authored width before, which is why a mis-measured label
                // showed up as a rule overhanging its own word.
                var underline = Rect(Element(tabUnderlines, i));
                if (underline != null)
                {
                    underline.anchoredPosition =
                        new Vector2(centres[slot], SystemMenuLayout.UnderlineOffsetY);

                    underline.sizeDelta = new Vector2(
                        SystemMenuLayout.UnderlineWidth(labelWidths[slot]),
                        SystemMenuLayout.UnderlineHeight);
                }
            }

            // One divider per gap, and only for the gaps this set has.
            if (tabDividers != null)
            {
                for (int i = 0; i < tabDividers.Length; i++)
                {
                    bool shown = i < defs.Count - 1;
                    tabDividers[i].SetShown(shown);
                    if (!shown) continue;

                    var rect = Rect(tabDividers[i]);
                    if (rect != null)
                    {
                        rect.anchoredPosition =
                            new Vector2(SystemMenuLayout.DividerCentreX(labelWidths, i), 0f);
                    }
                }
            }

            // A tab that just vanished cannot stay selected. Falls back to this
            // context's default rather than to index 0, which after leaving a
            // run would have been Character & Inventory anyway but after some
            // future reordering would not.
            if (!_visible.Contains(_selected))
            {
                Select(SystemMenuTabs.IndexOf(SystemMenuTabs.DefaultFor(inRun)));
            }

            RefreshTabChain();
            RefreshLintel(inRun);
        }

        // The tab strip as a Rail group (plan section 5/7), wrap by the
        // owner's default -- rewired here rather than declared once at
        // build time because WHICH tabs are visible is exactly what this
        // method just recomputed, and the five-tab authored layout on disk
        // is not what a three-tab context actually shows (SystemMenuScreen's
        // own header explains why the scene can't know that). Only VISIBLE
        // tabs are chained; a hidden tab's Explicit links are left whatever
        // they were, which does not matter -- Navigation.Mode.None is not
        // set on them, but they are also inactive, and Unity never routes a
        // Move onto an inactive Selectable.
        private void RefreshTabChain()
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

            RuntimeNavWiring.Chain(visibleButtons, horizontal: true, wrap: true);
        }

        // What each visible tab's label ACTUALLY draws at, in order.
        //
        // Falls back to the authored figure per tab rather than in bulk: a
        // headless fixture with no label still lays out, and a real bar with
        // one missing label still measures the other four.
        private IReadOnlyList<float> MeasuredLabelWidths(
            IReadOnlyList<int> visible, IReadOnlyList<SystemMenuTabDef> defs)
        {
            var widths = new float[visible.Count];

            for (int slot = 0; slot < visible.Count; slot++)
            {
                var label = LabelOf(visible[slot]);

                widths[slot] = label == null || string.IsNullOrEmpty(label.text)
                    ? defs[slot].LabelWidth
                    : label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;
            }

            return widths;
        }

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
                contextLine.SetContent(inRun && run != null
                    ? $"FLOOR {run.floor}  ·  ROOM {run.step}"
                    : UiStrings.SystemBetweenDescents.Format());
            }

            if (goldValue != null) goldValue.SetContent($"GOLD  {save?.Gold ?? 0}");
            if (embersValue != null) embersValue.SetContent($"EMBERS  {save?.EmberTotal() ?? 0}");
        }

        public void Select(SystemMenuTab tab) => Select(SystemMenuTabs.IndexOf(tab));

        // Exactly one pane and one underline live at a time.
        public void Select(int index)
        {
            if (panes == null || panes.Length == 0) return;

            _chosen = true;
            _selected = index < 0 ? 0 : index >= SystemMenuTabs.Count ? SystemMenuTabs.Count - 1 : index;

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

            RefreshPaneDownLink();
            RefreshSelectables();
        }

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
