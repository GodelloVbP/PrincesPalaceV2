using System.Collections.Generic;
using PrincesPalace.Domain.Glossary;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // Everything the game contains, in one place.
    //
    // Three columns: what KIND of thing (the rail), which one (the list), and
    // what it is (the plate). That shape is why this is one screen rather than
    // six -- a category is a value in GlossaryCategory plus a case in the Core
    // adapter, and the tree below never learns what a relic is.
    //
    // Replaces the Relics building, which had been dimmed-and-unpressable
    // since the hub was built.
    public sealed class GlossaryScreen
    {
        public const float PanelWidth = 1700f;
        public const float PanelHeight = 900f;

        private const float RailX = -700f;
        private const float RailTop = 300f;
        private const float RailPitch = 76f;

        private const float ListX = -180f;
        private const float ListTop = 290f;
        private const float ListPitch = 68f;

        private const float PlateX = 480f;

        public UiNode Root;
        public NodeRef Frame;
        public NodeRef CloseButton;

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

            var inside = new List<UiNode>
            {
                Ui.Label("GlossaryTitle", UiStrings.GlossaryTitle, new UiVec(900f, 56f), 34, "#F2DB9E",
                    Place.At(-360f, 388f)).AsDecor()
                    .Styled(TypographyRole.FunctionalHeading),
            };

            // ---- the rail: one button per category, built from the enum -----
            for (int i = 0; i < GlossaryCatalog.Categories.Count; i++)
            {
                inside.Add(screen.BuildCategory(i, GlossaryCatalog.Categories[i]));
            }

            // ---- the list ---------------------------------------------------
            for (int i = 0; i < GlossaryCatalog.RowsPerPage; i++)
            {
                inside.Add(screen.BuildRow(i));
            }

            var empty = Ui.Label("GlossaryEmptyHint", UiStrings.GlossaryEmpty, new UiVec(480f, 40f), 18,
                    "#7E6E9E", Place.At(ListX, 0f))
                .AsDecor()
                .Inactive();
            screen.EmptyHint = empty;
            inside.Add(empty);

            var page = Ui.Label("GlossaryPageLabel", UiStrings.GlossaryPage, new UiVec(260f, 34f), 16,
                    "#B8A8D9", Place.At(ListX, -390f))
                .AsDecor();
            // NoChrome: narrow arrow icon buttons, no plate to reskin.
            var prev = Ui.Button("GlossaryPrevPage", UiStrings.TalentPrev, new UiVec(52f, 44f), 20,
                Place.At(ListX - 200f, -390f)).NoChrome();
            var next = Ui.Button("GlossaryNextPage", UiStrings.TalentNext, new UiVec(52f, 44f), 20,
                Place.At(ListX + 200f, -390f)).NoChrome();
            screen.PageLabel = page;
            screen.PrevPageButton = prev;
            screen.NextPageButton = next;
            inside.Add(page);
            inside.Add(prev);
            inside.Add(next);

            // ---- the plate ---------------------------------------------------
            var icon = Ui.Sprite("GlossaryDetailIcon", null, Place.At(PlateX, 250f),
                    UiSize.Fixed(140f, 140f))
                .AsDecor();
            var detailName = Ui.Label("GlossaryDetailName", UiString.Runtime, new UiVec(600f, 50f), 26,
                    "#EDE6FF", Place.At(PlateX, 140f))
                .AsDecor();
            var detailMeta = Ui.Label("GlossaryDetailMeta", UiString.Runtime, new UiVec(600f, 34f), 17,
                    "#B8A8D9", Place.At(PlateX, 92f))
                .AsDecor();
            var detailBody = Ui.Label("GlossaryDetailBody", UiString.Runtime, new UiVec(600f, 240f), 16,
                    "#9C8FC4", Place.At(PlateX, -60f))
                .AsDecor()
                .Styled(TypographyRole.Body);
            var lockedBy = Ui.Label("GlossaryDetailLockedBy", UiStrings.GlossaryLockedBy, new UiVec(600f, 60f), 16,
                    "#D9A87E", Place.At(PlateX, -230f))
                .AsDecor()
                .Inactive();

            screen.DetailIcon = icon;
            screen.DetailName = detailName;
            screen.DetailMeta = detailMeta;
            screen.DetailBody = detailBody;
            screen.DetailLockedBy = lockedBy;

            inside.Add(Ui.Panel("GlossaryDetailPlate", Place.At(PlateX, 0f), UiSize.Fixed(660f, 780f))
                .Coloured("#1E1430D0").AsDecor());
            inside.Add(icon);
            inside.Add(detailName);
            inside.Add(detailMeta);
            inside.Add(detailBody);
            inside.Add(lockedBy);

            var close = Ui.Button("GlossaryCloseButton", UiStrings.Close, new UiVec(200f, 54f), 16,
                    Place.At(740f, 390f))
                .Themed(ButtonTheme.Silver);
            screen.CloseButton = close;
            inside.Add(close);

            var frame = Ui.Panel("GlossaryFrame", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), inside)
                .Coloured("#241736F5");
            screen.Frame = frame;

            var content = Ui.Panel("GlossaryContent", Place.At(0f, 0f), UiSize.Fill, frame);
            screen.Root = Ui.Modal("GlossaryPanel", "#0A0614F0", content).Inactive();
            return screen;
        }

        // BLUE, CAPTION-PRESERVING PLATE. The brief called for plain
        // Themed(Blue), but this row already declares its own Marker/
        // Caption/Count children -- Themed() would add a SECOND, blank
        // generated Label (node.Text is UiString.Runtime here) sharing
        // Visuals' box without being LAYERED against them, which A1 would
        // correctly report as the generated label sitting on top of the
        // hand-authored caption. ThemedPlate() is the kit's own answer for
        // exactly this shape (see FightScreen's submenu rows and BACK row,
        // migrated the same way) -- same plate, no redundant label.
        private UiNode BuildCategory(int index, GlossaryCategory category)
        {
            float y = RailTop - index * RailPitch;

            // The marker is a separate layer from the label, so "which category
            // am I on" and "what is it called" stay two channels -- the same
            // split the draft cards make between selection and rarity.
            var marker = Ui.Solid($"GlossaryCategory{index}Marker", "#F2DB9E00",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(0f, 1f), right: -5f), UiSize.Fill)
                .AsDecor();

            // Caption, NOT Label. UiEmitter generates a child called
            // "<button>Label" for every button's own text, so a hand-declared
            // child by that name collides with it and every lookup silently
            // takes the emitter's empty one. A4 caught this on the first run.
            var label = Ui.Label($"GlossaryCategory{index}Caption", UiString.Runtime, new UiVec(180f, 32f), 19,
                    "#EDE6FF", Place.At(-14f, 10f))
                .AsDecor();

            var count = Ui.Label($"GlossaryCategory{index}Count", UiStrings.GlossaryCount, new UiVec(180f, 24f), 13,
                    "#7E6E9E", Place.At(-14f, -16f))
                .AsDecor();

            var button = Ui.Button($"GlossaryCategory{index}", UiString.Runtime,
                    new UiVec(240f, 66f), 12, Place.At(RailX, y))
                .ThemedPlate(ButtonTheme.Blue);

            button.Children.Add(marker);
            button.Children.Add(label);
            button.Children.Add(count);
            button.LayerCaptionWithVisuals(marker, label, count);

            CategoryButtons.Add(button);
            CategoryMarkers.Add(marker);
            CategoryLabels.Add(label);
            CategoryCounts.Add(count);
            return button;
        }

        // SILVER, CAPTION-PRESERVING PLATE -- same reason as BuildCategory
        // above.
        private UiNode BuildRow(int index)
        {
            float y = ListTop - index * ListPitch;

            var marker = Ui.Solid($"GlossaryRow{index}Marker", "#F2DB9E00",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(0f, 1f), right: -4f), UiSize.Fill)
                .AsDecor();

            var name = Ui.Label($"GlossaryRow{index}Name", UiString.Runtime, new UiVec(440f, 30f), 18,
                    "#EDE6FF", Place.At(-10f, 10f))
                .AsDecor();

            var meta = Ui.Label($"GlossaryRow{index}Meta", UiString.Runtime, new UiVec(440f, 22f), 13,
                    "#7E6E9E", Place.At(-10f, -14f))
                .AsDecor();

            var row = Ui.Button($"GlossaryRow{index}", UiString.Runtime,
                    new UiVec(500f, 60f), 12, Place.At(ListX, y))
                .ThemedPlate(ButtonTheme.Silver);

            row.Children.Add(marker);
            row.Children.Add(name);
            row.Children.Add(meta);
            row.LayerCaptionWithVisuals(marker, name, meta);

            Rows.Add(row);
            RowMarkers.Add(marker);
            RowNames.Add(name);
            RowMetas.Add(meta);
            return row;
        }
    }
}
