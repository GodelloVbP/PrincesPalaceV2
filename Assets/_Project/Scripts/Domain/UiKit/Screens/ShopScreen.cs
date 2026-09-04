using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The in-run shop: gear, spell books and relics for run gold, plus a
    // sell modal for the bag. Nested inside MapScreen exactly the way the
    // relic draft nests inside the hub (F10, docs/PLAN_SHOP.md) -- entered
    // from the map, no scene of its own, tools/screenshot.ps1 cannot see it
    // by name for the same reason the draft cannot.
    //
    // GATE 2 (docs/PLAN_SHOP.md §7.3): the counts are ShopStock's own
    // constants, decided from gate 1's batch rather than from a layout guess
    // (docs/handoffs/shop_v2/GAP_AUDIT.md, "Gate exit checks -> Gate 1" ->
    // "Decided 2026-09-03"). Four gear cards, three books, three relics.
    //
    // LAYOUT IS A SIMPLE VERTICAL STACK, not the handoff's five-panel pixel
    // grid (PLAN_SHOP.md §3f). That grid was drawn against a prototype with
    // painted borders and icon art neither of which exists yet for this
    // screen; a flat, hand-placed stack the layout audit can check today is
    // worth more than a grid copied from coordinates nothing has verified
    // against this screen's real content. Restyling to the five-panel shape
    // is recorded as follow-up, not attempted here.
    //
    // SINGLE GLOBAL SELECTION, not per-section arming (§7.1 point 6). One
    // card is selected at a time, across every section; a second press on
    // it or the BUY button commits. The detail label doubles as the
    // "tooltip" the plan asks for -- it shows whatever is selected, which
    // is a simplification of "floats near the focused-or-hovered card": a
    // fixed-position label rather than a runtime pointer-tracking overlay.
    // Recorded as follow-up, not silently substituted.
    //
    // EVERY CHILD'S Place.At IS RELATIVE TO ITS IMMEDIATE PARENT. Cards and
    // section headers are direct children of the root panel, so their
    // coordinates are screen-absolute; a card's own name/meta/price labels
    // are children of the CARD, so theirs are card-local. Getting that one
    // rule backwards on a nested element is the single easiest way to break
    // this file -- see BuildOfferCard and BuildPackRow for the two places it
    // matters.
    public sealed class ShopScreen
    {
        public const float CardWidth = 380f;
        public const float CardHeight = 150f;
        public const float CardGap = 24f;

        // One shape, reused for gear, books and relics -- the three sections
        // differ only in count and which content type resolves the id.
        public sealed class OfferCard
        {
            public NodeRef Button;
            public NodeRef Name;
            public NodeRef Meta;
            public NodeRef Price;
        }

        // One shape, reused for every pack row.
        public sealed class PackRow
        {
            public NodeRef Row;
            public NodeRef Name;
            public NodeRef Meta;
            public NodeRef Price;
            public NodeRef SellOne;
            public NodeRef SellAll;
        }

        // Rows shown at once in the PACK modal. Not the bag's own capacity --
        // a fixed count emitted at build time, exactly like the relic draft's
        // three cards, with paging (below) covering a bag bigger than this.
        public const int PackRowCount = 6;

        public UiNode Root;

        public NodeRef GoldLabel;
        public NodeRef LeaveButton;
        public NodeRef LeaveButtonLabel;
        public NodeRef DetailLabel;
        public NodeRef BuyButton;
        public NodeRef PackButton;

        public NodeRef GearHeader;
        public NodeRef GearReroll;
        public NodeRef GearRerollLabel;
        public List<OfferCard> GearCards = new List<OfferCard>();

        public NodeRef BookHeader;
        public NodeRef BookReroll;
        public NodeRef BookRerollLabel;
        public List<OfferCard> BookCards = new List<OfferCard>();

        public NodeRef RelicHeader;
        public NodeRef RelicReroll;
        public NodeRef RelicRerollLabel;
        public List<OfferCard> RelicCards = new List<OfferCard>();

        public NodeRef PackRoot;
        public NodeRef PackCloseButton;
        public NodeRef PackEmptyHint;
        public NodeRef PackPrevPage;
        public NodeRef PackNextPage;
        public NodeRef PackPageLabel;
        public List<PackRow> PackRows = new List<PackRow>();

        public static ShopScreen Build()
        {
            var screen = new ShopScreen();
            var children = new List<UiNode>();

            // Decor, not AllowOverlap: A1 already exempts a decor node from
            // the overlap check, so an allowance here would waive nothing --
            // it is the map underneath, covered on purpose, that the comment
            // is for.
            var background = Ui.Solid("ShopBackground", "#1A1024E6", Place.Stretch(), UiSize.Fill)
                .AsDecor();
            children.Add(background);

            var title = Ui.Label("ShopTitle", UiStrings.ShopTitle, new UiVec(400f, 48f), 34, "#F2DB9E",
                    Place.At(0f, 500f))
                .Styled(TypographyRole.CeremonialTitle);
            children.Add(title);

            var gold = Ui.Label("ShopGoldLabel", UiStrings.ShopGold, new UiVec(280f, 44f), 22, "#F2DB9E",
                    Place.At(-800f, 500f))
                .Styled(TypographyRole.TacticalData);
            screen.GoldLabel = gold;
            children.Add(gold);

            var leave = Ui.Button("ShopLeaveButton", UiStrings.ShopLeave, new UiVec(220f, 56f), 22,
                    Place.At(800f, 500f))
                .Themed(ButtonTheme.Silver);
            screen.LeaveButton = leave;
            screen.LeaveButtonLabel = ThemedLabelOf(leave);
            children.Add(leave);

            // Three section blocks, each header+cards 206px tall (32 header +
            // 24 header-to-cards gap + 150 cards), 40px apart -- see
            // BuildSection's own comment for the arithmetic a header-y/
            // cards-y pair has to satisfy. Gear at the top, Relics at the
            // bottom, Books between: no ordering requirement forced this,
            // it just reads as "the two sections most players compare"
            // bracketing the smaller one.
            children.AddRange(screen.BuildSection("Gear", UiStrings.ShopSectionGear, 414f, 295f,
                ShopStock.GearCount, out screen.GearHeader, out screen.GearReroll, out screen.GearRerollLabel,
                screen.GearCards));
            children.AddRange(screen.BuildSection("Book", UiStrings.ShopSectionBooks, 164f, 45f,
                ShopStock.BookCount, out screen.BookHeader, out screen.BookReroll, out screen.BookRerollLabel,
                screen.BookCards));
            children.AddRange(screen.BuildSection("Relic", UiStrings.ShopSectionRelics, -86f, -205f,
                ShopStock.RelicCount, out screen.RelicHeader, out screen.RelicReroll, out screen.RelicRerollLabel,
                screen.RelicCards));

            var detail = Ui.Label("ShopDetailLabel", UiStrings.ShopDetailEmpty, new UiVec(1600f, 50f), 18,
                    "#C8BBE4", Place.At(0f, -340f))
                .Styled(TypographyRole.Body);
            screen.DetailLabel = detail;
            children.Add(detail);

            var buy = Ui.Button("ShopBuyButton", UiStrings.ShopBuy, new UiVec(300f, 64f), 24,
                    Place.At(-160f, -430f))
                .Themed(ButtonTheme.Gold);
            screen.BuyButton = buy;
            children.Add(buy);

            var pack = Ui.Button("ShopPackButton", UiStrings.ShopPack, new UiVec(300f, 64f), 24,
                    Place.At(160f, -430f))
                .Themed(ButtonTheme.Blue);
            screen.PackButton = pack;
            children.Add(pack);

            children.Add(screen.BuildPackModal());

            screen.Root = Ui.Panel("ShopPanel", UiSize.Fill, children).Inactive();
            return screen;
        }

        // One section: a header label + that section's own reroll button on
        // one line, then a row of `count` cards centred under it. Returns a
        // FLAT list -- header, reroll, cards -- so every element lands as a
        // direct, screen-absolute-positioned child of the root panel rather
        // than inside an auto-sized wrapper (a Panel sized UiSize.FromChildren()
        // still places itself via Place.Stretch() under Ui.Panel's own
        // convenience overload, which would silently override every explicit
        // coordinate below it -- flattening sidesteps that entirely).
        //
        // THE CALLER OWES THIS METHOD ONE INEQUALITY: with a 32px header, a
        // 150px card row and the HeaderCardGap below, `headerY - cardsY`
        // must be at least 16 + HeaderCardGap + 75 (half the header, the
        // gap, half a card) or the header and the card row's top edge
        // overlap -- caught by UiAudit's SiblingOverlap, not by this method,
        // since two absolute y values with no relationship declared between
        // them is exactly the class of bug the solver exists to catch
        // rather than trust.
        private const float HeaderHalfHeight = 16f;
        private const float HeaderCardGap = 24f;

        // Half the header width, in from the panel's own +-960 edge by this
        // margin -- 190 (header half-width) + 10 keeps the header's left
        // edge inside the panel at every UiFrames aspect this screen is
        // checked at, all of which are >= 1920 wide.
        private const float HeaderX = 760f;

        private List<UiNode> BuildSection(string prefix, UiString headerText, float headerY, float cardsY,
            int count, out NodeRef header, out NodeRef reroll, out NodeRef rerollLabel, List<OfferCard> cards)
        {
            var headerLabel = Ui.Label($"Shop{prefix}Header", headerText, new UiVec(380f, 32f), 22, "#E7B25C",
                    Place.At(-HeaderX, headerY))
                .Styled(TypographyRole.FunctionalHeading);
            header = headerLabel;

            var rerollButton = Ui.Button($"Shop{prefix}Reroll", UiStrings.ShopReroll, new UiVec(260f, 44f), 16,
                    Place.At(HeaderX, headerY))
                .Themed(ButtonTheme.Silver);
            reroll = rerollButton;
            rerollLabel = ThemedLabelOf(rerollButton);

            float pitch = CardWidth + CardGap;
            float firstX = -(count - 1) * pitch * 0.5f;

            var result = new List<UiNode> { headerLabel, rerollButton };
            for (int i = 0; i < count; i++)
            {
                var card = BuildOfferCard($"Shop{prefix}Card{i}", firstX + i * pitch, cardsY);
                cards.Add(card);
                result.Add(card.Button.Node);
            }

            return result;
        }

        // `x`/`y` are the CARD's own screen-absolute position; the three
        // labels below are its children, so THEIRS are card-local offsets
        // (0-centred), never x/y again.
        private OfferCard BuildOfferCard(string name, float x, float y)
        {
            var nameLabel = Ui.Label($"{name}Name", UiString.Runtime, new UiVec(340f, 30f), 20, "#EDE6FF",
                    Place.At(0f, 45f))
                .AsDecor();

            var metaLabel = Ui.Label($"{name}Meta", UiString.Runtime, new UiVec(340f, 24f), 15, "#B8A8D9",
                    Place.At(0f, 5f))
                .AsDecor();

            var priceLabel = Ui.Label($"{name}Price", UiStrings.ShopCardPrice, new UiVec(340f, 32f), 18,
                    "#FFD9A2", Place.At(0f, -45f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);

            var button = Ui.Button(name, UiString.Runtime, new UiVec(CardWidth, CardHeight), 12,
                    Place.At(x, y))
                .NoChrome();
            button.Children.Add(nameLabel);
            button.Children.Add(metaLabel);
            button.Children.Add(priceLabel);

            return new OfferCard { Button = button, Name = nameLabel, Meta = metaLabel, Price = priceLabel };
        }

        private UiNode BuildPackModal()
        {
            var background = Ui.Solid("ShopPackBackdrop", "#120A1AF2", Place.At(0f, 0f), UiSize.Fixed(1100f, 860f))
                .AsDecor();

            var titleLabel = Ui.Label("ShopPackTitle", UiStrings.ShopPack, new UiVec(400f, 40f), 28, "#F2DB9E",
                    Place.At(0f, 380f))
                .Styled(TypographyRole.FunctionalHeading);

            var close = Ui.Button("ShopPackClose", UiStrings.Close, new UiVec(160f, 48f), 18,
                    Place.At(460f, 380f))
                .Themed(ButtonTheme.Silver);
            PackCloseButton = close;

            var empty = Ui.Label("ShopPackEmpty", UiStrings.ShopPackEmpty, new UiVec(700f, 40f), 20,
                    "#8A7AA0", Place.At(0f, 0f))
                .Styled(TypographyRole.Body)
                .Inactive();
            PackEmptyHint = empty;

            const float RowHeight = 68f;
            const float RowGap = 8f;
            const float FirstRowY = 300f;

            var rowNodes = new List<UiNode>();
            for (int i = 0; i < PackRowCount; i++)
            {
                var row = BuildPackRow(i, FirstRowY - i * (RowHeight + RowGap), RowHeight);
                PackRows.Add(row);
                rowNodes.Add(row.Row.Node);
            }

            var prev = Ui.Button("ShopPackPrev", UiStrings.TalentPrev, new UiVec(56f, 56f), 20,
                    Place.At(-460f, -380f))
                .NoChrome()
                .Inactive();
            var next = Ui.Button("ShopPackNext", UiStrings.TalentNext, new UiVec(56f, 56f), 20,
                    Place.At(460f, -380f))
                .NoChrome()
                .Inactive();
            var pageLabel = Ui.Label("ShopPackPage", UiStrings.ShopPackPage, new UiVec(200f, 28f), 16,
                    "#B8A8D9", Place.At(0f, -380f))
                .Styled(TypographyRole.TacticalData)
                .Inactive();
            PackPrevPage = prev;
            PackNextPage = next;
            PackPageLabel = pageLabel;

            var content = new List<UiNode> { background, titleLabel, close, empty };
            content.AddRange(rowNodes);
            content.Add(prev);
            content.Add(next);
            content.Add(pageLabel);

            var panel = Ui.Panel("ShopPackContent", Place.At(0f, 0f), UiSize.Fixed(1100f, 860f), content);
            var modal = Ui.Modal("ShopPackModal", "#0A0614E0", panel).Inactive();
            PackRoot = modal;
            return modal;
        }

        // `y` is the ROW's own position, screen-relative-to-the-modal-content
        // (which itself sits at the screen's centre). Every element inside
        // the row is a CHILD of the row's wrapper panel, so theirs are
        // row-local: an x offset only, y = 0, never `y` again.
        private PackRow BuildPackRow(int index, float y, float height)
        {
            const float RowWidth = 980f;

            var name = Ui.Label($"ShopPackRow{index}Name", UiString.Runtime, new UiVec(320f, 26f), 18,
                    "#E9DFF8", Place.At(-300f, 0f))
                .AsDecor();

            var meta = Ui.Label($"ShopPackRow{index}Meta", UiString.Runtime, new UiVec(220f, 20f), 14,
                    "#9C8FC4", Place.At(-30f, 0f))
                .AsDecor();

            var price = Ui.Label($"ShopPackRow{index}Price", UiStrings.ShopSellPriceLabel, new UiVec(160f, 26f), 16,
                    "#FFD9A2", Place.At(180f, 0f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);

            var sellOne = Ui.Button($"ShopPackRow{index}SellOne", UiStrings.ShopSellOneButton,
                    new UiVec(90f, 40f), 14, Place.At(320f, 0f))
                .Themed(ButtonTheme.Green);

            // 120 wide, not 90: UiTextFitAudit measured "SELL ALL 99" (the
            // string's AuditSample) at 109px at size 14, and this was the
            // first scene build to run it against this screen. Sits flush
            // to the row's right edge (430 + 60 = 490 = RowWidth / 2);
            // SellOne moved 10px left to keep a 5px gap.
            var sellAll = Ui.Button($"ShopPackRow{index}SellAll", UiStrings.ShopSellAllButton,
                    new UiVec(120f, 40f), 14, Place.At(430f, 0f))
                .Themed(ButtonTheme.Crimson)
                .Inactive();

            var row = Ui.Panel($"ShopPackRow{index}", Place.At(0f, y), UiSize.Fixed(RowWidth, height),
                    name, meta, price, sellOne, sellAll)
                .AllowOverlap("a pack row's own bounding panel spans its children by construction; it draws nothing itself")
                .Inactive();

            return new PackRow { Row = row, Name = name, Meta = meta, Price = price, SellOne = sellOne, SellAll = sellAll };
        }

        // Themed() builds a <name>Label child in place (Ui.ApplyTheme) rather
        // than accepting one -- the label is generated FROM the button's own
        // declared text, not the other way round. A button whose caption
        // changes at runtime (this screen's LEAVE/REROLL) still needs a
        // NodeRef the wiring step can bind a TMP_Text to, and that generated
        // child's name is deterministic (`node.Name + "Label"`), so finding
        // it after Themed() is the whole trick.
        private static NodeRef ThemedLabelOf(UiNode themedButton) =>
            themedButton.Children.Find(c => c.Name == themedButton.Name + "Label");
    }
}
