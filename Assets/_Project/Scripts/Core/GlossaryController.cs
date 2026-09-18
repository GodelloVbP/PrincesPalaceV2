using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The glossary, driven.
    //
    // Knows about categories, pages and selection. Knows nothing about what a
    // relic or a monster is -- GlossaryEntries answers that, which is what lets
    // a new category be one enum value and one case rather than a new screen.
    public class GlossaryController : MonoBehaviour
    {
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

        [SerializeField] internal Button closeButton;

        [SerializeField] internal IconEntry[] icons;

        private static readonly Color MarkerLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color MarkerDark = new Color(0.95f, 0.86f, 0.62f, 0f);
        private static readonly Color NameNormal = new Color(0.93f, 0.90f, 1f, 1f);
        private static readonly Color NameLocked = new Color(0.49f, 0.43f, 0.62f, 1f);

        private int _category;
        private int _page;
        private int _selectedRow = -1;
        private bool _wired;

        // Pushed at the end of every Paint() (docs/GAMEPAD_NAVIGATION_PLAN.md
        // phase 3, AUDIT.md #158), popped on OnDisable -- the panel toggling
        // off is the only way this screen closes, mouse or Cancel alike (see
        // Close() below).
        private NavContext _navContext;

        // ALL SIX CATEGORIES, built once per open. Switching category is then
        // pure indexing rather than six content sweeps and two full
        // achievement-fact gathers per click.
        private List<List<GlossaryEntry>> _byCategory = new List<List<GlossaryEntry>>();

        private List<GlossaryEntry> _entries = new List<GlossaryEntry>();

        private void Start()
        {
            Wire();
        }

        // Refresh on OPEN. What is unlocked moves underneath this between
        // visits -- a boss put down since last time changes a locked row into
        // a readable one, which is most of the point of having the screen.
        private void OnEnable()
        {
            if (categoryButtons != null) Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int index = i;
                categoryButtons[i].onClick.AddListener(() => SelectCategory(index));
            }

            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].onClick.AddListener(() => SelectRow(index));
            }

            prevPageButton.onClick.AddListener(() => StepPage(-1));
            nextPageButton.onClick.AddListener(() => StepPage(1));
            closeButton.onClick.AddListener(Close);
        }

        // ONE implementation, shared by the Close button's own click and
        // this context's Cancel handler -- a lambda duplicated on both would
        // be the two-copies-of-one-rule docs/CODE_STANDARDS.md section 10
        // forbids the moment either one changed.
        private void Close() => gameObject.SetActive(false);

        private void SelectCategory(int index)
        {
            // Guarded against _byCategory, not against the enum. Wire() runs in
            // Start and Refresh in OnEnable, and a click that arrived between
            // them would index an empty list.
            if (index < 0 || index >= _byCategory.Count) return;

            _category = index;

            // A category switch resets BOTH page and selection. Landing on
            // page 4 of a category you just opened would be baffling, and a
            // selection describing an entry from the previous category would
            // be wrong rather than merely odd.
            _page = 0;
            _selectedRow = -1;

            // Repaint, do not rebuild. Nothing about what is unlocked can have
            // changed between two clicks on the same open screen.
            _entries = _byCategory[_category];
            PaintRail();
            Paint();
        }

        private void SelectRow(int index)
        {
            var page = GlossaryCatalog.Page(_entries, _page);
            _selectedRow = index < page.Count ? index : -1;
            Paint();
        }

        private void StepPage(int direction)
        {
            _page = GlossaryCatalog.ClampPage(_page + direction, _entries.Count);
            _selectedRow = -1;
            Paint();
        }

        public void Refresh()
        {
            _byCategory = GlossaryEntries.All(SaveSlotManager.CurrentSave);
            _category = Mathf.Clamp(_category, 0, _byCategory.Count - 1);
            _entries = _byCategory[_category];
            _page = GlossaryCatalog.ClampPage(_page, _entries.Count);

            PaintRail();
            Paint();
        }

        private void PaintRail()
        {
            for (int i = 0; i < categoryButtons.Length && i < _byCategory.Count; i++)
            {
                var entries = _byCategory[i];

                categoryLabels[i].SetContent(GlossaryCatalog.DisplayName(GlossaryCatalog.Categories[i]));
                categoryCounts[i].Set(UiStrings.GlossaryCount,
                    GlossaryCatalog.UnlockedCount(entries), entries.Count);
                categoryMarkers[i].color = i == _category ? MarkerLit : MarkerDark;

                // Blue ThemedPlate now, on top of the marker: the marker is
                // its own accent (which category, as a small lit tick) and
                // stays; the plate itself brightens through SetMenuState the
                // way every other themed selection does.
                ThemedButtonState.ApplySelection(categoryButtons[i], i == _category);
            }
        }

        private void Paint()
        {
            var page = GlossaryCatalog.Page(_entries, _page);

            if (emptyHint != null) emptyHint.SetActive(page.Count == 0);
            pageLabel.Set(UiStrings.GlossaryPage, _page + 1, GlossaryCatalog.PageCount(_entries.Count));

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
                rowMarkers[i].color = _selectedRow == i ? MarkerLit : MarkerDark;

                ThemedButtonState.ApplySelection(rows[i], _selectedRow == i);
            }

            PaintDetail(page);
            RefreshNavigation();
        }

        // TWO LISTS SIDE BY SIDE, not one -- the category rail is vertical
        // (RailX/RailTop/RailPitch, GlossaryScreen's own layout) and so is
        // the row list, so both are UiNavGroupKind.List rather than one of
        // them being mis-declared a Rail because AUDIT.md's prose calls it
        // "the rail". Rebuilt every Paint() because which ROWS are present
        // changes with the page and the category; the category list itself
        // never does (GlossaryCatalog.Categories is fixed), so it is passed
        // in full every time rather than filtered.
        //
        // The pager buttons are never hidden (GlossaryController never
        // calls SetActive on either -- StepPage's own ClampPage just
        // no-ops past either end), so they need no per-repaint filtering
        // either, unlike the rows they sit under.
        private void RefreshNavigation()
        {
            var activeRows = rows?.Where(r => r != null && r.gameObject.activeSelf).ToList()
                              ?? new List<Button>();

            var links = new List<UiNavLink<Selectable>?>
            {
                RuntimeNavWiring.Link(SelectedCategoryButton(), UiNavDirection.Right,
                    activeRows.Count > 0 ? activeRows[0] : null),
                RuntimeNavWiring.Link(activeRows.Count > 0 ? activeRows[0] : null, UiNavDirection.Left,
                    SelectedCategoryButton()),
                RuntimeNavWiring.Link(activeRows.Count > 0 ? activeRows[activeRows.Count - 1] : null,
                    UiNavDirection.Down, prevPageButton),
                RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Up,
                    activeRows.Count > 0 ? activeRows[activeRows.Count - 1] : null),
                RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Down, closeButton),
                RuntimeNavWiring.Link(nextPageButton, UiNavDirection.Down, closeButton),
                RuntimeNavWiring.Link(closeButton, UiNavDirection.Up, prevPageButton),
            };

            RuntimeNavWiring.Apply(
                new[]
                {
                    RuntimeNavWiring.Group("glossaryCategories", UiNavGroupKind.List, categoryButtons),
                    RuntimeNavWiring.Group("glossaryRows", UiNavGroupKind.List, activeRows, wrap: UiNavWrap.Clamp),
                    RuntimeNavWiring.Group("glossaryPager", UiNavGroupKind.Rail,
                        new[] { prevPageButton, nextPageButton }),
                },
                links);

            var selectables = new Dictionary<string, object>();
            foreach (var button in categoryButtons ?? System.Array.Empty<Button>())
            {
                if (button != null) selectables[button.name] = button.gameObject;
            }
            foreach (var row in activeRows) selectables[row.name] = row.gameObject;
            if (prevPageButton != null) selectables[prevPageButton.name] = prevPageButton.gameObject;
            if (nextPageButton != null) selectables[nextPageButton.name] = nextPageButton.gameObject;
            if (closeButton != null) selectables[closeButton.name] = closeButton.gameObject;

            object entry = SelectedCategoryButton() != null ? SelectedCategoryButton().gameObject
                : closeButton != null ? closeButton.gameObject : (object)null;

            if (_navContext == null) _navContext = new NavContext(entry, selectables, cancel: Close);
            else _navContext.Reconfigure(entry, selectables);

            // PushIfAbsent, not Push: this context outlives its time on the
            // stack now (see OnDisable), so re-opening the glossary puts the
            // SAME instance back rather than building a new one, and a
            // repaint while it is already top must not stack a second copy.
            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);
        }

        private Button SelectedCategoryButton() =>
            categoryButtons != null && _category >= 0 && _category < categoryButtons.Length
                ? categoryButtons[_category]
                : null;

        private void OnDisable()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            // _navContext is NOT nulled. It carries this screen's focus
            // memory (plan section 4: "remembered focus by stable id"), and a
            // modal that can be re-opened has to survive being closed for
            // that memory to mean anything -- a context recreated on the next
            // open would land on its entry every single time, which is the
            // whole of AUDIT.md #163's second half. What leaves is its place
            // on the stack; what stays is the controller's own record of
            // where the player was.
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
            ItemIcons.Apply(detailIcon, icons, entry.Locked ? null : entry.IconId);
        }
    }
}
