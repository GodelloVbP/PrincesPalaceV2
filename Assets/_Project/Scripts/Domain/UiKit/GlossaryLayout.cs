using PrincesPalace.Domain.Glossary;

namespace PrincesPalace.Domain.UiKit
{
    // The GLOSSARY pane's geometry (spec section 5), as pure arithmetic.
    //
    // Three columns inside the pane's 1520 x 756 working area: what KIND of
    // thing (the category rail), which one (the list), and what it is (the
    // detail). Everything is measured from the working area's own centre, the
    // space every node in this project is placed in.
    //
    // NO SCROLLING. The list pages (GlossaryCatalog.RowsPerPage rows, a pager
    // under them) and the rail is one row per GlossaryCategory, so a category
    // added to the enum has to fit here or the build audit says so.
    public static class GlossaryLayout
    {
        // The pane's declared frame, still the whole content area.
        public const float PaneWidth = SystemMenuLayout.PanelWidth;                                // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight; // 804

        public const float InsetX = 40f;
        public const float InsetY = 24f;
        public const float AreaWidth = PaneWidth - InsetX * 2f;    // 1520
        public const float AreaHeight = PaneHeight - InsetY * 2f;  // 756

        public const float Left = -AreaWidth * 0.5f;
        public const float Top = AreaHeight * 0.5f;

        public const float ColumnGap = 24f;

        // ---- the three columns ------------------------------------------------------
        public const float RailWidth = 280f;
        public const float ListWidth = 520f;
        public const float DetailWidth = AreaWidth - RailWidth - ListWidth - ColumnGap * 2f;   // 672

        public const float RailLeft = Left;
        public const float ListLeft = RailLeft + RailWidth + ColumnGap;
        public const float DetailLeft = ListLeft + ListWidth + ColumnGap;

        public static float CentreX(float left, float width) => left + width * 0.5f;

        // ---- the rail: a name over its "unlocked / total" count ------------------------
        public const float CategoryRowHeight = 64f;
        public const float CategoryRowGap = 4f;
        public const float CategoryInset = 16f;
        public const float CategoryLabelHeight = 30f;
        public const float CategoryCountHeight = 26f;

        public static float CategoryCentreY(int index) =>
            Top - CategoryRowHeight * 0.5f - index * (CategoryRowHeight + CategoryRowGap);

        // ---- the list ---------------------------------------------------------------------
        public const float ListRowHeight = 64f;
        public const float ListRowInset = 12f;
        public const float ListNameHeight = 30f;
        public const float ListMetaHeight = 27f;

        public static float ListRowCentreY(int slot) => Top - ListRowHeight * 0.5f - slot * ListRowHeight;

        public static float ListRowsBottom => Top - GlossaryCatalog.RowsPerPage * ListRowHeight;

        // The pager under the rows.
        public const float PagerArrowWidth = 52f;
        public const float PagerArrowHeight = 44f;
        public const float PagerLabelWidth = 120f;
        public const float PagerGap = 12f;
        public static float PagerCentreY => ListRowsBottom - PagerGap - PagerArrowHeight * 0.5f;
        public static float PagerLabelX => CentreX(ListLeft, ListWidth);
        public static float PagerPrevX => PagerLabelX - PagerLabelWidth * 0.5f - PagerArrowWidth * 0.5f - 8f;
        public static float PagerNextX => PagerLabelX + PagerLabelWidth * 0.5f + PagerArrowWidth * 0.5f + 8f;

        // ---- the detail ------------------------------------------------------------------
        public const float DetailIconSize = 120f;
        public const float DetailTextGap = 24f;
        public const float DetailNameHeight = 40f;
        public const float DetailMetaHeight = 30f;

        public static float DetailIconCentreX => DetailLeft + DetailIconSize * 0.5f;
        public static float DetailIconCentreY => Top - DetailIconSize * 0.5f;

        public static float DetailTextLeft => DetailLeft + DetailIconSize + DetailTextGap;
        public static float DetailTextWidth => DetailLeft + DetailWidth - DetailTextLeft;    // 528

        public static float DetailNameCentreY => Top - 8f - DetailNameHeight * 0.5f;
        public static float DetailMetaCentreY => Top - 8f - DetailNameHeight - 6f - DetailMetaHeight * 0.5f;

        // The body runs under the icon; a long entry (the Defense curve is the
        // longest) needs about eight lines of Body 20.
        public const float DetailBodyGap = 24f;
        public const float DetailBodyHeight = 400f;
        public static float DetailBodyCentreY => Top - DetailIconSize - DetailBodyGap - DetailBodyHeight * 0.5f;

        public const float DetailLockedByHeight = 60f;
        public static float DetailLockedByCentreY =>
            Top - DetailIconSize - DetailBodyGap - DetailBodyHeight - 8f - DetailLockedByHeight * 0.5f;
    }
}
