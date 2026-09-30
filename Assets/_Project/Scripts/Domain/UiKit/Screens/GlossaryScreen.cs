using System.Collections.Generic;
using PrincesPalace.Domain.Glossary;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // Everything the game contains, in one place: the GLOSSARY pane of the
    // system menu (plan C25, spec section 5, layout in GlossaryLayout).
    //
    // Three columns: what KIND of thing (the rail), which one (the list), and
    // what it is (the detail). That shape is why this is one pane rather than
    // one per kind -- a category is a value in GlossaryCategory plus a case in
    // the Core adapter, and the tree below never learns what a relic is.
    //
    // A category, a row and the pager are Buttons, not because pressing one
    // picks anything (selection follows focus, so the detail is always the
    // focused row) but because the pad reads the pane by moving focus, and a
    // row that cannot be focused cannot be read on a pad. The pager's two
    // arrows are the exception: pressing one turns the page.
    //
    // NO CARD CHROME. Like RUN, the pane sits on the menu's shared fill and the
    // columns are separated by space and a hairline, not by boxes.
    //
    // Type roles: FunctionalHeading (all caps, 24) for the category names, Body
    // for everything else. Not TacticalData: its material tints text violet.
    public sealed class GlossaryScreen
    {
        private const string Rule = "#C8AAE61F";
        private const string Name = FightHudPalette.TextPrimary;
        private const string Muted = FightHudPalette.TextMuted;
        private const string Body = FightHudPalette.TextPrimary;
        private const string Heading = FightHudPalette.GoldText;
        private const string LockedNote = "#D9A87E";
        private const string MarkerDark = "#F2DB9E00";

        public UiNode Root;

        public List<NodeRef> CategoryButtons = new List<NodeRef>();
        public List<NodeRef> CategoryLabels = new List<NodeRef>();
        public List<NodeRef> CategoryCounts = new List<NodeRef>();
        public List<NodeRef> CategoryMarkers = new List<NodeRef>();

        public List<NodeRef> Rows = new List<NodeRef>();
        public List<NodeRef> RowNames = new List<NodeRef>();
        public List<NodeRef> RowMetas = new List<NodeRef>();
        public List<NodeRef> RowMarkers = new List<NodeRef>();

        public NodeRef EmptyHint;
        public NodeRef PageLabel;
        public NodeRef PrevPageButton;
        public NodeRef NextPageButton;

        public NodeRef DetailName;
        public NodeRef DetailMeta;
        public NodeRef DetailBody;
        public NodeRef DetailLockedBy;
        public NodeRef DetailIcon;

        public static GlossaryScreen Build()
        {
            var screen = new GlossaryScreen();
            var children = new List<UiNode>();

            // ---- the rail: one button per category, built from the enum -----
            for (int i = 0; i < GlossaryCatalog.Categories.Count; i++)
            {
                children.Add(screen.BuildCategory(i));
            }

            // ---- the list ---------------------------------------------------
            for (int i = 0; i < GlossaryCatalog.RowsPerPage; i++)
            {
                children.Add(screen.BuildRow(i));
            }

            screen.BuildListFurniture(children);
            screen.BuildDetail(children);

            // Two hairlines between the columns, so the three read as a set
            // without boxes.
            children.Add(Divider("GlossaryRailRule", GlossaryLayout.RailLeft + GlossaryLayout.RailWidth + GlossaryLayout.ColumnGap * 0.5f));
            children.Add(Divider("GlossaryListRule", GlossaryLayout.ListLeft + GlossaryLayout.ListWidth + GlossaryLayout.ColumnGap * 0.5f));

            // The ground is the whole content area (the hosted-pane contract),
            // the working area a stretched child inside it.
            var ground = Ui.Panel("GlossaryPane", Place.At(0f, 0f),
                UiSize.Fixed(GlossaryLayout.PaneWidth, GlossaryLayout.PaneHeight));
            ground.Children.Add(Ui.Panel("GlossaryPaneContent",
                Place.Stretch(GlossaryLayout.InsetX, GlossaryLayout.InsetX, GlossaryLayout.InsetY, GlossaryLayout.InsetY),
                UiSize.Fill, children));

            screen.Root = ground;
            return screen;
        }

        private static UiNode Divider(string name, float x) =>
            Ui.Solid(name, Rule, new UiVec(1f, GlossaryLayout.AreaHeight), Place.At(x, 0f)).AsDecor();

        // A rail button is always there; a list row starts hidden and is shown
        // by the controller for as many entries as the page has.
        private static UiNode Row(string name, UiVec size, Place place, bool shownAtStart, params UiNode[] parts)
        {
            var row = Ui.Button(name, UiString.Runtime, size, 20, place)
                .NoChrome()
                .Quiet()
                .Styled(TypographyRole.Body);
            if (!shownAtStart) row = row.Inactive();
            foreach (var part in parts) row.Children.Add(part);
            return row;
        }

        // The selection tick along a row's left edge, dark until its row is the
        // one being read. A separate layer from the label, so "which one am I
        // on" and "what is it called" stay two channels.
        private static UiNode Marker(string name) =>
            Ui.Solid(name, MarkerDark, Place.Frac(new UiVec(0f, 0f), new UiVec(0f, 1f), right: -4f), UiSize.Fill)
                .AsDecor();

        private static UiNode Text(string name, UiString text, UiVec size, int fontSize, string colour,
                                   Place place, TypographyRole role, UiTextAlign align)
        {
            return Ui.Label(name, text, size, fontSize, colour, place)
                .Styled(role)
                .TextAligned(align)
                .AsDecor();
        }

        // ---- the rail ---------------------------------------------------------------

        private UiNode BuildCategory(int index)
        {
            float width = GlossaryLayout.RailWidth;
            float textWidth = width - GlossaryLayout.CategoryInset * 2f;

            var marker = Marker($"GlossaryCategory{index}Marker");

            // Caption, NOT Label. UiEmitter generates a child called
            // "<button>Label" for every button's own text, so a hand-declared
            // child by that name collides with it and every lookup silently
            // takes the emitter's empty one.
            var label = Text($"GlossaryCategory{index}Caption", UiString.Runtime,
                new UiVec(textWidth, GlossaryLayout.CategoryLabelHeight), 24, Name,
                Place.At(0f, 12f), TypographyRole.FunctionalHeading, UiTextAlign.Left);

            var count = Text($"GlossaryCategory{index}Count", UiStrings.GlossaryCount,
                new UiVec(textWidth, GlossaryLayout.CategoryCountHeight), 20, Muted,
                Place.At(0f, -14f), TypographyRole.Body, UiTextAlign.Left);

            var rule = Ui.Solid($"GlossaryCategory{index}Rule", Rule, new UiVec(width, 1f),
                    Place.At(0f, -GlossaryLayout.CategoryRowHeight * 0.5f + 0.5f))
                .AsDecor();

            var button = Row($"GlossaryCategory{index}",
                new UiVec(width, GlossaryLayout.CategoryRowHeight),
                Place.At(GlossaryLayout.CentreX(GlossaryLayout.RailLeft, width), GlossaryLayout.CategoryCentreY(index)),
                shownAtStart: true, rule, marker, label, count);

            CategoryButtons.Add(button);
            CategoryMarkers.Add(marker);
            CategoryLabels.Add(label);
            CategoryCounts.Add(count);
            return button;
        }

        // ---- the list ---------------------------------------------------------------

        private UiNode BuildRow(int index)
        {
            float width = GlossaryLayout.ListWidth;
            float textWidth = width - GlossaryLayout.ListRowInset * 2f;

            var marker = Marker($"GlossaryRow{index}Marker");

            var name = Text($"GlossaryRow{index}Name", UiString.Runtime,
                new UiVec(textWidth, GlossaryLayout.ListNameHeight), 22, Name,
                Place.At(0f, 12f), TypographyRole.Body, UiTextAlign.Left);
            name = name.Truncated();

            var meta = Text($"GlossaryRow{index}Meta", UiString.Runtime,
                new UiVec(textWidth, GlossaryLayout.ListMetaHeight), 20, Muted,
                Place.At(0f, -16.5f), TypographyRole.Body, UiTextAlign.Left);
            meta = meta.Truncated();

            var rule = Ui.Solid($"GlossaryRow{index}Rule", Rule, new UiVec(width, 1f),
                    Place.At(0f, -GlossaryLayout.ListRowHeight * 0.5f + 0.5f))
                .AsDecor();

            var row = Row($"GlossaryRow{index}",
                new UiVec(width, GlossaryLayout.ListRowHeight),
                Place.At(GlossaryLayout.CentreX(GlossaryLayout.ListLeft, width), GlossaryLayout.ListRowCentreY(index)),
                shownAtStart: false, rule, marker, name, meta);

            Rows.Add(row);
            RowMarkers.Add(marker);
            RowNames.Add(name);
            RowMetas.Add(meta);
            return row;
        }

        // The empty hint and the pager, under the rows.
        private void BuildListFurniture(List<UiNode> children)
        {
            var empty = Ui.Label("GlossaryEmptyHint", UiStrings.GlossaryEmpty,
                    new UiVec(GlossaryLayout.ListWidth, 40f), 20, Muted,
                    Place.At(GlossaryLayout.CentreX(GlossaryLayout.ListLeft, GlossaryLayout.ListWidth),
                        GlossaryLayout.ListRowCentreY(0)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Inactive();
            EmptyHint = empty;
            children.Add(empty);

            var pager = Ui.Pager("GlossaryPrevPage", Place.At(GlossaryLayout.PagerPrevX, GlossaryLayout.PagerCentreY),
                "GlossaryNextPage", Place.At(GlossaryLayout.PagerNextX, GlossaryLayout.PagerCentreY),
                new UiVec(GlossaryLayout.PagerArrowWidth, GlossaryLayout.PagerArrowHeight), 24,
                "GlossaryPageLabel", UiStrings.GlossaryPage,
                Place.At(GlossaryLayout.PagerLabelX, GlossaryLayout.PagerCentreY),
                new UiVec(GlossaryLayout.PagerLabelWidth, 34f), 20, Name);
            pager.Prev.Styled(TypographyRole.Body).Inactive();
            pager.Next.Styled(TypographyRole.Body).Inactive();
            pager.Label.Styled(TypographyRole.Body).AsDecor().Inactive();
            PageLabel = pager.Label;
            PrevPageButton = pager.Prev;
            NextPageButton = pager.Next;
            children.Add(pager.Label);
            children.Add(pager.Prev);
            children.Add(pager.Next);
        }

        // ---- the detail -------------------------------------------------------------

        private void BuildDetail(List<UiNode> children)
        {
            var icon = Ui.Sprite("GlossaryDetailIcon", null,
                    Place.At(GlossaryLayout.DetailIconCentreX, GlossaryLayout.DetailIconCentreY),
                    UiSize.Fixed(GlossaryLayout.DetailIconSize, GlossaryLayout.DetailIconSize))
                .AsDecor();

            float textCentre = GlossaryLayout.CentreX(GlossaryLayout.DetailTextLeft, GlossaryLayout.DetailTextWidth);

            var name = Text("GlossaryDetailName", UiString.Runtime,
                new UiVec(GlossaryLayout.DetailTextWidth, GlossaryLayout.DetailNameHeight), 28, Heading,
                Place.At(textCentre, GlossaryLayout.DetailNameCentreY), TypographyRole.Body, UiTextAlign.Left);

            var meta = Text("GlossaryDetailMeta", UiString.Runtime,
                new UiVec(GlossaryLayout.DetailTextWidth, GlossaryLayout.DetailMetaHeight), 20, Muted,
                Place.At(textCentre, GlossaryLayout.DetailMetaCentreY), TypographyRole.Body, UiTextAlign.Left);

            float bodyCentre = GlossaryLayout.CentreX(GlossaryLayout.DetailLeft, GlossaryLayout.DetailWidth);

            var body = Text("GlossaryDetailBody", UiString.Runtime,
                new UiVec(GlossaryLayout.DetailWidth, GlossaryLayout.DetailBodyHeight), 20, Body,
                Place.At(bodyCentre, GlossaryLayout.DetailBodyCentreY), TypographyRole.Body, UiTextAlign.TopLeft);

            var lockedBy = Ui.Label("GlossaryDetailLockedBy", UiStrings.GlossaryLockedBy,
                    new UiVec(GlossaryLayout.DetailWidth, GlossaryLayout.DetailLockedByHeight), 20, LockedNote,
                    Place.At(bodyCentre, GlossaryLayout.DetailLockedByCentreY))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft)
                .AsDecor()
                .Inactive();

            DetailIcon = icon;
            DetailName = name;
            DetailMeta = meta;
            DetailBody = body;
            DetailLockedBy = lockedBy;

            children.Add(icon);
            children.Add(name);
            children.Add(meta);
            children.Add(body);
            children.Add(lockedBy);
        }
    }
}
