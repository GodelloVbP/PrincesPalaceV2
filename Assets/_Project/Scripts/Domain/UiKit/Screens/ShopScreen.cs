using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The in-run shop: gear, spell books and relics for run gold, plus a
    // sell modal for the bag. Nested inside MapScreen exactly the way the
    // relic draft nests inside the hub (F10, docs/PLAN_SHOP.md) -- entered
    // from the map, no scene of its own, tools/screenshot.ps1 cannot see it
    // by name for the same reason the draft cannot. ShopCaptureTests is the
    // camera that can (tools/screenshot.ps1 -Runtime -RuntimeFilter
    // ShopCaptureTests).
    //
    // LAYOUT IS THE PROTOTYPE'S FIVE-PANEL GRID (docs/handoffs/shop_v2/
    // "Shop Screen v2.dc.html", which README.md §0 makes authoritative for
    // layout). The first build of this screen was a flat vertical stack of
    // bare text rows and said so in its own header: the grid was "copied from
    // coordinates nothing has verified against this screen's real content",
    // so it was recorded as follow-up instead. This is that follow-up. The
    // grid is now solved against the real content at all four audit frames
    // rather than trusted from the handoff's coordinate table.
    //
    // PROPORTIONS, NOT PIXELS, come from the prototype. Its CSS is authored
    // against its own viewport (the reference render is 924x540) and its
    // font sizes are roughly half this project's; every ratio below --
    // 1:1:1.3 columns, two equal rows, gear spanning the two narrow columns,
    // panel padding to card gap -- is the prototype's, while the type sizes
    // are this project's own TypographyRole bands. Copying its 11px metadata
    // line literally would have produced text half the size of every other
    // screen's.
    //
    // TWO DELIBERATE DEVIATIONS FROM THE PROTOTYPE, both decided upstream of
    // it in docs/PLAN_SHOP.md §7, which §0.7 of the handoff makes canonical
    // where the two disagree:
    //
    //   1. REROLL IS PER SECTION (§7.1 point 7), so each shelf's own
    //      REROLL button sits in that shelf's panel header rather than one
    //      whole-shop REROLL in the actions panel. The actions panel keeps
    //      gold, BUY, PACK and LEAVE.
    //   2. BUY EXISTS (§7.1 point 6). The prototype commits with a second
    //      press on the card; the review kept that and added BUY so keyboard
    //      and a future gamepad have one commit control. It takes the slot
    //      the prototype's REROLL occupied.
    //
    // WHAT THE PROTOTYPE HAS THAT THIS DOES NOT: a floating hover tooltip.
    // The detail line lives in the shopkeeper panel under the portrait slot
    // instead -- a fixed box the layout audit can see, rather than an overlay
    // positioned at runtime from a pointer coordinate it cannot. Recorded as
    // follow-up, same as it was before this pass.
    //
    // EVERY CHILD'S Place.At IS RELATIVE TO ITS IMMEDIATE PARENT. The five
    // panels are children of the root, so their coordinates are
    // screen-absolute; everything inside a panel is panel-local, and
    // everything inside a card is card-local. Getting that one rule backwards
    // on a nested element is the single easiest way to break this file.
    public sealed class ShopScreen
    {
        // ---- palette ----------------------------------------------------------
        private const string Ground = FightHudPalette.ShopGround;
        private const string PanelFill = FightHudPalette.CardFill;
        private const string PanelRim = FightHudPalette.BorderQuiet;
        private const string CardFill = FightHudPalette.CardFill;
        private const string CardRim = FightHudPalette.BorderQuiet;
        private const string ChipRim = FightHudPalette.BorderGold;
        private const string HeadingText = FightHudPalette.HeadingGold;
        private const string TitleText = FightHudPalette.TextPrimary;
        private const string NameText = FightHudPalette.TextPrimary;
        private const string MetaText = FightHudPalette.TextSecondary;
        private const string PriceText = FightHudPalette.GoldText;
        private const string GoldPlateFill = FightHudPalette.ScrollTrack;
        private const string GoldText = FightHudPalette.GoldLight;
        private const string DetailText = FightHudPalette.RowNameText;
        private const string QuietText = FightHudPalette.TextMuted;

        // ---- the outer grid ---------------------------------------------------
        //
        // The prototype's `padding:20px 28px`, a centred title, then a
        // 1fr/1fr/1.3fr by 1fr/1fr grid with an even gap. Written as
        // arithmetic from the frame edges rather than as a table of solved
        // numbers, so moving a margin moves everything that depends on it
        // instead of silently disagreeing with it.
        private const float ScreenHalfWidth = 960f;
        private const float ScreenHalfHeight = 540f;
        private const float MarginX = 56f;
        private const float MarginY = 40f;
        // 80, not 64: UiTextFitAudit measures CeremonialTitle at 56 needing
        // 75.5px of line box. Every box on this screen that a role's own
        // ascender/descender overruns is sized from that measurement rather
        // than from the font size, which is what the audit exists to force.
        private const float TitleHeight = 80f;
        private const float TitleGap = 28f;
        private const float ColGap = 24f;
        private const float RowGap = 24f;

        private const float TitleCentreY = ScreenHalfHeight - MarginY - TitleHeight * 0.5f;
        private const float GridTop = ScreenHalfHeight - MarginY - TitleHeight - TitleGap;
        private const float GridBottom = -ScreenHalfHeight + MarginY;
        private const float GridLeft = -ScreenHalfWidth + MarginX;
        private const float GridRight = ScreenHalfWidth - MarginX;

        private const float RowHeight = (GridTop - GridBottom - RowGap) * 0.5f;
        private const float Row1CentreY = GridTop - RowHeight * 0.5f;
        private const float Row2CentreY = GridBottom + RowHeight * 0.5f;

        // 532 + 24 + 532 + 24 + 696 = 1808 = the full inner width. The wide
        // column is the prototype's 1.3fr rounded to a whole number that
        // closes the row exactly -- an fr that does not add up leaves a
        // one-pixel seam the audit is right to notice.
        private const float NarrowColWidth = 532f;
        private const float WideColWidth = 696f;
        private const float Col1CentreX = GridLeft + NarrowColWidth * 0.5f;
        private const float Col2CentreX = GridLeft + NarrowColWidth + ColGap + NarrowColWidth * 0.5f;
        private const float Col3CentreX = GridRight - WideColWidth * 0.5f;
        private const float GearWidth = NarrowColWidth * 2f + ColGap;
        private const float GearCentreX = GridLeft + GearWidth * 0.5f;

        // ---- inside one panel -------------------------------------------------
        private const float PanelPad = 28f;
        // 48: FunctionalHeading at 34 needs a 45.1px line box.
        private const float PanelHeaderHeight = 48f;
        private const float PanelHeaderGap = 16f;
        private const float PanelHalfHeight = RowHeight * 0.5f;
        private const float HeaderCentreY = PanelHalfHeight - PanelPad - PanelHeaderHeight * 0.5f;
        private const float ContentTop = HeaderCentreY - PanelHeaderHeight * 0.5f - PanelHeaderGap;
        private const float ContentBottom = -PanelHalfHeight + PanelPad;
        private const float ContentHeight = ContentTop - ContentBottom;

        // The per-section REROLL, sat at the right end of its own panel's
        // header line. Sized here because the header label's width is what
        // is left over after it -- the two share one row and A1 checks that
        // they do not touch.
        private const float RerollWidth = 210f;
        private const float RerollHeight = 40f;
        private const float RerollGap = 12f;

        // ---- shelves ----------------------------------------------------------
        private const float ShelfCardGap = 16f;
        private const int GearColumns = 2;
        private const int GearRows = (ShopStock.GearCount + GearColumns - 1) / GearColumns;
        private const float GearCardGapX = 20f;
        private const float GearCardGapY = 16f;

        private const float ShelfCardWidth = NarrowColWidth - PanelPad * 2f;
        private const float GearCardWidth =
            (GearWidth - PanelPad * 2f - GearCardGapX * (GearColumns - 1)) / GearColumns;
        private const float GearCardHeight =
            (ContentHeight - GearCardGapY * (GearRows - 1)) / GearRows;

        // ---- one card's own metrics -------------------------------------------
        //
        // The prototype's card is one shape at two sizes: icon and name on the
        // first line, metadata and a price chip on the second. The gear cards
        // are 1.6x the height of a shelf card, so their padding, icon and chip
        // grow with them rather than floating in a box that is too big.
        private readonly struct CardMetrics
        {
            public readonly float Width;
            public readonly float Height;
            public readonly float Pad;
            public readonly float IconSize;
            public readonly float IconGap;
            public readonly float ChipWidth;
            public readonly float ChipHeight;
            public readonly int NameSize;
            public readonly int MetaSize;

            public CardMetrics(float width, float height, float pad, float iconSize, float iconGap,
                float chipWidth, float chipHeight, int nameSize, int metaSize)
            {
                Width = width;
                Height = height;
                Pad = pad;
                IconSize = iconSize;
                IconGap = iconGap;
                ChipWidth = chipWidth;
                ChipHeight = chipHeight;
                NameSize = nameSize;
                MetaSize = metaSize;
            }

            public float HalfWidth => Width * 0.5f;
            public float HalfHeight => Height * 0.5f;
            public float TopRowY => HalfHeight - Pad - IconSize * 0.5f;
            public float BottomRowY => -HalfHeight + Pad + ChipHeight * 0.5f;
            public float IconCentreX => -HalfWidth + Pad + IconSize * 0.5f;
            public float NameLeft => IconCentreX + IconSize * 0.5f + IconGap;
            public float NameWidth => HalfWidth - Pad - NameLeft;
            public float NameCentreX => NameLeft + NameWidth * 0.5f;
            public float ChipCentreX => HalfWidth - Pad - ChipWidth * 0.5f;
            public float MetaLeft => -HalfWidth + Pad;
            public float MetaRight => ChipCentreX - ChipWidth * 0.5f - IconGap;
            public float MetaWidth => MetaRight - MetaLeft;
            public float MetaCentreX => MetaLeft + MetaWidth * 0.5f;
        }

        // 98 tall for a shelf of three in a 326px content box; 155 for a gear
        // card in a 2x2. Both add up to less than their own box: 10+34+34+10
        // = 88 of 98, 22+52+44+22 = 140 of 155, and the slack is the breathing
        // room between the two lines.
        private static CardMetrics ShelfCard(float height) =>
            new CardMetrics(ShelfCardWidth, height, 10f, 34f, 14f, 180f, 34f, 28, 20);

        private static readonly CardMetrics GearCard =
            new CardMetrics(GearCardWidth, GearCardHeight, 22f, 52f, 14f, 200f, 44f, 30, 22);

        // One shape, reused for gear, books and relics -- the three sections
        // differ only in count and which content type resolves the id.
        public sealed class OfferCard
        {
            public NodeRef Button;
            public NodeRef Icon;
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
            // is for. Opaque now, per the design: see FightHudPalette.
            children.Add(Ui.Solid("ShopBackground", Ground, Place.Stretch(), UiSize.Fill).AsDecor());

            children.Add(Ui.Label("ShopTitle", UiStrings.ShopTitle, new UiVec(600f, TitleHeight), 56,
                    TitleText, Place.At(0f, TitleCentreY))
                .Styled(TypographyRole.CeremonialTitle));

            // Row 1: RELICS, SPELL BOOKS, SHOPKEEPER.
            children.Add(screen.BuildShelf("Relic", UiStrings.ShopSectionRelics,
                Col1CentreX, Row1CentreY, NarrowColWidth, ShopStock.RelicCount,
                out screen.RelicHeader, out screen.RelicReroll, out screen.RelicRerollLabel,
                screen.RelicCards));

            children.Add(screen.BuildShelf("Book", UiStrings.ShopSectionBooks,
                Col2CentreX, Row1CentreY, NarrowColWidth, ShopStock.BookCount,
                out screen.BookHeader, out screen.BookReroll, out screen.BookRerollLabel,
                screen.BookCards));

            children.Add(screen.BuildKeeperPanel());

            // Row 2: GEAR spanning both narrow columns, then the actions panel.
            children.Add(screen.BuildGearPanel());
            children.Add(screen.BuildActionsPanel());

            children.Add(screen.BuildPackModal());

            screen.Root = Ui.Panel("ShopPanel", UiSize.Fill, children).Inactive();
            return screen;
        }

        // ---- panels -----------------------------------------------------------

        // The fill, the rim and the header line every panel shares. Returns the
        // panel's own children so far; the caller appends its content and wraps
        // the lot. `reroll` is null for the two panels that have no shelf to
        // reroll, and when it is present the header label gives up exactly the
        // width the button takes so A1 never sees them touch.
        private static List<UiNode> PanelFrame(string name, UiString header, float width,
            out NodeRef headerRef, UiNode reroll)
        {
            var size = new UiVec(width, RowHeight);

            var parts = new List<UiNode>
            {
                Ui.Solid(name + "Fill", PanelFill, size, Place.At(0f, 0f)).AsDecor(),
            };
            parts.AddRange(Ui.Rim(name, size, PanelRim));

            float inner = width - PanelPad * 2f;
            float labelWidth = reroll == null ? inner : inner - RerollWidth - RerollGap;

            var label = Ui.Label(name + "Header", header, new UiVec(labelWidth, PanelHeaderHeight), 34,
                    HeadingText, Place.At(-inner * 0.5f + labelWidth * 0.5f, HeaderCentreY))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left);
            headerRef = label;
            parts.Add(label);

            if (reroll != null) parts.Add(reroll);

            return parts;
        }

        // One shelf panel: a header with its own REROLL, then `count` cards
        // stacked down the content box.
        private UiNode BuildShelf(string prefix, UiString header, float centreX, float centreY,
            float width, int count, out NodeRef headerRef, out NodeRef reroll, out NodeRef rerollLabel,
            List<OfferCard> cards)
        {
            // Themed(Silver), owner's HQ-kit instruction (2026-09-07),
            // replacing the hairline OutlineButton. 210x40 (5.25) is close
            // enough to the FiveByOne plate's 5.0 that the swap costs no
            // visible stretch.
            var rerollButton = Ui.Button($"Shop{prefix}Reroll", UiStrings.ShopReroll,
                    new UiVec(RerollWidth, RerollHeight), 22,
                    Place.At(width * 0.5f - PanelPad - RerollWidth * 0.5f, HeaderCentreY))
                .Themed(ButtonTheme.Silver);
            reroll = rerollButton;
            rerollLabel = Ui.CaptionOf(rerollButton);

            var parts = PanelFrame($"Shop{prefix}Panel", header, width, out headerRef, rerollButton);

            float cardHeight = (ContentHeight - ShelfCardGap * (count - 1)) / count;
            var metrics = ShelfCard(cardHeight);

            for (int i = 0; i < count; i++)
            {
                float y = ContentTop - cardHeight * 0.5f - i * (cardHeight + ShelfCardGap);
                var card = BuildOfferCard($"Shop{prefix}Card{i}", 0f, y, metrics);
                cards.Add(card);
                parts.Add(card.Button.Node);
            }

            return Ui.Panel($"Shop{prefix}Panel", Place.At(centreX, centreY),
                UiSize.Fixed(width, RowHeight), parts);
        }

        // GEAR: the same panel, but its cards sit in a 2-wide grid because the
        // panel spans two columns. Rows are derived from ShopStock.GearCount,
        // so a re-decided count (PLAN_SHOP §7.3 gate 1 explicitly reserves the
        // right) re-flows instead of overflowing.
        private UiNode BuildGearPanel()
        {
            // Themed(Silver) -- same conversion and same 5.25-vs-5.0 fit as
            // BuildShelf's own reroll above.
            var rerollButton = Ui.Button("ShopGearReroll", UiStrings.ShopReroll,
                    new UiVec(RerollWidth, RerollHeight), 22,
                    Place.At(GearWidth * 0.5f - PanelPad - RerollWidth * 0.5f, HeaderCentreY))
                .Themed(ButtonTheme.Silver);
            GearReroll = rerollButton;
            GearRerollLabel = Ui.CaptionOf(rerollButton);

            var parts = PanelFrame("ShopGearPanel", UiStrings.ShopSectionGear, GearWidth,
                out GearHeader, rerollButton);

            for (int i = 0; i < ShopStock.GearCount; i++)
            {
                int column = i % GearColumns;
                int row = i / GearColumns;

                float x = -(GearCardWidth + GearCardGapX) * (GearColumns - 1) * 0.5f
                          + column * (GearCardWidth + GearCardGapX);
                float y = ContentTop - GearCardHeight * 0.5f - row * (GearCardHeight + GearCardGapY);

                var card = BuildOfferCard($"ShopGearCard{i}", x, y, GearCard);
                GearCards.Add(card);
                parts.Add(card.Button.Node);
            }

            return Ui.Panel("ShopGearPanel", Place.At(GearCentreX, Row2CentreY),
                UiSize.Fixed(GearWidth, RowHeight), parts);
        }

        // SHOPKEEPER: a portrait slot with no portrait yet, and under it the
        // selected card's detail line. The detail is here rather than floating
        // over the cards because a runtime-positioned overlay is a rect the
        // layout audit cannot solve -- see this class's own header.
        private UiNode BuildKeeperPanel()
        {
            var parts = PanelFrame("ShopKeeperPanel", UiStrings.ShopSectionKeeper, WideColWidth,
                out _, null);

            float inner = WideColWidth - PanelPad * 2f;
            const float DetailHeight = 74f;
            const float SlotGap = 16f;
            float slotHeight = ContentHeight - DetailHeight - SlotGap;

            var pending = Ui.Label("ShopKeeperPending", UiStrings.ShopKeeperPending,
                    new UiVec(inner - 32f, 36f), 24, QuietText, Place.At(0f, 0f))
                .Styled(TypographyRole.Body)
                .AsDecor();

            parts.Add(Ui.OutlineBox("ShopKeeperSlot",
                Place.At(0f, ContentTop - slotHeight * 0.5f),
                new UiVec(inner, slotHeight), GoldPlateFill, PanelRim, new[] { pending }));

            var detail = Ui.Label("ShopDetailLabel", UiStrings.ShopDetailEmpty,
                    new UiVec(inner, DetailHeight), 22, DetailText,
                    Place.At(0f, ContentBottom + DetailHeight * 0.5f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft);
            DetailLabel = detail;
            parts.Add(detail);

            return Ui.Panel("ShopKeeperPanel", Place.At(Col3CentreX, Row1CentreY),
                UiSize.Fixed(WideColWidth, RowHeight), parts);
        }

        // SHOP: the gold total on its own plate, then the three controls that
        // are not a card -- BUY, PACK and, hard against the bottom edge with
        // the design's own gap above it, LEAVE.
        private UiNode BuildActionsPanel()
        {
            var parts = PanelFrame("ShopActionsPanel", UiStrings.ShopSectionActions, WideColWidth,
                out _, null);

            float inner = WideColWidth - PanelPad * 2f;
            const float PlateHeight = 62f;
            const float ActionGap = 16f;
            const float LeaveHeight = 76f;

            float goldY = ContentTop - PlateHeight * 0.5f;
            float buyY = goldY - PlateHeight - ActionGap;
            float packY = buyY - PlateHeight - ActionGap;
            float leaveY = ContentBottom + LeaveHeight * 0.5f;

            var gold = Ui.Label("ShopGoldLabel", UiStrings.ShopGold, new UiVec(inner - 24f, 48f), 36,
                    GoldText, Place.At(0f, 0f))
                .Styled(TypographyRole.TacticalData)
                .AsDecor();
            GoldLabel = gold;

            parts.Add(Ui.OutlineBox("ShopGoldPlate", Place.At(0f, goldY),
                new UiVec(inner, PlateHeight), GoldPlateFill, PanelRim, new[] { gold }));

            // Themed(Gold)/Themed(Silver), owner's HQ-kit instruction
            // (2026-09-07). NOT A CLEAN FIT: inner is 640 here, so Buy/Pack
            // (640x62, aspect 10.3) and Leave (640x76, aspect 8.4) both sit
            // far past even the widest plate shape (Row6x1, aspect 6.0) --
            // unlike Ui.Container, ButtonPlateArt.ShapeFor has no aspect
            // tolerance that refuses a bad fit, so these three will render
            // with the plate texture visibly stretched horizontally /
            // squashed vertically. Converted at the owner's specified theme
            // and the panel's existing box size regardless, since resizing
            // the actions column was not part of this instruction -- flagged
            // in the conversion report rather than resolved here.
            var buy = Ui.Button("ShopBuyButton", UiStrings.ShopBuy, new UiVec(inner, PlateHeight), 28,
                    Place.At(0f, buyY))
                .Themed(ButtonTheme.Gold);
            BuyButton = buy;
            parts.Add(buy);

            var pack = Ui.Button("ShopPackButton", UiStrings.ShopPack, new UiVec(inner, PlateHeight), 28,
                    Place.At(0f, packY))
                .Themed(ButtonTheme.Silver);
            PackButton = pack;
            parts.Add(pack);

            var leave = Ui.Button("ShopLeaveButton", UiStrings.ShopLeave, new UiVec(inner, LeaveHeight), 32,
                    Place.At(0f, leaveY))
                .Themed(ButtonTheme.Silver);
            LeaveButton = leave;
            LeaveButtonLabel = Ui.CaptionOf(leave);
            parts.Add(leave);

            return Ui.Panel("ShopActionsPanel", Place.At(Col3CentreX, Row2CentreY),
                UiSize.Fixed(WideColWidth, RowHeight), parts);
        }

        // ---- one offer card ---------------------------------------------------

        // `x`/`y` are the CARD's own PANEL-local position; everything built
        // below is a child of the card, so its coordinates are card-local
        // (0-centred), never x/y again.
        //
        // The icon slot is drawn whether or not art resolves for the offer:
        // ItemIcons.Apply disables the Image when a content id has no art, and
        // an empty rim reads as a slot where a bare gap reads as a mistake.
        // The price CHIP is the design's own signature -- the number in its own
        // hairline box at the card's bottom right, which is also where SOLD,
        // NO OFFER, NEED n and CONFIRM go.
        private OfferCard BuildOfferCard(string name, float x, float y, CardMetrics m)
        {
            var size = new UiVec(m.Width, m.Height);

            var children = new List<UiNode>
            {
                Ui.Solid($"{name}Fill", CardFill, size, Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim(name, size, CardRim));

            var icon = Ui.Sprite($"{name}Icon", null,
                    Place.At(m.IconCentreX, m.TopRowY), UiSize.Fixed(m.IconSize - 8f, m.IconSize - 8f))
                .AsDecor();

            children.Add(Ui.OutlineBox($"{name}IconSlot", Place.At(m.IconCentreX, m.TopRowY),
                new UiVec(m.IconSize, m.IconSize), null, PanelRim));
            children.Add(icon);

            var nameLabel = Ui.Label($"{name}Name", UiString.Runtime,
                    new UiVec(m.NameWidth, m.IconSize), m.NameSize, NameText,
                    Place.At(m.NameCentreX, m.TopRowY))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(nameLabel);

            var metaLabel = Ui.Label($"{name}Meta", UiString.Runtime,
                    new UiVec(m.MetaWidth, m.ChipHeight), m.MetaSize, MetaText,
                    Place.At(m.MetaCentreX, m.BottomRowY))
                .Styled(TypographyRole.TacticalData)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(metaLabel);

            // Sized for CONFIRM, not for the price. The chip's authored string
            // is the plain "{0} G", but its widest RUNTIME state is
            // "CONFIRM - 9999 G" (ShopCardConfirm), and a chip that fits only
            // the authored sample would clip the moment a card is selected --
            // which is the one state UiTextFitAudit cannot see, because the
            // audit measures what the tree declares.
            var priceLabel = Ui.Label($"{name}Price", UiStrings.ShopCardPrice,
                    new UiVec(m.ChipWidth - 16f, m.ChipHeight - 8f), m.MetaSize, PriceText,
                    Place.At(0f, 0f))
                .Styled(TypographyRole.TacticalData)
                .AsDecor();

            children.Add(Ui.OutlineBox($"{name}PriceChip", Place.At(m.ChipCentreX, m.BottomRowY),
                new UiVec(m.ChipWidth, m.ChipHeight), null, ChipRim, new[] { priceLabel }));

            var button = Ui.Button(name, UiString.Runtime, size, 12, Place.At(x, y)).NoChrome();
            foreach (var child in children) button.Children.Add(child);

            return new OfferCard
            {
                Button = button,
                Icon = icon,
                Name = nameLabel,
                Meta = metaLabel,
                Price = priceLabel,
            };
        }

        // ---- the PACK modal ---------------------------------------------------
        //
        // Matches docs/handoffs/shop_v2/screenshots/pack.png: a centred dialog
        // with a title, a close control, and one hairline-outlined row per bag
        // entry carrying name, metadata and sell price. Unchanged in shape by
        // this pass except for the frame -- the modal used to be a bare solid
        // with no rim at all, which was the one place it did not look like the
        // reference.
        private UiNode BuildPackModal()
        {
            const float ModalWidth = 1100f;
            const float ModalHeight = 860f;
            var modalSize = new UiVec(ModalWidth, ModalHeight);

            var content = new List<UiNode>
            {
                Ui.Solid("ShopPackBackdrop", FightHudPalette.PanelVioletDeep, Place.At(0f, 0f),
                        UiSize.Fixed(modalSize))
                    .AsDecor(),
            };
            content.AddRange(Ui.Rim("ShopPack", modalSize, PanelRim));

            content.Add(Ui.Label("ShopPackTitle", UiStrings.ShopPackTitle, new UiVec(500f, 56f), 40,
                    TitleText, Place.At(-290f, 380f))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left));

            // The rule under the title, exactly as the reference draws it.
            content.Add(Ui.Solid("ShopPackTitleRule", FightHudPalette.Hairline,
                    new UiVec(ModalWidth, 1f), Place.At(0f, 344f))
                .AsDecor());

            // Themed(Silver): 160x48 (3.33) lands almost exactly on the
            // Legacy/ThreeByOne plate (3.0).
            var close = Ui.Button("ShopPackClose", UiStrings.Close, new UiVec(160f, 48f), 26,
                    Place.At(460f, 380f))
                .Themed(ButtonTheme.Silver);
            PackCloseButton = close;
            content.Add(close);

            var empty = Ui.Label("ShopPackEmpty", UiStrings.ShopPackEmpty, new UiVec(700f, 40f), 24,
                    QuietText, Place.At(0f, 0f))
                .Styled(TypographyRole.Body)
                .Inactive();
            PackEmptyHint = empty;
            content.Add(empty);

            const float RowHeightPack = 68f;
            const float RowGapPack = 8f;
            const float FirstRowY = 280f;

            for (int i = 0; i < PackRowCount; i++)
            {
                var row = BuildPackRow(i, FirstRowY - i * (RowHeightPack + RowGapPack), RowHeightPack);
                PackRows.Add(row);
                content.Add(row.Row.Node);
            }

            var pager = Ui.Pager("ShopPackPrev", Place.At(-460f, -380f),
                "ShopPackNext", Place.At(460f, -380f), new UiVec(56f, 56f), 20,
                "ShopPackPage", UiStrings.ShopPackPage, Place.At(0f, -380f),
                new UiVec(200f, 28f), 20, MetaText);
            pager.Prev.Inactive();
            pager.Next.Inactive();
            pager.Label.Styled(TypographyRole.TacticalData).Inactive();
            PackPrevPage = pager.Prev;
            PackNextPage = pager.Next;
            PackPageLabel = pager.Label;

            content.Add(pager.Prev);
            content.Add(pager.Next);
            content.Add(pager.Label);

            var panel = Ui.Panel("ShopPackContent", Place.At(0f, 0f), UiSize.Fixed(modalSize), content);
            var modal = Ui.Modal("ShopPackModal", "#0A0614E0", panel).Inactive();
            PackRoot = modal;
            return modal;
        }

        // `y` is the ROW's own position, relative to the modal content (which
        // itself sits at the screen's centre). Every element inside the row is
        // a CHILD of the row's wrapper panel, so theirs are row-local: an x
        // offset only, y = 0, never `y` again.
        private PackRow BuildPackRow(int index, float y, float height)
        {
            const float RowWidth = 980f;
            var size = new UiVec(RowWidth, height);

            var children = new List<UiNode>
            {
                Ui.Solid($"ShopPackRow{index}Fill", FightHudPalette.SubmenuRowFill, size,
                        Place.At(0f, 0f))
                    .AsDecor(),
            };
            children.AddRange(Ui.Rim($"ShopPackRow{index}", size, PanelRim));

            var name = Ui.Label($"ShopPackRow{index}Name", UiString.Runtime, new UiVec(320f, 30f), 24,
                    DetailText, Place.At(-310f, 0f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(name);

            var meta = Ui.Label($"ShopPackRow{index}Meta", UiString.Runtime, new UiVec(170f, 26f), 20,
                    MetaText, Place.At(-55f, 0f))
                .Styled(TypographyRole.TacticalData)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(meta);

            var price = Ui.Label($"ShopPackRow{index}Price", UiStrings.ShopSellPriceLabel,
                    new UiVec(150f, 28f), 18, PriceText, Place.At(115f, 0f))
                .Styled(TypographyRole.TacticalData)
                .AsDecor();
            children.Add(price);

            // ThemedPlate(Silver) small plates, owner's HQ-kit instruction
            // (2026-09-07). ThemedPlate rather than Themed(): each needs its
            // own declared `<name>Caption` child the way OutlineButton always
            // built one (Runtime button text + a separate caption node), so
            // swapping factories keeps that shape and only trades the
            // hairline rim for the kit's plate art.
            var sellOneSize = new UiVec(120f, 40f);
            var sellOne = Ui.Button($"ShopPackRow{index}SellOne", UiString.Runtime, sellOneSize, 20,
                    Place.At(260f, 0f))
                .ThemedPlate(ButtonTheme.Silver);
            var sellOneCaption = Ui.Label($"ShopPackRow{index}SellOneCaption", UiStrings.ShopSellOneButton,
                    sellOneSize, 20, "#FFFFFFFF", Place.Stretch())
                .AsDecor()
                .Styled(TypographyRole.ButtonLabel);
            sellOne.Children.Add(sellOneCaption);
            sellOne.LayerCaptionWithVisuals(sellOneCaption);
            children.Add(sellOne);

            // 150 wide, not 120: UiTextFitAudit measures "SELL ALL 99" (the
            // string's AuditSample) against the button's own box, and this is
            // the wider of the two captions. Sits flush to the row's right edge
            // (405 + 75 = 480, 10px in from RowWidth / 2); SellOne sits to its
            // left with a 10px gap. 150x40 (3.75) still resolves to the
            // Legacy/ThreeByOne plate (3.0), same as SellOne's 120x40 (3.0).
            var sellAllSize = new UiVec(150f, 40f);
            var sellAll = Ui.Button($"ShopPackRow{index}SellAll", UiString.Runtime, sellAllSize, 20,
                    Place.At(405f, 0f))
                .ThemedPlate(ButtonTheme.Silver)
                .Inactive();
            var sellAllCaption = Ui.Label($"ShopPackRow{index}SellAllCaption", UiStrings.ShopSellAllButton,
                    sellAllSize, 20, "#FFFFFFFF", Place.Stretch())
                .AsDecor()
                .Styled(TypographyRole.ButtonLabel);
            sellAll.Children.Add(sellAllCaption);
            sellAll.LayerCaptionWithVisuals(sellAllCaption);
            children.Add(sellAll);

            var row = Ui.Panel($"ShopPackRow{index}", Place.At(0f, y), UiSize.Fixed(size), children)
                .Inactive();

            return new PackRow
            {
                Row = row,
                Name = name,
                Meta = meta,
                Price = price,
                SellOne = sellOne,
                SellAll = sellAll,
            };
        }
    }
}
