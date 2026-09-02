using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Reckoning's twin: what the run cost and what survived it.
    //
    // Deliberately the SAME shape -- 70% of the frame, centred, two columns,
    // expands into place. A death screen that looked nothing like the victory
    // screen would make the two feel like different games, and the player is
    // reading the same kinds of fact in both: what changed, and what they keep.
    //
    // The columns differ in what they carry. Left is per-character: the combat
    // ledger, which is the closest this game gets to telling you how the run
    // actually went. Right is the settlement: what was forfeited, what was
    // earned, how deep it got.
    public sealed class DefeatScreen
    {
        public const float PanelWidth = ReckoningScreen.PanelWidth;
        public const float PanelHeight = ReckoningScreen.PanelHeight;

        // Matches the Reckoning's, for the same reason: fixed at build time, so
        // it must cover the largest party the save can field.
        public const int RowCount = ReckoningScreen.RowCount;

        public UiNode Root;
        public NodeRef Frame;

        public NodeRef GoldLostLabel;
        public NodeRef EmbersLabel;
        public NodeRef DepthLabel;
        public NodeRef ExpLabel;

        public NodeRef ReturnButton;
        public NodeRef InspectButton;

        public List<NodeRef> RowGroups = new List<NodeRef>();
        public List<NodeRef> RowNames = new List<NodeRef>();
        public List<NodeRef> RowStats = new List<NodeRef>();

        private const float ColumnX = 336f;
        private const float RowHeight = 76f;
        private const float RowPitch = 84f;

        public static DefeatScreen Build()
        {
            var screen = new DefeatScreen();
            var inside = new List<UiNode>
            {
                // Not "YOU DIED". The run ended; the characters are still on the
                // roster with everything they learned, and a screen that says
                // otherwise is lying about its own save format.
                Ui.Label("DefeatTitle", UiStrings.DefeatTitle, new UiVec(900f, 64f), 40, "#D98A8A",
                    Place.At(0f, 318f)).AsDecor().Styled(TypographyRole.CeremonialTitle),
                Ui.Label("DefeatStatsHeading", UiStrings.DefeatStatsHeading, new UiVec(600f, 34f), 20,
                    "#B8A8D9", Place.At(-ColumnX, 248f)).AsDecor().Styled(TypographyRole.FunctionalHeading),
                Ui.Label("DefeatLostHeading", UiStrings.DefeatLost, new UiVec(600f, 34f), 20,
                    "#B8A8D9", Place.At(ColumnX, 248f)).AsDecor().Styled(TypographyRole.FunctionalHeading),
            };

            for (int i = 0; i < RowCount; i++)
            {
                inside.Add(screen.BuildRow(i));
            }

            // ---- the right column: what it cost, then what it paid ----------
            var goldLost = Ui.Label("DefeatGoldLost", UiStrings.DefeatGoldLost, new UiVec(600f, 48f), 26,
                    "#D98A8A", Place.At(ColumnX, 190f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            screen.GoldLostLabel = goldLost;
            inside.Add(goldLost);

            inside.Add(Ui.Label("DefeatKeptHeading", UiStrings.DefeatKept, new UiVec(600f, 34f), 20,
                "#B8A8D9", Place.At(ColumnX, 110f)).AsDecor().Styled(TypographyRole.FunctionalHeading));

            var embers = Ui.Label("DefeatEmbers", UiStrings.DefeatEmbers, new UiVec(600f, 48f), 26,
                    "#F2DB9E", Place.At(ColumnX, 52f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            screen.EmbersLabel = embers;
            inside.Add(embers);

            var exp = Ui.Label("DefeatExp", UiStrings.DefeatExp, new UiVec(600f, 36f), 18,
                    "#B8A8D9", Place.At(ColumnX, -6f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            screen.ExpLabel = exp;
            inside.Add(exp);

            var depth = Ui.Label("DefeatDepth", UiStrings.DefeatDepth, new UiVec(600f, 36f), 18,
                    "#9C8FC4", Place.At(ColumnX, -56f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            screen.DepthLabel = depth;
            inside.Add(depth);

            // ---- out -----------------------------------------------------------
            //
            // Two exits. The character sheet is reachable from here because the
            // roster is the ONE thing that survived, and "what did I actually
            // gain" is the question a death screen provokes -- sending the
            // player to the hub to go and look is a worse answer than a button.
            // SILVER, both. There is no Retry control on this screen (a run
            // that ended in defeat is over -- see RunManager.EndRun), so
            // there is no recommended-continuation action here to reserve
            // Gold for; both exits are ordinary navigation.
            var inspect = Ui.Button("DefeatInspectButton", UiStrings.DefeatInspect,
                    new UiVec(300f, 60f), 22, Place.At(-170f, -318f))
                .Themed(ButtonTheme.Silver);
            var ret = Ui.Button("DefeatReturnButton", UiStrings.DefeatToHub,
                    new UiVec(300f, 60f), 22, Place.At(170f, -318f))
                .Themed(ButtonTheme.Silver);

            screen.InspectButton = inspect;
            screen.ReturnButton = ret;
            inside.Add(inspect);
            inside.Add(ret);

            var frame = Ui.Panel("DefeatFrame", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), inside)
                .Coloured("#2A1230F5");
            screen.Frame = frame;

            var content = Ui.Panel("DefeatContent", Place.At(0f, 0f), UiSize.Fill, frame);

            // Darker than the Reckoning's 65%. The stage behind a victory is
            // worth keeping; the stage behind a defeat is your own party lying
            // on it, and lingering on that is a different tone than this screen
            // is going for.
            screen.Root = Ui.Modal("DefeatPanel", "#0A0614D9", content).Inactive();
            return screen;
        }

        private UiNode BuildRow(int index)
        {
            float y = 170f - index * RowPitch;

            var name = Ui.Label($"DefeatRow{index}Name", UiString.Runtime, new UiVec(560f, 30f), 22,
                    "#EDE6FF", Place.At(0f, 20f))
                .AsDecor();

            // One line of four numbers rather than four labelled rows: the
            // column is 600 wide and the labels would outweigh the figures.
            var stats = Ui.Label($"DefeatRow{index}Stats", UiString.Runtime, new UiVec(580f, 26f), 14,
                    "#9C8FC4", Place.At(0f, -18f))
                .AsDecor();

            var group = Ui.Panel($"DefeatRow{index}", Place.At(-ColumnX, y),
                    UiSize.Fixed(600f, RowHeight), name, stats)
                .Inactive();

            RowGroups.Add(group);
            RowNames.Add(name);
            RowStats.Add(stats);
            return group;
        }
    }
}
