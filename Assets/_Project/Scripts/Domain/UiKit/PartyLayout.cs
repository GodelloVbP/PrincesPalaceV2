namespace PrincesPalace.Domain.UiKit
{
    // The Party pane's geometry, as pure arithmetic -- mirrors RunStatsLayout's
    // shape (a hosted pane, top-down stacked sections, a build-time guard on
    // the one dimension that grows with content).
    //
    // FOUR SECTIONS TOP TO BOTTOM: a header row (banner + status), Formation
    // (the hero -- 3 FIXED seats, never more), a hairline, and Roster (N cards,
    // N being the only thing here that can outgrow its row). The toast is not
    // a fifth section -- it is pinned into the ROSTER heading row, right-
    // aligned against the empty space beside "ROSTER" itself, rather than
    // floating over the card row it used to hide. See the toast section below
    // and PartyScreen's own BuildToast comment.
    public static class PartyLayout
    {
        // Still the FRAME's declared size (SystemMenuPaneTests.EveryHostedPane
        // IsTheSizeOfTheContentArea pins it against the panel), same as every
        // other hosted pane.
        public const float PaneWidth = SystemMenuLayout.PanelWidth;
        public static float PaneHeight => SystemMenuLayout.ContentHeight;

        // THE PANE'S OWN DECLARED CONTENT HALF-EXTENTS, not PaneWidth/
        // PaneHeight * 0.5f -- see SystemMenuLayout.PaneContentHalfWidth/
        // HalfHeight's own comment. Every hosted pane reads this rather than
        // restating the number.
        public static float HalfWidth => SystemMenuLayout.PaneContentHalfWidth;
        public static float HalfHeight => SystemMenuLayout.PaneContentHalfHeight;

        // A small slack margin over the pane's own content inset, not a
        // second authored pad -- same pattern as RunStatsLayout.PadX/
        // PadTop/PadBottom.
        public const float PadX = 4f;
        public const float PadTop = 4f;
        public const float PadBottom = 4f;

        public static float ContentTop => HalfHeight - PadTop;
        public static float ContentBottom => -HalfHeight + PadBottom;
        public static float ContentLeft => -HalfWidth + PadX;
        public static float ContentRight => HalfWidth - PadX;

        public static float UsableWidth => ContentRight - ContentLeft;
        public static float UsableHeight => ContentTop - ContentBottom;

        // ---- header row: banner + links + status pill ---------------------------

        public const float HeaderHeight = 56f;
        public static float HeaderCentreY => ContentTop - HeaderHeight * 0.5f;

        // DECLARED ONCE rather than derived from the links either side of it:
        // the banner's real content is one sentence, so its box only has to be
        // wide enough for the longest authored copy, not for whatever is left
        // over once the status pill claims its own space. The gap this leaves
        // before the status pill is deliberate slack, same call RunStats makes
        // about the space under its shortest card.
        public const float BannerWidth = 640f;
        public static float BannerCentreX => ContentLeft + BannerWidth * 0.5f;

        public const float LinkGap = 20f;
        public const float CancelLinkWidth = 90f;
        public const float BenchLinkWidth = 170f;
        public const float LinkHeight = 24f;

        public static float CancelLinkCentreX =>
            BannerCentreX + BannerWidth * 0.5f + LinkGap + CancelLinkWidth * 0.5f;

        public static float BenchLinkCentreX =>
            CancelLinkCentreX + CancelLinkWidth * 0.5f + LinkGap + BenchLinkWidth * 0.5f;

        public const float StatusPillWidth = 300f;
        public const float StatusPillHeight = 34f;
        public static float StatusPillCentreX => ContentRight - StatusPillWidth * 0.5f;

        // ---- Formation heading row ------------------------------------------------

        public const float HeaderToFormationGap = 14f;
        public const float FormationHeadingHeight = 26f;
        public const float FormationHeadingWidth = 400f;
        public const float FormationHeadingToSubtitleGap = 2f;
        public const float FormationSubtitleHeight = 18f;
        public const float FormationHeadingToPanelGap = 10f;

        public static float FormationHeadingCentreY =>
            ContentTop - HeaderHeight - HeaderToFormationGap - FormationHeadingHeight * 0.5f;

        public static float FormationSubtitleCentreY =>
            FormationHeadingCentreY - FormationHeadingHeight * 0.5f
            - FormationHeadingToSubtitleGap - FormationSubtitleHeight * 0.5f;

        // ---- the Formation panel: 3 fixed seats, never more ------------------------
        //
        // SEAT COUNT IS FIXED at 3 -- the fight stage has exactly 3 party
        // slots (FightHudSpec.StageSlotsPerSide's own party half) -- so unlike
        // Roster's card row this has no build-time guard: there is nothing for
        // a designer to outgrow.
        public const int SeatCount = 3;

        public const float RibbonHeight = 26f;
        public const float RibbonToColumnsGap = 10f;

        public const float ColumnTopPad = 14f;
        public const float SeatLabelHeight = 22f;
        public const float SeatLabelToBadgeGap = 8f;
        public const float BadgeHeight = 26f;
        public const float BadgeToArtGap = 10f;

        // THE DESIGN'S OWN FIGURE. Every seat's art slot is this tall
        // regardless of what fills it -- a real stance, a monogram standee, or
        // nothing loaded yet -- which is what makes FeetLine below a real
        // shared line rather than an average.
        public const float ArtHeight = 150f;

        public const float ArtToGlowGap = 2f;
        public const float GlowHeight = 14f;
        public const float GlowToNameGap = 10f;
        public const float NameHeight = 24f;
        public const float NameToRoleGap = 2f;
        public const float RoleHeight = 20f;
        public const float ColumnBottomPad = 14f;

        // DERIVED, not authored -- the panel's height is a consequence of what
        // a column stacks, the same relationship RunStatsLayout.CardHeight
        // states for its own rows.
        public static float ColumnStackHeight =>
            ColumnTopPad + SeatLabelHeight + SeatLabelToBadgeGap + BadgeHeight + BadgeToArtGap
            + ArtHeight + ArtToGlowGap + GlowHeight + GlowToNameGap + NameHeight + NameToRoleGap
            + RoleHeight + ColumnBottomPad;

        public static float FormationPanelWidth => UsableWidth;
        public static float FormationPanelHeight => RibbonHeight + RibbonToColumnsGap + ColumnStackHeight;

        public static float FormationPanelCentreY =>
            FormationSubtitleCentreY - FormationSubtitleHeight * 0.5f
            - FormationHeadingToPanelGap - FormationPanelHeight * 0.5f;

        // ---- the 3 columns, in PANEL-LOCAL space (panel centre = 0,0) ------------

        public const float ColumnDividerWidth = 2f;

        public static float ColumnWidth =>
            (FormationPanelWidth - ColumnDividerWidth * (SeatCount - 1)) / SeatCount;

        public static float ColumnLeft(int visualIndex) =>
            -FormationPanelWidth * 0.5f + visualIndex * (ColumnWidth + ColumnDividerWidth);

        public static float ColumnCentreX(int visualIndex) =>
            ColumnLeft(visualIndex) + ColumnWidth * 0.5f;

        public static float DividerCentreX(int gapIndex) =>
            -FormationPanelWidth * 0.5f + ColumnWidth * (gapIndex + 1)
            + ColumnDividerWidth * gapIndex + ColumnDividerWidth * 0.5f;

        // SEAT INDEX 0 IS FRONT, matching the front-rank rule (squad index 0 =
        // front) -- but FRONT is drawn on the RIGHT, nearest the "facing the
        // enemy" ribbon, matching the fight stage's own party-left/enemy-right
        // orientation. So the seat that occupies data index i sits at the
        // MIRRORED visual column, and this is the one place that mapping is
        // allowed to live -- everything below reads visual columns through it
        // rather than restating the reversal.
        public static int VisualColumnForSeat(int seatIndex) => SeatCount - 1 - seatIndex;

        public static float SeatCentreX(int seatIndex) => ColumnCentreX(VisualColumnForSeat(seatIndex));

        public static float RibbonCentreY => FormationPanelHeight * 0.5f - RibbonHeight * 0.5f;
        public static float RibbonRuleY => FormationPanelHeight * 0.5f - RibbonHeight - RibbonToColumnsGap * 0.5f;

        public static float ColumnTop => FormationPanelHeight * 0.5f - RibbonHeight - RibbonToColumnsGap;

        public static float SeatLabelCentreY => ColumnTop - ColumnTopPad - SeatLabelHeight * 0.5f;

        public static float BadgeCentreY =>
            SeatLabelCentreY - SeatLabelHeight * 0.5f - SeatLabelToBadgeGap - BadgeHeight * 0.5f;

        public static float ArtTop => BadgeCentreY - BadgeHeight * 0.5f - BadgeToArtGap;
        public static float ArtCentreY => ArtTop - ArtHeight * 0.5f;

        // THE SHARED LINE. Every seat's art slot is the same fixed height, so
        // this is one number for all three columns -- the whole point of a
        // fixed-height, bottom-aligned slot.
        public static float FeetLine => ArtTop - ArtHeight;

        public static float GlowCentreY => FeetLine - ArtToGlowGap - GlowHeight * 0.5f;
        public static float NameCentreY => GlowCentreY - GlowHeight * 0.5f - GlowToNameGap - NameHeight * 0.5f;
        public static float RoleCentreY => NameCentreY - NameHeight * 0.5f - NameToRoleGap - RoleHeight * 0.5f;

        // The seat's own click target and its ring/scrim overlay all share this
        // box -- ColumnTop down to the panel's own floor.
        public static float ColumnButtonHeight => ColumnStackHeight;
        public static float ColumnButtonCentreY => ColumnTop - ColumnStackHeight * 0.5f;

        // ---- the hairline between Formation and Roster -----------------------------

        public const float FormationToHairlineGap = 14f;
        public const float HairlineHeight = 1f;
        public const float HairlineToRosterGap = 14f;

        public static float HairlineCentreY =>
            FormationPanelCentreY - FormationPanelHeight * 0.5f - FormationToHairlineGap;

        // ---- Roster: N cards, the one dimension that can outgrow its row -----------

        public const float RosterHeadingHeight = 24f;
        // Sized to "ROSTER" itself, left-aligned at ContentLeft -- the rest of
        // this row, out to ContentRight, is empty in every state. The toast
        // (below) claims that space right-aligned rather than adding a fifth
        // section to the pane.
        public const float RosterHeadingWidth = 300f;
        public const float RosterHeadingToRowGap = 12f;

        public static float RosterHeadingCentreY =>
            HairlineCentreY - HairlineToRosterGap - RosterHeadingHeight * 0.5f;

        public const float CardWidth = 150f;
        // Proportional to the handoff's 10px gap on a 112px card -- scaled up
        // with the wider card this kit's own button/plate widths call for.
        public const float CardGap = 14f;

        public const float CardTopPad = 10f;
        public const float CardArtHeight = 76f;
        public const float CardArtToNameGap = 6f;
        public const float CardNameHeight = 20f;
        public const float CardNameToRoleGap = 2f;
        public const float CardRoleHeight = 16f;
        public const float CardRoleToTagGap = 4f;
        public const float CardTagHeight = 16f;
        public const float CardBottomPad = 10f;

        public static float CardHeight =>
            CardTopPad + CardArtHeight + CardArtToNameGap + CardNameHeight + CardNameToRoleGap
            + CardRoleHeight + CardRoleToTagGap + CardTagHeight + CardBottomPad;

        public static float RosterRowCentreY =>
            RosterHeadingCentreY - RosterHeadingHeight * 0.5f - RosterHeadingToRowGap - CardHeight * 0.5f;

        public static float RosterRowWidth(int count) => CardWidth * count + CardGap * (count - 1);

        public static float CardCentreX(int index, int count)
        {
            float left = -RosterRowWidth(count) * 0.5f;
            return left + CardWidth * (index + 0.5f) + CardGap * index;
        }

        // ---- a card's own insides, in CARD-LOCAL space (card centre = 0,0) --------

        public static float CardTop => CardHeight * 0.5f;
        public static float CardArtCentreY => CardTop - CardTopPad - CardArtHeight * 0.5f;

        // THE SHARED LINE for every card, same reasoning as FeetLine above.
        public static float CardArtBottom => CardArtCentreY - CardArtHeight * 0.5f;

        public static float CardNameCentreY => CardArtBottom - CardArtToNameGap - CardNameHeight * 0.5f;

        public static float CardRoleCentreY =>
            CardNameCentreY - CardNameHeight * 0.5f - CardNameToRoleGap - CardRoleHeight * 0.5f;

        public static float CardTagCentreY =>
            CardRoleCentreY - CardRoleHeight * 0.5f - CardRoleToTagGap - CardTagHeight * 0.5f;

        // ---- the roster's build-time guard ------------------------------------------
        //
        // ARITHMETIC, not a fixed count -- same reasoning as the system menu's
        // tab-bar capacity guard: a row that already fills the pane must refuse
        // a card added to it, not silently run one off the right edge where
        // nothing tells the player there was ever a fourth.
        public static bool RosterFits(int count) => count > 0 && RosterRowWidth(count) <= UsableWidth;

        // The largest count RosterFits still accepts, for a guard message that
        // can name the ceiling rather than just refuse.
        public static int MaxRosterCards()
        {
            int n = 1;
            while (RosterFits(n + 1)) n++;
            return n;
        }

        // ---- the toast: pinned into the ROSTER heading row, not a floating overlay --
        //
        // It used to float over the bottom of the card row (anchored to the
        // pane's own floor), and that hid exactly the two lines a swap just
        // changed -- a card's role and its "In party (dot) ..."/"Benched" tag.
        // "ROSTER" is left-aligned and short; the rest of that row is empty in
        // every state (RosterHeadingWidth's own comment), so the toast claims
        // it right-aligned instead of adding a fifth section or covering the
        // cards.
        //
        // SAME HEIGHT as the heading row it shares -- one line only, so the
        // width has to be wide enough to hold the longest authored toast
        // string (UiStrings' own PartyToast* entries) without wrapping;
        // UiTextFitAudit is what actually proves that at build time, this is
        // just generous enough to clear it with real margin to spare.
        public const float ToastHeight = RosterHeadingHeight;
        public const float ToastWidth = 640f;

        public static float ToastCentreX => ContentRight - ToastWidth * 0.5f;
        public static float ToastCentreY => RosterHeadingCentreY;

        // ---- the drag ghost (P4): one reusable floating preview, not a fifth section

        // Sized off the SEAT column's own art proportions (the taller of the
        // two art slots) rather than the card's smaller one -- a preview that
        // shrinks when picked up off a card and grows when picked up off a
        // seat would read as two different ghosts rather than one reused node
        // following the pointer.
        public static float GhostArtWidth => ColumnWidth * 0.7f;
        public const float GhostArtHeight = ArtHeight;

        // ---- the roster drop zone (P4): a raycastable hit-region behind every card --

        public static float RosterDropZoneHeight => CardHeight;
    }
}
