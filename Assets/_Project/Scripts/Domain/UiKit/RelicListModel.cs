using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.UiKit
{
    // One relic as a list paints it. Plain values, so the model can sit in
    // Domain while the lookup that fills it (RelicRows) sits in Core beside
    // ContentDatabase.
    public readonly struct RelicRow
    {
        public readonly string Id;
        public readonly string Name;

        // The rarity as a word (colour alone excludes anyone who cannot
        // separate the bands), and the band itself for the colour. Empty and
        // Common on an unknown row, which has neither.
        public readonly string RarityText;
        public readonly RelicRarity Rarity;

        public readonly string Description;

        // Key into the presenter's icon table: the relic's own id, so an
        // unknown id simply finds nothing and the icon hides.
        public readonly string IconId;

        // False for an id whose definition is gone: the player still carries
        // SOMETHING, so the row exists and says so rather than the list
        // quietly getting shorter.
        public readonly bool Known;

        public RelicRow(string id, string name, string rarityText, RelicRarity rarity,
                        string description, string iconId, bool known)
        {
            Id = id;
            Name = name;
            RarityText = rarityText;
            Rarity = rarity;
            Description = description;
            IconId = iconId;
            Known = known;
        }
    }

    // The relic list every surface shares: the Reckoning's RELICS tab, the RUN
    // pane and the relic draft's held strip (plan C24). It owns the ORDER of
    // the rows (the order they are handed in, which for a descent is held
    // order: the draft's pick first) and the PAGE, through Paging, so no
    // surface keeps its own page arithmetic.
    //
    // Mutable in the one field a pager turns. Rows are fixed at construction:
    // a surface that holds a different set makes a new model and asks
    // WithPageOf to keep the reader where they were.
    public sealed class RelicListModel
    {
        private readonly List<RelicRow> _rows;
        private int _page;

        public RelicListModel(IReadOnlyList<RelicRow> rows, int pageSize)
        {
            _rows = rows == null ? new List<RelicRow>() : new List<RelicRow>(rows);
            PageSize = pageSize;
        }

        public int PageSize { get; }

        public int Count => _rows.Count;

        public bool IsEmpty => _rows.Count == 0;

        public IReadOnlyList<RelicRow> All => _rows;

        public int PageCount => Paging.PageCount(_rows.Count, PageSize);

        // More than one page: the only time a pager has anything to do.
        public bool IsPaged => PageCount > 1;

        // Zero-based, always inside the list. A count that shrank under the
        // reader (a relic sold) clamps rather than showing a blank page.
        public int Page => Paging.Clamp(_page, _rows.Count, PageSize);

        // The rows on the current page: short on the last page, never padded.
        public IReadOnlyList<RelicRow> Visible => Paging.Slice(_rows, Page, PageSize);

        public bool CanStep(int direction)
        {
            int next = Page + direction;
            return next >= 0 && next < PageCount;
        }

        // Clamped rather than wrapping: an arrow pressed past either end does
        // nothing. Returns whether the page changed, so a caller can skip a
        // repaint and a page-turn sound for a press that went nowhere.
        public bool Step(int direction)
        {
            if (!CanStep(direction)) return false;
            _page = Page + direction;
            return true;
        }

        public void SetPage(int page) => _page = Paging.Clamp(page, _rows.Count, PageSize);

        // The row's index in the whole list, or -1.
        public int IndexOf(string id)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Id == id) return i;
            }

            return -1;
        }

        // Turns to the page holding `id` and returns its slot on that page, or
        // -1 (page unchanged) when the list does not hold it. A deep link
        // lands here.
        public int ShowRelic(string id)
        {
            int index = IndexOf(id);
            if (index < 0 || PageSize <= 0) return -1;

            _page = index / PageSize;
            return index % PageSize;
        }

        // The same reader on a rebuilt list: keeps the page if it still
        // exists, and the model clamps it if it does not.
        public RelicListModel WithPageOf(RelicListModel previous)
        {
            if (previous != null) SetPage(previous.Page);
            return this;
        }
    }
}
