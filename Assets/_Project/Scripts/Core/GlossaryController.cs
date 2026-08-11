using System.Collections.Generic;
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

        [SerializeField] internal string[] iconIds;
        [SerializeField] internal Sprite[] iconSprites;

        private static readonly Color MarkerLit = new Color(0.95f, 0.86f, 0.62f, 1f);
        private static readonly Color MarkerDark = new Color(0.95f, 0.86f, 0.62f, 0f);
        private static readonly Color NameNormal = new Color(0.93f, 0.90f, 1f, 1f);
        private static readonly Color NameLocked = new Color(0.49f, 0.43f, 0.62f, 1f);

        private int _category;
        private int _page;
        private int _selectedRow = -1;
        private bool _wired;

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
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void SelectCategory(int index)
        {
            if (index < 0 || index >= GlossaryCatalog.Categories.Count) return;

            _category = index;

            // A category switch resets BOTH page and selection. Landing on
            // page 4 of a category you just opened would be baffling, and a
            // selection describing an entry from the previous category would
            // be wrong rather than merely odd.
            _page = 0;
            _selectedRow = -1;
            Refresh();
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
            var save = SaveSlotManager.CurrentSave;
            var category = GlossaryCatalog.Categories[_category];

            _entries = GlossaryEntries.For(category, save);
            _page = GlossaryCatalog.ClampPage(_page, _entries.Count);

            for (int i = 0; i < categoryButtons.Length && i < GlossaryCatalog.Categories.Count; i++)
            {
                var each = GlossaryCatalog.Categories[i];
                var entries = i == _category ? _entries : GlossaryEntries.For(each, save);

                categoryLabels[i].SetContent(GlossaryCatalog.DisplayName(each));
                categoryCounts[i].Set(UiStrings.GlossaryCount,
                    GlossaryCatalog.UnlockedCount(entries), entries.Count);
                categoryMarkers[i].color = i == _category ? MarkerLit : MarkerDark;
            }

            Paint();
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
                ItemIcons.Apply(detailIcon, iconIds, iconSprites, null);
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
            ItemIcons.Apply(detailIcon, iconIds, iconSprites, entry.Locked ? null : entry.IconId);
        }
    }
}
