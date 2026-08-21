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
        public const string BandWash = "#2E224499";
        public const string BandEdge = "#C8B4DE2E";

        // The rail, in two weights.
        //
        // RailDone was measured at 6B against the flat #120A18 ground this
        // screen used to have. The rail band's wash is new and lifts what is
        // behind the line, and at 42% the lit half came out reading pale lilac
        // rather than gold -- so the one thing the rail says without words,
        // that this part is yours, stopped being said. Raised until it is gold
        // again on the ground it is actually drawn on.
        public const string Rail = "#C8B4DE29";
        public const string RailDone = "#F2DB9EBF";
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

        // The seal pip: the panel's own ground, so the check knocked out of it
        // shows the gold disc underneath at exactly the disc's own light.
        public const string SealInk = "#120A18";

        public const string Halo = "#F2DB9E00";
        public const string Pulse = "#F2DB9E00";
        public const string Caret = "#F2DB9E";
        public const string Burst = "#F2DB9E00";

        public UiNode Root;

        public NodeRef Viewport;
        public NodeRef Content;
        public NodeRef RailFill;
        public NodeRef Shimmering;
        public NodeRef HereHalo;
        public NodeRef NextCaret;
        public NodeRef ClaimBurst;

        public NodeRef Summary;
        public NodeRef CollectButton;
        public NodeRef CollectCaption;
        public NodeRef CloseButton;

        public NodeRef Card;
        public NodeRef CardMat;
        public NodeRef CardArt;
        public NodeRef CardKicker;
        public NodeRef CardLevel;
        public NodeRef CardCaption;
        public NodeRef CardState;

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
        public List<NodeRef> RibbonTicks = new List<NodeRef>();
        public List<NodeRef> RibbonNumbers = new List<NodeRef>();

        public static RewardTrackScreen Build()
        {
            var screen = new RewardTrackScreen();

            // ITS OWN GROUND, and FULLY OPAQUE.
            //
            // It opens OVER the dossier, so anything less lets three columns of
            // numbers read straight through a hundred captions -- which is
            // exactly what #120A18FA did: the first capture of this screen has
            // Shawn's portrait and his ability scores legible behind the rail.
            // The dossier's own ground is FA because it opens over a painted
            // hub that is meant to stay faintly present; this opens over a
            // screen full of text, which is not.
            var ground = Ui.Solid("TrackGround", Ground,
                    new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                    Place.At(0f, 0f))
                .AsDecor();

            var children = new List<UiNode> { ground };
            children.AddRange(BuildRailBand(screen));
            children.Add(BuildCard(screen));
            children.AddRange(BuildRibbon(screen));
            children.AddRange(BuildSummaryRow(screen));

            screen.Root = Ui.Panel("RewardTrackPanel", Place.At(0f, 0f),
                    UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                    children)
                .Inactive();

            return screen;
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
            yield return Ui.Solid("TrackBandWash", BandWash,
                    new UiVec(SystemMenuLayout.PanelWidth, RewardTrackLayout.BandHeight),
                    Place.At(0f, RewardTrackLayout.BandCentreY))
                .AsDecor();

            yield return Ui.Solid("TrackBandTopEdge", BandEdge,
                    new UiVec(SystemMenuLayout.PanelWidth, 1f),
                    Place.At(0f, RewardTrackLayout.CentreY(RewardTrackLayout.BandTopFromTop)))
                .AsDecor();

            yield return Ui.Solid("TrackBandBottomEdge", BandEdge,
                    new UiVec(SystemMenuLayout.PanelWidth, 1f),
                    Place.At(0f, RewardTrackLayout.CentreY(RewardTrackLayout.BandHairlineFromTop)))
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

            var railFill = Ui.Solid("TrackRailFill", RailDone,
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailLitHeight),
                    Place.At(-RewardTrackLayout.ContentWidth * 0.5f, 0f, new UiVec(0f, 0.5f)))
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
            var halo = Ui.Sprite("TrackHereHalo", "proc:radial_glow",
                    new UiVec(RewardTrackLayout.HaloSize, RewardTrackLayout.HaloSize),
                    Place.At(0f, 0f))
                .Coloured(Halo)
                .AsDecor()
                .Inactive();
            screen.HereHalo = halo;
            railChildren.Add(halo);

            // The claim burst, likewise: one node, re-fired at each level in
            // turn 130ms apart, rather than a hundred sleeping particles.
            var burst = Ui.Sprite("TrackClaimBurst", "proc:rarity_burst",
                    new UiVec(RewardTrackLayout.HaloSize, RewardTrackLayout.HaloSize),
                    Place.At(0f, 0f))
                .Coloured(Burst)
                .AsDecor()
                .Inactive();
            screen.ClaimBurst = burst;
            railChildren.Add(burst);

            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                railChildren.AddRange(screen.BuildNode(level));
            }

            // NOTHING MARKED THE NEXT REWARD, which was one of the brief's six
            // open questions and the cheapest to answer. Drawn last so it sits
            // over the caption it hangs above.
            var caret = Ui.Sprite("TrackNextCaret", "proc:track_caret",
                    new UiVec(RewardTrackLayout.CaretWidth, RewardTrackLayout.CaretHeight),
                    Place.At(0f, RewardTrackLayout.CaretY))
                .Coloured(Caret)
                .AsDecor()
                .Inactive();
            screen.NextCaret = caret;
            railChildren.Add(caret);

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
            var viewport = Ui.Panel("TrackViewport",
                    Place.At(0f, RewardTrackLayout.BandCentreY),
                    UiSize.Fixed(SystemMenuLayout.PanelWidth, RewardTrackLayout.BandHeight),
                    content)
                .Clipping();
            screen.Viewport = viewport;

            yield return viewport;
        }

        private IEnumerable<UiNode> BuildNode(int level)
        {
            float x = RewardTrackLayout.NodeOffsetX(level);
            bool milestone = RewardTrackLayout.IsMilestone(level);
            float diameter = RewardTrackLayout.DiameterOf(level);

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
            var ring = Ui.Sprite($"TrackRing{level}", "proc:ring_hairline",
                    milestone
                        ? new UiVec(RewardTrackLayout.PlateRingDiameter(level),
                                    RewardTrackLayout.PlateRingDiameter(level))
                        : new UiVec(diameter, diameter),
                    Place.At(x, 0f))
                .Coloured(milestone ? Gold : RimToCome)
                .AsDecor();
            Rings.Add(ring);
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
            var mat = Ui.Sprite($"TrackMat{level}", "proc:solid_circle",
                    new UiVec(diameter, diameter), Place.At(0f, 0f))
                .Coloured(RewardTrackLayout.MatTintFor(level))
                .AsDecor();
            Mats.Add(mat);
            dot.Children.Add(mat);

            // The reward's mark, inside the disc. Keyed at BUILD time because
            // the track is static -- level 40 is an offer reroll in every save
            // there will ever be, so there is nothing here for a controller to
            // decide.
            string icon = RewardTrackLayout.IconFor(level) ?? "proc:ring_outline";
            var mark = Ui.Sprite($"TrackIcon{level}", icon,
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
            float pipSize = RewardTrackLayout.SealPipSize(level);
            var seal = Ui.Sprite($"TrackSeal{level}", "proc:seal_pip",
                    new UiVec(pipSize, pipSize),
                    Place.At(pip, -pip))
                .Coloured(SealInk)
                .AllowOverflow("the pip is meant to straddle the disc's rim rather than sit inside it - cutting the edge is what makes it read as applied to the node instead of as part of the reward's own mark")
                .AsDecor()
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
            var mat = Ui.Solid("TrackCardMat", DeepViolet,
                    new UiVec(RewardTrackLayout.CardPlateSize, RewardTrackLayout.CardPlateSize),
                    Place.At(RewardTrackLayout.CardPlateCentreX, 0f))
                .AsDecor();
            screen.CardMat = mat;

            // 86 inside a 104 mat: the 9px border handoff section 8 specifies,
            // and the reason the plate is not simply the art's own size.
            var art = Ui.Sprite("TrackCardArt", "proc:ring_outline",
                    new UiVec(RewardTrackLayout.CardArtSize, RewardTrackLayout.CardArtSize),
                    Place.At(RewardTrackLayout.CardPlateCentreX, 0f))
                .AsDecor();
            screen.CardArt = art;

            var kicker = Ui.Label("TrackCardKicker", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardTextWidth, RewardTrackLayout.CardKickerHeight),
                    RewardTrackLayout.CardKickerFont, TextQuiet,
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardKickerY))
                .Tracked(26f)
                .AsDecor();
            screen.CardKicker = kicker;

            var level = Ui.Label("TrackCardLevel", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardTextWidth, RewardTrackLayout.CardLevelHeight),
                    RewardTrackLayout.CardLevelFont, TextCollected,
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardLevelY))
                .Tracked(4f)
                .AsDecor();
            screen.CardLevel = level;

            var caption = Ui.Label("TrackCardCaption", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardTextWidth, RewardTrackLayout.CardCaptionHeight),
                    RewardTrackLayout.CardCaptionFont, Gold,
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardCaptionY))
                .Tracked(5f)
                .AsDecor();
            screen.CardCaption = caption;

            var state = Ui.Label("TrackCardState", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CardTextWidth, RewardTrackLayout.CardStateHeight),
                    RewardTrackLayout.CardStateFont, TextQuiet,
                    Place.At(RewardTrackLayout.CardTextCentreX, RewardTrackLayout.CardStateY))
                .Tracked(16f)
                .AsDecor();
            screen.CardState = state;

            var card = Ui.Panel("TrackCard", Place.At(0f, RewardTrackLayout.CardCentreY),
                UiSize.Fixed(RewardTrackLayout.CardWidth, RewardTrackLayout.CardHeight),
                mat, art, kicker, level, caption, state);
            screen.Card = card;

            return card;
        }

        // ---- the ascent ribbon ----------------------------------------------

        private static IEnumerable<UiNode> BuildRibbon(RewardTrackScreen screen)
        {
            yield return Ui.Label("TrackRibbonLabel", UiStrings.TrackRibbon,
                    new UiVec(RewardTrackLayout.RibbonWidth, RewardTrackLayout.RibbonLabelHeight),
                    RewardTrackLayout.RibbonLabelFont, TextQuiet,
                    Place.At(0f, RewardTrackLayout.RibbonLabelCentreY))
                .Tracked(20f)
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

            // NINETY-NINE TICKS, and almost all of them are off almost always.
            //
            // A tick stands only where a level is REACHED AND UNCOLLECTED, so
            // for a player collecting as they go the ribbon is a line, twelve
            // dots and a window. For one levelled by migration it is a comb of
            // gold showing exactly how much is owed and where -- which is the
            // case collect-all exists for, and the only way to see its size
            // without scrolling nineteen thousand pixels.
            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                var tick = Ui.Solid($"TrackRibbonTick{level}", Gold,
                        new UiVec(RewardTrackLayout.RibbonTickWidth,
                                  RewardTrackLayout.RibbonTickHeight),
                        Place.At(RewardTrackLayout.RibbonOffsetX(level),
                                 RewardTrackLayout.RibbonTickHeight * 0.5f))
                    .AsDecor()
                    .Inactive();
                screen.RibbonTicks.Add(tick);
                parts.Add(tick);
            }

            // The twelve landmarks, raised off the line and numbered. Emitted
            // after the ticks so a milestone that is also waiting still reads
            // as a milestone.
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

                parts.Add(Ui.Sprite($"TrackRibbonDot{level}", "proc:solid_circle",
                        new UiVec(RewardTrackLayout.RibbonDotSize, RewardTrackLayout.RibbonDotSize),
                        Place.At(x, RewardTrackLayout.RibbonDotY))
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
            var window = Ui.Panel("TrackRibbonWindow",
                Place.At(0f, 0f),
                UiSize.Fixed(RewardTrackLayout.RibbonWindowWidth(SystemMenuLayout.PanelWidth),
                             RewardTrackLayout.RibbonHeight),
                Ui.Rim("TrackRibbonWindowEdge",
                    new UiVec(RewardTrackLayout.RibbonWindowWidth(SystemMenuLayout.PanelWidth),
                              RewardTrackLayout.RibbonHeight),
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
            var collect = Ui.Button("TrackCollectButton", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CollectWidth, RewardTrackLayout.SummaryRowHeight),
                    15, Place.At(RewardTrackLayout.CollectCentreX,
                                 RewardTrackLayout.SummaryCentreY))
                .Inactive();

            // "Caption", NOT "Label" -- UiEmitter names every button's own
            // generated text "<button>Label", and a tree child by that name
            // ends up as the second GameObject of that name under one parent.
            // The wiring paints ours and every lookup by name takes the
            // emitter's empty one. See UiAudit A4b, and TalentScreen's
            // InvestButtonCaption, which is where it was found.
            var caption = Ui.Label("TrackCollectCaption", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CollectWidth - 20f,
                              RewardTrackLayout.SummaryRowHeight - 8f),
                    15, Gold, Place.At(0f, 0f))
                .Tracked(16f);
            collect.Children.Add(caption);

            screen.CollectButton = collect;
            screen.CollectCaption = caption;
            yield return collect;

            // WHERE YOU ARE, in words. Narrowed to twice the smaller of the two
            // gaps it has to clear -- see RewardTrackLayout.SummaryWidth, which
            // computes that from the buttons rather than restating it, because
            // the first version of this row put a full-width label under CLOSE
            // and UiAudit refused the build.
            var summary = Ui.Label("TrackSummary", UiString.Runtime,
                    new UiVec(RewardTrackLayout.SummaryWidth, 30f),
                    18, TextHere,
                    Place.At(0f, RewardTrackLayout.SummaryCentreY))
                .AsDecor();
            screen.Summary = summary;
            yield return summary;

            var close = Ui.Button("TrackCloseButton", UiStrings.TrackClose,
                new UiVec(RewardTrackLayout.CloseWidth, RewardTrackLayout.SummaryRowHeight), 15,
                Place.At(RewardTrackLayout.CloseCentreX, RewardTrackLayout.SummaryCentreY));
            screen.CloseButton = close;
            yield return close;
        }
    }
}
