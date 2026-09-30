using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The RUN pane: the descent at a glance, in a descent or a fight.
    //
    // THREE COLUMNS (spec section 4, layout in RunLayout):
    //
    //   RELICS      icon, name, rarity and the full description of everything
    //               carried, paged (RelicListModel)
    //   EFFECTS     the event buffs in force, bonus damage and second lives,
    //   SUPPLIES    then the consumables with counts, and the gold
    //   THIS DESCENT  the Battle / Fold / Spoils figures, condensed
    //
    // A relic row, an effect row and a supply row are each a Button: not
    // because pressing one does anything, but because the pad reads the pane
    // by moving focus, and a row that cannot be focused cannot be read on a
    // pad. RunController answers the footer with no "A" prompt for them.
    //
    // NO CARD CHROME. This pane sits on the shared SystemMenuFill like the
    // dossier does; the columns are separated by space, not by boxes.
    //
    // Type roles: Body for names, rarity, descriptions and figures. Not
    // TacticalData, whose material tints its text violet (a rarity colour or a
    // near-white number would come out lilac); FunctionalHeading, all caps at
    // 24, for the column headings.
    public sealed class RunScreen
    {
        private const string Rule = "#C8AAE61F";
        private const string Heading = FightHudPalette.GoldText;
        private const string Name = FightHudPalette.TextPrimary;
        private const string Muted = FightHudPalette.TextMuted;

        public UiNode Root;

        // ---- relics ----
        public NodeRef RelicsHeading;
        public NodeRef RelicPrevPageButton;
        public NodeRef RelicNextPageButton;
        public NodeRef RelicPageLabel;
        public NodeRef RelicEmptyHint;
        public List<NodeRef> RelicButtons = new List<NodeRef>();
        public List<NodeRef> RelicIcons = new List<NodeRef>();
        public List<NodeRef> RelicNames = new List<NodeRef>();
        public List<NodeRef> RelicRarities = new List<NodeRef>();
        public List<NodeRef> RelicBodies = new List<NodeRef>();

        // ---- effects ----
        public NodeRef EffectsEmptyHint;
        public List<NodeRef> EffectButtons = new List<NodeRef>();
        public List<NodeRef> EffectLabels = new List<NodeRef>();
        public List<NodeRef> EffectValues = new List<NodeRef>();

        // ---- supplies ----
        public NodeRef SuppliesEmptyHint;
        public NodeRef SupplyMore;
        public NodeRef GoldValue;
        public List<NodeRef> SupplyButtons = new List<NodeRef>();
        public List<NodeRef> SupplyIcons = new List<NodeRef>();
        public List<NodeRef> SupplyNames = new List<NodeRef>();
        public List<NodeRef> SupplyCounts = new List<NodeRef>();
        public List<NodeRef> SupplyEffects = new List<NodeRef>();

        // ---- this descent: in RunStatRows.AllRows order, the key beside the
        // node so a figure is looked up by what it IS rather than by position.
        public List<string> StatKeys = new List<string>();
        public List<NodeRef> StatValues = new List<NodeRef>();

        public static RunScreen Build()
        {
            var screen = new RunScreen();
            var children = new List<UiNode>();

            screen.BuildRelics(children);
            screen.BuildEffects(children);
            screen.BuildSupplies(children);
            screen.BuildDescent(children);

            // The ground is the whole content area (the hosted-pane contract),
            // the working area a stretched child inside it: the spec's 40 / 24
            // insets rather than Ui.SystemMenuPane's narrower shared ones.
            var ground = Ui.Panel("RunPane", Place.At(0f, 0f), UiSize.Fixed(RunLayout.PaneWidth, RunLayout.PaneHeight));
            ground.Children.Add(Ui.Panel("RunPaneContent",
                Place.Stretch(RunLayout.InsetX, RunLayout.InsetX, RunLayout.InsetY, RunLayout.InsetY),
                UiSize.Fill, children));

            screen.Root = ground;
            return screen;
        }

        // A column heading: FunctionalHeading, all caps at the floor.
        private static UiNode HeadingLabel(string name, UiString text, float left, float width, float y) =>
            Ui.Label(name, text, new UiVec(width, RunLayout.HeadingBoxHeight), 24, Heading,
                    Place.At(left + width * 0.5f, y))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();

        private static UiNode Row(string name, UiVec size, Place place, params UiNode[] parts)
        {
            var row = Ui.Button(name, UiString.Runtime, size, 20, place)
                .NoChrome()
                .Quiet()
                .Styled(TypographyRole.Body)
                .Inactive();
            foreach (var part in parts) row.Children.Add(part);
            return row;
        }

        private static UiNode Text(string name, UiString text, UiVec size, int fontSize, string colour,
                                   Place place, UiTextAlign align)
        {
            return Ui.Label(name, text, size, fontSize, colour, place)
                .Styled(TypographyRole.Body)
                .TextAligned(align)
                .AsDecor();
        }

        private static UiNode EmptyHint(string name, UiString text, float left, float width, float y, float height)
        {
            return Ui.Label(name, text, new UiVec(width, height), 20, Muted, Place.At(left + width * 0.5f, y))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft)
                .AsDecor()
                .Inactive();
        }

        // ---- relics -----------------------------------------------------------

        private void BuildRelics(List<UiNode> children)
        {
            var heading = Ui.Label("RunRelicsHeading", UiStrings.RunRelicsHeading,
                    new UiVec(RunLayout.PagerPrevX - RunLayout.RelicColumnLeft - RunLayout.PagerArrowWidth, RunLayout.HeadingBoxHeight),
                    24, Heading,
                    Place.At(RunLayout.RelicColumnLeft
                             + (RunLayout.PagerPrevX - RunLayout.RelicColumnLeft - RunLayout.PagerArrowWidth) * 0.5f,
                             RunLayout.HeadingCentreY))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            RelicsHeading = heading;
            children.Add(heading);

            var pager = Ui.Pager("RunRelicPrevPage", Place.At(RunLayout.PagerPrevX, RunLayout.HeadingCentreY),
                "RunRelicNextPage", Place.At(RunLayout.PagerNextX, RunLayout.HeadingCentreY),
                new UiVec(RunLayout.PagerArrowWidth, RunLayout.PagerArrowHeight), 24,
                "RunRelicPageLabel", UiStrings.RelicPage, Place.At(RunLayout.PagerLabelX, RunLayout.HeadingCentreY),
                new UiVec(RunLayout.PagerLabelWidth, RunLayout.HeadingBoxHeight), 20, Name);
            pager.Prev.Styled(TypographyRole.Body).Inactive();
            pager.Next.Styled(TypographyRole.Body).Inactive();
            pager.Label.Styled(TypographyRole.Body).AsDecor().Inactive();
            RelicPrevPageButton = pager.Prev;
            RelicNextPageButton = pager.Next;
            RelicPageLabel = pager.Label;
            children.Add(pager.Prev);
            children.Add(pager.Next);
            children.Add(pager.Label);

            for (int i = 0; i < RunLayout.RelicRows; i++)
            {
                var icon = Ui.Sprite($"RunRelic{i}Icon", null,
                        Place.At(RunLayout.RelicIconX, RunLayout.RelicIconY),
                        UiSize.Fixed(RunLayout.RelicIconSize, RunLayout.RelicIconSize))
                    .AsDecor();

                float textCentre = (RunLayout.RelicTextLeft + RunLayout.RelicTextRight) * 0.5f;
                float nameCentre = RunLayout.RelicTextLeft + RunLayout.RelicNameWidth * 0.5f;
                float rarityCentre = RunLayout.RelicTextRight - RunLayout.RelicRarityWidth * 0.5f;

                var name = Text($"RunRelic{i}Name", UiString.Runtime,
                    new UiVec(RunLayout.RelicNameWidth, RunLayout.RelicNameHeight), 22, Name,
                    Place.At(nameCentre, RunLayout.RelicNameY), UiTextAlign.Left);

                var rarity = Text($"RunRelic{i}Rarity", UiString.Runtime,
                    new UiVec(RunLayout.RelicRarityWidth, RunLayout.RelicNameHeight), 20, Muted,
                    Place.At(rarityCentre, RunLayout.RelicNameY), UiTextAlign.Right);

                var body = Text($"RunRelic{i}Body", UiString.Runtime,
                    new UiVec(RunLayout.RelicTextWidth, RunLayout.RelicBodyHeight), 20, Muted,
                    Place.At(textCentre, RunLayout.RelicBodyY), UiTextAlign.TopLeft);

                var rule = Ui.Solid($"RunRelic{i}Rule", Rule,
                        new UiVec(RunLayout.RelicColumnWidth, 1f),
                        Place.At(0f, -RunLayout.RelicRowHeight * 0.5f + 0.5f))
                    .AsDecor();

                var row = Row($"RunRelic{i}",
                    new UiVec(RunLayout.RelicColumnWidth, RunLayout.RelicRowHeight),
                    Place.At(RunLayout.ColumnCentreX(RunLayout.RelicColumnLeft, RunLayout.RelicColumnWidth),
                             RunLayout.RelicRowCentreY(i)),
                    rule, icon, name, rarity, body);

                RelicButtons.Add(row);
                RelicIcons.Add(icon);
                RelicNames.Add(name);
                RelicRarities.Add(rarity);
                RelicBodies.Add(body);
                children.Add(row);
            }

            RelicEmptyHint = EmptyHint("RunRelicsEmpty", UiStrings.RunNoRelics, RunLayout.RelicColumnLeft,
                RunLayout.RelicColumnWidth, RunLayout.RelicRowCentreY(0), RunLayout.RelicRowHeight);
            children.Add(RelicEmptyHint.Node);
        }

        // ---- effects ----------------------------------------------------------

        private void BuildEffects(List<UiNode> children)
        {
            children.Add(HeadingLabel("RunEffectsHeading", UiStrings.RunEffectsHeading,
                RunLayout.EffectColumnLeft, RunLayout.EffectColumnWidth, RunLayout.HeadingCentreY));

            float left = RunLayout.EffectColumnLeft;
            float width = RunLayout.EffectColumnWidth;

            for (int i = 0; i < RunLayout.EffectRows; i++)
            {
                float labelCentre = -width * 0.5f + 8f + RunLayout.EffectLabelWidth * 0.5f;
                float valueCentre = width * 0.5f - 8f - RunLayout.EffectValueWidth * 0.5f;

                var label = Text($"RunEffect{i}Name", UiString.Runtime,
                    new UiVec(RunLayout.EffectLabelWidth, RunLayout.EffectRowHeight - 4f), 20, Name,
                    Place.At(labelCentre, 0f), UiTextAlign.Left);

                var value = Text($"RunEffect{i}Value", UiString.Runtime,
                    new UiVec(RunLayout.EffectValueWidth, RunLayout.HeadingBoxHeight), 22, Name,
                    Place.At(valueCentre, 0f), UiTextAlign.Right);

                var rule = Ui.Solid($"RunEffect{i}Rule", Rule, new UiVec(width, 1f),
                        Place.At(0f, -RunLayout.EffectRowHeight * 0.5f + 0.5f))
                    .AsDecor();

                var row = Row($"RunEffect{i}", new UiVec(width, RunLayout.EffectRowHeight),
                    Place.At(RunLayout.ColumnCentreX(left, width), RunLayout.EffectRowCentreY(i)),
                    rule, label, value);

                EffectButtons.Add(row);
                EffectLabels.Add(label);
                EffectValues.Add(value);
                children.Add(row);
            }

            EffectsEmptyHint = EmptyHint("RunEffectsEmpty", UiStrings.RunNoEffects, left, width,
                RunLayout.EffectRowCentreY(0), RunLayout.EffectRowHeight);
            children.Add(EffectsEmptyHint.Node);
        }

        // ---- supplies ---------------------------------------------------------

        private void BuildSupplies(List<UiNode> children)
        {
            float left = RunLayout.EffectColumnLeft;
            float width = RunLayout.EffectColumnWidth;

            children.Add(HeadingLabel("RunSuppliesHeading", UiStrings.RunSuppliesHeading,
                left, width, RunLayout.SupplyHeadingCentreY));

            for (int i = 0; i < RunLayout.SupplyRows; i++)
            {
                var icon = Ui.Sprite($"RunSupply{i}Icon", null,
                        Place.At(-width * 0.5f + 8f + RunLayout.SupplyIconSize * 0.5f, 0f),
                        UiSize.Fixed(RunLayout.SupplyIconSize, RunLayout.SupplyIconSize))
                    .AsDecor();

                float textLeft = -width * 0.5f + 64f;
                float nameWidth = width - 64f - RunLayout.SupplyCountWidth - 8f;

                var name = Text($"RunSupply{i}Name", UiString.Runtime, new UiVec(nameWidth, 32f), 22, Name,
                    Place.At(textLeft + nameWidth * 0.5f, 16f), UiTextAlign.Left);

                var count = Text($"RunSupply{i}Count", UiStrings.RunSupplyCount,
                    new UiVec(RunLayout.SupplyCountWidth, 32f), 22, Name,
                    Place.At(width * 0.5f - 8f - RunLayout.SupplyCountWidth * 0.5f, 16f), UiTextAlign.Right);

                float effectWidth = width - 64f - 8f;
                var effect = Ui.Label($"RunSupply{i}Effect", UiString.Runtime, new UiVec(effectWidth, 27f), 20, Muted,
                        Place.At(textLeft + effectWidth * 0.5f, -16.5f))
                    .Styled(TypographyRole.Body)
                    .TextAligned(UiTextAlign.Left)
                    .Truncated()
                    .AsDecor();

                var rule = Ui.Solid($"RunSupply{i}Rule", Rule, new UiVec(width, 1f),
                        Place.At(0f, -RunLayout.SupplyRowHeight * 0.5f + 0.5f))
                    .AsDecor();

                var row = Row($"RunSupply{i}", new UiVec(width, RunLayout.SupplyRowHeight),
                    Place.At(RunLayout.ColumnCentreX(left, width), RunLayout.SupplyRowCentreY(i)),
                    rule, icon, name, count, effect);

                SupplyButtons.Add(row);
                SupplyIcons.Add(icon);
                SupplyNames.Add(name);
                SupplyCounts.Add(count);
                SupplyEffects.Add(effect);
                children.Add(row);
            }

            SuppliesEmptyHint = EmptyHint("RunSuppliesEmpty", UiStrings.RunNoSupplies, left, width,
                RunLayout.SupplyRowCentreY(0), RunLayout.SupplyRowHeight);
            children.Add(SuppliesEmptyHint.Node);

            // "and {n} more": the stockpile can hold more stacks than the four
            // rows above; the overflow says so rather than dropping them.
            SupplyMore = Ui.Label("RunSuppliesMore", UiStrings.RunSupplyMore,
                    new UiVec(width, RunLayout.SupplyMoreHeight), 20, Muted,
                    Place.At(RunLayout.ColumnCentreX(left, width), RunLayout.SupplyMoreCentreY))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Inactive();
            children.Add(SupplyMore.Node);

            // The gold, last: what the supplies can be spent on.
            var goldLabel = Text("RunGoldLabel", UiStrings.RunSupplyGold, new UiVec(200f, 32f), 22, Name,
                Place.At(-width * 0.5f + 8f + 100f, 0f), UiTextAlign.Left);
            var goldValue = Text("RunGoldValue", UiString.Runtime, new UiVec(160f, 32f), 22, Name,
                Place.At(width * 0.5f - 8f - 80f, 0f), UiTextAlign.Right);
            GoldValue = goldValue;

            children.Add(Ui.Panel("RunGoldRow",
                Place.At(RunLayout.ColumnCentreX(left, width), RunLayout.GoldRowCentreY),
                UiSize.Fixed(width, RunLayout.GoldRowHeight),
                goldLabel, goldValue));
        }

        // ---- this descent -----------------------------------------------------

        private void BuildDescent(List<UiNode> children)
        {
            var groups = RunStatRows.Groups;
            float left = RunLayout.DescentColumnLeft;
            float width = RunLayout.DescentColumnWidth;
            float centre = RunLayout.ColumnCentreX(left, width);

            // The column heading, then each group's own under it.
            children.Add(HeadingLabel("RunDescentHeading", UiStrings.RunDescentHeading, left, width,
                RunLayout.HeadingCentreY));

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];

                children.Add(Ui.Label($"RunStatGroup{group.Key}Heading", group.Heading,
                        new UiVec(width, RunLayout.HeadingBoxHeight), 24, Muted,
                        Place.At(centre, RunLayout.StatHeadingCentreY(groups, g)))
                    .Styled(TypographyRole.FunctionalHeading)
                    .TextAligned(UiTextAlign.Left)
                    .AsDecor());

                for (int r = 0; r < group.Rows.Count; r++)
                {
                    var row = group.Rows[r];
                    float y = RunLayout.StatRowCentreY(groups, g, r);

                    children.Add(Text($"RunStatName{row.Key}", row.Label,
                        new UiVec(RunLayout.StatNameWidth, RunLayout.StatRowHeight - 4f), 20, Muted,
                        Place.At(left + RunLayout.StatNameWidth * 0.5f, y), UiTextAlign.Left));

                    var value = Text($"RunStatValue{row.Key}", UiString.Runtime,
                        new UiVec(RunLayout.StatValueWidth, RunLayout.StatRowHeight - 4f), 22, Name,
                        Place.At(left + width - RunLayout.StatValueWidth * 0.5f, y), UiTextAlign.Right);

                    StatKeys.Add(row.Key);
                    StatValues.Add(value);
                    children.Add(value);
                }
            }
        }
    }
}
