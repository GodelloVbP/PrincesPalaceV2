using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Run statistics pane: three cards of figures, no charts.
    //
    // Hosted inside the system menu's Run statistics pane exactly as the
    // dossier and Options are -- its own screen class, its own controller, and
    // the menu knows nothing about either.
    //
    // NO CHARTS is the design's call and it is right: a run is a couple of
    // dozen rooms, and a trend line over sixteen points is a decoration
    // pretending to be an insight. Eighteen figures, plainly.
    //
    // WHAT IS NOT HERE: the design's header of floor, room, elapsed, days and
    // turns. Nothing counts elapsed, days or turns, and the LINTEL two inches
    // above this pane already prints floor and room. See RunStatRows.
    public sealed class RunStatsScreen
    {
        private const string CardFill = "#12091C8C";
        private const string CardRim = "#C8AAE638";
        private const string Heading = "#FFD9A2";
        private const string Name = "#8A7AA0";
        private const string Value = "#F4EBFF";
        private const string RowRule = "#C8AAE61F";

        public UiNode Root;

        // In RunStatRows.AllRows order, and carrying the key alongside so the
        // controller looks a figure up by what it IS rather than by where it
        // happens to sit. Index alignment across two files is the drift this
        // project keeps writing rules against.
        public List<string> ValueKeys = new List<string>();
        public List<NodeRef> Values = new List<NodeRef>();

        public static RunStatsScreen Build()
        {
            var screen = new RunStatsScreen();
            var groups = RunStatRows.Groups;

            if (!RunStatsLayout.CardsFit(groups))
            {
                throw new System.InvalidOperationException(
                    $"The Run statistics pane has {groups.Count} groups and its tallest needs " +
                    $"{RunStatsLayout.TallestCard(groups):F0}px of {RunStatsLayout.UsableHeight:F0}px, " +
                    $"against {(int)RunStatsLayout.ColumnCount} columns. Move rows between groups, drop " +
                    "one, or the pane has to scroll - which this layout was chosen specifically to avoid.");
            }

            var children = new List<UiNode>();
            for (int i = 0; i < groups.Count; i++)
            {
                children.Add(BuildCard(screen, groups, groups[i], i));
            }

            screen.Root = Ui.Panel("RunStatsPane", Place.At(0f, 0f),
                UiSize.Fixed(RunStatsLayout.PaneWidth, RunStatsLayout.PaneHeight), children);
            return screen;
        }

        private static UiNode BuildCard(RunStatsScreen screen,
            IReadOnlyList<RunStatGroupDef> groups, RunStatGroupDef group, int column)
        {
            int rows = group.Rows.Count;
            float width = RunStatsLayout.ColumnWidth;
            float height = RunStatsLayout.CardHeight(rows);

            var cardChildren = new List<UiNode>
            {
                Ui.Solid($"RunStatsCard{group.Key}Fill", CardFill, new UiVec(width, height), Place.At(0f, 0f))
                    .AsDecor()
                    .AllowOverlap("the fill is what the card's contents stand on"),
            };

            cardChildren.AddRange(Ui.Rim($"RunStatsCard{group.Key}", new UiVec(width, height),
                CardRim, "the rim traces the card it encloses"));

            cardChildren.Add(Ui.Label($"RunStatsCard{group.Key}Heading", group.Heading,
                    new UiVec(RunStatsLayout.CardContentWidth, RunStatsLayout.HeadingHeight), 16, Heading,
                    Place.At(0f, RunStatsLayout.HeadingCentreY(rows)))
                .AsDecor());

            for (int i = 0; i < rows; i++)
            {
                var row = group.Rows[i];
                float y = RunStatsLayout.RowCentreY(rows, i);

                cardChildren.Add(Ui.Label($"RunStatsName{row.Key}", row.Label,
                        new UiVec(RunStatsLayout.NameWidth, 22f), 14, Name,
                        Place.At(RunStatsLayout.NameCentreX, y))
                    .AsDecor());

                // RUNTIME, so there is no authored figure anywhere for a
                // controller to drift from. Every number on this pane is
                // written in exactly one place.
                var value = Ui.Label($"RunStatsValue{row.Key}", UiString.Runtime,
                        new UiVec(RunStatsLayout.ValueWidth, 22f), 15, Value,
                        Place.At(RunStatsLayout.ValueCentreX, y))
                    .AsDecor();

                screen.ValueKeys.Add(row.Key);
                screen.Values.Add(value);
                cardChildren.Add(value);

                // BETWEEN rows only. A rule under the last one is a second
                // bottom edge sitting just inside the card's own rim.
                if (i < rows - 1)
                {
                    cardChildren.Add(Ui.Solid($"RunStatsRule{row.Key}", RowRule,
                            new UiVec(RunStatsLayout.CardContentWidth, 1f),
                            Place.At(0f, y - RunStatsLayout.RowHeight * 0.5f))
                        .AsDecor());
                }
            }

            return Ui.Panel($"RunStatsCard{group.Key}",
                Place.At(RunStatsLayout.ColumnCentreX(column),
                         RunStatsLayout.CardCentreY(groups, rows)),
                UiSize.Fixed(width, height), cardChildren);
        }

                    }
}
