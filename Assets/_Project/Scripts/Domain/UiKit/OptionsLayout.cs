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

        // ONE COLUMN, not two, and that is what lets five settings fill an
        // 804-tall pane.
        //
        // Two columns put Audio's two rows beside Display's three and left both
        // stopping less than halfway down: a card cannot be made tall enough to
        // close the pane without rows deep enough to lose the control in. Full
        // width, stacked, the two cards and the restore button reach 694 of 696
        // -- and a wide row with its label at the left and its control at the
        // right is the shape every settings screen has, rather than a
        // compromise.
        //
        // What this does NOT fix is that there are five settings. The four cut
        // groups are in REMAINING.md and each is a feature rather than a layout
        // job; this is the honest arrangement of what exists.
        public const float ColumnCount = 1f;

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
        public const float CardPadY = 24f;
        public const float HeadingHeight = 40f;

        // 78 rather than 56, chosen against the pane rather than by eye: two
        // cards of two and three rows, their gaps, and the restore button come
        // to 694 of the 696 the pane has. CardsFit() is what keeps that true.
        public const float RowHeight = 78f;

        public static float CardContentWidth => ColumnWidth - CardPadX * 2f;            // 612
        public static float CardContentHalf => CardContentWidth * 0.5f;                 // 306

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

        // The controls sit in the card's RIGHT-HAND third rather than just
        // right of the label. On a 1420px row a control that starts where the
        // label ends leaves 800px of nothing after it, which reads as a row
        // that failed to finish rather than as a wide one.
        public const float ControlBlockWidth = 620f;
        public static float ControlLeft => CardContentHalf - ControlBlockWidth;

        public const float TrackWidth = 380f;
        public const float TrackHeight = 6f;
        public static float TrackCentreX => ControlLeft + TrackWidth * 0.5f;
        public static float TrackLeft => TrackCentreX - TrackWidth * 0.5f;

        public const float ValueWidth = 90f;
        public static float SliderValueCentreX => CardContentHalf - ValueWidth * 0.5f;

        public const float StepButtonSize = 34f;
        public static float StepPrevCentreX => ControlLeft + StepButtonSize * 0.5f;
        public static float StepNextCentreX => CardContentHalf - StepButtonSize * 0.5f;
        public const float StepValueWidth = 420f;
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
