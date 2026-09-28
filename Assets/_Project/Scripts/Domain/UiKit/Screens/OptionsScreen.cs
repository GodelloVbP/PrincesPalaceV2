using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Options pane: layout 2a, grouped cards in two columns.
    //
    // Hosted inside the system menu's Options pane exactly as the dossier is
    // hosted in Character & Inventory -- its own screen class, its own
    // controller, and the menu knows nothing about either.
    //
    // WHAT IS NOT HERE is deliberate and is the design's own rule applied:
    // every row binds to a real GameSettings value or it is not drawn. Four of
    // the six groups the design asked for have nothing behind them yet. See
    // OptionRows for the list and REMAINING.md for what each would need.
    public sealed class OptionsScreen
    {
        private const string CardFill = FightHudPalette.CardFill;
        private const string CardRim = FightHudPalette.Hairline;
        private const string Heading = FightHudPalette.GoldText;
        private const string Body = FightHudPalette.TextMuted;
        private const string Value = FightHudPalette.TextPrimary;
        private const string RowHover = "#C8AAE60D";
        private const string Track = FightHudPalette.Track;
        private const string TrackFill = FightHudPalette.GoldLight;

        public UiNode Root;

        // The row containers, in OptionRows.AllRows order. Each one IS the
        // tint plate as well as the parent of its label and controls.
        public List<NodeRef> RowHovers = new List<NodeRef>();

        // DENSE, and keyed rather than index-aligned with AllRows.
        //
        // The first shape here padded these with nulls so every array matched
        // AllRows position for position, and UiWiringSweep refused it -- rightly,
        // since a null element in a serialized array is exactly what an unwired
        // reference looks like, and the sweep cannot tell a deliberate hole from
        // a forgotten one. Carrying the key instead says which setting each
        // control belongs to without leaving gaps for the sweep to trip on.
        public List<string> SliderKeys = new List<string>();
        public List<NodeRef> SliderTracks = new List<NodeRef>();
        public List<NodeRef> SliderFills = new List<NodeRef>();
        public List<NodeRef> SliderValues = new List<NodeRef>();

        public List<string> StepperKeys = new List<string>();
        public List<NodeRef> StepPrev = new List<NodeRef>();
        public List<NodeRef> StepNext = new List<NodeRef>();
        public List<NodeRef> StepperValues = new List<NodeRef>();

        public NodeRef RestoreDefaults;

        public static OptionsScreen Build()
        {
            var screen = new OptionsScreen();
            var groups = OptionRows.Groups;

            // NO SCROLLING is a stated constraint, so it is checked rather than
            // hoped for. A group added later that pushes a column past the pane
            // fails here instead of running off the bottom of a screen nobody
            // scrolls because nothing told them they could.
            if (!OptionsLayout.CardsFit(groups))
            {
                throw new System.InvalidOperationException(
                    "The Options pane's cards no longer fit without scrolling. Column 0 needs " +
                    $"{OptionsLayout.ColumnHeight(groups, 0):F0}px and column 1 needs " +
                    $"{OptionsLayout.ColumnHeight(groups, 1):F0}px of {OptionsLayout.UsableHeight:F0}px. " +
                    "Move a group to the other column, shorten one, or the pane has to scroll - which " +
                    "layout 2a was chosen specifically to avoid.");
            }

            var children = new List<UiNode>();
            var nextTop = new float[(int)OptionsLayout.ColumnCount];
            for (int i = 0; i < nextTop.Length; i++) nextTop[i] = OptionsLayout.ContentTop;

            foreach (var group in groups)
            {
                float height = OptionsLayout.CardHeight(group.Rows.Count);
                float centreY = nextTop[group.Column] - height * 0.5f;
                nextTop[group.Column] -= height + OptionsLayout.CardGap;

                children.Add(BuildCard(screen, group, centreY));
            }

            // Under column 0, below its last card. Themed Crimson -- the
            // brief's one reset action -- which means dropping the .NoChrome()
            // this row used before there was a semantic kit to reach for.
            var restore = Ui.Button("OptionsRestoreDefaults", UiStrings.OptionsRestoreDefaults,
                    new UiVec(OptionsLayout.RestoreWidth, OptionsLayout.RestoreHeight), 14,
                    Place.At(OptionsLayout.ColumnCentreX(0),
                             nextTop[0] - OptionsLayout.RestoreHeight * 0.5f))
                .Themed(ButtonTheme.Crimson);
            screen.RestoreDefaults = restore;
            children.Add(restore);

            // APPLIES IMMEDIATELY, and the pane says so rather than leaving the
            // player hunting for a confirm button that is not there.
            //
            // ContentBottom + 12f, not ContentBottom - 14f: the Silver 2:1
            // container's own inset leaves only a 4px margin below
            // ContentBottom, so 14px below it would escape the container's
            // own content inset. Nothing else occupies this strip, so
            // sitting just inside the floor costs nothing.
            children.Add(Ui.Label("OptionsAppliesNote", UiStrings.OptionsAppliesImmediately,
                    new UiVec(520f, 18f), 13, Body,
                    Place.At(OptionsLayout.ColumnCentreX(0), OptionsLayout.ContentBottom + 12f))
                .AsDecor()
                .Styled(TypographyRole.Body));

            // BARE, not a kit container. This pane sits on the shared
            // SystemMenuFill with no ground of its own, same as the
            // dossier.
            var ground = Ui.SystemMenuPane("OptionsPane", "OptionsPaneContent",
                new UiVec(OptionsLayout.PaneWidth, OptionsLayout.PaneHeight), children.ToArray());

            screen.Root = ground;
            return screen;
        }

        private static UiNode BuildCard(OptionsScreen screen, OptionGroupDef group, float centreY)
        {
            float width = OptionsLayout.ColumnWidth;
            float height = OptionsLayout.CardHeight(group.Rows.Count);
            int rows = group.Rows.Count;

            var cardChildren = new List<UiNode>
            {
                Ui.Solid($"OptionsCard{group.Key}Fill", CardFill, new UiVec(width, height), Place.At(0f, 0f))
                    .AsDecor(),
            };

            cardChildren.AddRange(Ui.Rim($"OptionsCard{group.Key}", new UiVec(width, height),
                CardRim));

            cardChildren.Add(Ui.Label($"OptionsCard{group.Key}Heading", group.Heading,
                    new UiVec(OptionsLayout.HeadingWidth, OptionsLayout.HeadingHeight), 18, Heading,
                    Place.At(-OptionsLayout.CardContentHalf + OptionsLayout.HeadingWidth * 0.5f,
                             OptionsLayout.HeadingCentreY(rows)))
                .AsDecor()
                .Styled(TypographyRole.FunctionalHeading));

            for (int i = 0; i < rows; i++)
            {
                float y = OptionsLayout.RowCentreY(rows, i);
                cardChildren.Add(BuildRow(screen, group.Rows[i], y));
            }

            return Ui.Panel($"OptionsCard{group.Key}", Place.At(OptionsLayout.ColumnCentreX(group.Column), centreY),
                UiSize.Fixed(width, height), cardChildren);
        }

        // A row is a CONTAINER, not a stack of siblings.
        //
        // The first version laid the tint plate down as a sibling under the
        // label and controls and hung the hover off that, which could not work
        // twice over. AsDecor sets raycastTarget false, so the plate received
        // no pointer events at all -- and even raycastable, a sibling is not in
        // the control's parent chain, so reaching for the slider would have
        // fired exit on the plate and dropped the tint exactly when the player
        // was about to use the row.
        //
        // As the row's PARENT it gets both: enter and exit bubble up from
        // whichever child the pointer is actually over, so the whole row lights
        // as one thing.
        private static UiNode BuildRow(OptionsScreen screen, OptionRowDef row, float y)
        {
            string key = row.Key;
            var children = new List<UiNode>();

            // The label sits a little high when there is a note under it, so the
            // pair reads as one block rather than as two rows.
            float labelY = row.HasNote ? 9f : 0f;

            children.Add(Ui.Label($"OptionsRow{key}Label", row.Label,
                    new UiVec(OptionsLayout.LabelWidth, 20f), 15, Value,
                    Place.At(OptionsLayout.LabelCentreX, labelY))
                .AsDecor());

            if (row.HasNote)
            {
                children.Add(Ui.Label($"OptionsRow{key}Note", row.Note,
                        new UiVec(OptionsLayout.LabelWidth, 16f), 12, Body,
                        Place.At(OptionsLayout.LabelCentreX, -11f))
                    .AsDecor()
                    .Styled(TypographyRole.Body));
            }

            if (row.Kind == OptionKind.Slider)
            {
                var track = Ui.Solid($"OptionsRow{key}Track", Track,
                    new UiVec(OptionsLayout.TrackWidth, OptionsLayout.TrackHeight),
                    Place.At(OptionsLayout.TrackCentreX, 0f));

                // PIVOTED LEFT, at the track's left edge, so the controller can
                // grow it by width and it extends rightwards. Anchoring it
                // 0..value instead is worse than it looks: anchors are relative
                // to the PARENT, and the first version did exactly that and
                // spanned that fraction of the whole card, drawing a gold line
                // straight through the row's own label.
                var fill = Ui.Solid($"OptionsRow{key}Fill", TrackFill,
                        new UiVec(OptionsLayout.TrackWidth, OptionsLayout.TrackHeight),
                        Place.At(OptionsLayout.TrackLeft, 0f, new UiVec(0f, 0.5f)))
                    .AsDecor();

                var value = Ui.Label($"OptionsRow{key}Value", UiString.Runtime,
                        new UiVec(OptionsLayout.ValueWidth, 20f), 14, Value,
                        Place.At(OptionsLayout.SliderValueCentreX, 0f))
                    .AsDecor();

                screen.SliderKeys.Add(key);
                screen.SliderTracks.Add(track);
                screen.SliderFills.Add(fill);
                screen.SliderValues.Add(value);

                children.Add(track);
                children.Add(fill);
                children.Add(value);
            }
            else
            {
                // NOT THEMED: square icon buttons (step size both axes).
                var prev = Ui.Button($"OptionsRow{key}Prev", UiStrings.OverlayPrev,
                        new UiVec(OptionsLayout.StepButtonSize, OptionsLayout.StepButtonSize), 14,
                        Place.At(OptionsLayout.StepPrevCentreX, 0f))
                    .NoChrome();

                var next = Ui.Button($"OptionsRow{key}Next", UiStrings.OverlayNext,
                        new UiVec(OptionsLayout.StepButtonSize, OptionsLayout.StepButtonSize), 14,
                        Place.At(OptionsLayout.StepNextCentreX, 0f))
                    .NoChrome();

                var stepValue = Ui.Label($"OptionsRow{key}Value", UiString.Runtime,
                        new UiVec(OptionsLayout.StepValueWidth, 20f), 14, Value,
                        Place.At(OptionsLayout.StepValueCentreX, 0f))
                    .AsDecor();

                screen.StepperKeys.Add(key);
                screen.StepPrev.Add(prev);
                screen.StepNext.Add(next);
                screen.StepperValues.Add(stepValue);

                children.Add(prev);
                children.Add(stepValue);
                children.Add(next);
            }

            // Coloured, and deliberately NOT AsDecor: it needs an Image to tint
            // and a live raycast target to be hovered at all. The controller
            // fades it to nothing on wiring and back up on enter.
            var rowNode = Ui.Panel($"OptionsRow{key}",
                    Place.At(0f, y),
                    UiSize.Fixed(OptionsLayout.CardContentWidth, OptionsLayout.RowHeight - 8f),
                    children)
                .Coloured(RowHover);

            screen.RowHovers.Add(rowNode);
            return rowNode;
        }

                    }
}
