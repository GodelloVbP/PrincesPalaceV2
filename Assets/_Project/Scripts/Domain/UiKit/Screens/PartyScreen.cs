using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What the Party pane needs from outside Domain.
    //
    // Domain cannot see ContentDatabase, so the roster count arrives as an
    // input rather than being counted here -- same reasoning, same shape, as
    // MainMenuInputs.SlotCount reading off SaveSystem.SlotCount.
    // SystemMenuScreen.Build's own default keeps this buildable against the
    // three characters characters.json authors.
    public readonly struct PartyInputs
    {
        public readonly int RosterCardCount;

        public PartyInputs(int rosterCardCount)
        {
            RosterCardCount = rosterCardCount;
        }
    }

    // The Party pane: Formation (3 fixed seats) above Roster (N cards, shown
    // only while someone sits out or a seat is closed), hosted in the system
    // menu exactly as the dossier, Options and Run statistics are -- its own
    // screen class and its own controller (Core/PartyController.cs).
    //
    // Every state this pane can ever be in -- a carry preview, a selection
    // ring, a locked or closed scrim, a monogram standing in for missing art --
    // is declared here, inactive, because the scene is generated once and a
    // controller can only show or hide a node that already exists in it.
    //
    // SEATS ARE INDEXED FRONT-FIRST (0 = front, matching the front-rank rule
    // -- squad index 0 is what takes the enemies' blows) but
    // DRAWN REAR, MIDDLE, FRONT left to right, so the front seat sits nearest
    // the "facing the enemy" ribbon on the right -- matching the fight stage's
    // own party-left/enemy-right orientation. PartyLayout.VisualColumnForSeat
    // is the one place that reversal lives; every list on this screen stays in
    // seat-index (front-first) order regardless of where a seat draws.
    public sealed class PartyScreen
    {
        // ---- tokens ---------------------------------------------------------------
        //
        // Reused FightHudPalette tokens throughout -- PartyNameText/
        // PartyClassText already exist for exactly this pairing on the fight
        // HUD's own party plate, so a name/role pair here cannot read as a
        // different colour language from the one on the stage the player just
        // left.
        private const string CardFill = FightHudPalette.CardFill;
        private const string CardRim = FightHudPalette.Hairline;
        private const string Heading = FightHudPalette.GoldText;
        private const string Body = FightHudPalette.TextMuted;
        private const string TextPrimary = FightHudPalette.TextPrimary;
        private const string GoldLight = FightHudPalette.GoldLight;
        private const string NameText = FightHudPalette.PartyNameText;
        private const string RoleText = FightHudPalette.PartyClassText;
        private const string BadgeFill = FightHudPalette.PanelActive;
        private const string BadgeRim = FightHudPalette.BorderGold;
        private const string RingGold = FightHudPalette.BorderGold;

        // The scrim over a locked or closed seat. Not FightHudPalette.PanelRed
        // (that reads as danger, and neither state is a threat) -- a near-black
        // wash close to the system menu's own Scrim, opaque enough that the art
        // underneath cannot be read through it.
        private const string ScrimFill = "#0A0614D9";
        private const string ScrimCaption = FightHudPalette.TextSecondary;

        // The ground-glow's build-time default: the faint/neutral reading the
        // design's own token table names for an empty position.
        // PartyController overrides it per occupancy.
        private const string GlowNeutral = "#B4AA9629";    // rgba(180,170,150,.16)

        // The monogram standee's plate. A flat gold-rimmed card rather than a
        // themed kit plate -- it stands in for ANY character, so it cannot
        // borrow one character's own theme. CardFill (declared above) is the
        // same token every other pane's flat card ground already uses.
        private const string MonogramFill = CardFill;
        private const string MonogramRim = FightHudPalette.BorderGold;
        private const string MonogramLetter = FightHudPalette.GoldText;

        private const string DimWash = "#0A0614B0";

        // The drag ghost's art tint: ~0.8 alpha over whatever sprite loads
        // at runtime, following the pointer.
        private const string GhostArtTint = "#FFFFFFCC";

        public UiNode Root;

        // ---- singles ----------------------------------------------------------
        public NodeRef Banner;
        public NodeRef CancelLink;
        public NodeRef BenchLink;
        public NodeRef FilledCount;

        // The roster's hairline, heading, drop zone and cards as one node: the
        // controller shows it only while someone is unseated or a seat is
        // closed (PartyFormation.RosterVisible).
        public NodeRef RosterSection;

        // ---- drag-and-drop ----------------------------------------------------------

        // The reusable drag ghost -- ONE node, re-skinned per drag rather than
        // one per seat/card. See BuildDragGhost.
        public NodeRef DragGhost;
        public NodeRef DragGhostArt;
        public NodeRef DragGhostMonogramPlate;
        public NodeRef DragGhostMonogramLetter;

        // The roster's own raycastable hit-region, behind every card -- see
        // BuildRosterDropZone.
        public NodeRef RosterDropZone;

        // ---- per seat, in FRONT-FIRST (seat-index) order, length SeatCount ------
        public List<NodeRef> SeatButtons = new List<NodeRef>();
        public List<NodeRef> SeatBadges = new List<NodeRef>();

        // The carry preview's words (plan C22): a title, and two columns -- the
        // character being carried and the one in the seat -- each a name, HP and
        // both defences. A seat with nobody in it previews only the carried
        // column.
        public List<NodeRef> SeatBadgeTexts = new List<NodeRef>();
        public List<NodeRef> SeatBadgeCarriedColumns = new List<NodeRef>();
        public List<NodeRef> SeatBadgeCarriedNames = new List<NodeRef>();
        public List<NodeRef> SeatBadgeCarriedHps = new List<NodeRef>();
        public List<NodeRef> SeatBadgeCarriedDefs = new List<NodeRef>();
        public List<NodeRef> SeatBadgeSeatedColumns = new List<NodeRef>();
        public List<NodeRef> SeatBadgeSeatedNames = new List<NodeRef>();
        public List<NodeRef> SeatBadgeSeatedHps = new List<NodeRef>();
        public List<NodeRef> SeatBadgeSeatedDefs = new List<NodeRef>();

        // The facts that decide placement (plan C22), on the seat itself.
        public List<NodeRef> SeatVitals = new List<NodeRef>();
        public List<NodeRef> SeatHpFills = new List<NodeRef>();
        public List<NodeRef> SeatHpTexts = new List<NodeRef>();
        public List<NodeRef> SeatDefTexts = new List<NodeRef>();
        public List<NodeRef> SeatArts = new List<NodeRef>();
        public List<NodeRef> SeatMonogramPlates = new List<NodeRef>();
        public List<NodeRef> SeatMonogramLetters = new List<NodeRef>();
        public List<NodeRef> SeatGlows = new List<NodeRef>();

        public List<NodeRef> SeatNames = new List<NodeRef>();
        public List<NodeRef> SeatRoles = new List<NodeRef>();
        public List<NodeRef> SeatRings = new List<NodeRef>();
        public List<NodeRef> SeatScrims = new List<NodeRef>();
        public List<NodeRef> SeatScrimCaptions = new List<NodeRef>();

        // ---- per roster card, in row order, length RosterCardCount ---------------
        public List<NodeRef> CardButtons = new List<NodeRef>();
        public List<NodeRef> CardArts = new List<NodeRef>();

        // THE PORTRAIT FRAME, level 34's identity node: an outline round the
        // card's own art slot, hidden until it is collected. The kit's
        // OutlineBox at a gold hairline -- the same recipe the selection ring
        // and the monogram plate already wear, round a different rect.
        public List<NodeRef> CardPortraitFrames = new List<NodeRef>();
        public List<NodeRef> CardMonogramPlates = new List<NodeRef>();
        public List<NodeRef> CardMonogramLetters = new List<NodeRef>();
        public List<NodeRef> CardNames = new List<NodeRef>();

        // THE TITLE ROW, which is also the picker -- one Button per card, its
        // own baked label carrying whichever collected Title is selected. See
        // PartyLayout.CardTitleCentreY for why the line and the control are one
        // rect, and PartyController.CycleTitle for what a click does.
        public List<NodeRef> CardTitles = new List<NodeRef>();
        public List<NodeRef> CardRoles = new List<NodeRef>();
        public List<NodeRef> CardTags = new List<NodeRef>();
        public List<NodeRef> CardRings = new List<NodeRef>();
        public List<NodeRef> CardSelectedTags = new List<NodeRef>();
        public List<NodeRef> CardWashes = new List<NodeRef>();

        public static PartyScreen Build(PartyInputs inputs)
        {
            var screen = new PartyScreen();
            int rosterCount = inputs.RosterCardCount;

            // THE GUARD. Roster is the one dimension here that can outgrow its
            // row -- Formation is always exactly 3 seats -- so it gets the same
            // build-time refusal RunStats and Options give their own growable
            // dimension, naming the count rather than letting a card run off
            // the pane's right edge where nothing tells anybody it happened.
            if (!PartyLayout.RosterFits(rosterCount))
            {
                throw new System.InvalidOperationException(
                    $"The Party roster has {rosterCount} characters but the roster row only holds " +
                    $"{PartyLayout.MaxRosterCards()} at {PartyLayout.CardWidth:F0}px each in " +
                    $"{PartyLayout.UsableWidth:F0}px. Narrow the cards, widen the pane, or the row needs " +
                    "to wrap or scroll - it cannot simply be given another entry.");
            }

            var children = new List<UiNode>();
            children.AddRange(BuildHeader(screen));
            children.AddRange(BuildSubtitleRow());
            children.Add(BuildFormationPanel(screen));
            children.Add(BuildRosterSection(screen, rosterCount));

            // APPENDED LAST, so it draws over every other node on the pane --
            // "top-most" per the drag brief.
            children.Add(BuildDragGhost(screen));

            // BARE, not a kit container. This pane sits on the shared
            // SystemMenuFill with no ground of its own, same as the
            // dossier.
            var ground = Ui.SystemMenuPane("PartyPane", "PartyPaneContent",
                new UiVec(PartyLayout.PaneWidth, PartyLayout.PaneHeight), children.ToArray());

            screen.Root = ground;
            return screen;
        }

        // ---- header: heading, the hint, the links, the count -------------------------
        //
        // No status pill: the lintel's mode pill states the mode for the whole
        // menu, and a refusal goes to the menu's notice.

        private static IEnumerable<UiNode> BuildHeader(PartyScreen screen)
        {
            float y = PartyLayout.HeaderCentreY;

            yield return Ui.Label("PartyFormationHeading", UiStrings.PartyFormationHeading,
                    new UiVec(PartyLayout.FormationHeadingWidth, PartyLayout.FormationHeadingHeight), 24, Heading,
                    Place.At(PartyLayout.FormationHeadingCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.FunctionalHeading);

            var banner = Ui.Label("PartyBanner", UiStrings.PartyBannerDefault,
                    new UiVec(PartyLayout.BannerWidth, PartyLayout.BannerHeight), 20, TextPrimary,
                    Place.At(PartyLayout.BannerCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.Body);
            screen.Banner = banner;
            yield return banner;

            var cancel = Ui.Button("PartyCancelLink", UiStrings.PartyCancel,
                    new UiVec(PartyLayout.CancelLinkWidth, PartyLayout.LinkHeight), 20,
                    Place.At(PartyLayout.CancelLinkCentreX, y))
                .NoChrome()
                .Styled(TypographyRole.Body)
                .Inactive();
            screen.CancelLink = cancel;
            yield return cancel;

            var bench = Ui.Button("PartyBenchLink", UiStrings.PartySendToBench,
                    new UiVec(PartyLayout.BenchLinkWidth, PartyLayout.LinkHeight), 20,
                    Place.At(PartyLayout.BenchLinkCentreX, y))
                .NoChrome()
                .Styled(TypographyRole.Body)
                .Inactive();
            screen.BenchLink = bench;
            yield return bench;

            var filled = Ui.Label("PartyFormationFilledCount", UiString.Runtime,
                    new UiVec(PartyLayout.FilledCountWidth, PartyLayout.FormationHeadingHeight), 20, Body,
                    Place.At(PartyLayout.FilledCountCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Right)
                .Styled(TypographyRole.Body);
            screen.FilledCount = filled;
            yield return filled;
        }

        // The rank rule, and the ribbon that says which way the seats face at
        // the right end of the same row.
        private static IEnumerable<UiNode> BuildSubtitleRow()
        {
            float y = PartyLayout.SubtitleCentreY;

            yield return Ui.Label("PartyFormationSubtitle", UiStrings.PartyFormationSubtitle,
                    new UiVec(PartyLayout.SubtitleWidth, PartyLayout.SubtitleHeight), 20, Body,
                    Place.At(PartyLayout.SubtitleCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.Body);

            yield return Ui.Label("PartyFormationRibbon", UiStrings.PartyFacingRibbon,
                    new UiVec(PartyLayout.RibbonWidth, PartyLayout.RibbonHeight), 24, Body,
                    Place.At(PartyLayout.RibbonCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Right)
                .Styled(TypographyRole.TacticalData);
        }

        // ---- the Formation panel: 3 seat columns -----------------------------------

        private static UiNode BuildFormationPanel(PartyScreen screen)
        {
            float w = PartyLayout.FormationPanelWidth;
            float h = PartyLayout.FormationPanelHeight;

            var children = new List<UiNode>
            {
                Ui.Solid("PartyFormationFill", CardFill, new UiVec(w, h), Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim("PartyFormation", new UiVec(w, h), CardRim));

            for (int gap = 0; gap < PartyLayout.SeatCount - 1; gap++)
            {
                children.Add(Ui.Solid($"PartyFormationDivider{gap}", CardRim,
                        new UiVec(PartyLayout.ColumnDividerWidth, PartyLayout.ColumnButtonHeight),
                        Place.At(PartyLayout.DividerCentreX(gap), PartyLayout.ColumnButtonCentreY))
                    .AsDecor());
            }

            for (int seat = 0; seat < PartyLayout.SeatCount; seat++)
            {
                children.Add(BuildSeat(screen, seat));
            }

            return Ui.Panel("PartyFormationPanel", Place.At(0f, PartyLayout.FormationPanelCentreY),
                UiSize.Fixed(w, h), children);
        }

        // A seat column: one Button (the click target), with every runtime
        // state it can ever show declared and inactive under it. Coordinates
        // below are COLUMN-LOCAL (the button's own centre is 0,0) -- see
        // PartyLayout's own header for why the panel-space numbers this reads
        // do not need translating by hand at every line.
        private static UiNode BuildSeat(PartyScreen screen, int seatIndex)
        {
            float buttonCentreY = PartyLayout.ColumnButtonCentreY;
            var size = new UiVec(PartyLayout.ColumnWidth, PartyLayout.ColumnButtonHeight);
            string stem = $"PartySeat{seatIndex}";

            var button = Ui.Button(stem + "Button", UiString.Runtime, size,
                    20, Place.At(PartyLayout.SeatCentreX(seatIndex), buttonCentreY))
                .NoChrome()
                .Styled(TypographyRole.Body);

            float LocalY(float panelY) => panelY - buttonCentreY;

            // Drawn ground-first: the glow sits under the figure, which sits
            // under its own name plate, which sits under the selection ring
            // and the lock/closed scrim -- later siblings draw on top.
            var glow = Ui.Sprite(stem + "Glow", "proc:radial_glow",
                    Place.At(0f, LocalY(PartyLayout.GlowCentreY)),
                    UiSize.Fixed(PartyLayout.ColumnWidth * 0.5f, PartyLayout.GlowHeight))
                .Coloured(GlowNeutral)
                .AsDecor();
            button.Children.Add(glow);
            screen.SeatGlows.Add(glow);

            // NO BAKED SPRITE -- the controller loads a stance from Resources/Characters/
            // <id> and activates this; until then it stays off, because an
            // Image with no sprite renders as a filled rectangle (the same
            // "red brick under the monster's feet" bug FightScreen's own
            // stage sprites exist to avoid).
            var art = Ui.Sprite(stem + "Art", null,
                    Place.At(0f, LocalY(PartyLayout.ArtCentreY)),
                    UiSize.Fixed(PartyLayout.ColumnWidth * 0.7f, PartyLayout.ArtHeight))
                .Inactive()
                .AsDecor();
            button.Children.Add(art);
            screen.SeatArts.Add(art);

            // THE FALLBACK, active by default -- a build with no art loaded
            // yet reads as a plate and a letter rather
            // than as the rectangle above.
            float monogramSize = PartyLayout.ArtHeight * 0.58f;
            var plate = Ui.OutlineBox(stem + "MonogramPlate", Place.At(0f, LocalY(PartyLayout.ArtCentreY)),
                new UiVec(monogramSize, monogramSize), MonogramFill, MonogramRim);
            plate.AsDecor();
            button.Children.Add(plate);
            screen.SeatMonogramPlates.Add(plate);

            var letter = Ui.Label(stem + "MonogramLetter", UiString.Runtime,
                    new UiVec(monogramSize, monogramSize), 40, MonogramLetter,
                    Place.At(0f, LocalY(PartyLayout.ArtCentreY)))
                .AsDecor()
                .Styled(TypographyRole.FunctionalHeading);
            button.Children.Add(letter);
            screen.SeatMonogramLetters.Add(letter);

            var label = Ui.Label(stem + "Label", SeatLabelFor(seatIndex),
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.SeatLabelHeight), 24, Body,
                    Place.At(0f, LocalY(PartyLayout.SeatLabelCentreY)))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            button.Children.Add(label);

            var badge = BuildBadge(screen, stem, LocalY(PartyLayout.BadgeCentreY));
            button.Children.Add(badge);
            screen.SeatBadges.Add(badge);

            var name = Ui.Label(stem + "Name", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.NameHeight), 22, NameText,
                    Place.At(0f, LocalY(PartyLayout.NameCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(name);
            screen.SeatNames.Add(name);

            var role = Ui.Label(stem + "Role", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.RoleHeight), 20, RoleText,
                    Place.At(0f, LocalY(PartyLayout.RoleCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(role);
            screen.SeatRoles.Add(role);

            button.Children.Add(BuildVitals(screen, stem, LocalY(PartyLayout.VitalsCentreY)));

            var ring = Ui.OutlineBox(stem + "Ring", Place.At(0f, 0f), size, null, RingGold);
            ring.AsDecor().Inactive();
            button.Children.Add(ring);
            screen.SeatRings.Add(ring);

            var scrimCaption = Ui.Label(stem + "ScrimCaption", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 32f, 30f), 20, ScrimCaption,
                    Place.At(0f, LocalY(PartyLayout.RoleCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            screen.SeatScrimCaptions.Add(scrimCaption);

            var scrim = Ui.OutlineBox(stem + "Scrim", Place.At(0f, 0f), size, ScrimFill,
                FightHudPalette.BorderQuiet, new[] { scrimCaption });
            scrim.AsDecor().Inactive();
            button.Children.Add(scrim);
            screen.SeatScrims.Add(scrim);

            screen.SeatButtons.Add(button);
            return button;
        }

        // THE FACTS THAT DECIDE PLACEMENT (plan C22): an HP bar with its
        // numbers, and both defences. On Body, not TacticalData: that role's
        // material tints numbers violet, and a number that reads as a link is
        // wrong here (spec section 2).
        private static UiNode BuildVitals(PartyScreen screen, string stem, float centreY)
        {
            float barW = PartyLayout.BarWidth;

            var track = Ui.Solid(stem + "HpTrack", FightHudPalette.Track,
                    new UiVec(barW, PartyLayout.BarHeight),
                    Place.At(0f, PartyLayout.VitalsLocalY(PartyLayout.BarCentreY)))
                .AsDecor();

            // Grows from the track's left edge: a left pivot and a width, never
            // anchors, which would be measured against the seat.
            var fill = Ui.Solid(stem + "HpFill", FightHudPalette.HpBright,
                    new UiVec(barW, PartyLayout.BarHeight),
                    Place.At(-barW * 0.5f, PartyLayout.VitalsLocalY(PartyLayout.BarCentreY), new UiVec(0f, 0.5f)))
                .AsDecor();
            screen.SeatHpFills.Add(fill);

            var hp = Ui.Label(stem + "Hp", UiStrings.PartySeatHp,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.HpHeight), 24, TextPrimary,
                    Place.At(0f, PartyLayout.VitalsLocalY(PartyLayout.HpCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            screen.SeatHpTexts.Add(hp);

            var defences = Ui.Label(stem + "Defences", UiStrings.PartySeatDefences,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.DefHeight), 20, Body,
                    Place.At(0f, PartyLayout.VitalsLocalY(PartyLayout.DefCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            screen.SeatDefTexts.Add(defences);

            var vitals = Ui.Panel(stem + "Vitals", Place.At(0f, centreY),
                UiSize.Fixed(PartyLayout.ColumnWidth - 16f, PartyLayout.VitalsHeight),
                new[] { track, fill, hp, defences });
            vitals.AsDecor();
            screen.SeatVitals.Add(vitals);
            return vitals;
        }

        // THE CARRY PREVIEW: a title ("Swap with Odette") over two columns, the
        // carried character beside the one in the seat. Declared inactive like
        // every state; the controller writes the words.
        private static UiNode BuildBadge(PartyScreen screen, string stem, float centreY)
        {
            var title = Ui.Label(stem + "BadgeText", UiString.Runtime,
                    new UiVec(PartyLayout.BadgeWidth - 16f, PartyLayout.BadgeTitleHeight), 22, GoldLight,
                    Place.At(0f, PartyLayout.BadgeTitleCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            screen.SeatBadgeTexts.Add(title);

            var carried = BuildBadgeColumn(stem + "BadgeCarried", 0,
                out var carriedName, out var carriedHp, out var carriedDef);
            screen.SeatBadgeCarriedColumns.Add(carried);
            screen.SeatBadgeCarriedNames.Add(carriedName);
            screen.SeatBadgeCarriedHps.Add(carriedHp);
            screen.SeatBadgeCarriedDefs.Add(carriedDef);

            var seated = BuildBadgeColumn(stem + "BadgeSeated", 1,
                out var seatedName, out var seatedHp, out var seatedDef);
            screen.SeatBadgeSeatedColumns.Add(seated);
            screen.SeatBadgeSeatedNames.Add(seatedName);
            screen.SeatBadgeSeatedHps.Add(seatedHp);
            screen.SeatBadgeSeatedDefs.Add(seatedDef);

            var badge = Ui.OutlineBox(stem + "Badge", Place.At(0f, centreY),
                new UiVec(PartyLayout.BadgeWidth, PartyLayout.BadgeHeight), BadgeFill, BadgeRim,
                new[] { title, carried, seated });
            badge.AsDecor().Inactive();
            return badge;
        }

        // One column of the preview: name, HP, defences, three rows of the
        // badge's own grid.
        private static UiNode BuildBadgeColumn(string name, int column,
                                               out NodeRef nameLabel, out NodeRef hpLabel, out NodeRef defLabel)
        {
            float w = PartyLayout.BadgeColumnWidth;

            var nameNode = Ui.Label(name + "Name", UiString.Runtime,
                    new UiVec(w, PartyLayout.BadgeNameHeight), 20, GoldLight,
                    Place.At(0f, PartyLayout.BadgeNameCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            var hpNode = Ui.Label(name + "Hp", UiStrings.PartySeatHp,
                    new UiVec(w, PartyLayout.BadgeHpHeight), 24, TextPrimary,
                    Place.At(0f, PartyLayout.BadgeHpCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            var defNode = Ui.Label(name + "Defences", UiStrings.PartyBadgeDefences,
                    new UiVec(w, PartyLayout.BadgeDefHeight), 20, Body,
                    Place.At(0f, PartyLayout.BadgeDefCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);

            nameLabel = nameNode;
            hpLabel = hpNode;
            defLabel = defNode;

            return Ui.Panel(name, Place.At(PartyLayout.BadgeColumnCentreX(column), PartyLayout.BadgeColumnCentreY),
                UiSize.Fixed(w, PartyLayout.BadgeColumnHeight),
                new[] { nameNode, hpNode, defNode });
        }

        private static UiString SeatLabelFor(int seatIndex)
        {
            switch (seatIndex)
            {
                case 0: return UiStrings.PartySeatFront;
                case 1: return UiStrings.PartySeatMiddle;
                default: return UiStrings.PartySeatRear;
            }
        }

        // ---- Roster: hairline, heading, N cards -------------------------------------
        //
        // ONE SECTION NODE, so "shown only while someone is unseated or a seat
        // is closed" is one switch. Its children are placed in its own frame.

        private static UiNode BuildRosterSection(PartyScreen screen, int count)
        {
            var children = new List<UiNode>
            {
                Ui.Solid("PartyHairline", CardRim,
                        new UiVec(PartyLayout.UsableWidth, PartyLayout.HairlineHeight),
                        Place.At(0f, PartyLayout.RosterLocalY(PartyLayout.HairlineCentreY)))
                    .AsDecor(),

                Ui.Label("PartyRosterHeading", UiStrings.PartyRosterHeading,
                        new UiVec(PartyLayout.RosterHeadingWidth, PartyLayout.RosterHeadingHeight), 24, Heading,
                        Place.At(-PartyLayout.UsableWidth * 0.5f + PartyLayout.RosterHeadingWidth * 0.5f,
                                 PartyLayout.RosterLocalY(PartyLayout.RosterHeadingCentreY)))
                    .AsDecor()
                    .TextAligned(UiTextAlign.Left)
                    .Styled(TypographyRole.FunctionalHeading),

                // BEFORE the cards, so it draws BEHIND them (uGUI draws later
                // siblings on top) -- a card ends the drag on top of it, and the
                // gap between cards ends the drag directly on it. Both count as
                // a roster drop; PartyController.EndDrag treats landing on a
                // card the same as landing on this zone, so this node only has
                // to cover the gaps, not steal the cards' own raycasts.
                BuildRosterDropZone(screen, count),
            };

            for (int i = 0; i < count; i++)
            {
                children.Add(BuildCard(screen, i, count));
            }

            var section = Ui.Panel("PartyRosterSection", Place.At(0f, PartyLayout.RosterSectionCentreY),
                UiSize.Fixed(PartyLayout.UsableWidth, PartyLayout.RosterSectionHeight), children);
            screen.RosterSection = section;
            return section;
        }

        private static UiNode BuildRosterDropZone(PartyScreen screen, int count)
        {
            var size = new UiVec(PartyLayout.RosterRowWidth(count), PartyLayout.RosterDropZoneHeight);

            var zone = Ui.Solid("PartyRosterDropZone", "#00000000", size,
                Place.At(0f, PartyLayout.RosterLocalY(PartyLayout.RosterRowCentreY)));

            // NOT AsDecor -- decor forces raycastTarget off (UiEmitter), and
            // being a real raycast target is this node's entire job. It
            // deliberately covers the same ground every card in the row
            // does, which is what AllowOverlap states rather than hides.
            zone.AllowOverlap("the roster's own raycastable hit-region, behind every card in the row -- " +
                "a drag ending in the gap between two cards has to land on SOMETHING, and every card ending " +
                "the same drag is handled separately by PartyController.EndDrag");
            screen.RosterDropZone = zone;
            return zone;
        }

        private static UiNode BuildCard(PartyScreen screen, int index, int count)
        {
            var size = new UiVec(PartyLayout.CardWidth, PartyLayout.CardHeight);
            float x = PartyLayout.CardCentreX(index, count);
            float y = PartyLayout.RosterLocalY(PartyLayout.RosterRowCentreY);
            string stem = $"PartyCard{index}";
            float lineWidth = PartyLayout.CardWidth - 8f;

            var button = Ui.Button(stem + "Button", UiString.Runtime, size, 20, Place.At(x, y))
                .NoChrome()
                .Styled(TypographyRole.Body);

            var wash = Ui.Solid(stem + "Wash", DimWash, size, Place.At(0f, 0f)).AsDecor().Inactive();
            button.Children.Add(wash);
            screen.CardWashes.Add(wash);

            var art = Ui.Sprite(stem + "Art", null,
                    Place.At(0f, PartyLayout.CardArtCentreY),
                    UiSize.Fixed(PartyLayout.CardWidth * 0.7f, PartyLayout.CardArtHeight))
                .Inactive()
                .AsDecor();
            button.Children.Add(art);
            screen.CardArts.Add(art);

            float monogramSize = PartyLayout.CardArtHeight * 0.62f;
            var plate = Ui.OutlineBox(stem + "MonogramPlate", Place.At(0f, PartyLayout.CardArtCentreY),
                new UiVec(monogramSize, monogramSize), MonogramFill, MonogramRim);
            plate.AsDecor();
            button.Children.Add(plate);
            screen.CardMonogramPlates.Add(plate);

            var letter = Ui.Label(stem + "MonogramLetter", UiString.Runtime,
                    new UiVec(monogramSize, monogramSize), 30, MonogramLetter,
                    Place.At(0f, PartyLayout.CardArtCentreY))
                .AsDecor()
                .Styled(TypographyRole.FunctionalHeading);
            button.Children.Add(letter);
            screen.CardMonogramLetters.Add(letter);

            // ROUND THE ART SLOT, not round the card -- the card's own outline
            // is the selection ring (CardRings below) and a second rectangle
            // at the same inset would read as a doubled border rather than as
            // a frame on a picture.
            var portraitFrame = Ui.OutlineBox(stem + "PortraitFrame",
                Place.At(0f, PartyLayout.CardArtCentreY),
                new UiVec(PartyLayout.CardWidth - 16f, PartyLayout.CardArtHeight + 2f),
                null, IdentityMetals.PortraitFrame);
            portraitFrame.AsDecor().Inactive();
            button.Children.Add(portraitFrame);
            screen.CardPortraitFrames.Add(portraitFrame);

            var name = Ui.Label(stem + "Name", UiString.Runtime,
                    new UiVec(lineWidth, PartyLayout.CardNameHeight), 20, NameText,
                    Place.At(0f, PartyLayout.CardNameCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(name);
            screen.CardNames.Add(name);

            // THE TITLE, AND THE PICKER FOR IT, as one chromeless button.
            //
            // Hidden outright for a character with no collected Title -- which
            // is every character below level 31, so for most of a career this
            // row is a line of air under the name. A disabled control there would
            // advertise a feature the player has no way to reach yet.
            //
            // A BUTTON RATHER THAN A LABEL even when only one title is
            // collected: the controller refuses the cycle in that case (there
            // is nothing to cycle to) rather than the tree emitting two
            // different things depending on how far a save has got.
            var title = Ui.Button(stem + "Title", UiString.Runtime,
                    new UiVec(lineWidth, PartyLayout.CardTitleHeight), 20,
                    Place.At(0f, PartyLayout.CardTitleCentreY))
                .NoChrome()
                .Styled(TypographyRole.Body)
                .Inactive();
            button.Children.Add(title);
            screen.CardTitles.Add(title);

            var role = Ui.Label(stem + "Role", UiString.Runtime,
                    new UiVec(lineWidth, PartyLayout.CardRoleHeight), 20, RoleText,
                    Place.At(0f, PartyLayout.CardRoleCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(role);
            screen.CardRoles.Add(role);

            var tag = Ui.Label(stem + "Tag", UiString.Runtime,
                    new UiVec(lineWidth, PartyLayout.CardTagHeight), 20, GoldLight,
                    Place.At(0f, PartyLayout.CardTagCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(tag);
            screen.CardTags.Add(tag);

            // Over the art's top edge: its words are caps, so 24.
            var selectedTag = Ui.OutlineBox(stem + "SelectedTag",
                Place.At(0f, size.Y * 0.5f - 22f), new UiVec(150f, 36f), BadgeFill, BadgeRim,
                new[]
                {
                    Ui.Label(stem + "SelectedTagText", UiStrings.PartySelectedTag,
                            new UiVec(142f, 32f), 24, GoldLight, Place.At(0f, 0f))
                        .AsDecor()
                        .Styled(TypographyRole.TacticalData),
                });
            selectedTag.AsDecor().Inactive();
            button.Children.Add(selectedTag);
            screen.CardSelectedTags.Add(selectedTag);

            var ring = Ui.OutlineBox(stem + "Ring", Place.At(0f, 0f), size, null, RingGold);
            ring.AsDecor().Inactive();
            button.Children.Add(ring);
            screen.CardRings.Add(ring);

            screen.CardButtons.Add(button);
            return button;
        }

        // ---- the drag ghost (drag): one reusable floating overlay --------------------

        // ONE node, re-skinned per drag rather than one ghost per seat/card --
        // the controller shows the dragged actor's own sprite (or its
        // monogram, the same either/or every art slot already carries) at
        // ~0.8 alpha and repositions this single node to the pointer in
        // OnDrag. Hidden and non-raycastable by default (AsDecor, which also
        // waives the sibling-overlap check A1 would otherwise want for a
        // floating overlay).
        private static UiNode BuildDragGhost(PartyScreen screen)
        {
            float w = PartyLayout.GhostArtWidth;
            float h = PartyLayout.GhostArtHeight;

            var art = Ui.Sprite("PartyDragGhostArt", null, Place.At(0f, 0f), UiSize.Fixed(w, h))
                .Coloured(GhostArtTint)
                .Inactive()
                .AsDecor();
            screen.DragGhostArt = art;

            float monogramSize = h * 0.58f;
            var plate = Ui.OutlineBox("PartyDragGhostMonogramPlate", Place.At(0f, 0f),
                new UiVec(monogramSize, monogramSize), MonogramFill, MonogramRim);
            plate.AsDecor();
            screen.DragGhostMonogramPlate = plate;

            var letter = Ui.Label("PartyDragGhostMonogramLetter", UiString.Runtime,
                    new UiVec(monogramSize, monogramSize), 40, MonogramLetter, Place.At(0f, 0f))
                .AsDecor()
                .Styled(TypographyRole.FunctionalHeading);
            screen.DragGhostMonogramLetter = letter;

            var ghost = Ui.Panel("PartyDragGhost", Place.At(0f, 0f), UiSize.Fixed(w, h),
                new[] { art, plate, letter });
            ghost.AsDecor().Inactive();
            screen.DragGhost = ghost;
            return ghost;
        }
    }
}
