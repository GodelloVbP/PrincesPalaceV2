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
                    Place.At(0f, 430f)).AsDecor(),
            };

            // --- currency grants -------------------------------------------------
            var gold = Ui.Button("DebugGiveGoldButton", UiStrings.DebugGiveGold, new UiVec(280f, 56f), 20,
                Place.At(-310f, 348f));
            var embers = Ui.Button("DebugGiveEmbersButton", UiStrings.DebugGiveEmbers, new UiVec(280f, 56f), 20,
                Place.At(0f, 348f));
            var oneEmber = Ui.Button("DebugGiveOneEmberButton", UiStrings.DebugGiveOneEmber, new UiVec(280f, 56f), 20,
                Place.At(310f, 348f));

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

            for (int i = 0; i < filters.Length; i++)
            {
                // Evenly spaced about centre: four buttons 210 wide on a 220
                // pitch, so the row is symmetric however many there are.
                float x = (i - (filters.Length - 1) * 0.5f) * 220f;
                var filter = Ui.Button($"DebugFilter{i}", filters[i], new UiVec(210f, 48f), 18,
                    Place.At(x, 276f));

                screen.FilterButtons.Add(filter);
                chrome.Add(filter);
            }

            // --- the item list ------------------------------------------------------
            for (int i = 0; i < DebugMenuCatalog.RowsPerPage; i++)
            {
                float y = ListTop - i * (RowHeight + RowSpacing);

                var row = Ui.Button($"DebugRow{i}", UiString.Runtime, new UiVec(RowWidth, RowHeight), 16,
                    Place.At(0f, y));

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

            var page = Ui.Label("DebugPageLabel", UiStrings.DebugPage, new UiVec(320f, 40f), 18, "#B8A8D9",
                Place.At(0f, pagerY)).AsDecor();
            var prev = Ui.Button("DebugPrevPage", UiStrings.TalentPrev, new UiVec(56f, 44f), 20,
                Place.At(-260f, pagerY));
            var next = Ui.Button("DebugNextPage", UiStrings.TalentNext, new UiVec(56f, 44f), 20,
                Place.At(260f, pagerY));

            screen.PageLabel = page;
            screen.PrevPageButton = prev;
            screen.NextPageButton = next;
            chrome.Add(page);
            chrome.Add(prev);
            chrome.Add(next);

            var close = Ui.Button("DebugCloseButton", UiStrings.Close, new UiVec(220f, 56f), 18,
                Place.At(0f, -470f));
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
