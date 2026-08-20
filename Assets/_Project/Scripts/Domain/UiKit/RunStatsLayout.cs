using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The Run statistics pane's geometry, as pure arithmetic.
    //
    // THREE CARDS SIDE BY SIDE rather than the Options pane's two columns, and
    // the difference is the content's shape rather than taste: Options has two
    // groups of two and three rows, this has three groups of four, five and
    // eight. Stacked in two columns the eight-row card would tower over its
    // neighbour and leave a column and a half of nothing; across three it is
    // one card taller than the others, which reads as a table.
    //
    // NO SCROLLING, same as Options, and checked here for the same reason --
    // CardsFit() refuses a group that has outgrown the pane at build time
    // instead of letting it run off the bottom where nobody looks, because
    // nothing tells a player this pane could scroll.
    public static class RunStatsLayout
    {
        public const float PaneWidth = SystemMenuLayout.PanelWidth;                              // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight; // 804

        public const float HalfWidth = PaneWidth * 0.5f;
        public const float HalfHeight = PaneHeight * 0.5f;

        // The same 120px side clearance the dossier and Options get, so all
        // three panes sit their content in the same box and switching tabs does
        // not shift the margins under the player.
        public const float PadX = 120f;
        public const float PadTop = 44f;
        public const float PadBottom = 64f;

        public const float ColumnCount = 3f;

        // 50 rather than Options' 40, chosen so the column lands on a whole
        // number: (1600 - 240 - 100) / 3 is exactly 420. Half-pixel columns are
        // how a rim ends up one pixel thick on one card and two on the next.
        public const float ColumnGap = 50f;

        public const float ColumnWidth =
            (PaneWidth - PadX * 2f - ColumnGap * (ColumnCount - 1f)) / ColumnCount;   // 420

        public static float ColumnCentreX(int column) =>
            -HalfWidth + PadX + ColumnWidth * 0.5f + column * (ColumnWidth + ColumnGap);

        public const float ContentTop = HalfHeight - PadTop;
        public const float ContentBottom = -HalfHeight + PadBottom;

        public static float UsableHeight => ContentTop - ContentBottom;

        // ---- a card -------------------------------------------------------------

        public const float CardPadX = 24f;
        public const float CardPadY = 20f;
        public const float HeadingHeight = 34f;

        // Tighter than an Options row, because nothing here is a control -- a
        // row of figures wants to read as a list, and 56px apart it reads as
        // eight separate statements.
        public const float RowHeight = 46f;

        public const float CardContentWidth = ColumnWidth - CardPadX * 2f;      // 372
        public const float CardContentHalf = CardContentWidth * 0.5f;           // 186

        public static float CardHeight(int rows) =>
            CardPadY * 2f + HeadingHeight + rows * RowHeight;

        // Cards are TOP-ALIGNED TO EACH OTHER, and that shared top line is
        // CENTRED IN THE PANE.
        //
        // Two separate decisions and both are load-bearing. Aligning the cards
        // to each other rather than centring them individually is what puts
        // three headings on one line, which is the thing the eye uses to read
        // them as a set. Centring the BLOCK is because the tallest card is 442
        // of 696 usable pixels: hung from the pane's top it leaves a quarter of
        // the screen empty underneath and reads as a screen that failed to
        // finish drawing, rather than as a composed one.
        //
        // Measured off the TALLEST card, so the block's own top and bottom
        // margins match. Cutting a row from the tallest group therefore moves
        // every card, which is correct -- the set is one object.
        public static float BlockTop(IReadOnlyList<RunStatGroupDef> groups) =>
            ContentTop - (UsableHeight - TallestCard(groups)) * 0.5f;

        public static float CardCentreY(IReadOnlyList<RunStatGroupDef> groups, int rows) =>
            BlockTop(groups) - CardHeight(rows) * 0.5f;

        public static float HeadingCentreY(int rows) =>
            CardHeight(rows) * 0.5f - CardPadY - HeadingHeight * 0.5f;

        public static float RowCentreY(int rows, int index) =>
            CardHeight(rows) * 0.5f - CardPadY - HeadingHeight - RowHeight * (index + 0.5f);

        // ---- a row's insides ----------------------------------------------------
        //
        // The dossier's stat-table shape, reused rather than reinvented: the
        // name's box starts flush against the card's content edge and the
        // value's box ends flush against the other, with a hairline under the
        // row. Ui.Label centres text inside its box, so snug boxes at the edges
        // are what makes a pair of centred labels read as a two-column table.
        public const float NameWidth = 220f;
        public static float NameCentreX => -CardContentHalf + NameWidth * 0.5f;

        public const float ValueWidth = 120f;
        public static float ValueCentreX => CardContentHalf - ValueWidth * 0.5f;

        // ---- the no-scrolling guard ---------------------------------------------

        public static float TallestCard(IReadOnlyList<RunStatGroupDef> groups)
        {
            float tallest = 0f;
            foreach (var group in groups)
            {
                float height = CardHeight(group.Rows.Count);
                if (height > tallest) tallest = height;
            }

            return tallest;
        }

        public static bool CardsFit(IReadOnlyList<RunStatGroupDef> groups) =>
            groups.Count <= (int)ColumnCount && TallestCard(groups) <= UsableHeight;
    }
}
