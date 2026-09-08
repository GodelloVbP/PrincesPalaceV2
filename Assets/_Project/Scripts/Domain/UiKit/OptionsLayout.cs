using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The Options pane's geometry, as pure arithmetic.
    //
    // Layout 2a from the design pass: grouped cards in two columns, everything
    // visible at once with no scrolling. The no-scrolling part is a constraint
    // rather than a preference, and it is checked -- CardsFit() refuses a
    // column that has grown past the pane, so a group added later fails the
    // build instead of running off the bottom where nobody looks.
    public static class OptionsLayout
    {
        // The pane this sits in, which is the system menu's content area. This
        // is still the FRAME's declared size (SystemMenuPaneTests.EveryHosted
        // PaneIsTheSizeOfTheContentArea pins it against the panel).
        public const float PaneWidth = SystemMenuLayout.PanelWidth;      // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight;  // 804

        // THE PANE'S OWN DECLARED CONTENT HALF-EXTENTS, not PaneWidth/
        // PaneHeight * 0.5f -- see SystemMenuLayout.PaneContentHalfWidth/
        // HalfHeight's own comment. 744/357.78 against the old 800/402; the
        // ground that boundary used to be a Silver 2:1 Container's painted
        // border is bare now (owner's call, 2026-09-07), but the numbers are
        // unchanged so nothing in this pane moved.
        public static float HalfWidth => SystemMenuLayout.PaneContentHalfWidth;
        public static float HalfHeight => SystemMenuLayout.PaneContentHalfHeight;

        // A SMALL SLACK MARGIN over the pane's own content inset, not a
        // second authored pad. Same pattern as ExitsLayout.ContentMargin.
        // ColumnWidth comes out at 1480 either way -- 4px here costs less
        // than the old 60px PadX did, and the content inset already took
        // more than the difference.
        public const float PadX = 4f;
        public const float PadTop = 4f;
        public const float PadBottom = 4f;

        public const float ColumnGap = 40f;

        // TWO COLUMNS as of docs/PLAN_BATTLE_SPEED.md -- option B, shipped
        // after option A (one column, every row shrunk to fit a sixth) put
        // the restore button's bottom edge and the footer note within
        // 3.56px of each other and the reworded (longer) footer text landed
        // visibly UNDER the button in the G4 screenshot rather than below
        // it. Option A's own margin arithmetic was correct -- 704 of
        // 707.56 fit -- but "fits without overlapping the audit's overlap
        // check" and "reads as cramped by eye" are different questions, and
        // G4 is what decides between them.
        //
        // B keeps every row at its ORIGINAL height, which is what makes it
        // safe: column 0 (Audio, Display) is back to exactly the two-card
        // arrangement that shipped before this plan touched anything, 694 of
        // 707.56, footer and button with the same clearance they always had.
        // Column 1 holds Gameplay alone -- one card, one row, plenty of
        // room. Two columns put Audio's two rows beside Display's three
        // before there was a third group, which is why this project had
        // gone to one column in the first place; a lone Gameplay card in
        // its own column does not repeat that problem, because it does not
        // compete with anything else for column 1's height.
        public const float ColumnCount = 2f;

        // HalfWidth * 2f, not PaneWidth -- the content region is the pane's
        // own declared inset, narrower than the declared frame. Comes out at
        // 1480 either way: 744 * 2 - 8 (4px margin a side) equals 1600 - 120
        // (the old 60px PadX a side) exactly.
        public static float ColumnWidth =>
            (HalfWidth * 2f - PadX * 2f - ColumnGap * (ColumnCount - 1f)) / ColumnCount;   // 1480

        public static float ColumnCentreX(int column) =>
            -HalfWidth + PadX + ColumnWidth * 0.5f + column * (ColumnWidth + ColumnGap);

        public static float ContentTop => HalfHeight - PadTop;
        public static float ContentBottom => -HalfHeight + PadBottom;

        // ---- a card -------------------------------------------------------------

        // The heading sits in its OWN narrow box at the card's left edge rather
        // than a full-width one at its centre. Ui.Label centres text in its box
        // and there is no left-aligned helper, so a snug box at the left is how
        // a heading gets left-aligned here -- and over a 1420px row, a centred
        // one floats in the middle of nothing with its own rows starting far
        // away underneath it.
        public const float HeadingWidth = 320f;

        public const float CardPadX = 30f;

        // UNTOUCHED by option B, by design -- see ColumnCount's own header.
        // Every row is exactly the size it always was.
        public const float CardPadY = 24f;
        public const float HeadingHeight = 40f;
        public const float RowHeight = 78f;

        public static float CardContentWidth => ColumnWidth - CardPadX * 2f;            // 660 at two columns
        public static float CardContentHalf => CardContentWidth * 0.5f;                 // 330

        public static float CardHeight(int rows) =>
            CardPadY * 2f + HeadingHeight + rows * RowHeight;

        // Cards stack down their column with this between them.
        public const float CardGap = 36f;

        // Row `index` inside a card of `rows`, measured from the CARD's centre.
        public static float RowCentreY(int rows, int index) =>
            CardHeight(rows) * 0.5f - CardPadY - HeadingHeight - RowHeight * (index + 0.5f);

        public static float HeadingCentreY(int rows) =>
            CardHeight(rows) * 0.5f - CardPadY - HeadingHeight * 0.5f;

        // ---- a row's insides ----------------------------------------------------
        //
        // Label on the left, control on the right, and the two never negotiate:
        // the label's box ends where the control's begins, so a long label
        // truncates rather than shoving the slider off the card.
        public const float LabelWidth = 320f;
        public static float LabelCentreX => -CardContentHalf + LabelWidth * 0.5f;

        // NARROWED for option B, per its own header on ColumnCount: a
        // 620-wide control block plus a 320-wide label is 940, and a
        // 720-wide two-column card only has 660 of content width to spend
        // in total. 280 leaves the label's own right edge (-10) and the
        // control block's left edge (CardContentHalf - 280 = 50) 60px apart
        // -- narrower than the old single-column row, which is the whole
        // trade B makes.
        public const float ControlBlockWidth = 280f;
        public static float ControlLeft => CardContentHalf - ControlBlockWidth;

        public const float TrackWidth = 170f;
        public const float TrackHeight = 6f;
        public static float TrackCentreX => ControlLeft + TrackWidth * 0.5f;
        public static float TrackLeft => TrackCentreX - TrackWidth * 0.5f;

        public const float ValueWidth = 70f;
        public static float SliderValueCentreX => CardContentHalf - ValueWidth * 0.5f;

        public const float StepButtonSize = 34f;
        public static float StepPrevCentreX => ControlLeft + StepButtonSize * 0.5f;
        public static float StepNextCentreX => CardContentHalf - StepButtonSize * 0.5f;

        // 200, not 420 -- the gap between the two step buttons at this
        // ControlBlockWidth is 212 (ControlBlockWidth - 2*StepButtonSize -
        // the two half-button overlaps the centre formulas already account
        // for), so 420 would print outside its own buttons.
        public const float StepValueWidth = 200f;
        public static float StepValueCentreX =>
            (StepPrevCentreX + StepNextCentreX) * 0.5f;

        // ---- the restore button -------------------------------------------------

        public const float RestoreWidth = 280f;
        public const float RestoreHeight = 56f;

        // ---- the no-scrolling guard ---------------------------------------------

        // The tallest column, including the gaps between cards and the restore
        // button under column 0.
        public static float ColumnHeight(IReadOnlyList<OptionGroupDef> groups, int column)
        {
            float used = 0f;
            int cards = 0;

            foreach (var group in groups)
            {
                if (group.Column != column) continue;
                used += CardHeight(group.Rows.Count);
                cards++;
            }

            if (cards > 1) used += CardGap * (cards - 1);
            if (column == 0 && cards > 0) used += CardGap + RestoreHeight;

            return used;
        }

        public static float UsableHeight => ContentTop - ContentBottom;

        public static bool CardsFit(IReadOnlyList<OptionGroupDef> groups)
        {
            for (int column = 0; column < (int)ColumnCount; column++)
            {
                if (ColumnHeight(groups, column) > UsableHeight) return false;
            }

            return true;
        }
    }
}
