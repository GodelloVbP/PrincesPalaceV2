using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What the Party pane needs from outside Domain.
    //
    // Domain cannot see ContentDatabase, so the roster count arrives as an
    // input rather than being counted here -- same reasoning, same shape, as
    // MainMenuInputs.SlotCount reading off SaveSystem.SlotCount. Wiring the
    // REAL count (ContentDatabase.LoadOrdered<CharacterDefinition>().Count) is
    // a later package's job; SystemMenuScreen.Build's own default keeps this
    // buildable today against the three characters characters.json actually
    // authors.
    public readonly struct PartyInputs
    {
        public readonly int RosterCardCount;

        public PartyInputs(int rosterCardCount)
        {
            RosterCardCount = rosterCardCount;
        }
    }

    // The Party pane: Formation (3 fixed seats) above Roster (N cards), hosted
    // in the system menu exactly as the dossier, Options and Run statistics
    // are -- its own screen class, its own controller (a later package), and
    // the menu knows nothing about either.
    //
    // NO CONTROLLER, NO DOMAIN MODEL, NO SCREENREGISTRY WIRING HERE. Every
    // state this pane can ever be in -- a badge preview, a selection ring, a
    // locked or closed scrim, a monogram standing in for missing art, the
    // toast -- is declared now, inactive, because the scene is generated once
    // and a controller can only show or hide a node that already exists in it.
    //
    // SEATS ARE INDEXED FRONT-FIRST (0 = front, matching the front-rank rule
    // landed 2026-09-07 -- squad index 0 is what takes the enemies' blows) but
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

        // The ground-glow's build-time default. Genuinely no state to reflect
        // yet -- occupancy is runtime data a later package supplies -- so this
        // is the faint/neutral reading the design's own token table names for
        // an empty position, not a guess at green or gold.
        private const string GlowNeutral = "#B4AA9629";    // rgba(180,170,150,.16)

        // The monogram standee's plate. A flat gold-rimmed card rather than a
        // themed kit plate -- it stands in for ANY character, so it cannot
        // borrow one character's own theme. CardFill (declared above) is the
        // same token every other pane's flat card ground already uses.
        private const string MonogramFill = CardFill;
        private const string MonogramRim = FightHudPalette.BorderGold;
        private const string MonogramLetter = FightHudPalette.GoldText;

        private const string DimWash = "#0A0614B0";

        // The drag ghost's art tint (P4): ~0.8 alpha over whatever sprite
        // loads at runtime, per the handoff's own "the dragged card, ~0.8
        // alpha, following the pointer" copy.
        private const string GhostArtTint = "#FFFFFFCC";

        public UiNode Root;

        // ---- singles ----------------------------------------------------------
        public NodeRef Banner;
        public NodeRef CancelLink;
        public NodeRef BenchLink;
        public NodeRef StatusPill;
        public NodeRef StatusText;
        public NodeRef FilledCount;
        public NodeRef Toast;
        public NodeRef ToastText;

        // ---- P4: drag-and-drop ------------------------------------------------------

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
        public List<NodeRef> CardMonogramPlates = new List<NodeRef>();
        public List<NodeRef> CardMonogramLetters = new List<NodeRef>();
        public List<NodeRef> CardNames = new List<NodeRef>();
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
            children.AddRange(BuildFormationHeading(screen));
            children.Add(BuildFormationPanel(screen));
            children.Add(Ui.Solid("PartyHairline", CardRim,
                    new UiVec(PartyLayout.UsableWidth, PartyLayout.HairlineHeight),
                    Place.At(0f, PartyLayout.HairlineCentreY))
                .AsDecor());
            children.AddRange(BuildRoster(screen, rosterCount));
            children.Add(BuildToast(screen));

            // APPENDED LAST, so it draws over every other node on the pane,
            // toast included -- "top-most" per the P4 brief.
            children.Add(BuildDragGhost(screen));

            // A SILVER 2:1 CONTAINER, the same hosted-pane shape every other
            // system menu tab uses -- see RunStatsScreen's own comment on why
            // no size nudge is needed against the kit's measured aspect.
            var ground = Ui.SystemMenuPane("PartyPane", "PartyPaneContent", ButtonTheme.Silver,
                new UiVec(PartyLayout.PaneWidth, PartyLayout.PaneHeight), children.ToArray());

            screen.Root = ground;
            return screen;
        }

        // ---- header: banner + links + status pill ----------------------------------

        private static IEnumerable<UiNode> BuildHeader(PartyScreen screen)
        {
            float y = PartyLayout.HeaderCentreY;

            var banner = Ui.Label("PartyBanner", UiStrings.PartyBannerDefault,
                    new UiVec(PartyLayout.BannerWidth, PartyLayout.HeaderHeight), 16, TextPrimary,
                    Place.At(PartyLayout.BannerCentreX, y))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.Body);
            screen.Banner = banner;

            var cancel = Ui.Button("PartyCancelLink", UiStrings.PartyCancel,
                    new UiVec(PartyLayout.CancelLinkWidth, PartyLayout.LinkHeight), 14,
                    Place.At(PartyLayout.CancelLinkCentreX, y))
                .NoChrome()
                .Inactive();
            screen.CancelLink = cancel;

            var bench = Ui.Button("PartyBenchLink", UiStrings.PartySendToBench,
                    new UiVec(PartyLayout.BenchLinkWidth, PartyLayout.LinkHeight), 14,
                    Place.At(PartyLayout.BenchLinkCentreX, y))
                .NoChrome()
                .Inactive();
            screen.BenchLink = bench;

            var statusText = Ui.Label("PartyStatusText", UiString.Runtime,
                    new UiVec(PartyLayout.StatusPillWidth - 24f, PartyLayout.StatusPillHeight), 14, GoldLight,
                    Place.At(6f, 0f))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);

            var statusDot = Ui.Solid("PartyStatusDot", GoldLight, new UiVec(8f, 8f),
                    Place.At(-PartyLayout.StatusPillWidth * 0.5f + 18f, 0f))
                .AsDecor();

            var statusPill = Ui.OutlineBox("PartyStatusPill", Place.At(PartyLayout.StatusPillCentreX, y),
                new UiVec(PartyLayout.StatusPillWidth, PartyLayout.StatusPillHeight),
                CardFill, BadgeRim, new[] { statusDot, statusText });
            screen.StatusPill = statusPill;
            screen.StatusText = statusText;

            yield return banner;
            yield return cancel;
            yield return bench;
            yield return statusPill;
        }

        // ---- Formation's own heading row -------------------------------------------

        private static IEnumerable<UiNode> BuildFormationHeading(PartyScreen screen)
        {
            yield return Ui.Label("PartyFormationHeading", UiStrings.PartyFormationHeading,
                    new UiVec(PartyLayout.FormationHeadingWidth, PartyLayout.FormationHeadingHeight), 18, Heading,
                    Place.At(-PartyLayout.UsableWidth * 0.5f + PartyLayout.FormationHeadingWidth * 0.5f,
                             PartyLayout.FormationHeadingCentreY))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.FunctionalHeading);

            var filled = Ui.Label("PartyFormationFilledCount", UiString.Runtime,
                    new UiVec(PartyLayout.FormationHeadingWidth, PartyLayout.FormationHeadingHeight), 15, Body,
                    Place.At(PartyLayout.UsableWidth * 0.5f - PartyLayout.FormationHeadingWidth * 0.5f,
                             PartyLayout.FormationHeadingCentreY))
                .AsDecor()
                .TextAligned(UiTextAlign.Right)
                .Styled(TypographyRole.Body);
            screen.FilledCount = filled;
            yield return filled;

            yield return Ui.Label("PartyFormationSubtitle", UiStrings.PartyFormationSubtitle,
                    new UiVec(PartyLayout.UsableWidth, PartyLayout.FormationSubtitleHeight), 13, Body,
                    Place.At(0f, PartyLayout.FormationSubtitleCentreY))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.Body);
        }

        // ---- the Formation panel: ribbon + 3 seat columns --------------------------

        private static UiNode BuildFormationPanel(PartyScreen screen)
        {
            float w = PartyLayout.FormationPanelWidth;
            float h = PartyLayout.FormationPanelHeight;

            var children = new List<UiNode>
            {
                Ui.Solid("PartyFormationFill", CardFill, new UiVec(w, h), Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim("PartyFormation", new UiVec(w, h), CardRim));

            children.Add(Ui.Label("PartyFormationRibbon", UiStrings.PartyFacingRibbon,
                    new UiVec(w - 40f, PartyLayout.RibbonHeight), 13, Body,
                    Place.At(0f, PartyLayout.RibbonCentreY))
                .AsDecor()
                .TextAligned(UiTextAlign.Right)
                .Styled(TypographyRole.TacticalData));

            children.Add(Ui.Solid("PartyFormationRibbonRule", CardRim, new UiVec(w, 1f),
                    Place.At(0f, PartyLayout.RibbonRuleY))
                .AsDecor());

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
                    18, Place.At(PartyLayout.SeatCentreX(seatIndex), buttonCentreY))
                .NoChrome();

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

            // NO BAKED SPRITE -- P3 loads a stance from Resources/Characters/
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
            // yet (every seat, today) reads as a plate and a letter rather
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
                .AsDecor();
            button.Children.Add(letter);
            screen.SeatMonogramLetters.Add(letter);

            var label = Ui.Label(stem + "Label", SeatLabelFor(seatIndex),
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.SeatLabelHeight), 14, Body,
                    Place.At(0f, LocalY(PartyLayout.SeatLabelCentreY)))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            button.Children.Add(label);

            var badge = Ui.OutlineBox(stem + "Badge", Place.At(0f, LocalY(PartyLayout.BadgeCentreY)),
                new UiVec(PartyLayout.ColumnWidth - 40f, PartyLayout.BadgeHeight), BadgeFill, BadgeRim,
                new[]
                {
                    Ui.Label(stem + "BadgeText", UiString.Runtime,
                            new UiVec(PartyLayout.ColumnWidth - 56f, PartyLayout.BadgeHeight), 13, GoldLight,
                            Place.At(0f, 0f))
                        .AsDecor()
                        .Styled(TypographyRole.TacticalData),
                });
            badge.AsDecor().Inactive();
            button.Children.Add(badge);
            screen.SeatBadges.Add(badge);

            var name = Ui.Label(stem + "Name", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.NameHeight), 17, NameText,
                    Place.At(0f, LocalY(PartyLayout.NameCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(name);
            screen.SeatNames.Add(name);

            var role = Ui.Label(stem + "Role", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 16f, PartyLayout.RoleHeight), 13, RoleText,
                    Place.At(0f, LocalY(PartyLayout.RoleCentreY)))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(role);
            screen.SeatRoles.Add(role);

            var ring = Ui.OutlineBox(stem + "Ring", Place.At(0f, 0f), size, null, RingGold);
            ring.AsDecor().Inactive();
            button.Children.Add(ring);
            screen.SeatRings.Add(ring);

            var scrimCaption = Ui.Label(stem + "ScrimCaption", UiString.Runtime,
                    new UiVec(PartyLayout.ColumnWidth - 32f, 22f), 13, ScrimCaption,
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

        private static UiString SeatLabelFor(int seatIndex)
        {
            switch (seatIndex)
            {
                case 0: return UiStrings.PartySeatFront;
                case 1: return UiStrings.PartySeatMiddle;
                default: return UiStrings.PartySeatRear;
            }
        }

        // ---- Roster: heading + N cards ----------------------------------------------

        private static IEnumerable<UiNode> BuildRoster(PartyScreen screen, int count)
        {
            yield return Ui.Label("PartyRosterHeading", UiStrings.PartyRosterHeading,
                    new UiVec(PartyLayout.RosterHeadingWidth, PartyLayout.RosterHeadingHeight), 18, Heading,
                    Place.At(-PartyLayout.UsableWidth * 0.5f + PartyLayout.RosterHeadingWidth * 0.5f,
                             PartyLayout.RosterHeadingCentreY))
                .AsDecor()
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.FunctionalHeading);

            // BEFORE the cards, so it draws BEHIND them (uGUI draws later
            // siblings on top) -- a card ends the drag on top of it, and the
            // gap between cards ends the drag directly on it. Both count as
            // a roster drop; PartyController.EndDrag treats landing on a
            // card the same as landing on this zone, so this node only has
            // to cover the gaps, not steal the cards' own raycasts.
            yield return BuildRosterDropZone(screen, count);

            for (int i = 0; i < count; i++)
            {
                yield return BuildCard(screen, i, count);
            }
        }

        private static UiNode BuildRosterDropZone(PartyScreen screen, int count)
        {
            var size = new UiVec(PartyLayout.RosterRowWidth(count), PartyLayout.RosterDropZoneHeight);

            var zone = Ui.Solid("PartyRosterDropZone", "#00000000", size,
                Place.At(0f, PartyLayout.RosterRowCentreY));

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
            float y = PartyLayout.RosterRowCentreY;
            string stem = $"PartyCard{index}";

            var button = Ui.Button(stem + "Button", UiString.Runtime, size, 15, Place.At(x, y))
                .NoChrome();

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
                    new UiVec(monogramSize, monogramSize), 22, MonogramLetter,
                    Place.At(0f, PartyLayout.CardArtCentreY))
                .AsDecor();
            button.Children.Add(letter);
            screen.CardMonogramLetters.Add(letter);

            var name = Ui.Label(stem + "Name", UiString.Runtime,
                    new UiVec(PartyLayout.CardWidth - 12f, PartyLayout.CardNameHeight), 14, NameText,
                    Place.At(0f, PartyLayout.CardNameCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(name);
            screen.CardNames.Add(name);

            var role = Ui.Label(stem + "Role", UiString.Runtime,
                    new UiVec(PartyLayout.CardWidth - 12f, PartyLayout.CardRoleHeight), 11, RoleText,
                    Place.At(0f, PartyLayout.CardRoleCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(role);
            screen.CardRoles.Add(role);

            var tag = Ui.Label(stem + "Tag", UiString.Runtime,
                    new UiVec(PartyLayout.CardWidth - 12f, PartyLayout.CardTagHeight), 11, GoldLight,
                    Place.At(0f, PartyLayout.CardTagCentreY))
                .AsDecor()
                .Styled(TypographyRole.Body);
            button.Children.Add(tag);
            screen.CardTags.Add(tag);

            var selectedTag = Ui.OutlineBox(stem + "SelectedTag",
                Place.At(0f, size.Y * 0.5f - 12f), new UiVec(72f, 18f), BadgeFill, BadgeRim,
                new[]
                {
                    Ui.Label(stem + "SelectedTagText", UiStrings.PartySelectedTag,
                            new UiVec(64f, 16f), 10, GoldLight, Place.At(0f, 0f))
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

        // ---- the toast: pinned into the ROSTER heading row's own empty space -------

        private static UiNode BuildToast(PartyScreen screen)
        {
            var text = Ui.Label("PartyToastText", UiString.Runtime,
                    new UiVec(PartyLayout.ToastWidth - 32f, PartyLayout.ToastHeight), 14, TextPrimary,
                    Place.At(0f, 0f))
                .AsDecor()
                .Styled(TypographyRole.Body);

            var toast = Ui.OutlineBox("PartyToast",
                Place.At(PartyLayout.ToastCentreX, PartyLayout.ToastCentreY),
                new UiVec(PartyLayout.ToastWidth, PartyLayout.ToastHeight), "#12091CF2", BadgeRim,
                new[] { text });

            // A TOAST IS A FLOATING OVERLAY, hidden by default and shown only
            // for its ~2.4s -- but it used to float over the bottom of the
            // card row, hiding exactly the role/tag lines a swap just changed.
            // It now shares the ROSTER heading row instead, right-aligned into
            // that row's own empty space, so it never sits over a card. AsDecor
            // still does the only job it needs to here: non-interactive and
            // hidden by default. It no longer needs to waive a sibling-overlap
            // check (A1) -- this position has nothing left to overlap.
            toast.AsDecor().Inactive();
            screen.Toast = toast;
            screen.ToastText = text;
            return toast;
        }

        // ---- the drag ghost (P4): one reusable floating overlay --------------------

        // ONE node, re-skinned per drag rather than one ghost per seat/card --
        // the controller shows the dragged actor's own sprite (or its
        // monogram, the same either/or every art slot already carries) at
        // ~0.8 alpha and repositions this single node to the pointer in
        // OnDrag. Hidden and non-raycastable by default (AsDecor -- see
        // BuildToast's own comment on why that also waives the sibling-
        // overlap check A1 would otherwise want for a floating overlay).
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
                .AsDecor();
            screen.DragGhostMonogramLetter = letter;

            var ghost = Ui.Panel("PartyDragGhost", Place.At(0f, 0f), UiSize.Fixed(w, h),
                new[] { art, plate, letter });
            ghost.AsDecor().Inactive();
            screen.DragGhost = ghost;
            return ghost;
        }
    }
}
