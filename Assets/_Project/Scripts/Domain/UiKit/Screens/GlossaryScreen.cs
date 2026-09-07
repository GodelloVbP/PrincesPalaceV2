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
        // Silver, 2:1 KIT CONTAINER (owner's HQ-kit instruction,
        // 2026-09-07), replacing a flat #241736F5 panel authored at
        // 1700x900. TwoByOne (aspect 2.0) is the nearest kit ratio to that
        // working shape (1.89, a 5.5% miss -- closer than ThreeByTwo's 1.5,
        // which misses by 26%), but even TwoByOne needs the box GROWN to
        // pass ValidateContainerAspect at a size that still contains every
        // child once TwoByOne's own inset (3.5% each side, 5.5% top/bottom)
        // is carved out of it: CloseButton's 840px right edge and the
        // title/pager rows' ~417px top/bottom edges are the two extremes
        // that set the floor. 945 clears both with room to spare (content
        // half-width 872, half-height 420) and keeps the frame inside the
        // +-960/+-540 safe area every UiFrames.All frame guarantees at
        // minimum -- PanelWidth/PanelHeight derive from it rather than
        // restating the two numbers this replaces.
        private static readonly UiVec FrameSize = Ui.ContainerSizeForHeight(ContainerRatio.TwoByOne, 945f);
        public static float PanelWidth => FrameSize.X;
        public static float PanelHeight => FrameSize.Y;

        private const float RailX = -700f;
        private const float RailTop = 300f;

        // 84, up from 76: BuildCategory's plate grew 66 -> 80 tall (see its
        // own comment -- 240x66 was 21.2% off the Legacy plate's true 3:1,
        // ThemedButtonAspectLintTests) and needs at least that much pitch to
        // not overlap its neighbour. Seven categories at 84 span RailTop
        // down to RailTop - 6*84 = -204, comfortably inside the 2:1 frame's
        // +-420 content half-height.
        private const float RailPitch = 84f;

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

            var pager = Ui.Pager("GlossaryPrevPage", Place.At(ListX - 200f, -390f),
                "GlossaryNextPage", Place.At(ListX + 200f, -390f), new UiVec(52f, 44f), 20,
                "GlossaryPageLabel", UiStrings.GlossaryPage, Place.At(ListX, -390f),
                new UiVec(260f, 34f), 16, "#B8A8D9");
            pager.Label.AsDecor();
            screen.PageLabel = pager.Label;
            screen.PrevPageButton = pager.Prev;
            screen.NextPageButton = pager.Next;
            inside.Add(pager.Label);
            inside.Add(pager.Prev);
            inside.Add(pager.Next);

            // ---- the plate ---------------------------------------------------
            //
            // Silver, 3:4 KIT CONTAINER (owner's HQ-kit instruction,
            // 2026-09-07), sized exactly on ThreeByFour rather than the flat
            // panel's old 660x780 (aspect 0.846, 12.8% off 0.75 -- more than
            // ValidateContainerAspect allows). Width holds at 660
            // (ContainerSizeForHeight keeps the old 780 and derives 585 --
            // narrower, not wider, so this container's own footprint SHRINKS
            // inside GlossaryFrame rather than pushing that frame's resize
            // further). The kit's own content inset (6.9% each side) then
            // leaves 504px of usable width where the flat panel had all 660,
            // so DetailName/Meta/Body/LockedBy are re-solved to 480 (was
            // 600) -- narrower text, same font size, comfortably inside.
            //
            // Every child here is now inside the Container's own
            // ContainerContent, so its coordinates are LOCAL TO THE PLATE
            // (0-centred) rather than offset by PlateX the way the flat
            // panel's siblings used to be -- PlateX now names only where the
            // plate itself sits in the frame.
            var detailPlateSize = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 780f);
            const float DetailContentWidth = 480f;

            var icon = Ui.Sprite("GlossaryDetailIcon", null, Place.At(0f, 250f),
                    UiSize.Fixed(140f, 140f))
                .AsDecor();
            var detailName = Ui.Label("GlossaryDetailName", UiString.Runtime, new UiVec(DetailContentWidth, 50f), 26,
                    "#EDE6FF", Place.At(0f, 140f))
                .AsDecor();
            var detailMeta = Ui.Label("GlossaryDetailMeta", UiString.Runtime, new UiVec(DetailContentWidth, 34f), 17,
                    "#B8A8D9", Place.At(0f, 92f))
                .AsDecor();
            var detailBody = Ui.Label("GlossaryDetailBody", UiString.Runtime, new UiVec(DetailContentWidth, 240f), 16,
                    "#9C8FC4", Place.At(0f, -60f))
                .AsDecor()
                .Styled(TypographyRole.Body);
            var lockedBy = Ui.Label("GlossaryDetailLockedBy", UiStrings.GlossaryLockedBy,
                    new UiVec(DetailContentWidth, 60f), 16, "#D9A87E", Place.At(0f, -230f))
                .AsDecor()
                .Inactive();

            screen.DetailIcon = icon;
            screen.DetailName = detailName;
            screen.DetailMeta = detailMeta;
            screen.DetailBody = detailBody;
            screen.DetailLockedBy = lockedBy;

            var detailPlate = Ui.Container("GlossaryDetailPlate", ButtonTheme.Silver, ContainerRatio.ThreeByFour,
                Place.At(PlateX, 0f), detailPlateSize);
            Ui.ContainerContent(detailPlate, ContainerRatio.ThreeByFour, "GlossaryDetailContent",
                icon, detailName, detailMeta, detailBody, lockedBy);
            inside.Add(detailPlate);

            // 200x54 was 3.704:1 against the Legacy plate's true 3:1 --
            // ThemedButtonAspectLintTests. Height up to nominal, width kept;
            // the extra height comes off the TOP (button re-centred so its
            // old top edge at y=417 is unchanged), because it grew toward
            // the frame's own top edge and 390 + new-half overflowed it by
            // 2.8px at every aspect -- UiAudit ChildContainment.
            const float CloseOldTopEdge = 390f + 54f * 0.5f;
            var closeSize = Ui.PlateNominalSizeFor(200f, 54f);
            var closeY = CloseOldTopEdge - closeSize.Y * 0.5f;
            var close = Ui.Button("GlossaryCloseButton", UiStrings.Close, closeSize, 16,
                    Place.At(740f, closeY))
                .Themed(ButtonTheme.Silver)
                // Floats over the detail plate's top-right corner by design
                // (declared after it, so it draws on top) -- the flat
                // #1E1430D0 panel this replaced was AsDecor and so exempt
                // from this check without saying so; GlossaryDetailPlate is
                // a real audited Container now (its OWN content has to be
                // checked, which is the whole point), so the corner overlap
                // it always had with Close needs stating instead of hiding.
                .AllowOverlap(
                    "GlossaryCloseButton floats over the detail plate's top-right corner by design - it is " +
                    "declared last so it draws on top, same as every close/X control that sits on the panel " +
                    "it closes");
            screen.CloseButton = close;
            inside.Add(close);

            var frame = Ui.Container("GlossaryFrame", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                Place.At(0f, 0f), FrameSize);
            Ui.ContainerContent(frame, ContainerRatio.TwoByOne, "GlossaryFrameContent", inside.ToArray());
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

            // 240x66 was 21.2% off the Legacy plate's true 3:1 --
            // ThemedButtonAspectLintTests. Height up to nominal (240x80),
            // width kept; RailPitch grew to match (its own comment says how
            // much room that leaves).
            var categorySize = Ui.PlateNominalSizeFor(240f, 66f);
            var button = Ui.Button($"GlossaryCategory{index}", UiString.Runtime,
                    categorySize, 12, Place.At(RailX, y))
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

        // CHROMELESS, HAIRLINE ROW -- was a Silver ThemedPlate at 500x60
        // (8.3:1), a ratio no plate shape lands within the container kit's
        // own tolerance of (ThemedButtonAspectLintTests). The honest resize
        // (500x83.3 on Row6x1) only fits 8 of GlossaryCatalog.RowsPerPage's
        // 10 rows in the list's vertical span between the title and the
        // pager, so this follows CharacterDossierScreen.BuildNavRow's
        // precedent instead, same as DebugMenuScreen.BuildRow's own row:
        // no plate, a hairline rule under the row.
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

            var rule = Ui.Solid($"GlossaryRow{index}Rule", "#4A3E5C", new UiVec(500f, 1f),
                    Place.At(0f, -30f + 0.5f))
                .AsDecor();

            var row = Ui.Button($"GlossaryRow{index}", UiString.Runtime,
                    new UiVec(500f, 60f), 1, Place.At(ListX, y))
                .NoChrome()
                .Hovers(1.01f);

            row.Children.Add(rule);
            row.Children.Add(marker);
            row.Children.Add(name);
            row.Children.Add(meta);

            Rows.Add(row);
            RowMarkers.Add(marker);
            RowNames.Add(name);
            RowMetas.Add(meta);
            return row;
        }
    }
}
