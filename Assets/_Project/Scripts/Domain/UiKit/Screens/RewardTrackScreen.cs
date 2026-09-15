using System.Collections.Generic;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The reward track: one long line, a card that says what you are looking
    // at, and a ribbon that shows the whole hundred at once.
    //
    // A battle pass, read left to right. The rail was the whole screen once and
    // the brief's own open questions were all versions of the same complaint --
    // it is airy, nothing marks what is next, and eight nodes of ninety-nine
    // are visible. The two new bands are the answer: a FOCUS CARD in the upper
    // third that rests on the next reward until the pointer says otherwise, and
    // an ASCENT RIBBON in the lower third where all ninety-nine levels fit at
    // 14.5px apart and the visible window is a box you can drag.
    //
    // EVERY NODE IS EMITTED WITH ITS REAL POSITION. Level 40 sits at the same x
    // forever, so there is nothing here for a controller to place at runtime --
    // only the content rect slides, which is one anchoredPosition rather than a
    // hundred. The two exceptions prove the rule: the NEXT caret and the
    // here-node's halo are single nodes that move, because exactly one level is
    // next and exactly one is yours, and emitting ninety-nine of each to switch
    // ninety-eight off would be the same list twice.
    //
    // A hundred nodes is a lot of tree, and deliberately: UiAudit re-solves all
    // of it at four aspects, so a caption that outgrows its pitch or a dot that
    // escapes the rail is a build failure rather than something to notice in a
    // screenshot later.
    public sealed class RewardTrackScreen
    {
        // ---- the measured palette, handoff section 5 ------------------------
        //
        // Gold is applied as stroke, glow and small marks. The only broad fills
        // on this screen are the panel ground and the discs.

        public const string Ground = "#120A18";
        public const string Gold = "#F2DB9E";
        public const string DeepViolet = "#2E2244";
        public const string Pale = "#EDE6FF";
        public const string Lilac = "#D6C8E8";

        // NEW, and the reason is measurable: marks on a lit disc used to be
        // DeepViolet, which at 15px against #F2DB9E has too little contrast to
        // resolve as a shape. This is the same violet driven most of the way to
        // black and warmed, so it sits on gold as ink rather than as a stain.
        public const string MarkInk = "#2A1D14";

        // The rail band's soft wash, and the hairlines that bound it. The band
        // is what stops the rail floating in the middle of an empty panel.
        //
        // 2E, NOT 99. This was a flat 60% violet rect and the design's is an
        // 18% wash that fades to nothing at both edges -- three times too
        // strong and with a hard line at each end, which turned "the rail runs
        // through something" into a grey stripe laid across the panel. The
        // stripe is also what made the empty strip below it read as a hole:
        // give the middle third a solid block of its own and everything that is
        // not that block becomes a gap.
        //
        // The shape lives in the band_fade sprite; this is only how dark it
        // gets at its darkest.
        public const string BandWash = "#2E22442E";
        public const string BandEdgeTop = "#F2DB9E29";
        public const string BandEdgeBottom = "#F2DB9E1F";

        // The panel's own furniture: four corner brackets and a wash at the
        // middle, both of them the difference between a ground and a rect.
        public const string PanelWash = "#2E224452";
        public const string CornerBracket = "#F2DB9E4D";

        // The rail, in three weights: the dim line under everything, the lit
        // half over it, and the bloom under that.
        //
        // RAILDONE IS FULL GOLD BECAUSE THE SPRITE CARRIES THE ALPHA. The lit
        // rail is a ramp now -- 32% at the start of the run and 100% at the
        // player's own node -- so a flat tint here would flatten the one thing
        // the ramp exists to say, which is which END of the lit half is now.
        // It was measured at 6B, then raised to BF when the band wash lifted
        // the ground behind it; both of those were the same tint applied
        // evenly, and neither could brighten toward anything.
        public const string Rail = "#C8B4DE29";
        public const string RailDone = "#F2DB9E";
        public const string RailGlow = "#F2DB9E52";
        public const string Shimmer = "#F2DB9E00";

        // ---- disc tints, one per state --------------------------------------
        //
        // All four are the SAME baked sprite (proc:disc_metal) under a different
        // Image.color, which is what keeps a struck-metal highlight consistent
        // across a hundred discs in four states. See that bake's header for the
        // one thing this cannot do: bend a hue rather than darken it.

        public const string DiscToCome = "#3B2C56";
        public const string DiscLit = "#F2DB9E";

        // #F2ECFF rather than the handoff's #FFFFFF, so the shaded rim lands
        // near its #CFC2E8 instead of at a neutral grey.
        public const string DiscHere = "#F2ECFF";

        // The 1px rim on an unreached filler disc. A milestone gets the gold
        // plate ring instead, at the same node.
        public const string RimToCome = "#C8B4DE1F";

        // ---- text, one per state --------------------------------------------

        public const string TextToCome = "#D6C8E87A";
        public const string TextWaiting = "#F2DB9E";
        public const string TextCollected = "#EDE6FF";
        public const string TextCollectedDim = "#EDE6FFB8";
        public const string TextHere = "#F2DB9E";
        public const string TextQuiet = "#D6C8E88C";

        // The seal pip, in two parts: a near-black disc and the gold ring and
        // check drawn ON it. It used to be one node with the check knocked out,
        // showing the gold disc underneath -- see the seal_mark bake for why
        // that stopped being the better trick at nine pixels across.
        public const string SealInk = "#1A1024";
        public const string SealMark = "#F2DB9E";

        // ---- the focus card -------------------------------------------------

        public const string CardGround = "#241735";
        public const string CardEdge = "#F2DB9E4D";
        public const string CardDivider = "#C8B4DE1F";

        // The reward's name on the card, and the kicker above it. NEITHER IS
        // TINTED BY STATE, unlike everything on the rail: this is the focus
        // element, and dimming it for a reward not yet reached makes it hardest
        // to read exactly when it is doing its job.
        public const string CardName = "#F5F0FF";
        public const string CardKickerQuiet = "#F2DB9EB0";

        // The art plate: a near-black well, ringed brighter once the reward is
        // reachable, with the mat inside it carrying the reward-kind hue.
        public const string PlateGround = "#0E0714";
        public const string PlateEdgeLit = "#F2DB9E99";
        public const string PlateEdgeDim = "#F2DB9E52";
        public const string PlateMatEdge = "#D6C8E81F";
        public const string PlateTick = "#F2DB9E8A";

        // ---- the summary row and the ribbon ---------------------------------

        public const string Rule = "#F2DB9E4D";
        public const string TextFigureLabel = "#D6C8E888";
        public const string TextNextAt = "#D6C8E8AA";

        // A tick for a level not yet reached, and for one reached and
        // collected. The waiting weight is Gold, which is the point of it.
        public const string RibbonTickToCome = "#C8B4DE52";
        public const string RibbonTickReached = "#F2DB9EB8";
        public const string RibbonDotToCome = "#1C1228";

        // The milestone's aura, built transparent and driven entirely by the
        // motion pass -- so a screenshot taken at build time is not a picture
        // of a glow frozen at whatever phase the tree happened to declare.
        //
        // The other half of the cue, the turning ring, has no colour of its own
        // here: it IS the plate ring, which PaintNode already colours.
        public const string MilestoneAura = "#F2DB9E00";

        public const string Halo = "#F2DB9E00";
        public const string Pulse = "#F2DB9E00";
        public const string Caret = "#F2DB9E";
        public const string Burst = "#F2DB9E00";

        // ---- the card's painted art -----------------------------------------
        //
        // THE TALENT TREE'S ICON SET, which is already painted, already
        // circular and already in this game's hand: gold medallions on a dark
        // ground, which is what section 8 asks the reward art to be.
        //
        // ONLY THE CARD USES THEM, and the constraint is size rather than
        // taste. Section 8's filler mark is 15px and its milestone plate 44;
        // these are 240-350px of painted detail, and at 15px a painted
        // medallion is a gold dot. The strokes on the rail are there because a
        // stroke is the only thing that reads at that size -- the same reason
        // the design gives for choosing them. The card's slot is 86, which is
        // the one place on this screen with room for a painting.
        //
        // A SECOND CONSTRAINT KEEPS THEM OFF THE RAIL even at 44: a node's disc
        // is tinted by state, violet unreached and gold reached and pale where
        // the player stands, and a painted medallion tinted violet reads as a
        // painting behind glass rather than as an unreached reward.
        //
        // These are PLACEMENTS, not decisions. Twelve rewards mapped onto
        // twelve icons drawn for a different screen is a first fit and should
        // be overruled by anyone with an opinion; the whole map is the switch
        // in RewardTrackLayout.CardArtKeyFor.
        //
        // THESE ARE RE-POINTS OF A FIXED SET OF ART CONSTANTS
        // (docs/PLAN_REWARD_TRACKS.md §1's table), not new commissions --
        // each constant is named for what actually uses it today.
        public const string IconRoot = "UI/TalentTree/Icons/Processed";

        public const string StatArtKey = IconRoot + "/ability_score.png";
        public const string HealthArtKey = IconRoot + "/health.png";
        public const string RespecArtKey = IconRoot + "/regen.png";
        public const string SecondLifeArtKey = IconRoot + "/flame_unused.png";

        // was ExpArtKey (ExpFind) -> ElementalDamagePercent
        public const string ElementalArtKey = IconRoot + "/skill_cost.png";
        // was FavorArtKey (Favor) -> MaxMana
        public const string MaxManaArtKey = IconRoot + "/eye_unused.png";
        // was RestArtKey (RestBeforeBoss) -> SignatureGainPerTurn
        public const string SignatureGainArtKey = IconRoot + "/mana.png";
        // was RerollArtKey (OfferReroll) -> ManaRegen
        public const string ManaRegenArtKey = IconRoot + "/precision.png";
        // was OfferArtKey (WiderOffer) -> SignatureAbsorbs
        public const string SignatureAbsorbArtKey = IconRoot + "/cross_unused.png";
        // was RelicArtKey (StartingRelics) -> SignatureCapacity
        public const string SignatureCapacityArtKey = IconRoot + "/defense.png";
        // was ChosenRelicArtKey (ChosenStartingRelics) -> SignatureGainOnDamageTaken
        public const string SignatureHurtArtKey = IconRoot + "/fist_unused.png";
        // was SecondLifeRefreshArtKey (SecondLifeRefresh) -> UnlockSkill
        public const string UnlockSkillArtKey = IconRoot + "/skull_unused.png";

        // PHASE 3's seven new reward kinds. Same "placement, not a decision"
        // posture as the block above -- FuryGainOnAttack and
        // FuryStartOfFight repoint the two Processed icons the original
        // twelve left spare (attack.png, speed.png -- a Fury reward is
        // physical and fast, which is at least not an arbitrary pairing);
        // the other five have no spare Processed icon to repoint at all, so
        // they name a file that does not exist yet rather than collide with
        // an already-assigned one -- LoadSprite's existing graceful
        // degradation (null + a warning, never a thrown exception) is what
        // keeps that a placeholder instead of a build break. Phase 4/5 is
        // where real art (or a real repoint) replaces every one of these.
        public const string FuryGainArtKey = IconRoot + "/attack.png";
        public const string FuryStartArtKey = IconRoot + "/speed.png";
        public const string SpellCostDeltaArtKey = IconRoot + "/spell_cost_delta_unused.png";
        public const string SkillCostDeltaArtKey = IconRoot + "/skill_cost_delta_unused.png";
        public const string SkillFlatDeltaArtKey = IconRoot + "/skill_flat_delta_unused.png";
        public const string SignatureAbsorbPerPointArtKey = IconRoot + "/absorb_per_point_unused.png";
        public const string IdentityArtKey = IconRoot + "/identity_unused.png";

        public UiNode Root;

        public NodeRef Viewport;
        public NodeRef Content;
        public NodeRef RailFill;
        public NodeRef RailGlowFill;
        public NodeRef Shimmering;
        public NodeRef HereHalo;
        public NodeRef NextMark;
        // The claim burst's four rigs, and their parts flattened: rig k's spark
        // s is at k * BurstSparkCount + s. Flat because a scene serialises
        // arrays and not jagged ones, and because the controller walks them by
        // arithmetic rather than by shape.
        public List<NodeRef> BurstRoots = new List<NodeRef>();
        public List<NodeRef> BurstCores = new List<NodeRef>();
        public List<NodeRef> BurstRings = new List<NodeRef>();
        public List<NodeRef> BurstRays = new List<NodeRef>();
        public List<NodeRef> BurstSparks = new List<NodeRef>();

        public NodeRef SummaryLevel;
        public NodeRef SummaryNextAt;
        public NodeRef SummaryReward;
        public NodeRef CollectButton;
        public NodeRef CollectCaption;
        public NodeRef CollectPip;
        public NodeRef CloseButton;

        public NodeRef Card;
        public NodeRef CardMat;
        public NodeRef CardArt;
        public NodeRef CardKicker;
        public NodeRef CardLevel;
        public NodeRef CardCaption;
        public NodeRef CardState;
        public NodeRef CardStateDot;

        public NodeRef Ribbon;
        public NodeRef RibbonFill;
        public NodeRef RibbonPlayhead;
        public NodeRef RibbonWindow;
        public NodeRef RibbonGrab;

        // One entry per level from 2 to 100, in that order -- so index i is
        // level i + RewardTrackLayout.FirstLevel and the controller never has
        // to search. Seven lists rather than one node with seven children,
        // because uGUI has no compound and the emitter binds by list.
        public List<NodeRef> Dots = new List<NodeRef>();
        public List<NodeRef> Rings = new List<NodeRef>();
        public List<NodeRef> Pulses = new List<NodeRef>();
        public List<NodeRef> Mats = new List<NodeRef>();
        public List<NodeRef> Icons = new List<NodeRef>();
        public List<NodeRef> Seals = new List<NodeRef>();
        public List<NodeRef> Captions = new List<NodeRef>();
        public List<NodeRef> LevelNumbers = new List<NodeRef>();
        // The twelve landmarks' ambient cues, in ascending level order -- the
        // same order MilestoneLevels() walks, which is how the controller finds
        // the level an entry belongs to without storing it twice.
        public List<NodeRef> MilestoneAuras = new List<NodeRef>();
        public List<NodeRef> MilestoneRings = new List<NodeRef>();

        public List<NodeRef> RibbonTicks = new List<NodeRef>();
        public List<NodeRef> RibbonDots = new List<NodeRef>();
        public List<NodeRef> RibbonNumbers = new List<NodeRef>();

        // NOT migrated to TypographyRole in this pass, deliberately, unlike
        // the other four screens: almost every label here is one of a
        // hundred-plus pooled/runtime nodes (TrackCaption*, TrackLevel*,
        // TrackRibbonNumber*, the card's own fields) with its own tuned font
        // constant already threaded through RewardTrackLayout (CardCaptionFont,
        // MilestoneCaptionFont, and a dozen more) -- a role's own size band
        // and uppercase rule would fight numbers this class was carefully
        // measured against, for text that is not a title, heading or button
        // caption in the first place.
        public static RewardTrackScreen Build()
        {
            var screen = new RewardTrackScreen();

            var children = new List<UiNode>();
            children.AddRange(BuildPanelFurniture());
            children.AddRange(BuildRailBand(screen));
            children.Add(BuildCard(screen));
            children.AddRange(BuildRibbon(screen));
            children.AddRange(BuildSummaryRow(screen));

            // BARE, not a kit container (owner's call, 2026-09-07 -- every
            // frame inside the system menu read as ugly), UNLIKE THE OTHER
            // FOUR HOSTED PANES this still needs an opaque ground of its own:
            // it opens OVER the dossier's own content within the same parent
            // pane rather than sitting only on the shared, translucent
            // SystemMenuFill (#1A1024F5, 96%) every other pane relies on --
            // three columns of numbers must not read through a hundred
            // captions underneath. The Gold container's own painted art used
            // to carry that opacity; the flat #120A18FA Solid ground is what
            // did the same job before the container existed (git history),
            // so it comes back below as this pane's own first child.
            var ground = Ui.SystemMenuPane("RewardTrackPanel", "RewardTrackPanelContent",
                new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight), children.ToArray());
            ground.Children.Insert(0, Ui.Solid("RewardTrackPanelFill", Ground + "FA",
                    new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight), Place.At(0f, 0f))
                .AsDecor());
            ground.Inactive();

            screen.Root = ground;

            return screen;
        }

        // ---- the panel's own ground ------------------------------------------

        // A WASH AT THE MIDDLE AND FOUR CORNERS, which is all the difference
        // between a painted ground and a filled rect -- and this screen is
        // 1600x804 of one flat colour with three bands of content on it, so it
        // has more of that rect showing than anything else in the game.
        //
        // Neither is information. Both are the reason the eye reads the panel
        // as an object with edges and a centre rather than as the absence of
        // one, which is what the brief's "the screen is airy" was describing
        // and what no amount of moving the content around can fix.
        private static IEnumerable<UiNode> BuildPanelFurniture()
        {
            // Bigger than the panel it sits in, on purpose: the design's
            // gradient is stated by its RADII, 72% of the width and 58% of the
            // height, so the ellipse is nearly one and a half panels across and
            // what shows is its middle.
            yield return Ui.Sprite("TrackPanelWash", "proc:radial_glow",
                    new UiVec(RewardTrackLayout.WashWidth, RewardTrackLayout.WashHeight),
                    Place.At(0f, 0f))
                .Coloured(PanelWash)
                .AsDecor()
                .AllowOverflow("the wash is a gradient wider and taller than the panel because only its centre is meant to be seen - clipping it to the panel would put the ellipse's own edge on screen, which is the one part of a radial gradient that must never show");

            float cx = RewardTrackLayout.CornerCentreX;
            float cy = RewardTrackLayout.CornerCentreY;
            float arm = RewardTrackLayout.CornerArm;
            float stroke = RewardTrackLayout.CornerStroke;

            // Eight hairlines, two per corner. Written as a loop over the four
            // sign pairs rather than as eight authored positions, because eight
            // positions is eight chances to mistype one and the mistake would
            // be a corner three pixels out of true.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? 1f : -1f;
                string corner = $"{((i & 2) == 0 ? "Top" : "Bottom")}{((i & 1) == 0 ? "Left" : "Right")}";

                yield return Ui.Solid($"TrackCorner{corner}H", CornerBracket,
                        new UiVec(arm, stroke),
                        Place.At(cx * sx, (cy + arm * 0.5f - stroke * 0.5f) * sy))
                    .AsDecor();

                yield return Ui.Solid($"TrackCorner{corner}V", CornerBracket,
                        new UiVec(stroke, arm),
                        Place.At((cx + arm * 0.5f - stroke * 0.5f) * sx, cy * sy))
                    .AsDecor();
            }
        }

        // ---- the rail band --------------------------------------------------

        private static IEnumerable<UiNode> BuildRailBand(RewardTrackScreen screen)
        {
            // A SOFT GROUND UNDER THE RAIL, bounded by two hairlines.
            //
            // The brief's main open question was that the screen is airy: the
            // rail sat on nothing, in the middle of a flat panel, with the top
            // and bottom thirds empty. Two of those thirds now have bands of
            // their own, and this gives the middle one an edge so the rail
            // reads as running THROUGH something rather than across a void.
            // PaneContentWidth, NOT PanelWidth (balance-bot, 2026-09-02) --
            // see RewardTrackLayout.BandEdgeWidth's own comment: this is a
            // child of the container's content panel now, 1488 wide rather
            // than the declared 1600 frame.
            yield return Ui.Sprite("TrackBandWash", "proc:band_fade",
                    new UiVec(RewardTrackLayout.PaneContentWidth, RewardTrackLayout.BandHeight),
                    Place.At(0f, RewardTrackLayout.BandCentreY))
                .Coloured(BandWash)
                .AsDecor();

            yield return Ui.Sprite("TrackBandTopEdge", "proc:hairline_fade",
                    new UiVec(RewardTrackLayout.BandEdgeWidth, 1f),
                    Place.At(0f, RewardTrackLayout.CentreY(RewardTrackLayout.BandTopFromTop)))
                .Coloured(BandEdgeTop)
                .AsDecor();

            yield return Ui.Sprite("TrackBandBottomEdge", "proc:hairline_fade",
                    new UiVec(RewardTrackLayout.BandEdgeWidth, 1f),
                    Place.At(0f, RewardTrackLayout.CentreY(RewardTrackLayout.BandHairlineFromTop)))
                .Coloured(BandEdgeBottom)
                .AsDecor();

            var railChildren = new List<UiNode>();

            // THE RAIL, in two layers: the whole line dim, and a second bar over
            // it that the controller stretches to however far the player has
            // got. Two nodes rather than one tinted per-segment, because
            // "progress along a line" is one rect wide and a hundred coloured
            // hairlines is a hundred things to keep in step.
            railChildren.Add(Ui.Solid("TrackRail", Rail,
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailHeight),
                    Place.At(0f, 0f))
                .AsDecor());

            // The bloom under the lit half, stretched by the same width. Under
            // the rail rather than over it, so the line stays a line.
            var railGlow = Ui.Sprite("TrackRailGlow", "proc:rail_glow",
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailGlowHeight),
                    Place.At(-RewardTrackLayout.ContentWidth * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .Coloured(RailGlow)
                .AsDecor();
            screen.RailGlowFill = railGlow;
            railChildren.Add(railGlow);

            var railFill = Ui.Sprite("TrackRailFill", "proc:rail_ramp",
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailLitHeight),
                    Place.At(-RewardTrackLayout.ContentWidth * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .Coloured(RailDone)
                .AsDecor();
            screen.RailFill = railFill;
            railChildren.Add(railFill);

            // A 300px gradient walking the full 19,000, on a nine-second loop.
            // Built transparent: the controller fades it in, so a screenshot
            // taken at build time is not a picture of a light in the wrong
            // place.
            var shimmer = Ui.Sprite("TrackShimmer", "proc:shimmer_band",
                    new UiVec(RewardTrackLayout.ShimmerWidth, RewardTrackLayout.RailHeight * 2f),
                    Place.At(0f, 0f))
                .Coloured(Shimmer)
                .AsDecor();
            screen.Shimmering = shimmer;
            railChildren.Add(shimmer);

            // The breathing halo behind whichever node is the player's own.
            // ONE node the controller moves, because exactly one level is ever
            // yours -- the alternative is ninety-nine haloes with ninety-eight
            // switched off.
            // Built at a MILESTONE's size, which is the largest it is ever
            // asked to be -- the controller sizes it to whichever node the
            // player is standing on, and a node built smaller than its largest
            // case would have UiAudit measuring the wrong rect.
            float halo = RewardTrackLayout.MilestoneDiameter + RewardTrackLayout.HaloPad;
            var haloNode = Ui.Sprite("TrackHereHalo", "proc:radial_glow",
                    new UiVec(halo, halo),
                    Place.At(0f, 0f))
                .Coloured(Halo)
                .AsDecor()
                .Inactive();
            screen.HereHalo = haloNode;
            railChildren.Add(haloNode);

            // THE CLAIM BURST, four rigs deep, moved to whichever node is
            // being collected. Four rather than one because claims land 130ms
            // apart and a burst lives five times that; four rather than
            // ninety-nine because only a handful are ever alight at once.
            for (int rig = 0; rig < RewardTrackLayout.BurstRigs; rig++)
            {
                railChildren.Add(screen.BuildBurstRig(rig));
            }

            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                railChildren.AddRange(screen.BuildNode(level));
            }

            // NOTHING MARKED THE NEXT REWARD, which was one of the brief's six
            // open questions. Drawn last so it sits over everything in the
            // column it points into.
            //
            // A WORD AND A LINE, not an arrow. The first build was a chevron in
            // the only gap a centred caption left, which put it at the top of
            // the band a hundred pixels from its node; this hangs in the strip
            // between the caption's fixed lower edge and the disc's own rim,
            // where there is nothing else, and says what it means in letters.
            var nextMark = Ui.Panel("TrackNextMark",
                Place.At(0f, RewardTrackLayout.NextMarkCentreY),
                UiSize.Fixed(RewardTrackLayout.NextMarkWidth,
                             RewardTrackLayout.NextMarkHeight),
                Ui.Label("TrackNextWord", UiStrings.TrackNext,
                        new UiVec(RewardTrackLayout.NextMarkWidth,
                                  RewardTrackLayout.NextLabelHeight),
                        RewardTrackLayout.NextLabelFont, Caret,
                        Place.At(0f, RewardTrackLayout.NextLabelCentreY))
                    .Tracked(24f)
                    .AsDecor(),

                // Fading DOWNWARD, toward the node: a line that stops dead
                // above a disc reads as a join between two things, and a line
                // that dissolves into the gap reads as pointing across it.
                Ui.Sprite("TrackNextTick", "proc:track_drop",
                        new UiVec(RewardTrackLayout.NextTickWidth,
                                  RewardTrackLayout.NextTickHeight),
                        Place.At(0f, RewardTrackLayout.NextTickCentreY))
                    .Coloured(Caret)
                    .AsDecor());

            nextMark.AllowOverlap("this hangs deliberately over whichever column is next, which is the whole gesture - it is placed at runtime and sits at the content's centre only until the first paint, so what it overlaps at build time is an artefact of that");
            screen.NextMark = nextMark;
            railChildren.Add(nextMark);

            // Wider than the window it sits in -- that overflow IS the scroll,
            // and the viewport below clips it. Same construction as the map's
            // content rect, whose comment records the same fact.
            //
            // OFFSET BY RailOffsetY, which is the one place that correction is
            // applied. The band does not straddle the panel's vertical centre
            // evenly (112px above the line, 100 below, because the caption
            // stack is taller than the level number and the caret needs the
            // difference), but the rail itself has to land ON that centre. So
            // the whole content rect drops six pixels and every coordinate
            // inside it is stated relative to the rail, where it belongs.
            var content = Ui.Panel("TrackContent",
                    Place.At(0f, RewardTrackLayout.RailOffsetY, new UiVec(0f, 0.5f)),
                    UiSize.Fixed(RewardTrackLayout.ContentWidth,
                                 RewardTrackLayout.ScrollContentHeight),
                    railChildren)
                .AllowOverflow("the content rect is deliberately wider than the window it sits in - that overflow IS the scroll, and TrackViewport clips it - and it is twelve pixels taller because everything inside is measured from the rail at its centre while the band around it reaches further above that line than below");
            screen.Content = content;

            // The window. A coordinate frame and a mask, never a surface.
            //
            // PaneContentWidth, NOT PanelWidth (balance-bot, 2026-09-02) --
            // this is a child of the container's content panel now, 1488
            // wide rather than the declared 1600 frame; the controller
            // reads viewport.rect.width at runtime for every scroll/ribbon
            // computation (RewardTrackController.Input.cs, .cs), so the
            // narrower rect also carries through to those correctly rather
            // than needing a second update.
            var viewport = Ui.Panel("TrackViewport",
                    Place.At(0f, RewardTrackLayout.BandCentreY),
                    UiSize.Fixed(RewardTrackLayout.PaneContentWidth, RewardTrackLayout.BandHeight),
                    content)
                .Clipping();
            screen.Viewport = viewport;

            yield return viewport;
        }

        // ONE BURST, as four things landing together.
        //
        // A flash with no shape of its own, a shockwave leaving the node, rays
        // fanning out behind it, and six sparks thrown clear. Everything here
        // is built TRANSPARENT and inactive: the rig is inert until a claim
        // fires it, and a build-time screenshot must not be a picture of an
        // explosion nobody set off.
        private UiNode BuildBurstRig(int rig)
        {
            float size = RewardTrackLayout.BurstRigSize;
            var parts = new List<UiNode>();

            // The rays first, so they sit BEHIND the flash. A starburst is the
            // slowest and largest of the four and reads as what the light is
            // coming from; drawn over the core it reads as a cog.
            var rays = Ui.Sprite($"TrackBurstRays{rig}", "proc:rarity_burst",
                    new UiVec(RewardTrackLayout.BurstRaysSize, RewardTrackLayout.BurstRaysSize),
                    Place.At(0f, 0f))
                .Coloured(Burst)
                .AsDecor();
            BurstRays.Add(rays);
            parts.Add(rays);

            var core = Ui.Sprite($"TrackBurstCore{rig}", "proc:radial_glow",
                    new UiVec(RewardTrackLayout.BurstCoreSize, RewardTrackLayout.BurstCoreSize),
                    Place.At(0f, 0f))
                .Coloured(Burst)
                .AsDecor();
            BurstCores.Add(core);
            parts.Add(core);

            var ring = Ui.Sprite($"TrackBurstRing{rig}", "proc:ring_hairline",
                    new UiVec(RewardTrackLayout.BurstRingSize, RewardTrackLayout.BurstRingSize),
                    Place.At(0f, 0f))
                .Coloured(Burst)
                .AsDecor();
            BurstRings.Add(ring);
            parts.Add(ring);

            // Sparks last, over everything: they are the part that carries the
            // gesture outward, and debris behind the flash it came from is
            // debris nobody sees.
            for (int spark = 0; spark < RewardTrackLayout.BurstSparkCount; spark++)
            {
                var mote = Ui.Sprite($"TrackBurstSpark{rig}_{spark}", "proc:solid_circle",
                        new UiVec(RewardTrackLayout.BurstSparkSize,
                                  RewardTrackLayout.BurstSparkSize),
                        Place.At(0f, 0f))
                    .Coloured(Burst)
                    .AsDecor();
                BurstSparks.Add(mote);
                parts.Add(mote);
            }

            var root = Ui.Panel($"TrackBurst{rig}", Place.At(0f, 0f),
                UiSize.Fixed(size, size), parts);

            root.AsDecor()
                .AllowOverflow("everything in this rig is meant to leave it - the shockwave scales to 3.4x and the sparks travel 46px out - and the rig is sized to the ring's own reach rather than to the furthest thing in it, because a rect big enough for every part at its largest would be most of the band")
                .Inactive();

            BurstRoots.Add(root);
            return root;
        }

        private IEnumerable<UiNode> BuildNode(int level)
        {
            float x = RewardTrackLayout.NodeOffsetX(level);
            bool milestone = RewardTrackLayout.IsMilestone(level);
            float diameter = RewardTrackLayout.DiameterOf(level);

            // A MILESTONE'S TWO AMBIENT CUES, behind everything else on the
            // node: an aura that breathes and a broken ring that turns.
            //
            // Emitted here rather than in a pass of their own so that a
            // milestone is one contiguous run of nodes in the tree, and so the
            // z-order is stated by the order it is written in -- aura, ring,
            // pulse, plate, disc, from the back forward.
            if (milestone)
            {
                var aura = Ui.Sprite($"TrackAura{level}", "proc:radial_glow",
                        new UiVec(RewardTrackLayout.MilestoneAuraSize,
                                  RewardTrackLayout.MilestoneAuraSize),
                        Place.At(x, 0f))
                    .Coloured(MilestoneAura)
                    .AsDecor();
                MilestoneAuras.Add(aura);
                yield return aura;
            }

            // THE PULSE, behind everything, on any waiting node. A 1px ring
            // that grows and fades: the only thing on this screen that asks to
            // be clicked, and the reason it is per-node rather than one moved
            // around is that a character levelled by migration arrives with
            // dozens of levels waiting at once. Handoff section 4 calls that
            // the expected path, not an edge case.
            var pulse = Ui.Sprite($"TrackPulse{level}", "proc:ring_hairline",
                    new UiVec(diameter, diameter), Place.At(x, 0f))
                .Coloured(Pulse)
                .AllowOverflow("the pulse ring's whole gesture is growing past the disc it starts on - it scales to 2.1x and fades out, and a ring clipped at the rim would read as a flicker")
                .AsDecor()
                .Inactive();
            Pulses.Add(pulse);
            yield return pulse;

            // ONE RING NODE PER LEVEL, doing two jobs the state decides
            // between. On a milestone it is the gold hairline PLATE at inset
            // -7, which is what tells twelve landmarks from eighty-seven filler
            // nodes now that size alone has been judged insufficient. On filler
            // it is the 1px rim an unreached disc carries, at the disc's own
            // diameter, and it is transparent once that level is reached.
            //
            // Two nodes would be the obvious build and it would be one of them
            // switched off on every node forever.
            // A MILESTONE'S IS THE DASHED SPRITE AND TURNS; a filler node's is
            // the plain hairline and does not. Same node, same radius rule, two
            // shapes -- see MilestoneAuraSize's neighbour in the layout for why
            // the turning ring is this one rather than a second circle outside
            // it.
            var ring = Ui.Sprite($"TrackRing{level}",
                    milestone ? "proc:milestone_ring" : "proc:ring_hairline",
                    milestone
                        ? new UiVec(RewardTrackLayout.PlateRingDiameter(level),
                                    RewardTrackLayout.PlateRingDiameter(level))
                        : new UiVec(diameter, diameter),
                    Place.At(x, 0f))
                .Coloured(milestone ? Gold : RimToCome)
                .AsDecor();
            Rings.Add(ring);
            if (milestone) MilestoneRings.Add(ring);
            yield return ring;

            // A DISC, not a square, and a BUTTON, not decoration.
            //
            // Clicking a node is the screen's one verb now: a waiting node
            // collects, and any other glides the rail to centre it. Never a
            // no-op -- a hundred dots that do nothing when pressed teach the
            // player that none of them do.
            //
            // Hovers(1.16) rather than the default press animation, which is
            // handoff section 7's disc-hover exactly. It also means these click
            // silently; the controller plays the claim's own sound, which is
            // the one that carries meaning.
            //
            // proc:disc_metal, not proc:solid_circle: see that bake's header
            // for why a flat gold coin reads as tarnish.
            // NOT migrated to the semantic kit. There is no separate
            // "relic reward button" node to give Violet: every one of the
            // twelve reward kinds (stat/health/exp/favor/respec/rest/reroll/
            // offer/relic/chosen-relic/second-life/refresh) shares this same
            // TrackDot shape, distinguished only by the mat tint and icon a
            // CHILD carries (see MatTintFor/IconFor below) -- the disc itself
            // already wears bespoke art (proc:disc_metal via SpriteKey) that
            // a theme's plate would replace. Colouring only the relic-kind
            // dots would mean per-instance theming keyed off runtime reward
            // data, which is a different, bigger change than a plate swap.
            // Pooled with pinned Place/UiSize per level, measured by this
            // class's own audit and RewardTrackLayout's tests either way.
            var dot = Ui.Button($"TrackDot{level}", UiString.Runtime,
                    new UiVec(diameter, diameter), 1, Place.At(x, 0f))
                .Hovers(1.16f);
            dot.SpriteKey = "proc:disc_metal";
            Dots.Add(dot);

            // The mat, the mark and the seal are CHILDREN of the disc rather
            // than its siblings, so the hover scales the whole node instead of
            // sliding a disc out from under its own icon. All decoration, so
            // none of them takes the click meant for the disc.

            // THE ART SLOT'S MAT. Handoff section 8 turns every disc into a
            // slot for painted art; until that art lands the slot draws its
            // ghost glyph over a mat tinted by reward kind -- at 2E alpha and
            // only on an unreached node, because a tint over gold is tarnish.
            //
            // UNTINTED at build time. Which reward kind (and so which hue) a
            // level holds is per-character now (docs/PLAN_REWARD_TRACKS.md
            // §1, §8) -- this tree is shared by every save and every selected
            // character, so there is no kind to tint against yet. The
            // controller writes the real tint every Refresh
            // (RewardTrackController.PaintNode); what is baked here is only
            // the fallback an unmapped kind would draw, the same neutral
            // MatTintFor(TrackReward) already falls back to for a reward it
            // does not recognise.
            var mat = Ui.Sprite($"TrackMat{level}", "proc:solid_circle",
                    new UiVec(diameter, diameter), Place.At(0f, 0f))
                .Coloured(RewardTrackLayout.MatTintFor(TrackReward.None))
                .AsDecor();
            Mats.Add(mat);
            dot.Children.Add(mat);

            // The reward's mark, inside the disc. NEUTRAL at build time, for
            // the same reason the mat above is untinted: which reward kind
            // sits at this level is a per-character, per-save question since
            // P3/P4, so the tree that is shared by every character cannot
            // pick a mark for it. RewardTrackController.PaintNode writes the
            // real one every Refresh, from the selected character's own
            // track (a per-kind sprite array, ScreenRegistry.markByReward) --
            // this ring is what an unrecognised or not-yet-resolved kind
            // would show either way, so it is not a placeholder so much as
            // the mark's own fallback baked in up front.
            var mark = Ui.Sprite($"TrackIcon{level}", "proc:ring_outline",
                    new UiVec(RewardTrackLayout.IconSizeOf(level),
                              RewardTrackLayout.IconSizeOf(level)),
                    Place.At(0f, 0f))
                .AsDecor();
            Icons.Add(mark);
            dot.Children.Add(mark);

            // THE SEAL PIP, lower-right, straddling the rim. Hidden until the
            // controller says this level has been collected -- the one thing on
            // a node that is about the PLAYER rather than about the track.
            //
            // A second channel for a state the colour already carries, and
            // deliberately: gold-against-violet is a hue difference, and a mark
            // that is present or absent reads for someone who cannot see it.
            float pip = RewardTrackLayout.SealPipOffset(level);
            const float PipSize = RewardTrackLayout.SealPipSize;

            // TWO SPRITES IN A GROUP the controller switches as one: a dark
            // disc, and the gold ring and check drawn on it. One node cannot
            // say both, because Image.color tints a whole sprite and these are
            // two colours -- and the version that avoided the second node by
            // knocking the check out of the first is what made the pip
            // illegible at nine pixels.
            var seal = Ui.Panel($"TrackSeal{level}",
                Place.At(pip, -pip), UiSize.Fixed(PipSize, PipSize),
                Ui.Sprite($"TrackSealGround{level}", "proc:solid_circle",
                        new UiVec(PipSize, PipSize), Place.At(0f, 0f))
                    .Coloured(SealInk)
                    .AsDecor(),
                Ui.Sprite($"TrackSealMark{level}", "proc:seal_mark",
                        new UiVec(PipSize, PipSize), Place.At(0f, 0f))
                    .Coloured(SealMark)
                    .AsDecor());

            seal.AsDecor()
                .AllowOverflow("the pip is meant to straddle the disc's rim rather than sit inside it - cutting the edge is what makes it read as applied to the node instead of as part of the reward's own mark")
                .Inactive();

            Seals.Add(seal);
            dot.Children.Add(seal);

            yield return dot;

            // The reward, above. Two lines of room -- the longest thing the
            // track says is "YOUR SECOND LIFE RETURNS AT EVERY BOSS", which is
            // what set NodePitch in the first place.
            var caption = Ui.Label($"TrackCaption{level}", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CaptionWidth, RewardTrackLayout.CaptionHeight),
                    milestone ? RewardTrackLayout.MilestoneCaptionFont : RewardTrackLayout.CaptionFont,
                    TextToCome,
                    Place.At(x, RewardTrackLayout.CaptionY))
                .TextAligned(UiTextAlign.Bottom)
                .AsDecor();
            Captions.Add(caption);
            yield return caption;

            // The level, below, and set as a FIGURE rather than as a label --
            // 15 filler and 22 milestone, up from 12 and 16. A rail whose
            // landmarks are numbers has to be countable from across the panel.
            var number = Ui.Label($"TrackLevel{level}", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CaptionWidth, RewardTrackLayout.LevelNumberHeight),
                    milestone
                        ? RewardTrackLayout.MilestoneLevelNumberFont
                        : RewardTrackLayout.LevelNumberFont,
                    TextToCome,
                    Place.At(x, RewardTrackLayout.LevelNumberY))
                .AsDecor();
            LevelNumbers.Add(number);
            yield return number;
        }

        // ---- the focus card -------------------------------------------------

        private static UiNode BuildCard(RewardTrackScreen screen)
        {
            // WHAT THE RAIL IS POINTING AT, in type large enough to read from
            // where the player actually sits.
            //
            // It rests on the NEXT reward whenever nothing is hovered, which is
            // the second half of the answer to "nothing marks what is next":
            // the caret says where on the rail, and this says what and whether
            // it is yours yet.
            var parts = new List<UiNode>();

            // A PLATE, not a tint. The card is a lit object on a dark ground
            // and needs its own ground to be one: a gradient lighter at the
            // head than the foot, a gold hairline round it, and only then the
            // things it says.
            parts.Add(Ui.Sprite("TrackCardGround", "proc:card_ground",
                    new UiVec(RewardTrackLayout.CardWidth, RewardTrackLayout.CardHeight),
                    Place.At(0f, 0f))
                .Coloured(CardGround)
                .AsDecor());

            parts.AddRange(Ui.Rim("TrackCard",
                new UiVec(RewardTrackLayout.CardWidth, RewardTrackLayout.CardHeight), CardEdge));

            parts.AddRange(BuildCardPlate(screen));

            // ---- the header row: kicker, rule, LVL, figure ------------------

            var kicker = Ui.Label("TrackCardKicker", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardKickerWidth, RewardTrackLayout.CardKickerHeight),
                    RewardTrackLayout.CardKickerFont, TextQuiet,
                    Place.At(RewardTrackLayout.CardKickerCentreX, RewardTrackLayout.CardHeaderY))
                .Tracked(26f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.CardKicker = kicker;
            parts.Add(kicker);

            // The rule between the kicker and the level. It is what makes the
            // two ends of this line one row rather than two labels that happen
            // to share a y.
            parts.Add(Ui.Solid("TrackCardRule", CardEdge,
                    new UiVec(RewardTrackLayout.CardRuleWidth, 1f),
                    Place.At(RewardTrackLayout.CardRuleCentreX, RewardTrackLayout.CardHeaderY))
                .AsDecor());

            parts.Add(Ui.Label("TrackCardLevelWord", UiStrings.TrackCardLvl,
                    new UiVec(RewardTrackLayout.CardLevelWordWidth,
                              RewardTrackLayout.CardLevelWordHeight),
                    RewardTrackLayout.CardLevelWordFont, TextQuiet,
                    Place.At(RewardTrackLayout.CardLevelWordCentreX,
                             RewardTrackLayout.CardHeaderY))
                .Tracked(24f)
                .TextAligned(UiTextAlign.Right)
                .AsDecor());

            var level = Ui.Label("TrackCardLevel", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardLevelWidth, RewardTrackLayout.CardLevelHeight),
                    RewardTrackLayout.CardLevelFont, TextCollected,
                    Place.At(RewardTrackLayout.CardLevelCentreX, RewardTrackLayout.CardHeaderY))
                .Tracked(4f)
                .TextAligned(UiTextAlign.Right)
                .AsDecor();
            screen.CardLevel = level;
            parts.Add(level);

            // ---- the reward's name -----------------------------------------

            var caption = Ui.Label("TrackCardCaption", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardTextWidth, RewardTrackLayout.CardCaptionHeight),
                    RewardTrackLayout.CardCaptionFont, Gold,
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardCaptionY))
                .Tracked(5f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.CardCaption = caption;
            parts.Add(caption);

            // ---- the footer: a divider, a dot, and the state ----------------

            parts.Add(Ui.Solid("TrackCardDivider", CardDivider,
                    new UiVec(RewardTrackLayout.CardTextWidth, 1f),
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardDividerY))
                .AsDecor());

            // THE STATE, SAID TWICE: as a coloured dot and as words. The dot is
            // the same argument the seal pip makes on a node -- gold against
            // violet is a hue difference, and a screen whose four states differ
            // only by hue has one channel for the thing it is about.
            var dot = Ui.Sprite("TrackCardStateDot", "proc:solid_circle",
                    new UiVec(RewardTrackLayout.CardStateDotSize,
                              RewardTrackLayout.CardStateDotSize),
                    Place.At(RewardTrackLayout.CardStateDotCentreX,
                             RewardTrackLayout.CardStateY))
                .Coloured(Gold)
                .AsDecor();
            screen.CardStateDot = dot;
            parts.Add(dot);

            var state = Ui.Label("TrackCardState", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardStateTextWidth,
                              RewardTrackLayout.CardStateHeight),
                    RewardTrackLayout.CardStateFont, TextQuiet,
                    Place.At(RewardTrackLayout.CardStateCentreX, RewardTrackLayout.CardStateY))
                .Tracked(16f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.CardState = state;
            parts.Add(state);

            var card = Ui.Panel("TrackCard", Place.At(0f, RewardTrackLayout.CardCentreY),
                UiSize.Fixed(RewardTrackLayout.CardWidth, RewardTrackLayout.CardHeight),
                parts);
            screen.Card = card;

            return card;
        }

        // The art slot, as a framed well rather than a coloured square.
        //
        // ITS RIM DOES NOT CHANGE WITH STATE, which the design's does -- gold
        // at 99 on a reward you can reach and 52 on one you cannot. Four rim
        // edges are four nodes to find and repaint for a difference of 18% on a
        // one-pixel line, and the mat inside it already carries the state in a
        // colour that fills 86x86. If that turns out to be missed, the four
        // edges are there under one stem name.
        private static IEnumerable<UiNode> BuildCardPlate(RewardTrackScreen screen)
        {
            float x = RewardTrackLayout.CardPlateCentreX;

            yield return Ui.Solid("TrackCardPlate", PlateGround,
                    new UiVec(RewardTrackLayout.CardPlateSize, RewardTrackLayout.CardPlateSize),
                    Place.At(x, 0f))
                .AsDecor();

            foreach (var edge in Ui.Rim("TrackCardPlate",
                         new UiVec(RewardTrackLayout.CardPlateSize,
                                   RewardTrackLayout.CardPlateSize),
                         PlateEdgeLit, new UiVec(x, 0f)))
            {
                yield return edge;
            }

            // The mat: the art slot itself, 86 of the plate's 104, carrying the
            // reward kind's hue until there is art to carry it instead.
            //
            // A GRADIENT, NOT A FILL, and the first capture of this plate is
            // the argument: an 86px square of flat 18% lilac inside a gold
            // frame reads as a grey chip somebody forgot to draw on. The design
            // lights the mat from a point at 34%/26% and lets it fall away to
            // nothing, which is a well with something in it rather than a tile.
            var mat = Ui.Sprite("TrackCardMat", "proc:radial_glow",
                    new UiVec(RewardTrackLayout.CardArtSize, RewardTrackLayout.CardArtSize),
                    Place.At(x, 0f))
                .Coloured(DeepViolet)
                .AsDecor();
            screen.CardMat = mat;
            yield return mat;

            foreach (var edge in Ui.Rim("TrackCardMat",
                         new UiVec(RewardTrackLayout.CardArtSize, RewardTrackLayout.CardArtSize),
                         PlateMatEdge, new UiVec(x, 0f)))
            {
                yield return edge;
            }

            // Four corner ticks inside the plate's own rim, at the scale of one
            // object: the same gesture the panel makes at the scale of the
            // screen, and the thing that reads as "art goes here".
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? 1f : -1f;
                string corner = $"{((i & 2) == 0 ? "Top" : "Bottom")}{((i & 1) == 0 ? "Left" : "Right")}";

                float half = RewardTrackLayout.CardPlateSize * 0.5f
                             - RewardTrackLayout.CardTickInset;
                float arm = RewardTrackLayout.CardTickArm;

                yield return Ui.Solid($"TrackCardTick{corner}H", PlateTick,
                        new UiVec(arm, 1f),
                        Place.At(x + (half - arm * 0.5f) * sx, (half - 0.5f) * sy))
                    .AsDecor();

                yield return Ui.Solid($"TrackCardTick{corner}V", PlateTick,
                        new UiVec(1f, arm),
                        Place.At(x + (half - 0.5f) * sx, (half - arm * 0.5f) * sy))
                    .AsDecor();
            }

            // BUILT AT THE SLOT'S OWN 86, which is the size the painted
            // medallion fills. The controller shrinks it to 46 for the stroke
            // glyph it falls back to, and never the other way about: UiAudit
            // measures the rect the tree declares, so a node that grows at
            // runtime is a node it has never seen at the size it is drawn.
            var art = Ui.Sprite("TrackCardArt", "proc:ring_outline",
                    new UiVec(RewardTrackLayout.CardArtSize, RewardTrackLayout.CardArtSize),
                    Place.At(x, 0f))
                .AsDecor();
            screen.CardArt = art;
            yield return art;
        }

        // ---- the ascent ribbon ----------------------------------------------

        private static IEnumerable<UiNode> BuildRibbon(RewardTrackScreen screen)
        {
            // THE LABEL ROW, as a head and an instruction with a rule between
            // them. One centred caption saying both was the first build, and a
            // caption in the middle of an empty row reads as a note under a
            // picture rather than as the top of a scale.
            yield return Ui.Label("TrackRibbonTitle", UiStrings.TrackRibbonTitle,
                    new UiVec(RewardTrackLayout.RibbonTitleWidth,
                              RewardTrackLayout.RibbonLabelHeight),
                    RewardTrackLayout.RibbonLabelFont, TextQuiet,
                    Place.At(RewardTrackLayout.RibbonTitleCentreX,
                             RewardTrackLayout.RibbonLabelCentreY))
                .Tracked(26f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();

            yield return Ui.Solid("TrackRibbonLabelRule", CardEdge,
                    new UiVec(RewardTrackLayout.RibbonLabelRuleWidth, 1f),
                    Place.At(RewardTrackLayout.RibbonLabelRuleCentreX,
                             RewardTrackLayout.RibbonLabelCentreY))
                .AsDecor();

            yield return Ui.Label("TrackRibbonHint", UiStrings.TrackRibbonHint,
                    new UiVec(RewardTrackLayout.RibbonHintWidth,
                              RewardTrackLayout.RibbonLabelHeight),
                    RewardTrackLayout.RibbonLabelFont, TextQuiet,
                    Place.At(RewardTrackLayout.RibbonHintCentreX,
                             RewardTrackLayout.RibbonLabelCentreY))
                .Tracked(20f)
                .TextAligned(UiTextAlign.Right)
                .AsDecor();

            var parts = new List<UiNode>();

            parts.Add(Ui.Solid("TrackRibbonBase", Rail,
                    new UiVec(RewardTrackLayout.RibbonWidth, RewardTrackLayout.RibbonBaseHeight),
                    Place.At(0f, 0f))
                .AsDecor());

            // THE SAME LEFT-PIVOTED WIDTH the rail uses, and for the same
            // reason: this parent is 1440px wide and an anchor-driven fill
            // would be measured against it. The dossier's XP bar records what
            // that mistake looks like when it ships.
            var fill = Ui.Solid("TrackRibbonFill", RailDone,
                    new UiVec(RewardTrackLayout.RibbonWidth, RewardTrackLayout.RibbonBaseHeight),
                    Place.At(-RewardTrackLayout.RibbonWidth * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .AsDecor();
            screen.RibbonFill = fill;
            parts.Add(fill);

            // EIGHTY-SEVEN TICKS, ALL OF THEM ALWAYS ON.
            //
            // A tick used to stand only where a level was reached and
            // uncollected, which meant a player who collects as they go saw a
            // bare line with twelve dots on it: ninety-nine levels drawn as
            // twelve marks, saying nothing about how far apart they are or how
            // many there are between two of them.
            //
            // Every level standing as a hairline is what makes this a measure
            // of the ascent rather than a list of its landmarks -- and the
            // waiting weight, twice as wide and twice as tall in gold, then
            // reads AGAINST that comb rather than being the only thing on it.
            // The migrated character arriving with dozens of levels owed still
            // sees exactly how much and where, which is what the first build
            // was for.
            //
            // Milestones do not get one; their dot IS the mark at that level,
            // and a tick under it would be a second smaller landmark in the
            // same place.
            //
            // BUILT AT THE WAITING SIZE and shrunk by the controller, never the
            // other way about: UiAudit measures the rect the tree declares, so
            // a node that grows at runtime is a node it has never seen at the
            // size it is drawn.
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrackLayout.IsMilestone(level)) continue;

                var tick = Ui.Solid($"TrackRibbonTick{level}", RibbonTickToCome,
                        new UiVec(RewardTrackLayout.RibbonWaitingTickWidth,
                                  RewardTrackLayout.RibbonWaitingTickHeight),
                        Place.At(RewardTrackLayout.RibbonOffsetX(level), 0f))
                    .AsDecor();
                screen.RibbonTicks.Add(tick);
                parts.Add(tick);
            }

            // The twelve landmarks, ON the line and numbered above it. Emitted
            // after the ticks so a milestone always draws over its neighbours.
            //
            // The numbers are UiString.Runtime and filled by the controller,
            // even though every one of them is known here and will never
            // change. FromContent(level.ToString()) would be the shorter build
            // and it would put twelve pieces of text on screen that the string
            // manifest has never seen -- which is exactly the hole
            // EveryDeclaredLabelDrawsAManifestStringOrNamedContent watches, and
            // it cannot tell a numeral from smuggled prose.
            foreach (int level in RewardTrackLayout.MilestoneLevels())
            {
                float x = RewardTrackLayout.RibbonOffsetX(level);

                // A FILLED DOT ONCE REACHED AND A HOLLOW ONE UNTIL THEN, which
                // is two nodes: a disc the controller colours, and a hairline
                // ring that is always gold. One node cannot be both an empty
                // circle and a full one.
                var dot = Ui.Sprite($"TrackRibbonDot{level}", "proc:solid_circle",
                        new UiVec(RewardTrackLayout.RibbonDotSize, RewardTrackLayout.RibbonDotSize),
                        Place.At(x, 0f))
                    .Coloured(RibbonDotToCome)
                    .AsDecor();
                screen.RibbonDots.Add(dot);
                parts.Add(dot);

                parts.Add(Ui.Sprite($"TrackRibbonRing{level}", "proc:ring_hairline",
                        new UiVec(RewardTrackLayout.RibbonDotSize, RewardTrackLayout.RibbonDotSize),
                        Place.At(x, 0f))
                    .Coloured(Gold)
                    .AsDecor());

                var number = Ui.Label($"TrackRibbonNumber{level}", UiString.Runtime,
                        new UiVec(RewardTrackLayout.RibbonNumberWidth,
                                  RewardTrackLayout.RibbonNumberHeight),
                        RewardTrackLayout.RibbonNumberFont, TextQuiet,
                        Place.At(x, RewardTrackLayout.RibbonNumberY))
                    .AsDecor();
                screen.RibbonNumbers.Add(number);
                parts.Add(number);
            }

            // Where the player stands, on the same scale as everything else.
            var playhead = Ui.Solid("TrackRibbonPlayhead", Pale,
                    new UiVec(RewardTrackLayout.RibbonPlayheadWidth,
                              RewardTrackLayout.RibbonPlayheadHeight),
                    Place.At(0f, 0f))
                .AsDecor();
            screen.RibbonPlayhead = playhead;
            parts.Add(playhead);

            // THE VISIBLE WINDOW, as a box you can drag. Eight and a half nodes
            // of ninety-nine works out at 121px of the ribbon's 1440, which is
            // the clearest statement this screen makes about how much of the
            // track the rail can actually show.
            // RewardTrackLayout.PaneContentWidth, NOT PanelWidth (balance-
            // bot, 2026-09-02) -- this build-time box is the tree's own
            // guess at what viewport.rect.width will be; the controller
            // recomputes it from the real rect at runtime, but the two
            // should agree at build time rather than start 112px apart.
            var window = Ui.Panel("TrackRibbonWindow",
                Place.At(0f, 0f),
                UiSize.Fixed(RewardTrackLayout.RibbonWindowWidth(RewardTrackLayout.PaneContentWidth),
                             RewardTrackLayout.RibbonWindowHeight),
                Ui.Rim("TrackRibbonWindowEdge",
                    new UiVec(RewardTrackLayout.RibbonWindowWidth(RewardTrackLayout.PaneContentWidth),
                              RewardTrackLayout.RibbonWindowHeight),
                    Lilac));
            screen.RibbonWindow = window;
            parts.Add(window);

            // THE GRAB SURFACE, last so it is on top of everything it seeks
            // over, and chromeless so it draws nothing while doing it. A
            // Button's Image raycasts against its RECT rather than its alpha,
            // so a fully transparent one takes the drag exactly as a painted
            // plate would -- which is the mechanism UiEmitter's own header
            // records.
            var grab = Ui.Button("TrackRibbonGrab", UiString.Runtime,
                    new UiVec(RewardTrackLayout.RibbonWidth, RewardTrackLayout.RibbonHeight),
                    1, Place.At(0f, 0f))
                .NoChrome()
                .Quiet()
                .AllowOverlap("this covers the whole ribbon on purpose - it is the drag surface for the window box, and everything under it is decoration that draws the ribbon rather than anything that could take a click of its own");
            screen.RibbonGrab = grab;
            parts.Add(grab);

            var ribbon = Ui.Panel("TrackRibbon", Place.At(0f, RewardTrackLayout.RibbonCentreY),
                UiSize.Fixed(RewardTrackLayout.RibbonWidth, RewardTrackLayout.RibbonHeight),
                parts);
            screen.Ribbon = ribbon;

            yield return ribbon;
        }

        // ---- the summary row ------------------------------------------------

        private static IEnumerable<UiNode> BuildSummaryRow(RewardTrackScreen screen)
        {
            // COLLECT EVERYTHING, and it appears only when there is something
            // to collect. Equivalent to clicking the player's own node, which
            // is what makes it a shortcut rather than a second mechanism:
            // claiming is sequential either way, so there is never a collected
            // hole below an uncollected level and claimedTrackLevel stays a
            // single integer.
            //
            // GOLD THEMED PLATE now (owner's HQ-kit instruction, 2026-09-07),
            // superseding the "would fight the design" call this comment used
            // to make: the hairline-and-wash treatment was a deliberate
            // departure from the game's standard filled button so this row
            // would not sit as the brightest thing on a screen whose palette
            // otherwise uses gold only as stroke and glow. The owner's later
            // instruction is "all buttons are replaced with HQ ones" without
            // that exception, so it wins -- ThemedPlate rather than Themed()
            // because this button already declares its own caption/pip
            // children (see LayerCaptionWithVisuals below) the same way
            // FightScreen's verb rows and ResetConfirmYesButton do.
            // TrackCloseButton below is the same case.
            var collectSize = new UiVec(RewardTrackLayout.CollectWidth, RewardTrackLayout.CollectHeight);
            var collect = Ui.Button("TrackCollectButton", UiString.Runtime,
                    collectSize, 15, Place.At(RewardTrackLayout.CollectCentreX,
                                 RewardTrackLayout.SummaryCentreY))
                .ThemedPlate(ButtonTheme.Gold)
                .Inactive();

            // The pip on the button, which is the same 7px dot the design
            // pulses there -- the collect button appearing IS the notification
            // that the track owes something, and a lit dot is what makes it
            // read as an alert rather than as a control that was always there.
            var pip = Ui.Sprite("TrackCollectPip", "proc:solid_circle",
                    new UiVec(7f, 7f),
                    Place.At(-collectSize.X * 0.5f + 22f, 0f))
                .Coloured(Gold)
                .AsDecor();
            screen.CollectPip = pip;
            collect.Children.Add(pip);

            // "Caption", NOT "Label" -- UiEmitter names every button's own
            // generated text "<button>Label", and a tree child by that name
            // ends up as the second GameObject of that name under one parent.
            // The wiring paints ours and every lookup by name takes the
            // emitter's empty one. See UiAudit A4b, and TalentScreen's
            // InvestButtonCaption, which is where it was found. ThemedPlate
            // generates no <name>Label at all (CaptionPreserving), so this
            // stays the button's only caption -- ScreenRegistry.WireRewardTrack
            // still binds collectLabel off this exact NodeRef.
            var caption = Ui.Label("TrackCollectCaption", UiString.Runtime,
                    new UiVec(collectSize.X - 52f, collectSize.Y - 8f),
                    11, Gold, Place.At(9f, 0f))
                .Tracked(22f);
            collect.Children.Add(caption);
            collect.LayerCaptionWithVisuals(pip, caption);

            screen.CollectButton = collect;
            screen.CollectCaption = caption;
            yield return collect;

            // ---- WHERE YOU ARE, as five pieces on one line ------------------
            //
            // The whole assembly is centred on the panel and each piece is
            // placed by walking from its left edge, so the row cannot drift as
            // the level ticks from 9 to 10 to 100.
            //
            // It still has to clear both buttons -- see
            // RewardTrackLayout.SummaryWidth, which computes that clearance
            // from the buttons rather than restating it, because the first
            // version of this row put a full-width label under CLOSE and
            // UiAudit refused the build.
            yield return Ui.Label("TrackSummaryWord", UiStrings.TrackLevelWord,
                    new UiVec(RewardTrackLayout.LevelWordWidth, 20f),
                    RewardTrackLayout.LevelWordFont, TextFigureLabel,
                    Place.At(RewardTrackLayout.SummaryPartX(0),
                             RewardTrackLayout.SummaryCentreY))
                .Tracked(30f)
                .TextAligned(UiTextAlign.Right)
                .AsDecor();

            // THE FIGURE, at 34px against everything else's 12 or 13. The one
            // number on this screen that is about the player rather than about
            // the track, and it was set at the same size as the word in front
            // of it.
            var figure = Ui.Label("TrackSummaryLevel", UiString.Runtime,
                    new UiVec(RewardTrackLayout.LevelFigureWidth,
                              RewardTrackLayout.SummaryRowHeight),
                    RewardTrackLayout.LevelFigureFont, Gold,
                    Place.At(RewardTrackLayout.SummaryPartX(1),
                             RewardTrackLayout.SummaryCentreY))
                .Tracked(4f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.SummaryLevel = figure;
            yield return figure;

            yield return Ui.Solid("TrackSummaryRule", Rule,
                    new UiVec(RewardTrackLayout.SummaryRuleWidth, 1f),
                    Place.At(RewardTrackLayout.SummaryPartX(2),
                             RewardTrackLayout.SummaryCentreY))
                .AsDecor();

            var nextAt = Ui.Label("TrackSummaryNextAt", UiString.Runtime,
                    new UiVec(RewardTrackLayout.NextAtWidth, 20f),
                    RewardTrackLayout.NextAtFont, TextNextAt,
                    Place.At(RewardTrackLayout.SummaryPartX(3),
                             RewardTrackLayout.SummaryCentreY))
                .Tracked(20f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.SummaryNextAt = nextAt;
            yield return nextAt;

            // AND WHAT IT IS. This was cut from the summary once, at eighty
            // characters, for colliding with CLOSE; it comes back with a box of
            // its own that the text-fit audit measures against.
            var reward = Ui.Label("TrackSummaryReward", UiString.Runtime,
                    new UiVec(RewardTrackLayout.NextRewardWidth, 20f),
                    RewardTrackLayout.NextAtFont, Gold,
                    Place.At(RewardTrackLayout.SummaryPartX(4),
                             RewardTrackLayout.SummaryCentreY))
                .Tracked(20f)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            screen.SummaryReward = reward;
            yield return reward;

            // SILVER THEMED PLATE now (owner's HQ-kit instruction,
            // 2026-09-07), the same supersession TrackCollectButton's own
            // comment records.
            var closeSize = new UiVec(RewardTrackLayout.CloseWidth, RewardTrackLayout.CloseHeight);
            var close = Ui.Button("TrackCloseButton", UiString.Runtime,
                    closeSize, 15, Place.At(RewardTrackLayout.CloseCentreX,
                                 RewardTrackLayout.SummaryCentreY))
                .ThemedPlate(ButtonTheme.Silver);

            var closeCaption = Ui.Label("TrackCloseCaption", UiStrings.TrackClose,
                    new UiVec(closeSize.X - 20f, closeSize.Y - 8f),
                    12, Gold, Place.At(0f, 0f))
                .Tracked(24f);
            close.Children.Add(closeCaption);
            close.LayerCaptionWithVisuals(closeCaption);

            screen.CloseButton = close;
            yield return close;
        }
    }
}
