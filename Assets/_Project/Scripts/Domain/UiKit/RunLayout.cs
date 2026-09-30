namespace PrincesPalace.Domain.UiKit
{
    // The RUN pane's geometry (spec section 4), as pure arithmetic.
    //
    // Three columns inside the pane's 1520 x 756 working area: the relics, the
    // effects in force with the supplies under them, and the descent's
    // figures. Everything is measured from the working area's own centre, the
    // space every node in this project is placed in.
    //
    // NO SCROLLING. The relic list pages (RelicListModel) and every other list
    // has a fixed capacity with a stated overflow line, checked by the build
    // audit at four aspects rather than trusted here.
    public static class RunLayout
    {
        // The pane's declared frame, still the whole content area.
        public const float PaneWidth = SystemMenuLayout.PanelWidth;                              // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight; // 804

        // The working area inside it: 40 in from the sides, 24 from the top
        // and bottom (spec section 3).
        public const float InsetX = 40f;
        public const float InsetY = 24f;
        public const float AreaWidth = PaneWidth - InsetX * 2f;    // 1520
        public const float AreaHeight = PaneHeight - InsetY * 2f;  // 756

        public const float Left = -AreaWidth * 0.5f;
        public const float Top = AreaHeight * 0.5f;

        public const float ColumnGap = 24f;

        // ---- the three columns ------------------------------------------------------
        public const float RelicColumnWidth = 640f;
        public const float EffectColumnWidth = 400f;
        public const float DescentColumnWidth = AreaWidth - RelicColumnWidth - EffectColumnWidth - ColumnGap * 2f; // 432

        public const float RelicColumnLeft = Left;
        public const float EffectColumnLeft = RelicColumnLeft + RelicColumnWidth + ColumnGap;
        public const float DescentColumnLeft = EffectColumnLeft + EffectColumnWidth + ColumnGap;

        public static float ColumnCentreX(float left, float width) => left + width * 0.5f;

        // A column's heading line, at the top of the area.
        public const float HeadingHeight = 44f;
        public const float HeadingBoxHeight = 36f;
        public static float HeadingCentreY => Top - HeadingHeight * 0.5f;

        // ---- relics ---------------------------------------------------------------
        //
        // 132 tall rather than the spec's 88: the longest authored relic
        // description is 154 characters, which is three lines of Body 20 in the
        // text column, and a row that clips a relic's effect defeats the pane's
        // one job ("what does this relic do?"). Five rows fit the column under
        // the heading line; RunPaneTests measures every authored description
        // against the box.
        public const int RelicRows = 5;
        public const float RelicRowHeight = 132f;
        public const float RelicRowGap = 4f;
        public const float RelicRowsTop = Top - HeadingHeight - 8f;

        public static float RelicRowCentreY(int slot) =>
            RelicRowsTop - RelicRowHeight * 0.5f - slot * (RelicRowHeight + RelicRowGap);

        public static float RelicRowsBottom =>
            RelicRowsTop - RelicRows * RelicRowHeight - (RelicRows - 1) * RelicRowGap;

        // Inside a row, measured from the row's own centre.
        public const float RelicIconSize = 64f;
        public const float RelicIconX = -RelicColumnWidth * 0.5f + 16f + RelicIconSize * 0.5f;   // -272
        public const float RelicIconY = 18f;
        public const float RelicTextLeft = -RelicColumnWidth * 0.5f + 96f;                        // -224
        public const float RelicTextRight = RelicColumnWidth * 0.5f - 16f;                        // 304
        public const float RelicTextWidth = RelicTextRight - RelicTextLeft;                       // 528

        public const float RelicNameWidth = 350f;
        public const float RelicNameHeight = 30f;
        public const float RelicRarityWidth = RelicTextWidth - RelicNameWidth;                    // 178
        public static float RelicNameY => RelicRowHeight * 0.5f - 6f - RelicNameHeight * 0.5f;    // 46

        public const float RelicBodyHeight = 90f;
        public static float RelicBodyY => RelicNameY - RelicNameHeight * 0.5f - 2f - RelicBodyHeight * 0.5f;

        // The pager shares the heading's line, at the column's right end.
        public const float PagerArrowWidth = 44f;
        public const float PagerArrowHeight = 44f;
        public const float PagerLabelWidth = 84f;
        public static float PagerNextX => RelicColumnLeft + RelicColumnWidth - PagerArrowWidth * 0.5f;
        public static float PagerLabelX => PagerNextX - PagerArrowWidth * 0.5f - PagerLabelWidth * 0.5f;
        public static float PagerPrevX => PagerLabelX - PagerLabelWidth * 0.5f - PagerArrowWidth * 0.5f;

        // ---- effects ----------------------------------------------------------------
        public const int EffectRows = 4;
        public const float EffectRowHeight = 64f;
        public const float EffectRowGap = 4f;
        public static float EffectRowsTop => Top - HeadingHeight - 8f;

        public static float EffectRowCentreY(int slot) =>
            EffectRowsTop - EffectRowHeight * 0.5f - slot * (EffectRowHeight + EffectRowGap);

        public static float EffectRowsBottom =>
            EffectRowsTop - EffectRows * EffectRowHeight - (EffectRows - 1) * EffectRowGap;

        public const float EffectValueWidth = 90f;
        public const float EffectLabelWidth = EffectColumnWidth - EffectValueWidth - 24f;   // 286

        // ---- supplies -----------------------------------------------------------------
        //
        // Four rows: the two consumables in the game, each as a genuine and a
        // caravan-fake stack, are all the stockpile can hold as distinct rows.
        // A fifth stack would still be counted, in the overflow line.
        public const int SupplyRows = 4;
        public const float SupplyRowHeight = 64f;
        public const float SupplyRowGap = 4f;
        public const float SupplyHeadingHeight = 36f;

        public static float SupplyHeadingCentreY => EffectRowsBottom - 12f - SupplyHeadingHeight * 0.5f;
        public static float SupplyRowsTop => SupplyHeadingCentreY - SupplyHeadingHeight * 0.5f - 6f;

        public static float SupplyRowCentreY(int slot) =>
            SupplyRowsTop - SupplyRowHeight * 0.5f - slot * (SupplyRowHeight + SupplyRowGap);

        public static float SupplyRowsBottom =>
            SupplyRowsTop - SupplyRows * SupplyRowHeight - (SupplyRows - 1) * SupplyRowGap;

        public const float SupplyMoreHeight = 28f;
        public static float SupplyMoreCentreY => SupplyRowsBottom - 4f - SupplyMoreHeight * 0.5f;

        public const float GoldRowHeight = 48f;
        public static float GoldRowCentreY => SupplyMoreCentreY - SupplyMoreHeight * 0.5f - 8f - GoldRowHeight * 0.5f;

        public const float SupplyIconSize = 44f;
        public const float SupplyCountWidth = 80f;

        // ---- this descent -----------------------------------------------------------------
        public const float StatRowHeight = 36f;
        public const float StatGroupGap = 20f;
        public const float StatNameWidth = 290f;
        public const float StatValueWidth = DescentColumnWidth - StatNameWidth;   // 142

        // Centre y of group `group`'s heading, and of its row `row`, given the
        // row counts of every group above it.
        public static float StatHeadingCentreY(System.Collections.Generic.IReadOnlyList<RunStatGroupDef> groups, int group) =>
            StatGroupTop(groups, group) - HeadingBoxHeight * 0.5f;

        public static float StatRowCentreY(System.Collections.Generic.IReadOnlyList<RunStatGroupDef> groups, int group, int row) =>
            StatGroupTop(groups, group) - HeadingBoxHeight - 4f - StatRowHeight * 0.5f - row * StatRowHeight;

        private static float StatGroupTop(System.Collections.Generic.IReadOnlyList<RunStatGroupDef> groups, int group)
        {
            // Under the column's own heading line, like the other two columns.
            float top = Top - HeadingHeight - 8f;
            for (int i = 0; i < group; i++)
            {
                top -= HeadingBoxHeight + 4f + groups[i].Rows.Count * StatRowHeight + StatGroupGap;
            }

            return top;
        }

        public static float StatsBottom(System.Collections.Generic.IReadOnlyList<RunStatGroupDef> groups)
        {
            int last = groups.Count - 1;
            return StatGroupTop(groups, last) - HeadingBoxHeight - 4f - groups[last].Rows.Count * StatRowHeight;
        }
    }
}
