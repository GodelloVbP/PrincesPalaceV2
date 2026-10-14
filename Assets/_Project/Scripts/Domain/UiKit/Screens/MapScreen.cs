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
        // A FOREST FLOOR: the top-down canopy painting, tiled down the leg with
        // every second tile mirrored (MapLayout.BackgroundTileIsMirrored). Its
        // painted clearings were drawn for the old horizontal map and are not
        // aligned to the graph; a vertical painting is an art-brief item.
        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/forest_map_background.png";

        // The room tile itself: one painted tree, shared by every room, with
        // the type icon standing in the clearing beneath its canopy. Six of the
        // seven were committed with the backdrop and referenced by nothing for
        // as long as this screen existed in v2.
        public const string TreeKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_room.png";
        public const string FightIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_mob.png";
        public const string EliteIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_mob_elite.png";
        public const string BossIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_mob_boss.png";
        public const string RestIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_rest.png";
        public const string EventIconKey = "Assets/_Project/Art/Backgrounds/Processed/map_forest_event.png";
        public const string TreasureIconKey = "Assets/_Project/Art/Backgrounds/Processed/forest_map_chest.png";

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
        // painted yet -- Shop, Entry, ItemSpawn and Unknown show a bare tree,
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

                // No painted medallion yet: the chest stands in (art brief).
                case RoomType.Cache: return TreasureIconKey;
                default: return null;
            }
        }

        // How much of a tile's width the icon takes. Bigger rooms get a
        // proportionally bigger icon rather than a fixed one, so an elite reads
        // as more than a fight at a glance and not merely as a wider tree.
        public static float IconFractionFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Boss: return 0.62f;
                case RoomType.EliteFight: return 0.56f;
                default: return 0.48f;
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

        // One slot per node id.
        public List<NodeRef> NodeButtons = new List<NodeRef>();
        public List<NodeRef> NodeIcons = new List<NodeRef>();
        public List<NodeRef> NodeLabels = new List<NodeRef>();
        public List<NodeRef> NodeMarkers = new List<NodeRef>();

        // The fixed node grammar (plan section 7): pips for strength, three to
        // a node and flat in slot order, and one prize chip per node.
        public List<NodeRef> NodePips = new List<NodeRef>();
        public List<NodeRef> NodeChips = new List<NodeRef>();
        public List<NodeRef> NodeChipTexts = new List<NodeRef>();

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
        public NodeRef DepthLabel;
        public NodeRef GoldLabel;
        public NodeRef AbandonButton;
        public NodeRef RoomMessageLabel;
        public NodeRef OverviewLabel;

        // "Somebody wants looking at": how many characters carry a pip in the
        // CHARACTER tab (plan C20). Shown whenever that is more than none; a
        // book bought, a level gained or a reward earned is otherwise silent on
        // the map.
        public NodeRef AttentionLabel;

        // The frame the hint sits in. Shown and hidden with the label, which
        // keeps its own active state so "nothing waits" is still the label's.
        public NodeRef AttentionBadge;

        // The detail card for the hovered or focused room, and a second one
        // beside it while two routes are open. Inactive until a room is
        // pointed at; the controller fills the labels from MapNodeCard.
        public NodeRef PreviewPanel;
        public NodeRef PreviewTitle;
        public NodeRef PreviewBody;
        public NodeRef ComparePanel;
        public NodeRef CompareTitle;
        public NodeRef CompareBody;

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

        // The debug menu, the hub's own copy mounted here too (F1, dev
        // builds only): spell books, relics and every Tools row act on the
        // descent, and the hub has none to act on.
        public DebugMenuScreen Debug;

        // The bottom strip, under the lowest row the camera shows: the detail
        // card, or the pair of them.
        private const float CardY = -442f;
        private static readonly UiVec CardSize = new UiVec(700f, 176f);
        private const float CardGap = 20f;

        private static readonly UiVec AttentionBadgeSize = new UiVec(560f, 88f);

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

            // THE TOP BAND (plan section 7): the floor, the realm's name with the
            // rooms walked, and the gold.
            //
            // FunctionalHeading, not CeremonialTitle -- the map is a working
            // screen the player passes through every room, not a title or a
            // ceremonial moment the way Defeat/Reckoning's own titles are.
            var title = Ui.Label("MapTitleLabel", UiStrings.MapTitle, new UiVec(700f, 60f), 40, "#EDE6FF",
                Place.At(0f, 452f)).Styled(TypographyRole.FunctionalHeading);
            var depth = Ui.Label("MapDepthLabel", UiStrings.MapDepth, new UiVec(420f, 34f), 20, "#B8A8D9",
                Place.At(0f, 404f)).Styled(TypographyRole.TacticalData);
            var gold = Ui.Label("MapGoldLabel", UiStrings.MapGold, new UiVec(460f, 40f), 22, "#F2DB9E",
                Place.At(690f, 452f)).Styled(TypographyRole.TacticalData);

            // THE BOOK/POINT/REWARD NUDGE, as a badge and not a line: neither
            // the shop nor the menu is guaranteed to be the next thing the
            // player opens, so the map is where a pending book, point or
            // reward has to say so, and a 16 px line at the top corner was
            // missed (AUDIT #254). It sits under the abandon button, out of the
            // bottom strip the detail cards use. 520 wide at Body 26 wraps the
            // three-reason worst case onto two lines inside a 560x88 frame.
            var attention = Ui.Label("MapAttentionLabel", UiStrings.MapAttention, new UiVec(520f, 72f), 26,
                    FightHudPalette.GoldLight, Place.At(0f, 0f))
                .TextAligned(UiTextAlign.Centre)
                .Styled(TypographyRole.Body)
                .Inactive();
            var attentionBadge = Ui.OutlineBox("MapAttentionBadge", Place.At(-660f, 372f), AttentionBadgeSize,
                FightHudPalette.PanelPrimary, FightHudPalette.BorderGold, new[] { attention })
                .Inactive();
            screen.AttentionBadge = attentionBadge;
            screen.AttentionLabel = attention;

            // SILVER: leaving the descent, matching Cancel/Back everywhere
            // else in the kit. Room-node buttons are NOT themed here -- the
            // painted tree IS their plate (button.SpriteKey = TreeKey below),
            // so a theme's plate would replace the bespoke art rather than
            // dress a bare frame.
            // 240x60 was 4:1 against the FiveByOne plate's true 5:1 --
            // ThemedButtonAspectLintTests. Height down to nominal, width kept.
            var abandonSize = Ui.PlateNominalSizeFor(240f, 60f);
            var abandon = Ui.Button("AbandonRunButton", UiStrings.MapAbandon, abandonSize, 16,
                    Place.At(-800f, 452f))
                .Themed(ButtonTheme.Silver);

            // What the room the party just walked into did.
            //
            // Sits UNDER the depth line rather than over the wood, because it
            // is a heading about the descent's state and not a label on
            // anything in the scene. Beside the attention badge, not over it.
            //
            // Sized for the longest line any room can produce -- the unbuilt
            // shop's apology -- so a placeholder message cannot overflow a box
            // fitted to the short ones.
            var roomMessage = Ui.Label("MapRoomMessageLabel", UiStrings.MapRoomShop,
                new UiVec(760f, 32f), 18, "#C8BBE4", Place.At(190f, 364f))
                .Styled(TypographyRole.Body);

            // The hint for Y, and the overview's own banner in its place while
            // it is up; the same label, its text swapped by the controller.
            var overview = Ui.Label("MapOverviewLabel", UiStrings.MapOverviewHint, new UiVec(460f, 28f), 18,
                    FightHudPalette.GoldLight, Place.At(660f, 410f))
                .TextAligned(UiTextAlign.Right)
                .Styled(TypographyRole.TacticalData);
            screen.OverviewLabel = overview;

            // The cards for whichever room is hovered or focused, in the bottom
            // strip. Inspecting never commits: a card shows for any visible
            // room, reachable or not, so a route can be planned. The second
            // card appears while two routes are open, so the choice reads side
            // by side.
            var previewPanel = BuildCard("MapPreview", -(CardSize.X + CardGap) * 0.5f,
                out var previewTitle, out var previewBody);
            screen.PreviewPanel = previewPanel;
            screen.PreviewTitle = previewTitle;
            screen.PreviewBody = previewBody;
            var comparePanel = BuildCard("MapCompare", (CardSize.X + CardGap) * 0.5f,
                out var compareTitle, out var compareBody);
            screen.ComparePanel = comparePanel;
            screen.CompareTitle = compareTitle;
            screen.CompareBody = compareBody;

            screen.TitleLabel = title;
            screen.DepthLabel = depth;
            screen.GoldLabel = gold;
            screen.AbandonButton = abandon;
            screen.RoomMessageLabel = roomMessage;

            // The painted wood, tiled DOWN the leg. Every second tile is
            // mirrored top to bottom by the controller so neighbouring edges
            // match.
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
            var walker = screen.BuildWalker();
            var fog = screen.BuildFog();

            // BACKDROP, then the EASED WOOD over it, then TRAILS, then ROOTS, then NODES, then the HEART's
            // anchors, then the WALKER, then FOG. uGUI paints siblings in order
            // and this is the whole back-to-front stack: a path crossing over
            // the room it leads to would read as the rooms being behind the map
            // rather than on it, the figure walks IN FRONT of the wood it is
            // walking through, and the fog has to cover the forest it is
            // fading out.
            var content = Ui.Panel("MapContent",
                    Place.Pin(ContentTop, ContentTop, UiVec.Zero),
                    UiSize.Fixed(UiFrames.Reference.X, MapLayout.MaxContentHeight),
                    backdrops, easedWood, trails, roots, nodes, heartPortraits, heartNames, walker, fog)
                .AllowOverflow("the content rect is deliberately taller than the window it sits in - that overflow IS the scroll, and MapViewport clips it");

            screen.Content = content;

            // The window. Full-bleed and clipping, with no graphic of its own:
            // it is a coordinate frame and a mask, never a surface.
            var viewport = Ui.Panel("MapViewport", Place.Stretch(), UiSize.Fill, content)
                .Clipping()
                .AllowOverlap("a full-bleed viewport spans the headings it shares the panel with; it draws nothing and has no graphic to intercept a click");

            screen.Viewport = viewport;

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

            // The old paperdoll is gone; the system menu's Character pane is
            // the character screen now. SheetPanel opens the menu instead, so
            // every caller -- C, I, Escape, the hub's building -- reaches the
            // dossier without any of them knowing the screen changed.

            // Viewport FIRST so the headings and the abandon button draw over
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
                viewport, title, depth, gold, attentionBadge, previewPanel, comparePanel, abandon,
                roomMessage, overview, shop.Root, eventRoom.Root, systemMenu.Root, debug.Root);

            return screen;
        }

        // One detail card: a gold-rimmed violet plate with a heading and up to
        // five body lines (enemies, pay and prize, the anchor's promise, the
        // skip heal, the rooms it closes).
        private static UiNode BuildCard(string stem, float x, out NodeRef titleRef, out NodeRef bodyRef)
        {
            var title = Ui.Label(stem + "Title", UiString.Runtime, new UiVec(CardSize.X - 40f, 28f), 22,
                    FightHudPalette.HeadingGold, Place.At(0f, 62f))
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.FunctionalHeading);
            var body = Ui.Label(stem + "Body", UiString.Runtime, new UiVec(CardSize.X - 40f, 118f), 19,
                    FightHudPalette.TextPrimary, Place.At(0f, -16f))
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.Body);
            titleRef = title;
            bodyRef = body;

            return Ui.OutlineBox(stem + "Panel", Place.At(x, CardY), CardSize,
                FightHudPalette.PanelViolet, FightHudPalette.BorderGold, new[] { title, body })
                .Inactive();
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
            var eased = Ui.Sprite("MapEasedWood", BackgroundKey,
                    Place.Pin(ContentTop, ContentTop, UiVec.Zero),
                    UiSize.Fixed(MapLayout.BackgroundTileWidth, MapLayout.MaxContentHeight))
                .AsDecor()
                .AllowOverflow("the eased wood spans the whole leg, as tall as the content rect it sits in")
                .Inactive();

            EasedWood = eased;
            return eased;
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
            var core = Ui.Solid($"MapTrack{index}", TrailCoreAheadHex,
                    Place.Frac(new UiVec(0f, 0.24f), new UiVec(1f, 0.76f)), UiSize.Fill)
                .AsDecor();

            var segment = Ui.Solid($"MapTrail{index}", TrailAheadHex,
                    new UiVec(8f, 8f), OnContent(0f, -MapLayout.TopMargin))
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

        // A room's caption, and the colour it takes while it is explaining a
        // refused press (MapController.Refuse).
        public const string CaptionHex = "#EDE6FF";
        public const string RefusalHex = "#E0786E";

        // The pip and the chip: gold on the tile's own canopy, the chip's
        // plate dark so its text keeps contrast on any prize.
        public const string PipHex = "#F2DB9E";
        public const string ChipRimHex = "#C9A24B";
        public const string ChipFillHex = "#241A38F2";
        public const string ChipTextHex = "#F2DB9E";

        // The fog drawn over a room a focused choice would close off.
        public const string VeilHex = "#0B0813B8";

        private const float MarkerWidth = 118f;

        private UiNode BuildNode(int index)
        {
            // Declared at the WIDEST a tile can be, because which slot holds the
            // boss is a runtime fact. The controller shrinks it per type; the
            // audit therefore checks a box at least as big as the real one.
            var size = new UiVec(MapLayout.MaxTileWidth, MapLayout.MaxTileHeight);

            // "Name", NOT "Label". UiEmitter generates every button's own
            // caption as "<button>Label", so "MapNode3Label" produced two
            // GameObjects with that name under one parent -- the wiring bound
            // this one and painted it, while every lookup by name silently took
            // the emitter's empty one. UiAudit's A4b is what catches it now.
            var label = Ui.Label($"MapNode{index}Name", UiString.Runtime,
                    new UiVec(MapLayout.CaptionWidth, MapLayout.CaptionHeight), 13,
                    CaptionHex, Place.At(0f, -size.Y * 0.5f - MapLayout.CaptionGap))
                .AllowOverflow("the caption sits BELOW its node deliberately - inside, it would cover the room icon it names");

            // The type icon, standing in the clearing under the tree's canopy
            // rather than dead centre of it -- the branches close in overhead
            // and an icon at true centre fights them.
            //
            // Declared with the fight icon and swapped at runtime: which room a
            // slot holds is exactly what the build cannot know, and a Sprite
            // with no key at all emits an Image with no sprite, which draws as
            // a white quad the moment anything activates it.
            var icon = Ui.Sprite($"MapNode{index}Icon", FightIconKey,
                    Place.At(0f, -size.Y * 0.08f),
                    UiSize.Fixed(MapLayout.MaxTileWidth * 0.62f, MapLayout.MaxTileWidth * 0.62f))
                .AsDecor()
                .Inactive();

            // The "you are here" tag. Its own node rather than a tint on the
            // button, so the button keeps saying what KIND of room it is while
            // this says where the party is -- two facts that have to be
            // readable at once. Words in a gold-rimmed plate: the flat 18 px
            // square it replaced said nothing to a player who had not been
            // told what it meant.
            var markerSize = new UiVec(MarkerWidth, MapLayout.MarkerHeight);
            var marker = Ui.Solid($"MapNode{index}Marker", FightHudPalette.PanelViolet,
                    Place.At(0f, size.Y * 0.5f + MapLayout.MarkerGap), UiSize.Fixed(markerSize))
                .AllowOverflow("the you-are-here tag sits ABOVE its node so the node keeps saying what KIND of room it is")
                .Inactive();
            foreach (var edge in Ui.Rim($"MapNode{index}Marker", markerSize, FightHudPalette.BorderGold))
            {
                marker.Children.Add(edge);
            }
            marker.Children.Add(Ui.Label($"MapNode{index}MarkerText", UiStrings.MapYouAreHere, markerSize, 13,
                FightHudPalette.GoldLight, Place.At(0f, 0f)).Styled(TypographyRole.TacticalData));

            // Strength as pips along the tile's top edge, inside it.
            var pips = new List<UiNode>();
            for (int p = 0; p < NodeStrength.Max; p++)
            {
                var pip = Ui.Solid($"MapNode{index}Pip{p}", PipHex,
                        Place.At((p - (NodeStrength.Max - 1) * 0.5f) * MapLayout.PipPitch, size.Y * 0.5f - 18f),
                        UiSize.Fixed(MapLayout.PipSize, MapLayout.PipSize))
                    .AsDecor()
                    .Inactive();
                pips.Add(pip);
                NodePips.Add(pip);
            }

            // The prize chip, beside the tile; its size follows strength, set
            // at runtime. A gold rim is the chip itself and the dark fill sits
            // inside it by a fixed inset, so resizing the chip resizes both.
            var chipText = Ui.Label($"MapNode{index}ChipText", UiString.Runtime,
                    new UiVec(MapLayout.MaxChipWidth - 6f, MapLayout.MaxChipHeight - 4f), 12,
                    ChipTextHex, Place.Stretch(3f, 3f, 2f, 2f))
                .Styled(TypographyRole.TacticalData);
            var chipFill = Ui.Solid($"MapNode{index}ChipFill", ChipFillHex,
                    Place.Stretch(2f, 2f, 2f, 2f), UiSize.Fill)
                .AsDecor();
            chipFill.Children.Add(chipText);
            var chip = Ui.Solid($"MapNode{index}Chip", ChipRimHex,
                    Place.At(size.X * 0.5f + MapLayout.ChipGap + MapLayout.MaxChipWidth * 0.5f, -20f),
                    UiSize.Fixed(MapLayout.MaxChipWidth, MapLayout.MaxChipHeight))
                .AsDecor()
                .AllowOverflow("the chip stands beside its tile so the icon stays readable")
                .Inactive();
            chip.Children.Add(chipFill);

            // The fog over the whole tile when a focused choice would close
            // this room off.
            // The tree again, tinted by the controller (VeilHex) rather than a
            // flat rect, so the fog keeps the tile's own silhouette.
            var veil = Ui.Sprite($"MapNode{index}Veil", TreeKey, Place.Stretch(0f, 0f, 0f, 0f), UiSize.Fill)
                .AsDecor()
                .Inactive();

            var button = Ui.Button($"MapNode{index}", UiString.Runtime, size, 12,
                OnContent(0f, -MapLayout.TopMargin));

            // The painted tree IS the button's plate, so the tile the player
            // clicks and the tile they see are one graphic. Without this the
            // emitter would put its shared gold button frame behind every room.
            button.SpriteKey = TreeKey;

            button.Children.Add(icon);
            foreach (var pip in pips) button.Children.Add(pip);
            button.Children.Add(veil);
            button.Children.Add(label);
            button.Children.Add(marker);
            button.Children.Add(chip);

            NodeButtons.Add(button);
            NodeIcons.Add(icon);
            NodeLabels.Add(label);
            NodeMarkers.Add(marker);
            NodeChips.Add(chip);
            NodeChipTexts.Add(chipText);
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
