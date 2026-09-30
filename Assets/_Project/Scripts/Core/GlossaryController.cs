using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The GLOSSARY pane, driven (plan C25, spec sections 5 and 9).
    //
    // Knows about categories, pages and selection. Knows nothing about what a
    // relic or a monster is -- GlossaryEntries answers that, which is what lets
    // a new category be one enum value and one case rather than a new screen.
    //
    // SELECTION FOLLOWS FOCUS, on the rail and in the list alike. A pad has no
    // spare button to "open" a row, and a mouse hover-then-click is one gesture
    // too many for a reference, so the category under focus is the one listed
    // and the row under focus is the one described (SelectIndex reports both).
    // Nothing on the rail or in the list does anything when pressed, which the
    // footer says by offering no A there (IMenuPromptSource).
    //
    // NAVIGATION. Entry is the category rail; Right reaches the list, Left
    // comes back, Up from either column's first row reaches the selected tab,
    // and Down from the last row reaches the pager when there is one. LB/RB
    // step categories (INavSectionStrip). There is no NavContext of its own:
    // the menu's is the one, and Cancel closes the menu.
    public class GlossaryController : MonoBehaviour, INavPaneEntry, INavSectionStrip, IMenuPromptSource
    {
        [SerializeField] internal SystemMenuController menu;

        [SerializeField] internal Button[] categoryButtons;
        [SerializeField] internal Image[] categoryMarkers;
        [SerializeField] internal TMP_Text[] categoryLabels;
        [SerializeField] internal TMP_Text[] categoryCounts;

        [SerializeField] internal Button[] rows;
        [SerializeField] internal Image[] rowMarkers;
        [SerializeField] internal TMP_Text[] rowNames;
        [SerializeField] internal TMP_Text[] rowMetas;

        [SerializeField] internal GameObject emptyHint;
        [SerializeField] internal TMP_Text pageLabel;
        [SerializeField] internal Button prevPageButton;
        [SerializeField] internal Button nextPageButton;

        [SerializeField] internal Image detailIcon;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailMeta;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal GameObject detailLockedBy;
        [SerializeField] internal TMP_Text detailLockedByLabel;

        // Item and relic art by id, baked with the scene. A status's badge is
        // Resources art and is loaded when its row is described.
        [SerializeField] internal IconEntry[] icons;

        private static readonly Color MarkerLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color MarkerDark = new Color(0.95f, 0.86f, 0.62f, 0f);
        private static readonly Color NameNormal = new Color(0.93f, 0.90f, 1f, 1f);
        private static readonly Color NameLocked = new Color(0.49f, 0.43f, 0.62f, 1f);
        private static readonly Color RailDim = new Color(0.72f, 0.66f, 0.85f, 1f);

        private int _category;
        private int _page;
        private int _selectedRow = -1;
        private bool _wired;

        // EVERY CATEGORY, built once per open. Switching category is then pure
        // indexing rather than a content sweep and two achievement-fact
        // gathers per press.
        private List<List<GlossaryEntry>> _byCategory = new List<List<GlossaryEntry>>();
        private List<GlossaryEntry> _entries = new List<GlossaryEntry>();

        private readonly Dictionary<string, Sprite> _statusArt = new Dictionary<string, Sprite>();

        public int CategoryIndex => _category;

        public int PageIndex => _page;

        // Refresh on OPEN. What is unlocked moves underneath this between
        // visits -- a boss put down since last time changes a locked row into
        // a readable one, which is most of the point of having the pane.
        private void OnEnable()
        {
            if (categoryButtons == null) return;

            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int index = i;
                categoryButtons[i].onClick.AddListener(() => SelectCategory(index));
                Watch(categoryButtons[i], index, SelectCategory);
            }

            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].onClick.AddListener(() => SelectRow(index));
                Watch(rows[i], index, SelectRow);
            }

            prevPageButton.onClick.AddListener(() => StepPage(-1));
            nextPageButton.onClick.AddListener(() => StepPage(1));
        }

        // Focus is selection: the control the pad or keyboard lands on is the
        // one that becomes the category or the row being read.
        private static void Watch(Button button, int index, System.Action<int> select)
        {
            var watch = button.gameObject.GetComponent<SelectIndex>() ?? button.gameObject.AddComponent<SelectIndex>();
            watch.Index = index;
            watch.Changed = (i, selected) =>
            {
                if (selected) select(i);
            };
        }

        // ---- selection ----------------------------------------------------------------

        private void SelectCategory(int index)
        {
            // Guarded against _byCategory, not against the enum. Refresh runs in
            // OnEnable, and a press that arrived before it would index an empty
            // list. The same category again is a no-op: focus reports it, and a
            // click on the category already showing must not throw away the
            // page the reader is on.
            if (index < 0 || index >= _byCategory.Count || index == _category) return;

            SetCategory(index);
        }

        // A category switch resets BOTH page and selection. Landing on page 4
        // of a category you just opened would be baffling, and a selection
        // describing an entry from the previous category would be wrong rather
        // than merely odd.
        private void SetCategory(int index)
        {
            _category = index;
            _page = 0;
            _selectedRow = -1;
            _entries = _byCategory[_category];

            PaintRail();
            Paint();
        }

        private void SelectRow(int index)
        {
            var page = GlossaryCatalog.Page(_entries, _page);
            int row = index >= 0 && index < page.Count ? index : -1;
            if (row == _selectedRow) return;

            _selectedRow = row;
            PaintSelection(page);
        }

        private void StepPage(int direction)
        {
            int page = GlossaryCatalog.ClampPage(_page + direction, _entries.Count);
            if (page == _page) return;

            _page = page;
            _selectedRow = -1;
            Paint();
        }

        public void Refresh()
        {
            Wire();
            _byCategory = GlossaryEntries.All(SaveSlotManager.CurrentSave);
            _category = Mathf.Clamp(_category, 0, _byCategory.Count - 1);
            _entries = _byCategory[_category];
            _page = GlossaryCatalog.ClampPage(_page, _entries.Count);
            _selectedRow = -1;

            PaintRail();
            Paint();
        }

        // ---- deep link -------------------------------------------------------------------

        // `Open(Glossary, entry id)`: selects the entry's category, turns to its
        // page and puts focus on its row. False when no category lists it. The
        // first category listing the id wins, in rail order.
        public bool ShowEntry(string entryId)
        {
            Wire();
            if (_byCategory.Count == 0) Refresh();

            for (int c = 0; c < _byCategory.Count; c++)
            {
                int at = _byCategory[c].FindIndex(e => e.Id == entryId);
                if (at < 0) continue;

                _category = c;
                _entries = _byCategory[c];
                _page = at / GlossaryCatalog.RowsPerPage;
                _selectedRow = at % GlossaryCatalog.RowsPerPage;

                PaintRail();
                Paint();

                var row = rows[_selectedRow];
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(row.gameObject);
                return true;
            }

            return false;
        }

        // ---- painting -----------------------------------------------------------------------

        private void PaintRail()
        {
            for (int i = 0; i < categoryButtons.Length && i < _byCategory.Count; i++)
            {
                var entries = _byCategory[i];
                bool selected = i == _category;

                categoryLabels[i].SetContent(GlossaryCatalog.DisplayName(GlossaryCatalog.Categories[i]));
                categoryLabels[i].color = selected ? NameNormal : RailDim;
                categoryCounts[i].Set(UiStrings.GlossaryCount,
                    GlossaryCatalog.UnlockedCount(entries), entries.Count);
                categoryMarkers[i].color = selected ? MarkerLit : MarkerDark;
            }
        }

        private void Paint()
        {
            var page = GlossaryCatalog.Page(_entries, _page);
            int pages = GlossaryCatalog.PageCount(_entries.Count);
            bool paged = pages > 1;

            if (emptyHint != null) emptyHint.SetActive(page.Count == 0);

            prevPageButton.gameObject.SetActive(paged);
            nextPageButton.gameObject.SetActive(paged);
            pageLabel.gameObject.SetActive(paged);
            if (paged) pageLabel.Set(UiStrings.GlossaryPage, _page + 1, pages);

            for (int i = 0; i < rows.Length; i++)
            {
                bool present = i < page.Count;
                rows[i].gameObject.SetActive(present);
                if (!present) continue;

                var entry = page[i];

                // A locked row still shows its NAME. Hiding it would make the
                // glossary unable to tell you what there is to find, which is
                // most of the reason to open one.
                rowNames[i].SetContent(entry.Name);
                rowNames[i].color = entry.Locked ? NameLocked : NameNormal;
                rowMetas[i].SetContent(entry.Meta);
            }

            PaintSelection(page);
            RefreshNavigation();
        }

        // The row markers and the detail: everything that changes when the
        // reader moves within a page, and nothing that needs the pad's links
        // rebuilt.
        private void PaintSelection(IReadOnlyList<GlossaryEntry> page)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rowMarkers[i] != null) rowMarkers[i].color = _selectedRow == i ? MarkerLit : MarkerDark;
            }

            PaintDetail(page);
        }

        private void PaintDetail(IReadOnlyList<GlossaryEntry> page)
        {
            if (_selectedRow < 0 || _selectedRow >= page.Count)
            {
                detailName.SetContent("");
                detailMeta.SetContent("");
                detailBody.Set(UiStrings.GlossaryPick);
                detailLockedBy.SetActive(false);
                ItemIcons.Apply(detailIcon, icons, null);
                return;
            }

            var entry = page[_selectedRow];

            detailName.SetContent(entry.Name);
            detailMeta.SetContent(entry.Meta);

            // The body is withheld on a locked entry, not the name. What it IS
            // stays secret; that it EXISTS does not.
            if (entry.Locked) detailBody.Set(UiStrings.GlossaryLocked);
            else detailBody.SetContent(entry.Body);

            bool explainable = entry.Locked && !string.IsNullOrEmpty(entry.LockedBy);
            detailLockedBy.SetActive(explainable);
            if (explainable) detailLockedByLabel.Set(UiStrings.GlossaryLockedBy, entry.LockedBy);

            // A locked entry shows no icon either -- the silhouette of a thing
            // is a spoiler in a game whose relics are mostly a surprise.
            string iconId = entry.Locked ? null : entry.IconId;
            if (GlossaryCatalog.Categories[_category] == GlossaryCategory.Statuses) ApplyStatusArt(iconId);
            else ItemIcons.Apply(detailIcon, icons, iconId);
        }

        // A status row's id is its badge's slug, so its art is the fight's own
        // (Resources/Status/<slug>), loaded on demand and remembered. A status
        // with no art on disk hides the icon, the way a missing item does.
        private void ApplyStatusArt(string slug)
        {
            if (detailIcon == null) return;

            Sprite sprite = null;
            if (!string.IsNullOrEmpty(slug) && !_statusArt.TryGetValue(slug, out sprite))
            {
                sprite = Resources.Load<Sprite>("Status/" + slug);
                _statusArt[slug] = sprite;
            }

            detailIcon.sprite = sprite;
            detailIcon.enabled = sprite != null;
            detailIcon.preserveAspect = true;
        }

        // ---- navigation ----------------------------------------------------------------------

        // INavPaneEntry: the category rail, on the category being read.
        public Selectable NavEntry => SelectedCategoryButton();

        private Button SelectedCategoryButton() =>
            categoryButtons != null && _category >= 0 && _category < categoryButtons.Length
                ? categoryButtons[_category]
                : null;

        private List<Button> ActiveRows() =>
            rows == null
                ? new List<Button>()
                : rows.Where(r => r != null && r.gameObject.activeSelf).ToList();

        // TWO LISTS SIDE BY SIDE: the category rail and the row list are both
        // vertical. Rebuilt on every Paint because which rows are present
        // changes with the page and the category; the rail never does.
        private void RefreshNavigation()
        {
            var activeRows = ActiveRows();
            Selectable first = activeRows.Count > 0 ? activeRows[0] : null;
            Selectable last = activeRows.Count > 0 ? activeRows[activeRows.Count - 1] : null;
            var tab = menu != null ? menu.SelectedTabSelectable : null;

            bool paged = prevPageButton != null && prevPageButton.gameObject.activeSelf;
            var pager = paged ? new Selectable[] { prevPageButton, nextPageButton } : new Selectable[0];

            var links = new List<UiNavLink<Selectable>?>();

            foreach (var category in categoryButtons)
            {
                links.Add(RuntimeNavWiring.Link(category, UiNavDirection.Right, first));
            }

            foreach (var row in activeRows)
            {
                links.Add(RuntimeNavWiring.Link(row, UiNavDirection.Left, SelectedCategoryButton()));
            }

            links.Add(RuntimeNavWiring.Link(categoryButtons.Length > 0 ? categoryButtons[0] : null,
                UiNavDirection.Up, tab));
            links.Add(RuntimeNavWiring.Link(first, UiNavDirection.Up, tab));

            if (paged)
            {
                links.Add(RuntimeNavWiring.Link(last, UiNavDirection.Down, prevPageButton));
                links.Add(RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Up, last));
                links.Add(RuntimeNavWiring.Link(nextPageButton, UiNavDirection.Up, last));
                links.Add(RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Left, SelectedCategoryButton()));
            }

            RuntimeNavWiring.Apply(new[]
            {
                RuntimeNavWiring.Group("glossaryCategories", UiNavGroupKind.List, categoryButtons, wrap: UiNavWrap.Clamp),
                RuntimeNavWiring.Group("glossaryRows", UiNavGroupKind.List, activeRows, wrap: UiNavWrap.Clamp),
                RuntimeNavWiring.Group("glossaryPager", UiNavGroupKind.Rail, pager),
            }.Where(g => g != null), links);

            // The tab's Down link points at this pane's entry, which is the
            // category being read: re-point it now that the category moved.
            if (menu != null) menu.RefreshNavLinks();
        }

        // LB/RB: the previous or next category. Focus stays where it was in
        // kind: on the rail it lands on the new category, and anywhere else on
        // the new list's first row, so paging never drops the pad into the void.
        public void StepSection(int direction)
        {
            Wire();
            if (_byCategory.Count == 0) Refresh();

            int next = _category + direction;
            if (next < 0 || next >= _byCategory.Count) return;

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool onRail = selected == null || categoryButtons.Any(b => b != null && b.gameObject == selected);

            SetCategory(next);

            if (EventSystem.current == null) return;

            var rowsNow = ActiveRows();
            var target = onRail || rowsNow.Count == 0
                ? SelectedCategoryButton().gameObject
                : rowsNow[0].gameObject;
            EventSystem.current.SetSelectedGameObject(target);
        }

        // ---- the footer ----------------------------------------------------------------------

        // No "A" on the rail or in the list: selection follows focus, so
        // pressing one does nothing and a prompt for a no-op is worse than
        // none. The pager and the tab bar fall back to the frame's row, where A
        // does turn a page or switch to another tab.
        IReadOnlyList<MenuPrompt> IMenuPromptSource.PromptsFor(Selectable focused)
        {
            var tab = SystemMenuTabs.Def(SystemMenuTab.Glossary);

            return focused == null || IsRailOrRow(focused)
                ? MenuPrompts.ReadOnly(tab)
                : MenuPrompts.Defaults(tab);
        }

        private bool IsRailOrRow(Selectable focused)
        {
            foreach (var group in new[] { categoryButtons, rows })
            {
                if (group == null) continue;
                foreach (var button in group)
                {
                    if (button == focused) return true;
                }
            }

            return false;
        }
    }
}
