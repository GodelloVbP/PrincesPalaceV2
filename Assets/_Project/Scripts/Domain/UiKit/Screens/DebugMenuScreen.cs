using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.DebugMenu;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The debug menu: currency grants and an item picker.
    //
    // Coordinates live INLINE here rather than in a DebugMenuAnchors sibling,
    // unlike HubAnchors and OverlayAnchors. Those exist because their layouts
    // are irregular -- eight slots placed against a painted body, buildings
    // staged in depth -- and every number needed one home two files could
    // read. This is a title, three buttons, a filter row and a list. Splitting
    // it would be ceremony.
    //
    // It is deliberately plain. A debug tool that takes design effort is a
    // debug tool that stops getting extended.
    public sealed class DebugMenuScreen
    {
        public UiNode Root;

        public NodeRef CloseButton;
        public NodeRef GiveGoldButton;
        public NodeRef GiveEmbersButton;
        public NodeRef GiveOneEmberButton;

        public NodeRef PageLabel;
        public NodeRef PrevPageButton;
        public NodeRef NextPageButton;

        // Indexed to match DebugMenuCatalog's kind values: 0 Consumable,
        // 1 Weapon, 2 Equipment, plus KindAll at the front.
        public List<NodeRef> FilterButtons = new List<NodeRef>();

        // Indexed 0..RowsPerPage-1, refilled per page at runtime.
        public List<NodeRef> RowButtons = new List<NodeRef>();
        public List<NodeRef> RowLabels = new List<NodeRef>();

        private const float RowWidth = 900f;
        private const float RowHeight = 44f;
        private const float RowSpacing = 6f;
        private const float ListTop = 210f;

        public static DebugMenuScreen Build()
        {
            var screen = new DebugMenuScreen();

            var chrome = new List<UiNode>
            {
                Ui.Label("DebugTitle", UiStrings.DebugTitle, new UiVec(400f, 56f), 34, "#F2DB9E",
                    Place.At(0f, 430f)).AsDecor()
                    .Styled(TypographyRole.FunctionalHeading),
            };

            // --- currency grants -------------------------------------------------
            // Silver: a debug tool has no recommended action or danger to
            // colour-code, per the brief's own "Silver only" rule for this
            // screen.
            var gold = Ui.Button("DebugGiveGoldButton", UiStrings.DebugGiveGold, new UiVec(280f, 56f), 20,
                    Place.At(-310f, 348f))
                .Themed(ButtonTheme.Silver);
            var embers = Ui.Button("DebugGiveEmbersButton", UiStrings.DebugGiveEmbers, new UiVec(280f, 56f), 20,
                    Place.At(0f, 348f))
                .Themed(ButtonTheme.Silver);
            var oneEmber = Ui.Button("DebugGiveOneEmberButton", UiStrings.DebugGiveOneEmber, new UiVec(280f, 56f), 20,
                    Place.At(310f, 348f))
                .Themed(ButtonTheme.Silver);

            screen.GiveGoldButton = gold;
            screen.GiveEmbersButton = embers;
            screen.GiveOneEmberButton = oneEmber;
            chrome.Add(gold);
            chrome.Add(embers);
            chrome.Add(oneEmber);

            // --- kind filter ------------------------------------------------------
            var filters = new[]
            {
                UiStrings.DebugFilterAll,
                UiStrings.DebugFilterConsumable,
                UiStrings.DebugFilterWeapon,
                UiStrings.DebugFilterEquipment,
            };

            // SILVER: a pooled filter row at a pinned width (210x48) -- a tab
            // strip, but a plain one (the button's own text IS the caption,
            // no separate child), so plain Themed() fits without conflict.
            for (int i = 0; i < filters.Length; i++)
            {
                // Evenly spaced about centre: four buttons 210 wide on a 220
                // pitch, so the row is symmetric however many there are.
                float x = (i - (filters.Length - 1) * 0.5f) * 220f;
                // 210x48 was 4.375:1 against the plate's true FiveByOne 5:1 --
                // ThemedButtonAspectLintTests. Height down to the plate's own
                // nominal (Ui.PlateNominalSizeFor keeps the authored width),
                // width untouched so the 220 pitch between filters stays.
                var filterSize = Ui.PlateNominalSizeFor(210f, 48f);
                var filter = Ui.Button($"DebugFilter{i}", filters[i], filterSize, 18,
                        Place.At(x, 276f))
                    .Themed(ButtonTheme.Silver);

                screen.FilterButtons.Add(filter);
                chrome.Add(filter);
            }

            // --- the item list ------------------------------------------------------
            // CHROMELESS, HAIRLINE ROW: was a ThemedPlate at 900x44 (20.5:1),
            // a ratio no plate shape gets within the container kit's own
            // tolerance of -- ThemedButtonAspectLintTests. 900x150 (Row6x1's
            // nominal for this width) would only fit 6 of RowsPerPage on
            // screen, so this follows CharacterDossierScreen.BuildNavRow's
            // precedent instead: no plate at all, a single hairline rule
            // under the row, hover/press from the default hover-scale.
            for (int i = 0; i < DebugMenuCatalog.RowsPerPage; i++)
            {
                float y = ListTop - i * (RowHeight + RowSpacing);

                var row = Ui.Button($"DebugRow{i}", UiString.Runtime, new UiVec(RowWidth, RowHeight), 1,
                        Place.At(0f, y))
                    .NoChrome()
                    .Hovers(1.01f);

                row.Children.Add(Ui.Solid($"DebugRow{i}Rule", "#4A3E5C", new UiVec(RowWidth, 1f),
                    Place.At(0f, -RowHeight * 0.5f + 0.5f)).AsDecor());

                // The label is a child so it can be left-aligned inside a
                // centred button without the button's own text fighting it.
                // NOT "DebugRow{i}Label" -- that is the name UiEmitter gives the
                // button's own generated caption, and the collision is invisible
                // to every by-name lookup. See UiAudit's A4b check.
                var label = Ui.Label($"DebugRow{i}Name", UiString.Runtime,
                        new UiVec(RowWidth - 40f, RowHeight - 8f), 16, "#EDE6FF", Place.At(0f, 0f))
                    .AsDecor();
                row.Children.Add(label);

                screen.RowButtons.Add(row);
                screen.RowLabels.Add(label);
                chrome.Add(row);
            }

            // --- pager --------------------------------------------------------------
            float pagerY = ListTop - DebugMenuCatalog.RowsPerPage * (RowHeight + RowSpacing) - 26f;

            var pager = Ui.Pager("DebugPrevPage", Place.At(-260f, pagerY),
                "DebugNextPage", Place.At(260f, pagerY), new UiVec(56f, 44f), 20,
                "DebugPageLabel", UiStrings.DebugPage, Place.At(0f, pagerY),
                new UiVec(320f, 40f), 18, "#B8A8D9");
            pager.Label.AsDecor();

            screen.PageLabel = pager.Label;
            screen.PrevPageButton = pager.Prev;
            screen.NextPageButton = pager.Next;
            chrome.Add(pager.Label);
            chrome.Add(pager.Prev);
            chrome.Add(pager.Next);

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
            // reading 12 item names over a painted nebula is worse than reading
            // them over a flat field.
            screen.Root = Ui.Modal("DebugMenuPanel", "#0A0614FA", content).Inactive();
            return screen;
        }
    }
}
