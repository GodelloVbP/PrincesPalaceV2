using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.DebugMenu;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The debug menu: a category rail, an item picker, and the Resources/
    // Tools verbs that share its list.
    //
    // Coordinates live INLINE here rather than in a DebugMenuAnchors sibling,
    // unlike HubAnchors and DossierLayout. Those exist because their layouts
    // are irregular -- eight slots placed against a painted body, buildings
    // staged in depth -- and every number needed one home two files could
    // read. This is a title, a category rail, a sub-filter
    // row, a two-column list and a grant bar. Splitting it would be
    // ceremony.
    //
    // It is deliberately plain. A debug tool that takes design effort is a
    // debug tool that stops getting extended.
    //
    // BUILT AROUND DebugMenuCatalog's category/sub-filter model: a category
    // rail (left) over a 2x12 list, with a grant bar underneath holding the
    // plus/quantity modifiers. Gold, embers and levels are rows on the
    // Resources tab, beside the Tools tab's verbs, because a fixed button
    // row cannot hold one row per character without the tree knowing the
    // roster.
    public sealed class DebugMenuScreen
    {
        public UiNode Root;

        public NodeRef CloseButton;

        // One per DebugCategory, in its declared order -- the controller
        // indexes both with the same number.
        public List<NodeRef> CategoryButtons = new List<NodeRef>();

        // POOLED at 12 -- the largest sub-filter axis (tier chips, All+0..10).
        // Equipment's slot chips use the first 7 and the controller hides the
        // rest; Consumables/Sets hide all twelve. One pool rather than two
        // differently-sized rows so the tree does not have to know in
        // advance which axis a category will need.
        //
        // ThemedPlate + an explicit caption child, NOT plain Themed() -- a
        // tier chip reads "T4", a slot chip reads "GLOVES", and which one a
        // given pooled button shows changes every time the category does.
        // Themed() would bake the button's construction-time text in as its
        // generated caption with nothing for the controller to repaint.
        public List<NodeRef> SubFilterButtons = new List<NodeRef>();
        public List<NodeRef> SubFilterLabels = new List<NodeRef>();

        // Indexed 0..RowsPerPage-1, refilled per page at runtime. ROW MAJOR
        // -- index i sits in column i % Columns, row i / Columns -- same
        // order ShopController's gear grid already uses, and for the same
        // reason: UiNavLinkBuilder's Grid kind (UiNavLinkBuilder.cs) walks a
        // group's member LIST assuming consecutive entries are adjacent
        // columns in one row, gridRowLength members per row. A column-major
        // list would make DebugRow1 (visually below DebugRow0) its Grid
        // neighbour to the RIGHT instead of below -- laid out here in the
        // order the nav math needs, not the order that reads prettiest on
        // paper.
        public List<NodeRef> RowButtons = new List<NodeRef>();
        public List<NodeRef> RowLabels = new List<NodeRef>();

        public NodeRef PageLabel;
        public NodeRef PrevPageButton;
        public NodeRef NextPageButton;

        // The grant bar: PLUS and QUANTITY are sticky modifiers (contract 3),
        // not per-row -- one stepper and one x1/x5/x10 selector serve every
        // row on every page until the menu closes.
        public NodeRef PlusMinusButton;
        public NodeRef PlusPlusButton;
        public NodeRef PlusLabel;
        public List<NodeRef> QtyButtons = new List<NodeRef>();
        public NodeRef ToastLabel;

        private const float RowWidth = 400f;
        private const float RowHeight = 30f;
        private const float RowSpacing = 4f;
        private const float ListTop = 175f;
        private const float ColumnGapHalf = 30f;

        public static DebugMenuScreen Build()
        {
            var screen = new DebugMenuScreen();

            var chrome = new List<UiNode>
            {
                Ui.Label("DebugTitle", UiStrings.DebugTitle, new UiVec(400f, 56f), 34, "#F2DB9E",
                    Place.At(0f, 430f)).AsDecor()
                    .Styled(TypographyRole.FunctionalHeading),
            };

            // --- category rail ------------------------------------------------------
            // A VERTICAL rail on the far left (x=-780), disjoint on X from the
            // sub-filter chips and the list, which is enough for A1 whatever
            // the frame. Silver throughout: a debug tool has no recommended
            // action or danger to colour-code.
            var categories = new[]
            {
                UiStrings.DebugCategoryWeapons,
                UiStrings.DebugCategoryEquipment,
                UiStrings.DebugCategoryConsumables,
                UiStrings.DebugCategorySets,
                UiStrings.DebugCategoryBooks,
                UiStrings.DebugCategoryRelics,
                UiStrings.DebugCategoryResources,
                UiStrings.DebugCategoryTools,
            };

            const float RailX = -780f;
            const float RailTop = 290f;
            const float RailPitch = 66f;

            for (int i = 0; i < categories.Length; i++)
            {
                var railSize = Ui.PlateNominalSizeFor(180f, 50f);
                var category = Ui.Button($"DebugCategory{i}", categories[i], railSize, 17,
                        Place.At(RailX, RailTop - i * RailPitch))
                    .Themed(ButtonTheme.Silver);

                screen.CategoryButtons.Add(category);
                chrome.Add(category);
            }

            // --- sub-filter row -------------------------------------------------------
            // Pooled 12-wide; SEE SubFilterButtons' own doc comment for why
            // one pool serves both the tier axis and the slot axis.
            const float SubFilterY = 255f;
            const float SubFilterPitch = 74f;
            int subFilterCount = 12;

            for (int i = 0; i < subFilterCount; i++)
            {
                float x = (i - (subFilterCount - 1) * 0.5f) * SubFilterPitch;
                var chipSize = Ui.PlateNominalSizeFor(66f, 44f);

                // ThemedPlate + an explicit caption -- see SubFilterLabels'
                // own doc comment on the field above for why this cannot be
                // plain Themed().
                var caption = Ui.Label($"DebugSubFilter{i}Caption", UiString.Runtime, new UiVec(chipSize.X - 6f, 20f),
                        13, "#EDE6FF", Place.At(0f, 0f))
                    .AsDecor();

                var chip = Ui.Button($"DebugSubFilter{i}", UiString.Runtime, chipSize, 13,
                        Place.At(x, SubFilterY))
                    .ThemedPlate(ButtonTheme.Silver);
                chip.Children.Add(caption);
                chip.LayerCaptionWithVisuals(caption);

                screen.SubFilterButtons.Add(chip);
                screen.SubFilterLabels.Add(caption);
                chrome.Add(chip);
            }

            // --- the item list: two columns x RowsPerColumn rows ---------------------
            // CHROMELESS, HAIRLINE ROW, same shape as CharacterDossierScreen.
            // BuildNavRow and this screen's own pre-overhaul list: a plate at
            // this width/height ratio clears no shape in the container kit's
            // tolerance (ThemedButtonAspectLintTests), so rows draw a single
            // hairline rule instead of a plate.
            for (int i = 0; i < DebugMenuCatalog.RowsPerPage; i++)
            {
                int column = i % DebugMenuCatalog.Columns;
                int row = i / DebugMenuCatalog.Columns;

                float x = column == 0
                    ? -ColumnGapHalf - RowWidth * 0.5f
                    : ColumnGapHalf + RowWidth * 0.5f;
                float y = ListTop - row * (RowHeight + RowSpacing);

                var rowNode = Ui.Button($"DebugRow{i}", UiString.Runtime, new UiVec(RowWidth, RowHeight), 1,
                        Place.At(x, y))
                    .NoChrome()
                    .Hovers(1.01f);

                rowNode.Children.Add(Ui.Solid($"DebugRow{i}Rule", "#4A3E5C", new UiVec(RowWidth, 1f),
                    Place.At(0f, -RowHeight * 0.5f + 0.5f)).AsDecor());

                // The label is a child so it can be left-aligned inside a
                // centred button without the button's own text fighting it.
                // NOT "DebugRow{i}Label" -- that is the name UiEmitter gives the
                // button's own generated caption, and the collision is invisible
                // to every by-name lookup. See UiAudit's A4b check.
                var label = Ui.Label($"DebugRow{i}Name", UiString.Runtime,
                        new UiVec(RowWidth - 30f, RowHeight - 6f), 14, "#EDE6FF", Place.At(0f, 0f))
                    .AsDecor();
                rowNode.Children.Add(label);

                screen.RowButtons.Add(rowNode);
                screen.RowLabels.Add(label);
                chrome.Add(rowNode);
            }

            // --- pager --------------------------------------------------------------
            const float PagerY = -260f;

            var pager = Ui.Pager("DebugPrevPage", Place.At(-260f, PagerY),
                "DebugNextPage", Place.At(260f, PagerY), new UiVec(56f, 44f), 20,
                "DebugPageLabel", UiStrings.DebugPage, Place.At(0f, PagerY),
                new UiVec(320f, 40f), 18, "#B8A8D9");
            pager.Label.AsDecor();

            screen.PageLabel = pager.Label;
            screen.PrevPageButton = pager.Prev;
            screen.NextPageButton = pager.Next;
            chrome.Add(pager.Label);
            chrome.Add(pager.Prev);
            chrome.Add(pager.Next);

            // --- grant bar: plus stepper, quantity selector, toast -------------------
            const float GrantBarY = -330f;

            // NoChrome, same as the pager arrows -- every plate shape the kit
            // has is a wide rectangle (3:1 at the narrowest, ButtonPlateArt's
            // own header), and a "-"/"+" stepper is the one control on this
            // screen that is honestly square. Forcing it onto a 3:1 plate is
            // what PagerPrev/PagerNext already refuse to do.
            var minus = Ui.Button("DebugPlusMinusButton", UiStrings.DebugPlusMinus, new UiVec(50f, 50f), 24,
                    Place.At(-160f, GrantBarY))
                .NoChrome();
            var plusLabel = Ui.Label("DebugPlusLabel", UiStrings.DebugPlusValue, new UiVec(140f, 44f), 20,
                    "#EDE6FF", Place.At(-60f, GrantBarY))
                .AsDecor();
            var plus = Ui.Button("DebugPlusPlusButton", UiStrings.DebugPlusPlus, new UiVec(50f, 50f), 24,
                    Place.At(40f, GrantBarY))
                .NoChrome();

            screen.PlusMinusButton = minus;
            screen.PlusLabel = plusLabel;
            screen.PlusPlusButton = plus;
            chrome.Add(minus);
            chrome.Add(plusLabel);
            chrome.Add(plus);

            var qtyTexts = new[] { UiStrings.DebugQty1, UiStrings.DebugQty5, UiStrings.DebugQty10 };
            for (int i = 0; i < qtyTexts.Length; i++)
            {
                // 120 wide, not 90: the plate's 3:1 nominal at 90 is 30 tall,
                // and an 18pt caption needs 32 -- the text-fit audit refused it.
                float x = 230f + i * 130f;
                var qtySize = Ui.PlateNominalSizeFor(120f, 46f);
                var qty = Ui.Button($"DebugQty{i}", qtyTexts[i], qtySize, 18,
                        Place.At(x, GrantBarY))
                    .Themed(ButtonTheme.Silver);

                screen.QtyButtons.Add(qty);
                chrome.Add(qty);
            }

            // --- toast ---------------------------------------------------------------
            // One line, no modal, no sound -- contract 7. UiString.Runtime,
            // same as every other label this screen fills at runtime rather
            // than at build time -- there is nothing to say until the first
            // grant happens.
            var toast = Ui.Label("DebugToastLabel", UiString.Runtime, new UiVec(900f, 30f), 16,
                    "#B8A8D9", Place.At(0f, -395f))
                .AsDecor();
            screen.ToastLabel = toast;
            chrome.Add(toast);

            // 220x56 was 3.929:1 against the FiveByOne plate's true 5:1 --
            // ThemedButtonAspectLintTests. Height down to nominal, width kept.
            var closeSize = Ui.PlateNominalSizeFor(220f, 56f);
            var close = Ui.Button("DebugCloseButton", UiStrings.Close, closeSize, 18,
                    Place.At(0f, -470f))
                .Themed(ButtonTheme.Silver);
            screen.CloseButton = close;
            chrome.Add(close);

            var content = Ui.Panel("DebugMenuContent", Place.At(0f, 0f), UiSize.Fill, chrome);

            // Near-opaque. Unlike the character overlay, there is nothing behind
            // this worth seeing -- it is a tool, not part of the fiction, and
            // reading item names over a painted nebula is worse than reading
            // them over a flat field.
            screen.Root = Ui.Modal("DebugMenuPanel", "#0A0614FA", content).Inactive();
            return screen;
        }
    }
}
