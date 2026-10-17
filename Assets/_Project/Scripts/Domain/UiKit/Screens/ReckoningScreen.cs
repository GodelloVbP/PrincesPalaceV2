using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Reckoning: what a fight paid out.
    //
    // An OVERLAY on the fight, not a scene of its own. The stage is not torn
    // down behind it, which is what lets it expand INTO place from the fight
    // that produced it rather than cutting to a new screen.
    //
    // Two phases in one frame: the page row (the choice), then the board
    // (docs/PLAN_REWARD_SCREENS.md C8) -- the party's portraits in their
    // experience rings with their deeds, the haul strip and Continue. The
    // crest and the gold stack belong to the frame, so they stand through both.
    public sealed class ReckoningScreen
    {
        // 3:2, MATCHING THE SPREAD'S OWN ASPECT (reward_spread.png, 1536x1024),
        // so the cloth and its rope hem are never stretched.
        public const float PanelWidth = 1344f;
        public const float PanelHeight = 896f;

        // Where the ROPE HEM stops, measured off reward_spread.png by scanning
        // inward per edge for the last gold pixel: sides 6.1-6.9%, top 8.0%
        // with the paw crest dipping to 12.3% at the centre, bottom 9.5%
        // (the corner ties reach further in than the rope). Each bound sits a
        // little inside its measurement; the top clears the crest's foot, not
        // just the rope, because the crest is where the medallion sits.
        public const float ContentHalfWidth = PanelWidth * 0.5f - PanelWidth * 0.07f;
        public const float ContentTop = PanelHeight * 0.5f - PanelHeight * 0.135f;
        public const float ContentBottom = -(PanelHeight * 0.5f - PanelHeight * 0.10f);

        // How far the painted border reaches in from each side, derived from
        // ContentHalfWidth rather than restating 9.8%. The phase clip and
        // NothingSitsOnThePaintedBorder then cannot drift apart: the box the
        // sweep is clipped to IS the box content is asserted to stay inside.
        public const float BorderInsetX = PanelWidth * 0.5f - ContentHalfWidth;

        // BAKED, not painted. The painted attempt came back as a stubby star:
        // short rays, a hard silhouette, and it read as a spiky blob rather
        // than as light. The generated one holds all three things the brief
        // actually needed -- feathered tips, a fade well inside the canvas so
        // rotation cannot clip a ray, and true radial symmetry so a spin does
        // not wobble. Compared side by side before switching.
        public const string BurstKey = "proc:rarity_burst";

        public UiNode Root;

        // The painted frame itself. FIXED, and it stays fixed: it is revealed
        // by the wipe below rather than animated, which is the whole point.
        public NodeRef Frame;

        // The thing that actually moves. A mask whose WIDTH the controller
        // opens from 0, uncovering a frame that never changes size.
        //
        // The dimmer must NOT travel with it -- a dimmer growing from a point
        // would let the fight flash at full brightness for the first frames of
        // the expand.
        public NodeRef FrameWipe;

        // The dimmer Ui.Modal builds. Held so the gloom can FADE UP with the
        // expand rather than snapping on -- a dim that arrives in one frame
        // reads as a scene cut, which is the one thing this overlay is not.
        public NodeRef Dimmer;

        // TWO PHASES in one container: pick your spoils, then the board
        // (docs/PLAN_REWARD_SCREENS.md C8) that reads what the fight was worth.
        public NodeRef FrameGlow;
        public NodeRef FlareGlow;
        public NodeRef OfferPhase;
        public NodeRef SummaryPhase;

        // THE CREST (C1): the medallion of the room just cleared, on the
        // frame's crest from the wipe's first frame, and the crack an outcome
        // form's Fell lays over it. Both swapped or shown by the controller.
        public NodeRef CrestMedallion;
        public NodeRef CrestCrack;

        // THE GOLD STACK (C2), top-right on the frame itself rather than on a
        // phase, so the count runs inside the wipe whichever phase opens. A
        // chip that paid as gold shows its own icon here and dissolves.
        public NodeRef GoldStack;
        public NodeRef GoldGlyph;
        public NodeRef GoldCount;
        public NodeRef GoldDissolve;

        // What the settlement had to say that is not a payout -- a caravan
        // fake falling apart, then "No refunds." (FakeWear): a character
        // speaking, so it stays text, at the board's foot beside Continue.
        // Hidden when empty, which is nearly every fight.
        public NodeRef NoticeLabel;

        // Continue: the gold forward-arrow plate, no word.
        public NodeRef ContinueButton;

        // THE PARTY (C8): one portrait per member in squad order, each in its
        // experience ring (track, the violet before, the amber this fight), a
        // rim mark for one who fell, the level numeral on a badge that takes
        // the reward track's waiting pulse on a level-up, and the deeds row
        // under it. Unused portraits are hidden.
        public List<NodeRef> PortraitButtons = new List<NodeRef>();
        public List<NodeRef> PortraitFlares = new List<NodeRef>();
        public List<NodeRef> PortraitRingTracks = new List<NodeRef>();
        public List<NodeRef> PortraitRingBefores = new List<NodeRef>();
        public List<NodeRef> PortraitRingGains = new List<NodeRef>();
        public List<NodeRef> PortraitFaces = new List<NodeRef>();
        public List<NodeRef> PortraitMarks = new List<NodeRef>();
        public List<NodeRef> PortraitPulses = new List<NodeRef>();
        public List<NodeRef> PortraitLevelDiscs = new List<NodeRef>();
        public List<NodeRef> PortraitLevels = new List<NodeRef>();
        public List<NodeRef> DeedRows = new List<NodeRef>();

        // DeedsPerRow glyph + count pairs per portrait, flattened row-major.
        public List<NodeRef> DeedGlyphs = new List<NodeRef>();
        public List<NodeRef> DeedCounts = new List<NodeRef>();

        // THE HAUL STRIP (C2, C8): everything this fight left the party
        // holding that is not equipped, one chip each, in a clamped rail.
        public NodeRef HaulStrip;
        public List<NodeRef> HaulChips = new List<NodeRef>();
        public List<NodeRef> HaulPlinths = new List<NodeRef>();
        public List<NodeRef> HaulIcons = new List<NodeRef>();

        // The board's one focus detail, shared by the portraits and the haul.
        public NodeRef BoardTip;
        public NodeRef BoardTipTitle;
        public NodeRef BoardTipBody;

        public List<NodeRef> OfferButtons = new List<NodeRef>();
        public List<NodeRef> OfferHalos = new List<NodeRef>();
        public List<NodeRef> OfferBursts = new List<NodeRef>();

        public List<NodeRef> OfferIcons = new List<NodeRef>();
        public List<NodeRef> OfferNames = new List<NodeRef>();
        public List<NodeRef> OfferDeltas = new List<NodeRef>();
        public List<NodeRef> OfferFeatures = new List<NodeRef>();

        // The card face's other parts (docs/PLAN_REWARD_SCREENS.md C5).
        public List<NodeRef> OfferPlinths = new List<NodeRef>();
        public List<NodeRef> OfferGhosts = new List<NodeRef>();
        public List<NodeRef> OfferChains = new List<NodeRef>();
        public List<NodeRef> OfferWearerDiscs = new List<NodeRef>();
        public List<NodeRef> OfferWearerPortraits = new List<NodeRef>();
        public List<NodeRef> OfferBands = new List<NodeRef>();
        public List<NodeRef> OfferBandTiers = new List<NodeRef>();
        public List<NodeRef> OfferBandPluses = new List<NodeRef>();
        public List<NodeRef> OfferChevrons = new List<NodeRef>();
        public List<NodeRef> OfferDeltaGlyphs = new List<NodeRef>();

        // The spark that flies into a card's slot band on a hone or a leave.
        public List<NodeRef> OfferSparks = new List<NodeRef>();

        // THE REVEAL'S PARTS (docs/PLAN_REWARD_SCREENS.md C2-C4, C10): the map
        // chip that flies in and becomes the page, the cache it unfolds into
        // on a page that earns one, a card back per card, one ember per
        // choice above the row, and the transparent catcher under everything
        // that turns a click on empty space into a skip. All placeholders
        // (proc discs and the map's chips) until A1/A3 land.
        public NodeRef Catcher;
        public NodeRef ChipGear;
        public NodeRef ChipRelic;
        public NodeRef Cache;
        public NodeRef CacheArt;
        public NodeRef CacheSeam;
        public List<NodeRef> OfferBacks = new List<NodeRef>();
        public List<NodeRef> OfferFaces = new List<NodeRef>();
        public List<NodeRef> Embers = new List<NodeRef>();

        // The shared focus detail, shown under the focused face-up card.
        public NodeRef CardTip;
        public NodeRef CardTipTitle;
        public NodeRef CardTipBody;

        // "Hone instead", one per gear card, and the way off the page.
        // Each carries its face as glyph + count (RewardArt glyphs, swapped by
        // the controller); the door's face is up to three of those groups.
        public List<NodeRef> HoneButtons = new List<NodeRef>();
        public List<NodeRef> HoneGlyphs = new List<NodeRef>();
        public List<NodeRef> HoneCounts = new List<NodeRef>();
        public NodeRef LeaveButton;
        public NodeRef LeaveDoorGlyph;
        public NodeRef LeaveHoneGlyph;
        public NodeRef LeaveHoneCount;
        public NodeRef LeaveCoinGlyph;
        public NodeRef LeaveCoinCount;

        // THE EXPERIENCE RING'S THREE TINTS. The light neutral is where the
        // member already stood, the amber what this fight paid, and the track
        // the channel both are cut into. tools/measure_bar.py reads all three
        // out of this file rather than carrying copies.
        //
        // Neutral, not violet: a violet track and arc on a violet cloth hid
        // the before-arc. The amber is saturated rather than a desaturated
        // cream: against the cloth a desaturated yellow reads as beige, and
        // this is the one element on the board meant to look like a reward.
        public const string BarFillTint = "#FFC24A";
        public const string BarBeforeTint = "#B5ADA3";
        private const string BarTrackTint = "#0A0A0A99";

        // THE BOARD'S TEXT TIERS. Primary (a level-up, the gold) is gold and
        // amber; secondary (the deeds, a quiet badge's numeral) is muted
        // parchment, its glyphs tinted the same. Each reads at 4.5:1 or better
        // on the darkened cloth under it (BoardShadeTint).
        public const string Parchment = "#E8DCC0";
        public const string ParchmentMuted = "#E8DCC0D9";
        public const string GoldText = "#FFC84A";
        public const string BadgeQuiet = "#1A1022F2";
        public const string BadgeLevelled = "#FFC24AFF";
        public const string BadgeLevelledNumeral = "#2A1406";
        public const float BadgeLevelledScale = 1.3f;

        // The ring is the talent collar's baked ring, radial-filled by the
        // controller (Image.type Filled / Radial360), so it needs no shader.
        public const string RingKey = "proc:collar_ring";

        // THE CREST. The medallion sits on the spread's paw crest (centre
        // measured at 8.8% down the canvas). On the wipe beside the frame
        // rather than inside it: it covers the painted crest by design, which
        // NothingSitsOnThePaintedBorder refuses for anything inside the frame,
        // and the wipe still reveals it.
        public const float CrestSize = 96f;
        public const float CrestY = PanelHeight * 0.5f - PanelHeight * 0.088f;

        // The Fell overlay (reward_crack.png): a diagonal spanning 0.56 of its
        // canvas's width and 0.98 of its height, laid square over the
        // medallion so the split runs rim to rim.
        public const string CrackKey = RewardArt.Dir + "reward_crack.png";

        // The gold stack, inside the content box's top-right corner: clear of
        // the offer row's top (CardTop) and of the board's right portrait.
        public const float GoldStackWidth = 176f;
        public const float GoldStackHeight = 52f;
        public const float GoldStackX = ContentHalfWidth - GoldStackWidth * 0.5f;
        public const float GoldStackY = ContentTop - 2f - GoldStackHeight * 0.5f;
        public const float GoldGlyphSize = 46f;

        // THE PORTRAITS. Fixed at build time, so it must cover the largest
        // party the save can field: EffectiveMaxSquadSize caps the squad at
        // FightHudSpec.StageSlotsPerSide, which is 3; unused portraits are
        // hidden. Pinned against EffectiveMaxSquadSize by ReckoningTests.
        public const int RowCount = 3;
        public const float RingSize = 150f;
        public const float FaceSize = 108f;
        public const float PortraitPitch = 300f;
        public const float BadgeSize = 44f;
        public const float BadgeOffset = 53f;
        public const float MarkSize = 36f;

        // The deeds under each portrait: dealt, taken, healed, felled.
        public const int DeedsPerRow = 4;
        public const float DeedsWidth = 280f;
        public const float DeedsHeight = 32f;
        public const float DeedsGap = 8f;

        // The haul: one chip per thing the fight left the party holding and
        // per event relic received, each in a dark round socket, a clamped
        // rail. No caption; focus names a chip.
        public const int HaulChipCount = 12;
        public const float HaulChipSize = 64f;
        public const float HaulIconSize = 44f;
        public const float HaulChipPitch = 76f;
        public const float HaulHeight = 64f;
        public const float HaulWidth = HaulChipCount * HaulChipPitch;
        public const float HaulGap = 24f;

        // THE BOARD'S BLOCK: portraits, deeds and the haul read as one group,
        // centred in the cloth between the content box's top and Continue's
        // top, so no band of empty cloth splits the party from its haul.
        public static readonly UiVec ContinueSize = Ui.PlateNominalSizeFor(300f, 80f);
        public static readonly float ContinueY = ContentBottom + ContinueSize.Y * 0.5f + 1f;
        public const float BoardBlockHeight = RingSize + DeedsGap + DeedsHeight + HaulGap + HaulHeight;
        public static readonly float BoardBlockCentre = (ContentTop + ContinueY + ContinueSize.Y * 0.5f) * 0.5f;
        public static readonly float PortraitY = BoardBlockCentre + BoardBlockHeight * 0.5f - RingSize * 0.5f;
        public static readonly float DeedsY = PortraitY - RingSize * 0.5f - DeedsGap - DeedsHeight * 0.5f;
        public static readonly float HaulY = DeedsY - DeedsHeight * 0.5f - HaulGap - HaulHeight * 0.5f;

        // The darkening under the block: a soft radial in near-black, so the
        // cloth recedes behind the text without a plate's edge.
        public const string BoardShadeTint = "#0A050CC7";

        // The board's detail, between the haul and Continue.
        public const float BoardTipWidth = 560f;
        public const float BoardTipTitleHeight = 30f;

        public static float PortraitX(int index, int count) => (index - (count - 1) * 0.5f) * PortraitPitch;

        public static float HaulChipX(int index, int count) => (index - (count - 1) * 0.5f) * HaulChipPitch;

        public static ReckoningScreen Build()
        {
            var screen = new ReckoningScreen();

            // ---- phase one: the choice ---------------------------------
            // No heading, Favor line or ledger: the cards, the whetstones and
            // the door say what the page is and what became of it.
            var offerChildren = new List<UiNode>();

            // The top-tier flare's light, behind everything on the page and
            // sized to the content box, so its falloff ends inside the rope.
            var flare = Ui.Sprite("ReckoningFlareGlow", "proc:radial_glow",
                    Place.At(0f, (ContentTop + ContentBottom) * 0.5f),
                    UiSize.Fixed(ContentHalfWidth * 2f, ContentTop - ContentBottom))
                .Coloured("#FFFFFF00")
                .AsDecor();
            screen.FlareGlow = flare;
            offerChildren.Add(flare);

            // FIRST, so every control draws and catches clicks over it; it only
            // answers while a reveal beat runs (the controller shows it then).
            var catcher = Ui.Button("ReckoningCatcher", UiString.Runtime, Place.Stretch(), UiSize.Fill)
                .NoChrome()
                .NoHoverBox("the catcher is the whole row's empty space; nothing is focused on it")
                .Quiet()
                .Inactive()
                .AllowOverlap("the catcher lies under the whole row so a click anywhere during a beat skips it");
            screen.Catcher = catcher;
            offerChildren.Add(catcher);

            // OfferRowLayout.MaxCards cards, edge to edge -- the tree is
            // emitted once for every save, and a page shows k + 2 of them.
            // OfferRowLayout and ReckoningController.LayOutOfferRow re-centre
            // the row for however many a page actually shows.
            for (int i = 0; i < OfferRowLayout.MaxCards; i++)
            {
                offerChildren.Add(screen.BuildOffer(i));
            }

            for (int i = 0; i < OfferRowLayout.MaxCards; i++)
            {
                offerChildren.Add(screen.BuildHone(i));
            }

            // The reveal's particles (RevealFeel), over the cards they leave;
            // transparent at rest.
            for (int i = 0; i < RevealMoteCount; i++)
            {
                var mote = Ui.Sprite($"ReckoningRevealMote{i}", "proc:radial_glow", Place.At(0f, CardCentreY),
                        UiSize.Fixed(RevealMoteSize, RevealMoteSize))
                    .Coloured("#FFFFFF00")
                    .AsDecor();
                screen.RevealMotes.Add(mote);
                offerChildren.Add(mote);
            }

            for (int i = 0; i < MaxChoices; i++)
            {
                var ember = Ui.Sprite($"ReckoningEmber{i}", "proc:radial_glow",
                        Place.At(EmberX(i, MaxChoices), EmberY), UiSize.Fixed(EmberSize, EmberSize))
                    .Coloured("#FFB45A00")
                    .AsDecor();
                screen.Embers.Add(ember);
                offerChildren.Add(ember);
            }

            // The chip and the cache share the row's centre with the cards they
            // become, never at the same moment.
            const string cardsShareIt = "the cache stands where the cards will be dealt, never at the same time";
            var chipGear = Ui.Sprite("ReckoningChipGear", ChipGearKey, Place.At(0f, CardCentreY),
                    UiSize.Fixed(ChipSize, ChipSize))
                .AsDecor()
                .Inactive();
            var chipRelic = Ui.Sprite("ReckoningChipRelic", ChipRelicKey, Place.At(0f, CardCentreY),
                    UiSize.Fixed(ChipSize, ChipSize))
                .AsDecor()
                .Inactive();
            screen.ChipGear = chipGear;
            screen.ChipRelic = chipRelic;

            // The bundle or tin for the page's class; the controller swaps the
            // sprite (RewardArt.CacheId) and seats the seam's light on it.
            var cacheArt = Ui.Sprite("ReckoningCacheArt", null, Place.Stretch(), UiSize.Fill)
                .AsDecor();
            cacheArt.PreserveAspect = true;
            screen.CacheArt = cacheArt;
            var cacheSeam = Ui.Solid("ReckoningCacheSeam", "#FFD98A00", new UiVec(CacheWidth * 0.7f, 6f), Place.At(0f, 0f))
                .AsDecor();
            screen.CacheSeam = cacheSeam;
            var cache = Ui.Button("ReckoningCache", UiString.Runtime, new UiVec(CacheWidth, CacheHeight), 14,
                    Place.At(0f, CardCentreY))
                .NoChrome()
                .Quiet()
                .Inactive()
                .AllowOverlap(cardsShareIt);
            cache.Children.Add(cacheArt);
            cache.Children.Add(cacheSeam);
            screen.Cache = cache;
            offerChildren.Add(chipGear);
            offerChildren.Add(chipRelic);
            offerChildren.Add(cache);

            // Leave, and when nothing is left to choose, the same button
            // carries on. Sized and seated like the summary's Continue so the
            // two read as one control moving along.
            var leaveSize = Ui.PlateNominalSizeFor(260f, 60f);
            var leave = Ui.Button("ReckoningLeaveButton", UiString.Runtime, leaveSize, 18,
                    Place.At(0f, ContentBottom + leaveSize.Y * 0.5f + 1f))
                .Themed(ButtonTheme.Gold)
                .Quiet();
            // The door, then what leaving pays (whetstones, coins): glyph groups
            // the controller seats. At rest they sit apart so the audit sees no
            // overlap.
            var leaveDoorGlyph = GlyphPart("ReckoningLeaveDoorGlyph", -112f);
            var leaveHoneGlyph = GlyphPart("ReckoningLeaveHoneGlyph", -64f);
            var leaveHoneCount = CountPart("ReckoningLeaveHoneCount", -30f, 40f);
            var leaveCoinGlyph = GlyphPart("ReckoningLeaveCoinGlyph", 8f);
            var leaveCoinCount = CountPart("ReckoningLeaveCoinCount", 56f, 60f);
            screen.LeaveDoorGlyph = leaveDoorGlyph;
            screen.LeaveHoneGlyph = leaveHoneGlyph;
            screen.LeaveHoneCount = leaveHoneCount;
            screen.LeaveCoinGlyph = leaveCoinGlyph;
            screen.LeaveCoinCount = leaveCoinCount;
            leave.Children.Add(leaveDoorGlyph);
            leave.Children.Add(leaveHoneGlyph);
            leave.Children.Add(leaveHoneCount);
            leave.Children.Add(leaveCoinGlyph);
            leave.Children.Add(leaveCoinCount);
            screen.LeaveButton = leave;
            offerChildren.Add(leave);

            // The focus detail, last so it draws over the cards it describes.
            var tip = ItemComparisonPanel.Build("ReckoningCardTip", Place.At(0f, 0f),
                new UiVec(CardTipWidth, System.Math.Min(CardTipHeight, CardTipMaxHeight)), CardTipTitleHeight, 20, "#F2DB9E", 15, "#D9CCF2");
            tip.Panel.Inactive();
            screen.CardTip = tip.Panel;
            screen.CardTipTitle = tip.Title;
            screen.CardTipBody = tip.Body;
            offerChildren.Add(tip.Panel);

            var offerPhase = Ui.Panel("ReckoningOfferPhase", Place.Stretch(), UiSize.Fill, offerChildren);
            screen.OfferPhase = offerPhase;

            // ---- phase two: the board (C8) ------------------------------
            var summaryChildren = new List<UiNode>();

            // FIRST, under everything on the board: the cloth darkened behind
            // the block, a radial with no edge, not a plate.
            var shade = Ui.Sprite("ReckoningBoardShade", "proc:radial_glow", Place.At(0f, BoardBlockCentre),
                    UiSize.Fixed(ContentHalfWidth * 2f, (ContentTop - BoardBlockCentre) * 2f))
                .Coloured(BoardShadeTint)
                .AsDecor();
            summaryChildren.Add(shade);

            for (int i = 0; i < RowCount; i++)
            {
                summaryChildren.Add(screen.BuildPortrait(i));
                summaryChildren.Add(screen.BuildDeeds(i));
            }

            // Continue sits on the content box's floor.
            var continueSize = ContinueSize;
            var continueY = ContinueY;

            summaryChildren.Add(screen.BuildHaul(HaulY));

            var notice = Ui.Label("ReckoningNoticeLabel", UiString.Runtime, new UiVec(360f, 54f), 15,
                    "#E8A07A", Place.At(-ContentHalfWidth + 180f, continueY))
                .AsDecor()
                .Styled(TypographyRole.Body)
                .Inactive();
            screen.NoticeLabel = notice;
            summaryChildren.Add(notice);

            // The forward arrow: the same mark the door carries once nothing is
            // left to choose, so the two read as one control moving along.
            var continueButton = Ui.Button("ReckoningContinueButton", UiStrings.ReckoningForward,
                    continueSize, 20, Place.At(0f, continueY))
                .Themed(ButtonTheme.Gold)
                .Quiet();
            screen.ContinueButton = continueButton;
            summaryChildren.Add(continueButton);

            // The details last, so each draws over what it describes.
            float haulBottom = HaulY - HaulHeight * 0.5f;
            float continueTop = continueY + continueSize.Y * 0.5f;
            var boardTip = ItemComparisonPanel.Build("ReckoningBoardTip",
                Place.At(0f, (haulBottom + continueTop) * 0.5f),
                new UiVec(BoardTipWidth, haulBottom - continueTop - 8f), BoardTipTitleHeight, 20, "#F2DB9E", 15,
                "#D9CCF2");
            boardTip.Panel.Inactive();
            screen.BoardTip = boardTip.Panel;
            screen.BoardTipTitle = boardTip.Title;
            screen.BoardTipBody = boardTip.Body;
            summaryChildren.Add(boardTip.Panel);

            var summaryPhase = Ui.Panel("ReckoningSummaryPhase", Place.Stretch(), UiSize.Fill, summaryChildren)
                .Inactive();

            // The offer and the summary are the same box at two moments.
            Ui.Exclusive(offerPhase, summaryPhase);
            screen.SummaryPhase = summaryPhase;

            // ---- the spread -------------------------------------------------
            //
            // Prince's cloth, laid out under the whole screen (reward_spread,
            // 3:2 like the panel). Built as a frame holder (Ui.ArtFrame): the
            // art is a Decor child of a non-Decor wrapper, so content beside
            // it is still audited against its own siblings.
            var frame = Ui.ArtFrame("ReckoningFrame", RewardArt.SpreadKey, Place.At(0f, 0f),
                new UiVec(PanelWidth, PanelHeight));

            // The phases hang inside a CLIP inset to the painted border rather
            // than off the frame directly.
            //
            // SweepToSummary sends the outgoing cards a full panel width
            // sideways. With nothing bounding them they crossed the gold border
            // and floated over the battlefield before disappearing. Clipped
            // here they slide UNDER the border, which is what a frame around a
            // window is for.
            //
            // Inset horizontally ONLY, and symmetrically, so the clip's centre
            // is still exactly the frame's centre. Every child inside is placed
            // relative to that centre, so an asymmetric inset -- and the
            // vertical border IS asymmetric, the crest being deeper than the
            // bottom ornament -- would silently move all of them. The sweep is
            // horizontal, so the top and bottom edges have nothing to clip
            // anyway.
            var phaseClip = Ui.Panel("ReckoningPhaseClip",
                    Place.Stretch(left: BorderInsetX, right: BorderInsetX), UiSize.Fill,
                    offerPhase, summaryPhase)
                .Clipping();

            frame.Children.Add(phaseClip);
            frame.Children.Add(screen.BuildGoldStack());
            screen.Frame = frame;

            // The WIPE, and the reason it exists as a node at all.
            //
            // Animating frame.localScale.x from 0 would scale CHILDREN too,
            // so the border's corner ornaments, the three cards and every
            // label would compress horizontally and spring out to full
            // width -- a rubbery stretch, and no amount of easing fixes it:
            // a squash is not a wipe.
            //
            // A mask whose WIDTH opens reveals a frame that never deforms.
            // Centred, and grown symmetrically about that centre, so the fixed
            // frame inside it does not drift while it is being uncovered.
            var wipe = Ui.Panel("ReckoningFrameWipe", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), frame, screen.BuildCrest())
                .Clipping();
            screen.FrameWipe = wipe;

            // A soft drop behind the frame. Sits BEHIND it in declaration
            // order and is deliberately larger, so it reads as the panel
            // casting light and shadow onto the fight rather than as a
            // second rectangle.
            // RADIAL, NOT THE BURST. This used the rayed rarity_burst sprite,
            // which has spokes -- a container does not throw spokes, and at
            // #2A1A4A (near-black violet) over an already dark fight it was
            // invisible at any alpha. A soft falloff in a LIGHTER violet reads
            // as the panel lighting the room behind it, which is what the
            // "soft glow around the container" was asking for.
            var glow = Ui.Sprite("ReckoningFrameGlow", "proc:radial_glow", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth * 1.5f, PanelHeight * 1.6f))
                .Coloured("#8A63D800")
                .AsDecor()
                .AllowOverflow("the glow is deliberately larger than the frame it sits behind - that bleed IS the drop");
            screen.FrameGlow = glow;

            var content = Ui.Panel("ReckoningContent", Place.At(0f, 0f), UiSize.Fill,
                    glow, wipe)
                .AllowOverlap("the glow sits under the frame by design");

            // As dim as the overlay goes while the fight still shows (the
            // ceiling ReckoningScreenTests pins), so the HUD and enemy panel
            // at the edges recede behind the Reckoning.
            var root = Ui.Modal("ReckoningPanel", "#0A0614BF", content).Inactive();
            screen.Dimmer = root.Children[0];
            screen.Root = root;
            return screen;
        }

        // ---- the board's parts ----------------------------------------------------

        private UiNode BuildPortrait(int index)
        {
            float x = PortraitX(index, RowCount);
            const string light = "the level-up flare is light thrown past the ring, wider than the portrait by design";

            var flare = Ui.Sprite($"ReckoningPortrait{index}Flare", "proc:radial_glow", Place.At(0f, 0f),
                    UiSize.Fixed(RingSize * 1.4f, RingSize * 1.4f))
                .Coloured("#FFB45A00")
                .AsDecor()
                .AllowOverflow(light);
            var track = Ui.Sprite($"ReckoningPortrait{index}RingTrack", RingKey, Place.Stretch(), UiSize.Fill)
                .Coloured(BarTrackTint)
                .AsDecor();
            var before = Ui.Sprite($"ReckoningPortrait{index}RingBefore", RingKey, Place.Stretch(), UiSize.Fill)
                .Coloured(BarBeforeTint)
                .AsDecor();
            var gain = Ui.Sprite($"ReckoningPortrait{index}RingGain", RingKey, Place.Stretch(), UiSize.Fill)
                .Coloured(BarFillTint)
                .AsDecor();
            var face = Ui.Sprite($"ReckoningPortrait{index}Face", null, Place.At(0f, 0f),
                    UiSize.Fixed(FaceSize, FaceSize))
                .AsDecor();
            face.PreserveAspect = true;

            // The cracked heart on the rim of one who fell during the fight.
            var mark = Ui.Sprite($"ReckoningPortrait{index}Mark", null, Place.At(-BadgeOffset, BadgeOffset),
                    UiSize.Fixed(MarkSize, MarkSize))
                .AsDecor()
                .Inactive();
            mark.PreserveAspect = true;

            // The level badge, and behind it the waiting pulse (BeaconPulse,
            // attached at build) a level-up switches on.
            var pulse = Ui.Sprite($"ReckoningPortrait{index}Pulse", "proc:radial_glow",
                    Place.At(BadgeOffset, -BadgeOffset), UiSize.Fixed(BadgeSize * 1.6f, BadgeSize * 1.6f))
                .Coloured("#FFC24AFF")
                .AsDecor()
                .AllowOverflow("the waiting pulse is light around the badge, reaching past the ring by design")
                .Inactive();
            var disc = Ui.Sprite($"ReckoningPortrait{index}LevelDisc", "proc:solid_circle",
                    Place.At(BadgeOffset, -BadgeOffset), UiSize.Fixed(BadgeSize, BadgeSize))
                .Coloured(BadgeQuiet)
                .AsDecor();
            var level = Ui.Label($"ReckoningPortrait{index}Level", UiStrings.ReckoningLevelNumeral,
                    new UiVec(BadgeSize, BadgeSize - 8f), 20, Parchment, Place.At(BadgeOffset, -BadgeOffset))
                .AsDecor()
                .Styled(TypographyRole.TacticalData);

            var button = Ui.Button($"ReckoningPortrait{index}", UiString.Runtime, new UiVec(RingSize, RingSize), 14,
                    Place.At(x, PortraitY))
                .NoChrome()
                .Quiet()
                .Inactive();
            button.Children.Add(flare);
            // The amber runs from twelve o'clock to where they stand now and the
            // violet over it to where they stood, so the gain shows as the arc
            // the violet leaves uncovered.
            button.Children.Add(track);
            button.Children.Add(gain);
            button.Children.Add(before);
            button.Children.Add(face);
            button.Children.Add(mark);
            button.Children.Add(pulse);
            button.Children.Add(disc);
            button.Children.Add(level);

            PortraitButtons.Add(button);
            PortraitFlares.Add(flare);
            PortraitRingTracks.Add(track);
            PortraitRingBefores.Add(before);
            PortraitRingGains.Add(gain);
            PortraitFaces.Add(face);
            PortraitMarks.Add(mark);
            PortraitPulses.Add(pulse);
            PortraitLevelDiscs.Add(disc);
            PortraitLevels.Add(level);
            return button;
        }

        // Glyph + number pairs the controller seats (PaintGroups); at rest they
        // sit apart so the audit sees no overlap.
        private UiNode BuildDeeds(int index)
        {
            var children = new List<UiNode>();
            for (int k = 0; k < DeedsPerRow; k++)
            {
                float x = -DeedsWidth * 0.5f + 14f + k * 70f;
                var glyph = GlyphPart($"ReckoningDeeds{index}Glyph{k}", x).Coloured(ParchmentMuted);
                var count = Ui.Label($"ReckoningDeeds{index}Count{k}", UiString.Runtime, new UiVec(40f, 28f), 20,
                        ParchmentMuted, Place.At(x + 34f, 0f))
                    .TextAligned(UiTextAlign.Left)
                    .AsDecor()
                    .Styled(TypographyRole.TacticalData);
                DeedGlyphs.Add(glyph);
                DeedCounts.Add(count);
                children.Add(glyph);
                children.Add(count);
            }

            var row = Ui.Panel($"ReckoningDeeds{index}", Place.At(PortraitX(index, RowCount), DeedsY),
                    UiSize.Fixed(DeedsWidth, DeedsHeight), children)
                .Inactive();
            DeedRows.Add(row);
            return row;
        }

        private UiNode BuildHaul(float y)
        {
            var children = new List<UiNode>();
            for (int i = 0; i < HaulChipCount; i++)
            {
                // The socket, and inside it the thing's quality as a soft light.
                var socket = Ui.Sprite($"ReckoningHaulChip{i}Socket", "proc:solid_circle", Place.Stretch(),
                        UiSize.Fill)
                    .Coloured("#120A18E6")
                    .AsDecor();
                var plinth = Ui.Sprite($"ReckoningHaulChip{i}Plinth", "proc:radial_glow", Place.Stretch(), UiSize.Fill)
                    .Coloured("#B8A8D966")
                    .AsDecor();
                var icon = Ui.Sprite($"ReckoningHaulChip{i}Icon", null, Place.At(0f, 0f),
                        UiSize.Fixed(HaulIconSize, HaulIconSize))
                    .AsDecor();
                icon.PreserveAspect = true;

                var chip = Ui.Button($"ReckoningHaulChip{i}", UiString.Runtime,
                        new UiVec(HaulChipSize, HaulChipSize), 14, Place.At(HaulChipX(i, HaulChipCount), 0f))
                    .NoChrome()
                    .Quiet()
                    .Inactive();
                chip.Children.Add(socket);
                chip.Children.Add(plinth);
                chip.Children.Add(icon);

                HaulChips.Add(chip);
                HaulPlinths.Add(plinth);
                HaulIcons.Add(icon);
                children.Add(chip);
            }

            var strip = Ui.Panel("ReckoningHaul", Place.At(0f, y), UiSize.Fixed(HaulWidth, HaulHeight), children);
            HaulStrip = strip;
            return strip;
        }

        private UiNode BuildCrest()
        {
            var medallion = Ui.Sprite("ReckoningCrestMedallion", null, Place.Stretch(), UiSize.Fill)
                .AsDecor();
            medallion.PreserveAspect = true;
            var crack = Ui.Sprite("ReckoningCrestCrack", CrackKey, Place.Stretch(), UiSize.Fill)
                .AsDecor()
                .Inactive();
            crack.PreserveAspect = true;
            CrestMedallion = medallion;
            CrestCrack = crack;
            return Ui.Panel("ReckoningCrest", Place.At(0f, CrestY), UiSize.Fixed(CrestSize, CrestSize),
                    medallion, crack)
                .AsDecor();
        }

        private UiNode BuildGoldStack()
        {
            float glyphX = -GoldStackWidth * 0.5f + GoldGlyphSize * 0.5f + 4f;
            var glyph = Ui.Sprite("ReckoningGoldGlyph", null, Place.At(glyphX, 0f),
                    UiSize.Fixed(GoldGlyphSize, GoldGlyphSize))
                .AsDecor();
            glyph.PreserveAspect = true;
            var dissolve = Ui.Sprite("ReckoningGoldDissolve", null, Place.At(glyphX, 0f),
                    UiSize.Fixed(GoldGlyphSize + 6f, GoldGlyphSize + 6f))
                .AsDecor()
                .Inactive();
            dissolve.PreserveAspect = true;
            float countWidth = GoldStackWidth - GoldGlyphSize - 12f;
            var count = Ui.Label("ReckoningGoldCount", UiStrings.ReckoningGoldCount, new UiVec(countWidth, 46f), 36,
                    GoldText, Place.At(GoldStackWidth * 0.5f - countWidth * 0.5f, 0f))
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Styled(TypographyRole.TacticalData);

            GoldGlyph = glyph;
            GoldDissolve = dissolve;
            GoldCount = count;
            var stack = Ui.Panel("ReckoningGoldStack", Place.At(GoldStackX, GoldStackY),
                    UiSize.Fixed(GoldStackWidth, GoldStackHeight), glyph, dissolve, count)
                .AsDecor();
            GoldStack = stack;
            return stack;
        }

        // ---- one page card's vertical bands -----------------------------------
        //
        // ALL OF IT DERIVED FROM THE BAND HEIGHTS, top to bottom: the art, the
        // name, the quality line, who it is for, the one stat it moves, one line
        // about it, what it loses, and the TAKE caption. Everything else (each
        // band's centre, the card's height and centre) falls out, so a retune of
        // one band moves the frame with it and the containment audit never sees
        // a label hanging below its own card.
        //
        // A relic card has no recipient or stat line, so its description takes
        // those two bands as well (RelicFeatureBand).
        private const float CardTop = 200f;

        // The art zone: plinth, ghost, item and wearer medallion share it. The
        // item's box leaves 8 px above and the plinth sits on the zone's floor.
        private const float IconBand = 190f;
        public const float IconBoxHeight = 150f;
        private const float BandBand = 28f;
        private const float NameBand = 52f;
        private const float DeltaBand = 32f;
        private const float FeatureBand = 62f;

        private const float IconTop = CardTop;
        private const float IconBottom = IconTop - IconBand;
        private const float BandTop = IconBottom;
        private const float NameTop = BandTop - BandBand;
        private const float DeltaTop = NameTop - NameBand;
        private const float FeatureTop = DeltaTop - DeltaBand;
        private const float CardBottom = FeatureTop - FeatureBand;
        public const float CardHeight = CardTop - CardBottom;
        public const float CardCentreY = (CardTop + CardBottom) * 0.5f;

        // Band centres relative to the CARD's centre, which is what a child's
        // Place is measured from.
        private const float IconCentre = IconTop - 8f - IconBoxHeight * 0.5f - CardCentreY;
        private const float PlinthCentre = IconBottom + OfferRowLayout.PlinthHeight * 0.5f + 4f - CardCentreY;
        private const float GhostHeight = IconBoxHeight * OfferRowLayout.GhostScale;
        private const float GhostCentre = IconBottom + GhostHeight * 0.5f + 8f - CardCentreY;
        private const float MedallionCentre = IconTop - 4f - OfferRowLayout.MedallionSize * 0.5f - CardCentreY;
        private const float BandCentre = BandTop - BandBand * 0.5f - CardCentreY;
        private const float NameCentre = NameTop - NameBand * 0.5f - CardCentreY;
        private const float DeltaCentre = DeltaTop - DeltaBand * 0.5f - CardCentreY;
        public const float FeatureCentre = FeatureTop - FeatureBand * 0.5f - CardCentreY;
        public const float FeatureHeight = FeatureBand;

        // A relic has no stat line, so its description takes that band too.
        public const float RelicFeatureHeight = DeltaBand + FeatureBand;
        public const float RelicFeatureCentre = DeltaTop - RelicFeatureHeight * 0.5f - CardCentreY;

        // The focus detail's box; its height is refitted to the text at runtime.
        // It sits UNDER the row, never beside a card: beside, it covered the
        // neighbouring cards. Wide so the text runs to few lines. Built at
        // CardTipHeight; CardTipMaxHeight is the band between the cards' bottom
        // edge and the painted border, which the refit on show keeps it inside.
        public const float CardTipWidth = 560f;
        public const float CardTipHeight = 200f;
        public const float CardTipTitleHeight = 30f;
        public const float CardTipMinBody = 60f;
        public const float CardTipGap = 14f;
        public const float CardTipMaxHeight = CardBottom - CardTipGap - ContentBottom;

        // The reveal's placeholders: k <= MaxCards - 2 embers in a row above
        // the cards, the map chip and the cache at the row's centre.
        public const int MaxChoices = OfferRowLayout.MaxCards - 2;
        public const float EmberSize = 26f;
        public const float EmberPitch = 40f;
        public const float EmberY = CardTop + 30f;
        public const float ChipSize = 96f;
        public const float CacheWidth = 260f;
        public const float CacheHeight = 190f;
        public const string ChipGearKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_gear.png";
        public const string ChipRelicKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_relic.png";

        // Ember i of `count`, centred on the row.
        public static float EmberX(int index, int count) => (index - (count - 1) * 0.5f) * EmberPitch;

        // Under each gear card: "hone instead". In the offer phase's own coordinates.
        public const float SparkSize = 28f;
        private const float HoneWidth = 172f;
        private const float HoneGap = 8f;

        // THE REVEAL'S LIGHT per card, behind its face and back (C4): the
        // under-glow that builds before the flip and stays faintly lit in the
        // quality's colour, and the landing's rays, core flash and ring. All
        // transparent at rest; the controller paints them from the clock.
        public List<NodeRef> OfferUnderGlows = new List<NodeRef>();
        public List<NodeRef> OfferRays = new List<NodeRef>();
        public List<NodeRef> OfferFlashes = new List<NodeRef>();
        public List<NodeRef> OfferRings = new List<NodeRef>();
        public List<NodeRef> OfferShadows = new List<NodeRef>();
        public List<NodeRef> OfferRims = new List<NodeRef>();
        public List<NodeRef> OfferFaceTints = new List<NodeRef>();
        public List<NodeRef> OfferFaceRims = new List<NodeRef>();
        public const float CardShadowDrop = 8f;
        public const float CardShadowSpread = 1.12f;

        // The face's opaque ground: the card back's own violet, darkened.
        public const string FaceGround = "#1C1228";

        // The light's sizes, as fractions of the card's box.
        public const float UnderGlowWidth = 1.3f;
        public const float UnderGlowHeight = 1.15f;
        public const float RaysSpan = 1.6f;
        public const float FlashSpan = 0.8f;
        public const float RingSpan = 0.7f;

        // The reveal's particle pool, shared by every card on the row.
        public List<NodeRef> RevealMotes = new List<NodeRef>();
        public const int RevealMoteCount = 64;
        public const float RevealMoteSize = 20f;

        // One card of the page. A button, because taking is a click on the card
        // itself, and a CARD across the width rather than a row in a list: the
        // item is the object on this screen.
        //
        // Positioned for a FULL-WIDTH row (MaxCards wide). A narrower one is
        // re-centred at runtime by ReckoningController, from the same
        // OfferRowLayout.CardX this calls, so the two widths cannot drift apart.
        private UiNode BuildOffer(int index)
        {
            int count = OfferRowLayout.MaxCards;
            float cardWidth = OfferRowLayout.CardWidth(count);
            float labelWidth = OfferRowLayout.LabelWidth(count);
            float x = OfferRowLayout.CardX(index, count);

            // The art's box is as wide as its card allows (OfferRowLayout.
            // IconWidth); ItemIcons.Apply fits the art inside it, so widening
            // the box is the whole of "make the item bigger".
            float iconWidth = OfferRowLayout.IconWidth(count);
            float haloSize = OfferRowLayout.HaloDiameter(count);
            float burstSize = OfferRowLayout.BurstDiameter(count);
            float ghostWidth = OfferRowLayout.GhostWidth(count);

            // BACK TO FRONT, which is the child order below: plinth, halo,
            // burst, the worn item's ghost, the new item, then the marks that
            // sit on them.
            //
            // The plinth is the item's quality as a silhouette: its own art
            // per quality step (RewardArt.PlinthId, swapped at runtime), each
            // on the same canvas at its own size. The halo and burst repeat
            // the colour behind the item.
            var plinth = Ui.Sprite($"ReckoningOffer{index}Plinth", RewardArt.PathOf(RewardArt.PlinthCommon),
                    Place.At(0f, PlinthCentre), UiSize.Fixed(OfferRowLayout.PlinthWidth, OfferRowLayout.PlinthHeight))
                .AsDecor();
            plinth.PreserveAspect = true;

            // TWO layers behind the icon, because one was not readable.
            //
            // The halo is the soft mass: a plain radial falloff, wider than the
            // icon, carrying the quality colour where the eye actually looks.
            // The burst is the rayed sprite, and its rays are the only part of
            // it that ever cleared the icon -- behind a solid object a starburst
            // shows as a few spikes and nothing else.
            var halo = Ui.Sprite($"ReckoningOffer{index}Halo", "proc:radial_glow",
                    Place.At(0f, IconCentre), UiSize.Fixed(haloSize, haloSize))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the halo is deliberately larger than the icon it sits behind - that bleed IS the quality signal");

            var burst = Ui.Sprite($"ReckoningOffer{index}Burst", BurstKey,
                    Place.At(0f, IconCentre), UiSize.Fixed(burstSize, burstSize))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the burst is deliberately larger than the icon it sits behind - that bleed IS the quality signal");

            // What the wearer has in this slot now: behind and to the left,
            // small and faint. No honing is drawn on it; that is the slot's.
            var ghost = Ui.Sprite($"ReckoningOffer{index}Ghost", null,
                    Place.At(OfferRowLayout.GhostX(count), GhostCentre), UiSize.Fixed(ghostWidth, GhostHeight))
                .AsDecor();

            var icon = Ui.Sprite($"ReckoningOffer{index}Icon", null,
                    Place.At(0f, IconCentre), UiSize.Fixed(iconWidth, IconBoxHeight))
                .AsDecor();

            // An inert card's art is greyed and the chain lies diagonally over
            // it; the words are the tooltip's alone.
            var chain = Ui.Sprite($"ReckoningOffer{index}Chain", RewardArt.ChainKey,
                    Place.At(0f, IconCentre), UiSize.Fixed(iconWidth, IconBoxHeight))
                .Coloured("#FFFFFF00")
                .AsDecor();
            chain.PreserveAspect = true;

            // Identity only: who this is for. A square portrait on a disc until
            // the medallion art exists.
            var wearerDisc = Ui.Sprite($"ReckoningOffer{index}WearerDisc", "proc:solid_circle",
                    Place.At(OfferRowLayout.MedallionX(count), MedallionCentre),
                    UiSize.Fixed(OfferRowLayout.MedallionSize, OfferRowLayout.MedallionSize))
                .Coloured("#3A2A52FF")
                .AsDecor();
            var wearerPortrait = Ui.Sprite($"ReckoningOffer{index}WearerPortrait", null,
                    Place.At(OfferRowLayout.MedallionX(count), MedallionCentre),
                    UiSize.Fixed(OfferRowLayout.MedallionSize - 6f, OfferRowLayout.MedallionSize - 6f))
                .AsDecor();

            // THE SLOT BAND: the plinth's front. The tier numeral on the left,
            // the SLOT's honing on the right (it stays lit when the items swap).
            var band = Ui.Solid($"ReckoningOffer{index}Band", "#1D1226E6",
                    new UiVec(OfferRowLayout.BandWidth(count), BandBand - 4f), Place.At(0f, BandCentre))
                .AsDecor();
            var bandTier = Ui.Label($"ReckoningOffer{index}BandTier", UiString.Runtime,
                    new UiVec(64f, BandBand - 4f), 16, Parchment, Place.At(-OfferRowLayout.BandLabelX(count), BandCentre))
                .AsDecor();
            var bandPlus = Ui.Label($"ReckoningOffer{index}BandPlus", UiString.Runtime,
                    new UiVec(64f, BandBand - 4f), 16, "#8A7BA6", Place.At(OfferRowLayout.BandLabelX(count), BandCentre))
                .AsDecor();

            var name = Ui.Label($"ReckoningOffer{index}Name", UiString.Runtime, new UiVec(labelWidth, NameBand), 20,
                    "#EDE6FF", Place.At(0f, NameCentre))
                .AsDecor()
                .Styled(TypographyRole.Body);

            // The stat's glyph and its number take the left, the chevron the
            // right; the chevron's shape (up, down, flat) carries the sign.
            var delta = Ui.Label($"ReckoningOffer{index}Delta", UiString.Runtime,
                    new UiVec(OfferRowLayout.DeltaLabelWidth(count), DeltaBand), 20, Parchment,
                    Place.At(OfferRowLayout.DeltaLabelX(count), DeltaCentre))
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
            var deltaGlyph = Ui.Sprite($"ReckoningOffer{index}DeltaGlyph", null,
                    Place.At(OfferRowLayout.DeltaGlyphX(count), DeltaCentre),
                    UiSize.Fixed(RewardArt.GlyphSize, RewardArt.GlyphSize))
                .AsDecor();
            deltaGlyph.PreserveAspect = true;
            var chevron = Ui.Sprite($"ReckoningOffer{index}Chevron", RewardArt.PathOf(RewardArt.Level),
                    Place.At(OfferRowLayout.ChevronX(count), DeltaCentre),
                    UiSize.Fixed(OfferRowLayout.ChevronSize, OfferRowLayout.ChevronSize))
                .AsDecor();
            chevron.PreserveAspect = true;

            var feature = Ui.Label($"ReckoningOffer{index}Feature", UiString.Runtime,
                    new UiVec(labelWidth, FeatureBand), 13, "#D9CCF2", Place.At(0f, FeatureCentre))
                .TextAligned(UiTextAlign.TopLeft)
                .AsDecor();
            var spark = Ui.Sprite($"ReckoningOffer{index}Spark", "proc:radial_glow",
                    Place.At(0f, BandCentre), UiSize.Fixed(SparkSize, SparkSize))
                .Coloured("#FFE9A800")
                .AsDecor();

            // NO PLATE. The card is its burst, its icon and its lines -- the
            // item is the object, and a gold button frame behind it turned
            // treasures into menu entries.
            //
            // Chromeless rather than an empty SpriteKey, which the emitter
            // reads as "use the shared button frame" and is exactly how this
            // survived being called fixed twice: BuildOffer removed the
            // declared children and the plate came straight back from the
            // fallback.
            //
            // NOT migrated to the semantic kit, even though taking one is this
            // screen's "Accept" (Green): a themed plate behind each card is
            // precisely the mistake the note above records fixing.
            var button = Ui.Button($"ReckoningOffer{index}", UiString.Runtime,
                    new UiVec(cardWidth, CardHeight), 14, Place.At(x, CardCentreY))
                .NoChrome()
                .Quiet();

            // The reveal's light, FIRST so it sits behind the face and the back
            // and does not flip with them: the controller turns the face and
            // back, never the button.
            const string spills = "the reveal's light spills past the card on purpose - that bleed is the quality read";
            var underGlow = Ui.Sprite($"ReckoningOffer{index}UnderGlow", "proc:radial_glow", Place.At(0f, 0f),
                    UiSize.Fixed(cardWidth * UnderGlowWidth, CardHeight * UnderGlowHeight))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow(spills);
            var rays = Ui.Sprite($"ReckoningOffer{index}Rays", BurstKey, Place.At(0f, 0f),
                    UiSize.Fixed(cardWidth * RaysSpan, cardWidth * RaysSpan))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow(spills);
            var flash = Ui.Sprite($"ReckoningOffer{index}Flash", "proc:radial_glow", Place.At(0f, 0f),
                    UiSize.Fixed(cardWidth * FlashSpan, cardWidth * FlashSpan))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow(spills);
            var ring = Ui.Sprite($"ReckoningOffer{index}Ring", "proc:ring_hairline", Place.At(0f, 0f),
                    UiSize.Fixed(cardWidth * RingSpan, cardWidth * RingSpan))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow(spills);
            // The card on the cloth: a soft drop shadow under it and a faint
            // lighter rim over it, both turned with the face and back and
            // seated by the controller on the card back's own drawn shape.
            var shadow = Ui.Sprite($"ReckoningOffer{index}Shadow", "proc:card_shadow", Place.At(0f, -CardShadowDrop),
                    UiSize.Fixed(cardWidth * CardShadowSpread, cardWidth / RewardArt.CardBackAspect * CardShadowSpread))
                .Coloured("#00000073")
                .AsDecor()
                .AllowOverflow("a shadow reaches past the card it falls from");
            var rim = Ui.Sprite($"ReckoningOffer{index}Rim", "proc:card_rim", Place.At(0f, 0f),
                    UiSize.Fixed(cardWidth, cardWidth / RewardArt.CardBackAspect))
                .Coloured("#FFF2D94D")
                .AsDecor();
            button.Children.Add(underGlow);
            button.Children.Add(shadow);
            button.Children.Add(rays);
            button.Children.Add(flash);
            button.Children.Add(ring);
            OfferUnderGlows.Add(underGlow);
            OfferShadows.Add(shadow);
            OfferRims.Add(rim);
            OfferRays.Add(rays);
            OfferFlashes.Add(flash);
            OfferRings.Add(ring);

            // The face is one node so the controller can hide all of it while
            // the back shows: the back art keeps its own aspect and is narrower
            // than the card, so it cannot cover the face by size.
            // OPAQUE, so the cloth never shows through a face-up card: a dark
            // plate in the card's own shape, a quality-coloured wash over it
            // and a quality-coloured rim round it. The controller sets the two
            // colours; the plate stays one dark violet so the quality leads.
            var ground = Ui.Sprite($"ReckoningOffer{index}Ground", "proc:card_plate", Place.Stretch(), UiSize.Fill)
                .Coloured(FaceGround)
                .AsDecor();
            var faceTint = Ui.Sprite($"ReckoningOffer{index}FaceTint", "proc:radial_glow", Place.Stretch(), UiSize.Fill)
                .Coloured("#FFFFFF00")
                .AsDecor();
            var faceRim = Ui.Sprite($"ReckoningOffer{index}FaceRim", "proc:card_rim", Place.Stretch(), UiSize.Fill)
                .Coloured("#FFFFFF00")
                .AsDecor();
            OfferFaceTints.Add(faceTint);
            OfferFaceRims.Add(faceRim);

            var face = Ui.Panel($"ReckoningOffer{index}Face", Place.Stretch(), UiSize.Fill, new List<UiNode>
            {
                ground, faceTint, plinth, halo, burst, ghost, icon, chain, wearerDisc, wearerPortrait,
                band, bandTier, bandPlus, name, delta, deltaGlyph, chevron, feature, spark, faceRim,
            });
            button.Children.Add(face);
            OfferFaces.Add(face);

            // The card back, LAST, shown in the face's place until the card flips.
            var back = Ui.Sprite($"ReckoningOffer{index}Back", RewardArt.CardBackKey, Place.Stretch(), UiSize.Fill)
                .AsDecor()
                .Inactive();
            back.PreserveAspect = true;
            button.Children.Add(back);
            OfferBacks.Add(back);
            button.Children.Add(rim);

            OfferButtons.Add(button);
            OfferPlinths.Add(plinth);
            OfferHalos.Add(halo);
            OfferBursts.Add(burst);
            OfferGhosts.Add(ghost);
            OfferIcons.Add(icon);
            OfferChains.Add(chain);
            OfferWearerDiscs.Add(wearerDisc);
            OfferWearerPortraits.Add(wearerPortrait);
            OfferBands.Add(band);
            OfferBandTiers.Add(bandTier);
            OfferBandPluses.Add(bandPlus);
            OfferNames.Add(name);
            OfferDeltas.Add(delta);
            OfferDeltaGlyphs.Add(deltaGlyph);
            OfferChevrons.Add(chevron);
            OfferFeatures.Add(feature);
            OfferSparks.Add(spark);
            return button;
        }

        // "Hone instead", under its card. Fixed size at every card count: a
        // plate is a 3:1 or 5:1 art and stretching it with the row would bend
        // it, so only its x follows the card.
        private UiNode BuildHone(int index)
        {
            var size = Ui.PlateNominalSizeFor(HoneWidth, 40f);
            float x = OfferRowLayout.CardX(index, OfferRowLayout.MaxCards);
            float y = CardBottom - HoneGap - size.Y * 0.5f;

            var button = Ui.Button($"ReckoningHone{index}", UiString.Runtime, size, 14, Place.At(x, y))
                .Themed(ButtonTheme.Violet)
                .Quiet();

            // The whetstone, or the coins and the card's price when every shown
            // slot is spent; the controller swaps the glyph and seats the pair.
            var glyph = GlyphPart($"ReckoningHone{index}Glyph", -30f);
            var count = CountPart($"ReckoningHone{index}Count", 20f, 60f);
            button.Children.Add(glyph);
            button.Children.Add(count);
            HoneButtons.Add(button);
            HoneGlyphs.Add(glyph);
            HoneCounts.Add(count);
            return button;
        }

        // A glyph slot on a control's face: square, art swapped at runtime.
        private static UiNode GlyphPart(string name, float x)
        {
            var glyph = Ui.Sprite(name, null, Place.At(x, 0f), UiSize.Fixed(RewardArt.GlyphSize, RewardArt.GlyphSize))
                .AsDecor();
            glyph.PreserveAspect = true;
            return glyph;
        }

        // The number beside a glyph.
        private static UiNode CountPart(string name, float x, float width) =>
            Ui.Label(name, UiString.Runtime, new UiVec(width, 28f), 24, "#F2DB9E", Place.At(x, 0f))
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Styled(TypographyRole.TacticalData);
    }
}
