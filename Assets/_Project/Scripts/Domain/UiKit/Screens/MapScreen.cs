using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The descent map: where the party is, and where they may go next.
    //
    // The one screen whose contents genuinely are not known at build time -- a
    // realm is generated from a seed at runtime, so the nodes are a POOL sized
    // from MapLayout.MaxNodes, one slot per node id. The controller places each
    // used slot from MapGraph.
    //
    // It is also the one screen TALLER THAN THE CANVAS: entry at the top, heart
    // at the bottom, and the content rect sliding up under a mask while the
    // camera pins the current room near the top. See MapLayout for why every
    // coordinate is content-local.
    public sealed class MapScreen
    {
        // THE HUSH: a top-down forest strip that tiles vertically with no
        // mirroring (tools/ingest_map_art.py crossfades its seam), laid down the
        // leg. Its middle is plain floor so the graph reads on it.
        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/Processed/realm_hush_found.png";

        // The room tile: one medallion per room type, which is also the
        // button's plate. The shapes differ so a room reads in greyscale.
        public const string FightIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_fight.png";
        public const string EliteIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_elite.png";
        public const string BossIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_boss.png";
        public const string RestIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_rest.png";
        public const string EventIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_event.png";
        public const string TreasureIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_treasure.png";

        public const string ShopIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_shop.png";
        public const string CacheIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_cache.png";
        public const string EntryIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_node_entry.png";

        public const string ChipGoldKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_gold.png";
        public const string ChipPotionKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_potion.png";
        public const string ChipGearKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_gear.png";
        public const string ChipRelicKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_relic.png";
        public const string ChipBookKey = "Assets/_Project/Art/Backgrounds/Processed/map_chip_book.png";

        // The strip's eased twin: same pixels' framing, light and colour
        // changed, so MapEasing can show it through discs over the found one.
        public const string EasedBackgroundKey = "Assets/_Project/Art/Backgrounds/Processed/realm_hush_eased.png";

        // The boss's lair, laid under the heart at the bottom of the leg.
        public const string HeartBackdropKey = "Assets/_Project/Art/Backgrounds/Processed/realm_hush_heart.png";

        // The party's figure. Declared with Shawn because he is the only
        // character with actor art, and REPLACED at runtime from whoever is
        // leading the squad -- the controller resolves
        // CharacterDefinition.battleSpritePath through the same
        // StanceAnimationLibrary the fight stage uses, so a second character
        // needs a content entry and no code.
        //
        // His idle, specifically: there is no walk cycle in this project's art
        // for anybody. See MapWalk for what stands in for one.
        public const string WalkerKey = "Assets/_Project/Resources/Characters/sheep/idle.png";

        // Shawn's idle is 540x370. Only the DECLARED box needs this -- the
        // controller measures whatever sprite it actually resolves -- but a
        // declared box has to be some real shape for the audit to check, and
        // the wrong one would have the build checking a figure the game never
        // draws.
        private const float ShawnAspect = 540f / 370f;

        // The one place a room type maps to painted art. Null for anything not
        // drawn -- ItemSpawn and Unknown show a bare tree,
        // which is the house's graceful-degradation posture rather than a
        // missing-sprite magenta quad.
        public static string IconKeyFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Fight: return FightIconKey;
                case RoomType.EliteFight: return EliteIconKey;
                case RoomType.Boss: return BossIconKey;
                case RoomType.Rest: return RestIconKey;
                case RoomType.Event: return EventIconKey;
                case RoomType.Treasure: return TreasureIconKey;

                case RoomType.Shop: return ShopIconKey;
                case RoomType.Cache: return CacheIconKey;
                case RoomType.Entry: return EntryIconKey;
                default: return null;
            }
        }

        public UiNode Root;

        // The scrolling half. The viewport is the fixed window and does the
        // clipping; the content is the tall rect that slides under it.
        public NodeRef Viewport;
        public NodeRef Content;
        public List<NodeRef> Backdrops = new List<NodeRef>();
        public NodeRef Fog;

        // The eased wood laid over the found one (MapController.Easing.cs).
        public NodeRef EasedWood;
        public NodeRef HeartBackdrop;

        // One slot per node id.
        public List<NodeRef> NodeButtons = new List<NodeRef>();

        // The soft pulsing ring on a room that can be entered.
        public List<NodeRef> NodeGlows = new List<NodeRef>();

        // The fixed node grammar (plan section 7): pips for strength, three to
        // a node and flat in slot order, and one prize chip per node.
        public List<NodeRef> NodePips = new List<NodeRef>();
        public List<NodeRef> NodeChips = new List<NodeRef>();

        // The painted fog over a room the focused choice would close off.
        public List<NodeRef> NodeVeils = new List<NodeRef>();

        // Every straight piece of every trail. Flat rather than nested per
        // link, because the controller walks links in one pass and a jagged
        // array would only make the index arithmetic worse.
        public List<NodeRef> TrailSegments = new List<NodeRef>();

        // The lighter inner track of each segment, one per TrailSegments entry
        // and in the same order. Tinted alongside its verge, so a road cannot
        // end up with a walked surface inside an unreachable edge.
        public List<NodeRef> TrailCores = new List<NodeRef>();

        // The roots joining each anchor to the heart, the same two layers as a
        // trail.
        public List<NodeRef> RootSegments = new List<NodeRef>();
        public List<NodeRef> RootCores = new List<NodeRef>();

        // The heart's anchors: portraits and their names, the front slots then
        // the queue (MapGraph.SlotCentre).
        public List<NodeRef> HeartPortraits = new List<NodeRef>();
        public List<NodeRef> HeartNames = new List<NodeRef>();

        public NodeRef Walker;

        public NodeRef TitleLabel;
        public NodeRef GoldLabel;
        public NodeRef GoldIcon;
        public NodeRef BankedLabel;
        public NodeRef BankedIcon;
        public NodeRef RoomMessageLabel;
        public NodeRef OverviewLabel;

        // The same overlay the hub and the fight mount. Live here:
        // between rooms is when gear is meant to change.
        public SystemMenuScreen SystemMenu;

        // The in-run shop, entered from a Shop room (F10, docs/PLAN_SHOP.md).
        // Nested here the same way the draft nests in the hub -- entered from
        // this screen, no scene of its own. Declared BEFORE the system menu
        // among Root's children (below), so the menu can be opened from
        // inside the shop (a player checking equipment/skills mid-shop) and
        // draws ON TOP of it -- the ordering AUDIT.md #173's Shop line calls
        // out as the gap.
        public ShopScreen Shop;

        // An event room's panel, nested exactly as the shop is and for the
        // same reason; declared before the system menu among Root's children
        // so Start opens the menu OVER an event, as it does over the shop.
        public EventScreen Event;

        // The honing modal: the shop's HONE opens it over the shop, a Rest
        // room opens it with a HEAL beside the rows. Before the system menu
        // among Root's children for the same reason as the shop.
        public HoneScreen Hone;

        // The debug menu, the hub's own copy mounted here too (F1, dev
        // builds only): spell books, relics and every Tools row act on the
        // descent, and the hub has none to act on.
        public DebugMenuScreen Debug;

        // Anchored to the content rect's TOP CENTRE, which is the origin every
        // MapLayout coordinate is measured from. Declared once here so no
        // construction site restates it and drifts.
        private static readonly UiVec ContentTop = new UiVec(0.5f, 1f);

        private static Place OnContent(float x, float y) =>
            Place.Pin(ContentTop, UiVec.Centre, new UiVec(x, y));

        // partyRosterCardCount and systemMenuLabelWidths thread straight through to
        // SystemMenuScreen.
        // Build -- see HubScreen.Build's identical parameter for why it
        // defaults to 3 and who is meant to override it.
        public static MapScreen Build(
            int partyRosterCardCount = 3, SystemMenuLabelWidths systemMenuLabelWidths = null)
        {
            var screen = new MapScreen();

            // THE TOP-RIGHT STACK: floor and room, gold, then the overview
            // hint, right-aligned down the right edge over the bare wood. Every
            // element is pinned to the TOP edge, so a frame that is not 16:9
            // does not drift them. Heights are tight so the stack ends above
            // MapRoomMessageLabel (top at -160).
            //
            // OverArt(backing: false): a dark ring on the glyphs and no plate,
            // because the text sits straight on the painted wood.
            //
            // FunctionalHeading, not CeremonialTitle -- the map is a working
            // screen the player passes through every room, not a title or a
            // ceremonial moment the way Defeat/Reckoning's own titles are.
            // Wide enough for the worst case, FLOOR 99 and ROOM 999 on one line;
            // the box grows left from the fixed right edge (x 920).
            var title = Ui.Label("MapTitleLabel", UiStrings.MapTitle, new UiVec(660f, 56f), 40, "#EDE6FF",
                OnContent(590f, -42f)).TextAligned(UiTextAlign.Right).OverArt(backing: false)
                .Styled(TypographyRole.FunctionalHeading);
            var gold = Ui.Label("MapGoldLabel", UiStrings.MapGold, new UiVec(200f, 32f), 22, "#F2DB9E",
                OnContent(820f, -90f)).TextAligned(UiTextAlign.Right).OverArt(backing: false)
                .Styled(TypographyRole.TacticalData);

            // The coin before the amount. The controller slides it to the left
            // of the right-aligned text, whose width is a runtime fact.
            var goldIcon = Ui.Sprite("MapGoldIcon", ChipGoldKey, OnContent(850f, -90f),
                    UiSize.Fixed(GoldIconSize, GoldIconSize))
                .AsDecor();

            // Banked gold (the Cache room) reads the same way, to the left of the
            // purse: the cache's own icon and the amount. Hidden while nothing is
            // banked; the controller slides both to the purse's left edge.
            var banked = Ui.Label("MapBankedLabel", UiStrings.MapGold, new UiVec(200f, 32f), 22, "#F2DB9E",
                OnContent(500f, -90f)).TextAligned(UiTextAlign.Right).OverArt(backing: false)
                .Styled(TypographyRole.TacticalData);
            var bankedIcon = Ui.Sprite("MapBankedIcon", CacheIconKey, OnContent(380f, -90f),
                    UiSize.Fixed(GoldIconSize, GoldIconSize))
                .AsDecor();

            // What the room the party just walked into did.
            //
            // Sits UNDER the top-right stack rather than over the wood, because it
            // is a heading about the descent's state and not a label on
            // anything in the scene.
            //
            // Sized for the longest line any room can produce -- the unbuilt
            // shop's apology -- so a placeholder message cannot overflow a box
            // fitted to the short ones.
            var roomMessage = Ui.Label("MapRoomMessageLabel", UiStrings.MapRoomShop,
                new UiVec(760f, 32f), 18, "#C8BBE4", OnContent(190f, -176f))
                .Styled(TypographyRole.Body);

            // The hint for Y, and the overview's own banner in its place while
            // it is up; the same label, its text swapped by the controller.
            var overview = Ui.Label("MapOverviewLabel", UiStrings.MapOverviewHint, new UiVec(460f, 24f), 18,
                    FightHudPalette.GoldLight, OnContent(690f, -122f))
                .TextAligned(UiTextAlign.Right)
                .OverArt(backing: false)
                .Styled(TypographyRole.TacticalData);
            screen.OverviewLabel = overview;

            screen.TitleLabel = title;
            screen.GoldLabel = gold;
            screen.GoldIcon = goldIcon;
            screen.BankedLabel = banked;
            screen.BankedIcon = bankedIcon;
            screen.RoomMessageLabel = roomMessage;

            // The painted wood, tiled DOWN the leg; the strip tiles by itself.
            var backdrops = Ui.Pool("MapBackdrops", MapLayout.MaxBackgroundTiles, i => screen.BuildBackdrop(i))
                .AsDecor();

            var trails = Ui.Pool("MapTrails", MapLayout.SegmentCapacity, i => screen.BuildTrailSegment(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var roots = Ui.Pool("MapRoots", MapLayout.RootSegmentCapacity, i => screen.BuildRootSegment(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var nodes = Ui.Pool("MapNodes", MapLayout.MaxNodes, i => screen.BuildNode(i))
                .AllowOverlap("an invisible pool container spans the panel by construction and has no graphic to intercept anything");

            var heartPortraits = Ui.Pool("MapHeartPortraits", MapGraph.HeartSlots, i => screen.BuildHeartPortrait(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var heartNames = Ui.Pool("MapHeartNames", MapGraph.HeartSlots, i => screen.BuildHeartName(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var easedWood = screen.BuildEasedWood();
            var heartBackdrop = screen.BuildHeartBackdrop();
            var walker = screen.BuildWalker();
            var fog = screen.BuildFog();

            // BACKDROP, then the EASED WOOD over it, then the HEART's lair (over
            // the eased wood, which would otherwise paint the strip across it),
            // then TRAILS, then ROOTS, then NODES, then the HEART's
            // anchors, then the WALKER, then FOG. uGUI paints siblings in order
            // and this is the whole back-to-front stack: a path crossing over
            // the room it leads to would read as the rooms being behind the map
            // rather than on it, the figure walks IN FRONT of the wood it is
            // walking through, and the fog has to cover the forest it is
            // fading out.
            var content = Ui.Panel("MapContent",
                    Place.Pin(ContentTop, ContentTop, UiVec.Zero),
                    UiSize.Fixed(UiFrames.Reference.X, MapLayout.MaxContentHeight),
                    backdrops, easedWood, heartBackdrop, trails, roots, nodes, heartPortraits, heartNames, walker, fog)
                .AllowOverflow("the content rect is deliberately taller than the window it sits in - that overflow IS the scroll, and MapViewport clips it");

            screen.Content = content;

            // The window. Full-bleed and clipping, with no graphic of its own:
            // it is a coordinate frame and a mask, never a surface.
            // The transparent catcher behind the content takes a press on bare
            // wood, so a drag that starts there reaches the viewport (the
            // decor sprites take no raycasts and would swallow the press).
            var dragCatcher = Ui.Solid("MapDragCatcher", "#00000000", Place.Stretch(), UiSize.Fill)
                .AllowOverlap("a transparent full-bleed catcher for the drag-to-scroll press; it draws nothing");
            var viewport = Ui.Panel("MapViewport", Place.Stretch(), UiSize.Fill, dragCatcher, content)
                .Clipping()
                .AllowOverlap("a full-bleed viewport spans the headings it shares the panel with; it draws nothing and has no graphic to intercept a click");

            screen.Viewport = viewport;

            // A soft dark gradient hugging the four screen edges, fixed to the
            // screen (a sibling of the viewport, not a child of the scrolling
            // content), over the map and under the text and the modals.
            var vignette = Ui.Sprite("MapVignette", VignetteKey, Place.Stretch(), UiSize.Fill)
                .Coloured(VignetteHex)
                .AsDecor();

            // The character sheet, reachable between rooms.
            //
            // NOT locked here, unlike the fight's copy: the map is exactly
            // where changing your gear is supposed to happen. Walking back to
            // the hub to swap a breastplate between two rooms was the thing
            // that made having it in the fight only half an answer.
            // The overarching menu -- built here, but placed in Root's
            // children AFTER shop.Root below, so it draws over everything it
            // can be opened on top of, the shop included (see Shop's own
            // field comment above for why that is now true and was not
            // before).
            var systemMenu = SystemMenuScreen.Build(partyRosterCardCount, systemMenuLabelWidths);
            screen.SystemMenu = systemMenu;

            var shop = ShopScreen.Build();
            screen.Shop = shop;

            var eventRoom = EventScreen.Build();
            screen.Event = eventRoom;

            var hone = HoneScreen.Build();
            screen.Hone = hone;

            // The old paperdoll is gone; the system menu's Character pane is
            // the character screen now. SheetPanel opens the menu instead, so
            // every caller -- C, I, Escape, the hub's building -- reaches the
            // dossier without any of them knowing the screen changed.

            // Viewport FIRST so the headings draw over
            // the scrolling wood rather than under it. SHOP then SYSTEM MENU,
            // last of all -- the menu has to draw over the shop it can now be
            // opened on top of (AUDIT.md #173's Shop line), the same "opened
            // FROM here, drawn OVER everything" rule the hub and the fight
            // already apply to their own nested modals.
            // DEBUG LAST: it is opened over whatever else is up, the system
            // menu included, and has to draw over it.
            var debug = DebugMenuScreen.Build();
            screen.Debug = debug;

            screen.Root = Ui.Panel("MapPanel", UiSize.Fill,
                viewport, vignette, title, gold, goldIcon, banked, bankedIcon,
                roomMessage, overview, shop.Root, eventRoom.Root, hone.Root, systemMenu.Root, debug.Root);

            return screen;
        }

        // One tile of the painted wood, pinned by its own top edge so tile i
        // starts exactly where the leg's tiling says it should.
        private UiNode BuildBackdrop(int index)
        {
            // Fixed to the art's own canvas. Fill would take the parent's size,
            // and the parent is the whole scrolling leg: one tile of the forest
            // stretched over thousands of units, which is the "zoomed in forest"
            // this screen once shipped (MapScreenTests).
            var tile = Ui.Sprite($"MapBackdrop{index}", BackgroundKey,
                    Place.Pin(ContentTop, ContentTop, new UiVec(0f, MapLayout.BackgroundTileY(index))),
                    UiSize.Fixed(MapLayout.BackgroundTileWidth, MapLayout.BackgroundTileHeight))
                .AsDecor()
                .AllowOverflow("the last tile deliberately overhangs the content it fills - a forest that stopped exactly at the content edge would show a hard cut");

            // Declared ACTIVE, unlike every other pool member here, and the
            // controller only ever hides the surplus. A wood that had to wait
            // for a run to exist before it drew anything left the resting
            // screenshot a black rectangle -- and a screen that renders as
            // nothing at rest is exactly what the capture tool exists to catch,
            // so it cannot be the normal state of this one.
            Backdrops.Add(tile);
            return tile;
        }

        // The same painting again, over the whole leg in ONE image: the
        // controller gives it the easing material, which tiles the painting down
        // the leg itself and shows it only inside the discs MapEasing lays
        // around the rooms walked. Declared inactive: with the stock material it
        // would paint the whole painting at full strength, so the controller
        // shows it once the easing material is on.
        private UiNode BuildEasedWood()
        {
            var eased = Ui.Sprite("MapEasedWood", EasedBackgroundKey,
                    Place.Pin(ContentTop, ContentTop, UiVec.Zero),
                    UiSize.Fixed(MapLayout.BackgroundTileWidth, MapLayout.MaxContentHeight))
                .AsDecor()
                .AllowOverflow("the eased wood spans the whole leg, as tall as the content rect it sits in")
                .Inactive();

            EasedWood = eased;
            return eased;
        }

        // The lair under the heart, hung from the content's bottom edge; the
        // controller places it, because the content height is per realm. Its
        // top edge is baked to fade, so it meets the strip without a seam.
        private UiNode BuildHeartBackdrop()
        {
            var lair = Ui.Sprite("MapHeartBackdrop", HeartBackdropKey,
                    Place.Pin(ContentTop, ContentTop, UiVec.Zero),
                    UiSize.Fixed(MapLayout.HeartBackdropWidth, MapLayout.HeartBackdropHeight))
                .AsDecor()
                .AllowOverflow("the lair hangs from the content's bottom edge and may rise above the heart row")
                .Inactive();

            HeartBackdrop = lair;
            return lair;
        }

        // The party, standing in the wood. Declared at the entrance's own
        // standing spot and moved from there; sized off Shawn's 540x370 idle,
        // with the controller re-deriving the width from whatever art the
        // actual squad leader turns out to have.
        private UiNode BuildWalker()
        {
            var entrance = MapWalk.Standing(new UiVec(0f, -MapLayout.TopMargin));

            var walker = Ui.Sprite("MapWalker", WalkerKey,
                    OnContent(entrance.X, entrance.Y),
                    UiSize.Fixed(MapWalk.FigureHeight * ShawnAspect, MapWalk.FigureHeight))
                .AsDecor()
                .AllowOverflow("the figure stands beside its tile and overhangs the canopy on purpose - so do the trails it walks along")
                .Inactive();

            Walker = walker;
            return walker;
        }

        // The fade at the end of the wood, below the heart. Declared at the
        // bottom of the tallest leg; the controller moves it to the real one.
        private UiNode BuildFog()
        {
            var label = Ui.Label("MapFogLabel", UiStrings.MapFog, new UiVec(320f, 80f), 15, "#6B6180",
                Place.At(0f, 0f));

            // A fade, not a slab: scrim_floor is clear at its top and darkest
            // at its bottom, so the painted wood runs under it to the very edge
            // of the leg. A flat colour here read as the backdrop ending a
            // quarter of the way up the screen.
            var fog = Ui.Sprite("MapFog", "proc:scrim_floor",
                    OnContent(0f, -MapLayout.MaxContentHeight + FogHeight * 0.5f),
                    UiSize.Fixed(UiFrames.Reference.X, FogHeight))
                .Coloured("#040503EB")
                .AsDecor();

            fog.Children.Add(label);

            Fog = fog;
            return fog;
        }

        public const float FogHeight = 280f;

        // One straight piece of one trail. Declared at the top with a
        // placeholder size because a segment's real position, length and
        // rotation all depend on which two rooms a realm happened to generate --
        // the same arrangement the fight screen's VFX and damage-popup pools
        // use, and the reason the pool rather than the member carries the audit
        // exemption.
        // A ROAD, not a line.
        //
        // Each piece is two layers: the outer rect is the VERGE, the churned
        // dark earth a path pushes aside, and the inner one is the TRACK, the
        // dust actually walked on. One flat brown rectangle reads as a diagram
        // connecting two icons; a lighter core inside a darker edge reads as
        // something with a surface, and it costs one extra decor node per
        // segment.
        //
        // The core is placed FRACTIONALLY, not with a pixel inset. Segment
        // thickness is set at runtime and ranges from 6 to 22 depending on the
        // trail's state, so a fixed inset would leave the closed trails with no
        // verge at all and the walked ones with a hairline of one.
        private UiNode BuildTrailSegment(int index)
        {
            // NAMED OFF THE PREFIX ON PURPOSE. MapFlowTests finds every drawn
            // piece by the "MapTrail" prefix and asserts each has real geometry
            // -- and this one has none, because it is placed fractionally
            // inside its parent rather than sized. Calling it MapTrail...Core
            // swept it into that filter and failed the test, correctly: it is
            // not a segment, it is a layer of one.
            var core = Ui.Sprite($"MapTrack{index}", TrailStrokeKey,
                    Place.Frac(new UiVec(0f, 0.24f), new UiVec(1f, 0.76f)), UiSize.Fill)
                .AsDecor();

            var segment = Ui.Sprite($"MapTrail{index}", TrailStrokeKey,
                    OnContent(0f, -MapLayout.TopMargin), UiSize.Fixed(8f, 8f))
                .AsDecor()
                .Inactive();

            segment.Children.Add(core);

            TrailSegments.Add(segment);
            TrailCores.Add(core);
            return segment;
        }

        // A root: the trail's two layers in living wood. "MapRoot" and "MapGrip"
        // stay off the "MapTrail" prefix the trail tests count.
        private UiNode BuildRootSegment(int index)
        {
            var core = Ui.Solid($"MapGrip{index}", RootCoreLivingHex,
                    Place.Frac(new UiVec(0f, 0.3f), new UiVec(1f, 0.7f)), UiSize.Fill)
                .AsDecor();

            var segment = Ui.Solid($"MapRoot{index}", RootLivingHex,
                    new UiVec(8f, 8f), OnContent(0f, -MapLayout.TopMargin))
                .AsDecor()
                .Inactive();

            segment.Children.Add(core);

            RootSegments.Add(segment);
            RootCores.Add(core);
            return segment;
        }

        // The four states a trail can be in. Colours rather than one tint at
        // four alphas: a walked path and an unreachable one differ in what they
        // MEAN, and the eye separates hue faster than it separates opacity.
        //
        // The verge half, which is the darker outer edge.
        // Soft-edged stroke the controller tints per state; both layers of a
        // road use it so neither ends on a hard line.
        public const string TrailStrokeKey = "proc:trail_stroke";
        public const string PipKey = "proc:pip_round";
        public const string GlowRingKey = "proc:node_glow_ring";
        public const string VignetteKey = "proc:vignette_band";

        // Black at 40%: a filter on the edges, not a frame.
        public const string VignetteHex = "#00000066";
        public const float GoldIconSize = 26f;

        public const string TrailTakenHex = "#3A2312";
        public const string TrailOpenHex = "#462D19F2";
        public const string TrailAheadHex = "#2E1C108C";
        public const string TrailClosedHex = "#1C110D4D";

        // The track half: sunlit dust on a forest floor, warmer and lighter
        // than the earth around it. Each keeps its state's own alpha, so a
        // closed road is still faint all the way through rather than a bright
        // core inside a ghost.
        public const string TrailCoreTakenHex = "#9A7748";
        public const string TrailCoreOpenHex = "#B98F55F2";
        public const string TrailCoreAheadHex = "#6E5537A0";
        public const string TrailCoreClosedHex = "#3F332552";

        // A root while its anchor stands, and once it has fallen.
        public const string RootLivingHex = "#1F2E14F2";
        public const string RootCoreLivingHex = "#6F8F3CF2";
        public const string RootWitheredHex = "#2A2420A0";
        public const string RootCoreWitheredHex = "#4A4038A0";

        // A road has to be thick enough for the verge to read AS a verge --
        // at 7px an "ahead" trail has only two pixels of edge and two of
        // core, so the other states are wider.
        public const float TrailWidthTaken = 24f;
        public const float TrailWidthOpen = 20f;
        public const float TrailWidthAhead = 12f;
        public const float TrailWidthClosed = 7f;

        public const float RootWidthLiving = 18f;
        public const float RootWidthWithered = 8f;

        // The per-state tile tints live in MapController, not here, and
        // deliberately break the convention the trail colours above follow.
        // Nothing in this tree declares a tile's resting colour -- every tile
        // is painted by state on the first Refresh -- and "current" has to be
        // brighter than the sprite's own colour, which a hex string cannot say
        // and a Color above 1.0 can.

        // The heart's anchor names, and the colour a tile's plate flashes to on a
        // refused press (MapController.Refuse).
        public const string CaptionHex = "#EDE6FF";
        public const string RefusalHex = "#E0786E";

        // The pips: gold on the tile's own canopy.
        public const string PipHex = "#F2DB9E";

        // The ring on a room that can be entered; the controller pulses its alpha.
        public const string GlowHex = "#F2DB9E";

        // The fog drawn over a room a focused choice would close off.
        public const string VeilHex = "#0B0813B8";

        private UiNode BuildNode(int index)
        {
            // Declared at the WIDEST a tile can be, because which slot holds the
            // boss is a runtime fact. The controller shrinks it per type; the
            // audit therefore checks a box at least as big as the real one.
            var size = new UiVec(MapLayout.MaxTileWidth, MapLayout.MaxTileHeight);

            // Strength as pips along the tile's top edge, inside it.
            var pips = new List<UiNode>();
            for (int p = 0; p < NodeStrength.Max; p++)
            {
                var pip = Ui.Sprite($"MapNode{index}Pip{p}", PipKey,
                        Place.At((p - (NodeStrength.Max - 1) * 0.5f) * MapLayout.PipPitch, size.Y * 0.5f - 18f),
                        UiSize.Fixed(MapLayout.PipSize, MapLayout.PipSize))
                    .AsDecor()
                    .Inactive();
                pips.Add(pip);
                NodePips.Add(pip);
            }

            // The prize icon beside the tile, no plate and no words; its size
            // follows strength, set at runtime, and its sprite follows the kind.
            var chip = Ui.Sprite($"MapNode{index}Chip", ChipGoldKey,
                    Place.At(size.X * 0.5f + MapLayout.ChipGap + MapLayout.MaxChipHeight * 0.5f, -20f),
                    UiSize.Fixed(MapLayout.MaxChipHeight, MapLayout.MaxChipHeight))
                .AsDecor()
                .AllowOverflow("the chip stands beside its tile so the icon stays readable")
                .Inactive();

            // The fog over the whole tile when a focused choice would close
            // this room off.
            // The medallion again, tinted by the controller (VeilHex) rather than
            // a flat rect, so the fog keeps the tile's own silhouette.
            var veil = Ui.Sprite($"MapNode{index}Veil", FightIconKey, Place.Stretch(0f, 0f, 0f, 0f), UiSize.Fill)
                .AsDecor()
                .Inactive();

            var button = Ui.Button($"MapNode{index}", UiString.Runtime, size, 12,
                OnContent(0f, -MapLayout.TopMargin));

            // The medallion IS the button's plate, so the tile the player
            // clicks and the tile they see are one graphic; the controller swaps
            // the sprite per room type. Without this the emitter would put its
            // shared gold button frame behind every room.
            button.SpriteKey = FightIconKey;

            // The default hover/focus box is a hard white outline; a room that
            // can be chosen gets a soft glow instead. Named "HoverRim" so
            // UiEmitter wires it into the same HoverBox the default rim uses.
            Ui.RemoveDefaultHoverBox(button);
            button.Children.Add(Ui.Sprite("HoverRim", GlowRingKey,
                    Place.Stretch(-MapLayout.GlowPad, -MapLayout.GlowPad, -MapLayout.GlowPad, -MapLayout.GlowPad),
                    UiSize.Fill)
                .AsDecor()
                .AllowOverflow("the glow ring reaches past the tile (MapLayout.GlowPad) so it reads as light around the room")
                .Inactive());

            // The ambient ring: on every room that can be entered, softly
            // pulsing. The HoverRim above is the stronger, steady state for
            // the room under the pointer or the focus.
            var glow = Ui.Sprite($"MapNode{index}Glow", GlowRingKey,
                    Place.Stretch(-MapLayout.GlowPad, -MapLayout.GlowPad, -MapLayout.GlowPad, -MapLayout.GlowPad),
                    UiSize.Fill)
                .Coloured(GlowHex)
                .AsDecor()
                .AllowOverflow("the glow ring reaches past the tile (MapLayout.GlowPad) so it reads as light around the room")
                .Inactive();

            button.Children.Add(glow);
            foreach (var pip in pips) button.Children.Add(pip);
            button.Children.Add(veil);
            button.Children.Add(chip);

            NodeButtons.Add(button);
            NodeGlows.Add(glow);
            NodeChips.Add(chip);
            NodeVeils.Add(veil);

            // Hidden until a realm says otherwise: most of the pool is empty in
            // any given realm, and an inactive node cannot be clicked by accident.
            return button.Inactive();
        }

        // Where an anchor stands at the heart: its portrait, in the slot
        // MapGraph.SlotCentre gives. The art is the anchor's own idle, swapped
        // in at runtime.
        private UiNode BuildHeartPortrait(int index)
        {
            var portrait = Ui.Sprite($"MapHeartPortrait{index}", EliteIconKey,
                    OnContent(0f, -MapLayout.TopMargin),
                    UiSize.Fixed(MapGraph.FrontWidth, MapGraph.FrontWidth))
                .AsDecor()
                .AllowOverflow("an anchor's idle is drawn wider than its slot's box and overhangs the wood")
                .Inactive();

            HeartPortraits.Add(portrait);
            return portrait;
        }

        private UiNode BuildHeartName(int index)
        {
            var name = Ui.Label($"MapHeartName{index}", UiString.Runtime,
                    new UiVec(MapGraph.FrontWidth, MapLayout.CaptionHeight), 13, CaptionHex,
                    OnContent(0f, -MapLayout.TopMargin))
                .Inactive();

            HeartNames.Add(name);
            return name;
        }
    }
}
