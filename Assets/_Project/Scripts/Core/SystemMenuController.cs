using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

        // Panels that own Escape while they are open.
        //
        // Wired per scene rather than found by name: the fight has its reward
        // screen and the hub its glossary and relic draft, and a second
        // listener that opened this menu on top of one the player was trying to
        // close would be a bug reported as "Escape does the wrong thing".
        [UiOptional("the map has no other Escape-owning panel; the hub has a glossary " +
                    "and a relic draft, and the fight has its reward screen")]
        [SerializeField] internal GameObject[] escapeConsumers;

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

        public bool IsOpen => panel != null && panel.activeSelf;

        public int SelectedIndex => _selected;

        // The tabs on show, as indices into SystemMenuTabs.All. Exposed because
        // "which tabs does this context have" is the single most testable claim
        // the context rule makes.
        public IReadOnlyList<int> VisibleTabs => _visible;

        private void Start()
        {
            Wire();
            if (!_chosen) Select(SystemMenuTabs.IndexOf(SystemMenuTabs.DefaultFor(RunManager.HasRun)));
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
            SetActive(tabHovers[index], show);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Closing beats opening: if this menu is already up, Escape is
            // unambiguously about it.
            if (IsOpen)
            {
                Close();
                return;
            }

            if (SomethingElseOwnsEscape()) return;
            Open();
        }

        private bool SomethingElseOwnsEscape()
        {
            if (escapeConsumers == null) return false;

            foreach (var consumer in escapeConsumers)
            {
                if (consumer != null && consumer.activeInHierarchy) return true;
            }

            return false;
        }

        public void Open()
        {
            Wire();
            ApplyContext();
            SetActive(panel, true);
            Pause();
        }

        public void Close()
        {
            SetActive(panel, false);
            Resume();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // Restores the clock even if the object is torn down while open -- a
        // scene change with the menu up would otherwise leave the next scene
        // running at timeScale 0, which looks like a hang and is not one.
        private void OnDisable() => Resume();

        private void Pause()
        {
            if (_paused) return;
            _paused = true;
            _resumeTimeScale = Time.timeScale;
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
            bool inRun = RunManager.HasRun;
            _visible = new List<int>(SystemMenuTabs.VisibleIndices(inRun));

            var defs = SystemMenuTabs.Visible(inRun);
            var centres = SystemMenuLayout.TabCentresX(defs);
            var widths = SystemMenuLayout.TabWidths(defs);

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                int slot = _visible.IndexOf(i);
                bool shown = slot >= 0;

                SetActive(TabObject(i), shown);
                if (!shown)
                {
                    SetActive(Element(tabHovers, i), false);
                    SetActive(Element(tabUnderlines, i), false);
                    continue;
                }

                Move(TabRect(i), centres[slot], widths[slot], SystemMenuLayout.TabHeight);
                Move(Rect(Element(tabHovers, i)), centres[slot], widths[slot], SystemMenuLayout.TabHeight);

                // The underline keeps its authored width -- it is the label's,
                // not the box's, so it does not change with the mode.
                var underline = Rect(Element(tabUnderlines, i));
                if (underline != null)
                {
                    underline.anchoredPosition =
                        new Vector2(centres[slot], SystemMenuLayout.UnderlineOffsetY);
                }
            }

            // One divider per gap, and only for the gaps this set has.
            if (tabDividers != null)
            {
                for (int i = 0; i < tabDividers.Length; i++)
                {
                    bool shown = i < defs.Count - 1;
                    SetActive(tabDividers[i], shown);
                    if (!shown) continue;

                    var rect = Rect(tabDividers[i]);
                    if (rect != null)
                    {
                        rect.anchoredPosition =
                            new Vector2(SystemMenuLayout.DividerCentreX(defs, i), 0f);
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

            RefreshLintel(inRun);
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
                SetActive(panes[i], i == pane);
            }

            if (tabUnderlines != null)
            {
                for (int i = 0; i < tabUnderlines.Length; i++)
                {
                    SetActive(tabUnderlines[i], i == _selected);
                }
            }

            // The hover plate under the tab just selected comes down with it,
            // so a click does not leave both markers lit.
            if (tabHovers != null && _selected < tabHovers.Length)
            {
                SetActive(tabHovers[_selected], false);
            }
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

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
