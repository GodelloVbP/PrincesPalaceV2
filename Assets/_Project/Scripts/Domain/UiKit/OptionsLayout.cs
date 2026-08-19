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
        // The pane this sits in, which is the system menu's content area.
        public const float PaneWidth = SystemMenuLayout.PanelWidth;      // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight;  // 804

        public const float HalfWidth = PaneWidth * 0.5f;
        public const float HalfHeight = PaneHeight * 0.5f;

        // The same 120px side clearance the dossier gets, so the two panes sit
        // their content in the same box and switching tabs does not shift the
        // margins under the player.
        public const float PadX = 120f;
        public const float PadTop = 44f;
        public const float PadBottom = 64f;

        public const float ColumnGap = 40f;
        public const float ColumnCount = 2f;

        public const float ColumnWidth =
            (PaneWidth - PadX * 2f - ColumnGap * (ColumnCount - 1f)) / ColumnCount;   // 660

        public static float ColumnCentreX(int column) =>
            -HalfWidth + PadX + ColumnWidth * 0.5f + column * (ColumnWidth + ColumnGap);

        public const float ContentTop = HalfHeight - PadTop;
        public const float ContentBottom = -HalfHeight + PadBottom;

        // ---- a card -------------------------------------------------------------

        public const float CardPadX = 24f;
        public const float CardPadY = 18f;
        public const float HeadingHeight = 34f;
        public const float RowHeight = 56f;

        public const float CardContentWidth = ColumnWidth - CardPadX * 2f;            // 612
        public const float CardContentHalf = CardContentWidth * 0.5f;                 // 306

        public static float CardHeight(int rows) =>
            CardPadY * 2f + HeadingHeight + rows * RowHeight;

        // Cards stack down their column with this between them.
        public const float CardGap = 28f;

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
        public const float LabelWidth = 260f;
        public static float LabelCentreX => -CardContentHalf + LabelWidth * 0.5f;

        public const float TrackWidth = 220f;
        public const float TrackHeight = 6f;
        public static float TrackCentreX => 90f;
        public static float TrackLeft => TrackCentreX - TrackWidth * 0.5f;

        public const float ValueWidth = 70f;
        public static float SliderValueCentreX => 250f;

        public const float StepButtonSize = 26f;
        public static float StepPrevCentreX => 60f;
        public static float StepNextCentreX => 262f;
        public const float StepValueWidth = 150f;
        public static float StepValueCentreX => 161f;

        // ---- the restore button -------------------------------------------------

        public const float RestoreWidth = 220f;
        public const float RestoreHeight = 44f;

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
