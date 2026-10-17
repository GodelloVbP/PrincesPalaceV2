namespace PrincesPalace.Domain.UiKit
{
    // The Party pane's geometry, as pure arithmetic -- mirrors OptionsLayout's
    // shape (a hosted pane, top-down stacked sections, a build-time guard on
    // the one dimension that grows with content).
    //
    // TOP TO BOTTOM: a header row (heading, the hint, the links, the count), a
    // subtitle row (the rank rule and the ribbon), Formation (the hero -- 3
    // FIXED seats, never more), and, only while someone is unseated or a seat
    // is closed, a hairline and Roster (N cards, N being the only thing here
    // that can outgrow its row). Messages are the menu's footer notice, and the
    // mode is the lintel's pill; neither is a part of this pane.
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
        // second authored pad -- same pattern as OptionsLayout.PadX/
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

        // ---- header row: heading, the hint, the links, the count -----------------------
        //
        // ONE ROW, and a subtitle row under it that also carries the ribbon: the
        // type floor (plan C14) makes every line taller, and Formation, its
        // seats and the roster all have to fit the pane at once.
        //
        // A label's box is at least 1.33 x its font size or TMP draws nothing
        // (LabelLineHeightLintTests, LineRatio 1.33), so 24 needs 32 and 20
        // needs 27.
        public const float HeaderHeight = 40f;
        public static float HeaderCentreY => ContentTop - HeaderHeight * 0.5f;

        public const float HeaderGap = 16f;
        public const float FormationHeadingWidth = 200f;
        public const float FormationHeadingHeight = 32f;

        public static float FormationHeadingCentreX => ContentLeft + FormationHeadingWidth * 0.5f;

        // Sized for the longer of the two authored hints.
        public const float BannerWidth = 560f;
        public const float BannerHeight = 32f;
        public static float BannerCentreX =>
            ContentLeft + FormationHeadingWidth + HeaderGap + BannerWidth * 0.5f;

        public const float CancelLinkWidth = 110f;
        public const float BenchLinkWidth = 190f;
        public const float LinkHeight = 32f;

        public static float CancelLinkCentreX =>
            BannerCentreX + BannerWidth * 0.5f + HeaderGap + CancelLinkWidth * 0.5f;

        public static float BenchLinkCentreX =>
            CancelLinkCentreX + CancelLinkWidth * 0.5f + HeaderGap + BenchLinkWidth * 0.5f;

        public const float FilledCountWidth = 260f;
        public static float FilledCountCentreX => ContentRight - FilledCountWidth * 0.5f;

        // ---- the subtitle row: the rank rule, and the ribbon at its right ---------------

        public const float SubtitleHeight = 32f;
        public const float RibbonWidth = 320f;
        public const float RibbonHeight = 32f;

        public static float SubtitleWidth => UsableWidth - RibbonWidth - HeaderGap;
        public static float SubtitleCentreX => ContentLeft + SubtitleWidth * 0.5f;
        public static float RibbonCentreX => ContentRight - RibbonWidth * 0.5f;
        public static float SubtitleCentreY => ContentTop - HeaderHeight - SubtitleHeight * 0.5f;

        // ---- the Formation panel: 3 fixed seats, never more ------------------------------
        //
        // SEAT COUNT IS FIXED at 3 -- the fight stage has exactly 3 party
        // slots (FightHudSpec.StageSlotsPerSide's own party half) -- so unlike
        // Roster's card row this has no build-time guard: there is nothing for
        // a designer to outgrow.
        public const int SeatCount = 3;

        public const float HeaderToPanelGap = 6f;

        // One seat, top down. Every height is a line of the type on it.
        public const float ColumnTopPad = 6f;
        public const float SeatLabelHeight = 32f;
        public const float SeatLabelToArtGap = 4f;

        // Every seat's art slot is this tall regardless of what fills it -- a
        // real stance, a monogram standee, or nothing loaded yet -- which is
        // what makes FeetLine below a real shared line rather than an average.
        public const float ArtHeight = 160f;

        public const float ArtToGlowGap = 2f;
        public const float GlowHeight = 12f;
        public const float GlowToNameGap = 4f;
        public const float NameHeight = 30f;
        public const float NameToRoleGap = 0f;
        public const float RoleHeight = 28f;
        public const float RoleToBarGap = 8f;
        public const float BarHeight = 12f;
        public const float BarToHpGap = 4f;
        // "HP 180 / 240" is capitals and digits with no descender, so it is held
        // to the all-caps floor of 24 (MenuTypeFloor), and a 24 line needs 32.
        public const float HpHeight = 32f;
        public const float DefHeight = 28f;
        public const float ColumnBottomPad = 8f;

        // DERIVED, not authored -- the panel's height is a consequence of what
        // a column stacks, the same relationship a card's height has to
        // its rows.
        public static float ColumnStackHeight =>
            ColumnTopPad + SeatLabelHeight + SeatLabelToArtGap + ArtHeight + ArtToGlowGap + GlowHeight
            + GlowToNameGap + NameHeight + NameToRoleGap + RoleHeight + RoleToBarGap + BarHeight
            + BarToHpGap + HpHeight + DefHeight + ColumnBottomPad;

        public static float FormationPanelWidth => UsableWidth;
        public static float FormationPanelHeight => ColumnStackHeight;

        public static float FormationPanelCentreY =>
            ContentTop - HeaderHeight - SubtitleHeight - HeaderToPanelGap - FormationPanelHeight * 0.5f;

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

        public static float ColumnTop => FormationPanelHeight * 0.5f;

        public static float SeatLabelCentreY => ColumnTop - ColumnTopPad - SeatLabelHeight * 0.5f;

        public static float ArtTop => ColumnTop - ColumnTopPad - SeatLabelHeight - SeatLabelToArtGap;
        public static float ArtCentreY => ArtTop - ArtHeight * 0.5f;

        // THE SHARED LINE. Every seat's art slot is the same fixed height, so
        // this is one number for all three columns -- the whole point of a
        // fixed-height, bottom-aligned slot.
        public static float FeetLine => ArtTop - ArtHeight;

        public static float GlowCentreY => FeetLine - ArtToGlowGap - GlowHeight * 0.5f;
        public static float NameCentreY => GlowCentreY - GlowHeight * 0.5f - GlowToNameGap - NameHeight * 0.5f;
        public static float RoleCentreY => NameCentreY - NameHeight * 0.5f - NameToRoleGap - RoleHeight * 0.5f;
        public static float BarCentreY => RoleCentreY - RoleHeight * 0.5f - RoleToBarGap - BarHeight * 0.5f;
        public static float HpCentreY => BarCentreY - BarHeight * 0.5f - BarToHpGap - HpHeight * 0.5f;
        public static float DefCentreY => HpCentreY - HpHeight * 0.5f - DefHeight * 0.5f;

        public static float BarWidth => ColumnWidth - 80f;

        // THE VITALS: bar, HP line and defence line as one node, so a seat
        // nobody sits in switches all three off together. Its children are
        // placed in its own frame (VitalsLocalY).
        public static float VitalsHeight => BarHeight + BarToHpGap + HpHeight + DefHeight;
        public static float VitalsCentreY => (BarCentreY + BarHeight * 0.5f + DefCentreY - DefHeight * 0.5f) * 0.5f;
        public static float VitalsLocalY(float panelY) => panelY - VitalsCentreY;

        // The seat's own click target and its ring/scrim overlay all share this
        // box -- ColumnTop down to the panel's own floor.
        public static float ColumnButtonHeight => ColumnStackHeight;
        public static float ColumnButtonCentreY => ColumnTop - ColumnStackHeight * 0.5f;

        // ---- the carry preview (plan C22), over the seat's art -----------------------
        //
        // A title ("Swap with Odette") and two columns, the carried character
        // beside the one in the seat, each a name, HP and both defences. It sits
        // over the art rather than below it because the stack under the art is
        // already full; the sprite is what the player already knows.
        public const float BadgeTitleHeight = 30f;
        public const float BadgeNameHeight = 28f;
        public const float BadgeHpHeight = 32f;
        public const float BadgeDefHeight = 28f;
        private const float BadgePad = 6f;

        public const float BadgeHeight =
            BadgePad + BadgeTitleHeight + BadgeNameHeight + BadgeHpHeight + BadgeDefHeight + BadgePad;
        public const float BadgeColumnHeight = BadgeNameHeight + BadgeHpHeight + BadgeDefHeight;
        public static float BadgeWidth => ColumnWidth - 24f;
        public static float BadgeCentreY => ArtTop - BadgeHeight * 0.5f;

        // In BADGE-local space (badge centre = 0,0).
        public static float BadgeTitleCentreY => BadgeHeight * 0.5f - BadgePad - BadgeTitleHeight * 0.5f;

        // The column as one node, centred under the title.
        public static float BadgeColumnCentreY => BadgeTitleCentreY - BadgeTitleHeight * 0.5f - BadgeColumnHeight * 0.5f;

        // Inside a column, measured from the column's own centre.
        public static float BadgeNameCentreY => BadgeColumnHeight * 0.5f - BadgeNameHeight * 0.5f;
        public static float BadgeHpCentreY => BadgeNameCentreY - BadgeNameHeight * 0.5f - BadgeHpHeight * 0.5f;
        public static float BadgeDefCentreY => BadgeHpCentreY - BadgeHpHeight * 0.5f - BadgeDefHeight * 0.5f;

        public static float BadgeColumnWidth => BadgeWidth * 0.5f - 12f;
        public static float BadgeColumnCentreX(int column) => (column == 0 ? -1f : 1f) * BadgeWidth * 0.25f;

        // ---- the hairline between Formation and Roster -----------------------------

        public const float FormationToHairlineGap = 10f;
        public const float HairlineHeight = 1f;
        public const float HairlineToRosterGap = 10f;

        public static float HairlineCentreY =>
            FormationPanelCentreY - FormationPanelHeight * 0.5f - FormationToHairlineGap;

        // ---- Roster: N cards, the one dimension that can outgrow its row -----------
        //
        // Shown only while someone is unseated or a seat is closed
        // (PartyFormation.RosterVisible); the section is one node so that is
        // one switch.

        public const float RosterHeadingHeight = 32f;
        // Sized to "ROSTER" itself, left-aligned at ContentLeft -- the rest of
        // this row, out to ContentRight, is empty in every state.
        public const float RosterHeadingWidth = 300f;
        public const float RosterHeadingToRowGap = 6f;

        public static float RosterHeadingCentreY =>
            HairlineCentreY - HairlineHeight * 0.5f - HairlineToRosterGap - RosterHeadingHeight * 0.5f;

        public const float CardWidth = 176f;
        public const float CardGap = 12f;

        // The card is a column of Body 20 lines under its art, and it is as tall
        // as the pane has left under Formation: the roster row must stay inside
        // ContentBottom (SystemMenuPaneTests reads RosterRowCentreY for how much
        // of the pane the roster uses).
        public const float CardTopPad = 6f;
        public const float CardArtHeight = 66f;
        public const float CardArtToNameGap = 4f;
        public const float CardNameHeight = 28f;
        public const float CardNameToTitleGap = 0f;
        public const float CardTitleHeight = 28f;
        public const float CardTitleToRoleGap = 0f;
        public const float CardRoleHeight = 28f;
        public const float CardRoleToTagGap = 0f;
        public const float CardTagHeight = 28f;
        public const float CardBottomPad = 6f;

        public static float CardHeight =>
            CardTopPad + CardArtHeight + CardArtToNameGap + CardNameHeight + CardNameToTitleGap
            + CardTitleHeight + CardTitleToRoleGap
            + CardRoleHeight + CardRoleToTagGap + CardTagHeight + CardBottomPad;

        public static float RosterRowCentreY =>
            RosterHeadingCentreY - RosterHeadingHeight * 0.5f - RosterHeadingToRowGap - CardHeight * 0.5f;

        public static float RosterRowWidth(int count) => CardWidth * count + CardGap * (count - 1);

        public static float CardCentreX(int index, int count)
        {
            float left = -RosterRowWidth(count) * 0.5f;
            return left + CardWidth * (index + 0.5f) + CardGap * index;
        }

        // THE ROSTER SECTION, one node from the hairline to the foot of the
        // cards, so hiding the roster is one switch. Its children are placed in
        // ITS frame: RosterLocalY turns a pane-frame Y into that.
        public static float RosterSectionTop => HairlineCentreY + HairlineHeight * 0.5f;
        public static float RosterSectionBottom => RosterRowCentreY - CardHeight * 0.5f;
        public static float RosterSectionHeight => RosterSectionTop - RosterSectionBottom;
        public static float RosterSectionCentreY => (RosterSectionTop + RosterSectionBottom) * 0.5f;

        public static float RosterLocalY(float paneY) => paneY - RosterSectionCentreY;

        // ---- a card's own insides, in CARD-LOCAL space (card centre = 0,0) --------

        public static float CardTop => CardHeight * 0.5f;
        public static float CardArtCentreY => CardTop - CardTopPad - CardArtHeight * 0.5f;

        // THE SHARED LINE for every card, same reasoning as FeetLine above.
        public static float CardArtBottom => CardArtCentreY - CardArtHeight * 0.5f;

        public static float CardNameCentreY => CardArtBottom - CardArtToNameGap - CardNameHeight * 0.5f;

        // THE TITLE, AND THE PICKER THAT CHOOSES IT, are the same rect: the
        // line the player reads is the thing they click to change it. A
        // separate chevron beside it would be a second target on a
        // 176px card, and a title the player cannot change is not worth a
        // control at all.
        public static float CardTitleCentreY =>
            CardNameCentreY - CardNameHeight * 0.5f - CardNameToTitleGap - CardTitleHeight * 0.5f;

        public static float CardRoleCentreY =>
            CardTitleCentreY - CardTitleHeight * 0.5f - CardTitleToRoleGap - CardRoleHeight * 0.5f;

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
