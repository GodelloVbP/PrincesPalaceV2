using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The descent map: where the party is, and where they may go next.
    //
    // The one screen whose contents genuinely are not known at build time -- a
    // leg is generated from a seed at runtime, so the nodes are a POOL sized
    // from MapLayout.Capacity, which is (columns x max width) and therefore
    // every position a leg could possibly use. That is why the pool has real
    // audited coordinates rather than the kind-based exemption: the positions
    // ARE computable, only which of them are occupied is not.
    //
    // It is also the one screen WIDER THAN THE CANVAS. Nine columns at the
    // background's own clearing pitch is ~7400 units behind a 1920 window, so
    // the shape here is viewport -> content -> everything, with the content
    // rect sliding under a mask and the camera pinning the current room to the
    // left painted clearing. See MapLayout for why every x is content-local.
    public sealed class MapScreen
    {
        // A FOREST FLOOR, not a nebula.
        //
        // This screen was pointing at Divine_principality_nebula while
        // forest_map_background.png sat in the same folder unreferenced by
        // anything -- and that art was clearly drawn for this screen: it is a
        // top-down canopy with clearings punched through it, one per room, laid
        // out in columns. The nebula is a fine backdrop for somewhere; the
        // descent is through a wood.
        //
        // TILED, not stretched. It paints one pair of clearing columns, so it
        // repeats every MapLayout.BackgroundPeriod and scrolls with the leg.
        // Stretching it flat -- which is what this screen did until now -- put
        // the clearings wherever the aspect happened to land them and every
        // room stood on canopy.
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

        public static float TileWidthFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Boss: return MapLayout.BossTileWidth;
                case RoomType.EliteFight: return MapLayout.EliteTileWidth;
                default: return MapLayout.TileWidth;
            }
        }

        public UiNode Root;

        // The scrolling half. The viewport is the fixed window and does the
        // clipping; the content is the wide rect that slides under it.
        public NodeRef Viewport;
        public NodeRef Content;
        public List<NodeRef> Backdrops = new List<NodeRef>();
        public NodeRef Fog;

        public List<NodeRef> NodeButtons = new List<NodeRef>();
        public List<NodeRef> NodeIcons = new List<NodeRef>();
        public List<NodeRef> NodeLabels = new List<NodeRef>();
        public List<NodeRef> NodeMarkers = new List<NodeRef>();

        // Every straight piece of every trail. Flat rather than nested per
        // link, because the controller walks links in one pass and a jagged
        // array would only make the index arithmetic worse.
        public List<NodeRef> TrailSegments = new List<NodeRef>();

        // The lighter inner track of each segment, one per TrailSegments entry
        // and in the same order. Tinted alongside its verge, so a road cannot
        // end up with a walked surface inside an unreachable edge.
        public List<NodeRef> TrailCores = new List<NodeRef>();

        public NodeRef Walker;

        public NodeRef TitleLabel;
        public NodeRef DepthLabel;
        public NodeRef GoldLabel;
        public NodeRef AbandonButton;
        public NodeRef RoomMessageLabel;

        // The same overlay the hub and the fight mount. Live here:
        // between rooms is when gear is meant to change.
        public SystemMenuScreen SystemMenu;

        // The in-run shop, entered from a Shop room (F10, docs/PLAN_SHOP.md).
        // Nested here the same way the draft nests in the hub -- entered from
        // this screen, no scene of its own. Declared LAST among Root's
        // children (below) so it draws over the system menu too -- there is
        // no dossier access from inside the shop in v1 (docs/PLAN_SHOP.md
        // §2c), and drawing on top is what makes that true rather than a
        // z-order accident.
        public ShopScreen Shop;

        // Anchored to the content rect's LEFT edge, which is the origin every
        // MapLayout x is measured from. Declared once here so no construction
        // site restates it and drifts.
        private static readonly UiVec ContentEdge = new UiVec(0f, 0.5f);

        private static Place OnContent(float x, float y) =>
            Place.Pin(ContentEdge, UiVec.Centre, new UiVec(x, y));

        public static MapScreen Build()
        {
            var screen = new MapScreen();

            // FunctionalHeading, not CeremonialTitle -- the map is a working
            // screen the player passes through every room, not a title or a
            // ceremonial moment the way Defeat/Reckoning's own titles are.
            var title = Ui.Label("MapTitleLabel", UiStrings.MapTitle, new UiVec(700f, 60f), 40, "#EDE6FF",
                Place.At(0f, 452f)).Styled(TypographyRole.FunctionalHeading);
            var depth = Ui.Label("MapDepthLabel", UiStrings.MapDepth, new UiVec(420f, 34f), 20, "#B8A8D9",
                Place.At(0f, 404f)).Styled(TypographyRole.TacticalData);
            var gold = Ui.Label("MapGoldLabel", UiStrings.MapGold, new UiVec(320f, 40f), 22, "#F2DB9E",
                Place.At(690f, 452f)).Styled(TypographyRole.TacticalData);

            // SILVER: leaving the descent, matching Cancel/Back everywhere
            // else in the kit. Room-node buttons are NOT themed here -- the
            // painted tree IS their plate (button.SpriteKey = TreeKey below),
            // so a theme's plate would replace the bespoke art rather than
            // dress a bare frame.
            var abandon = Ui.Button("AbandonRunButton", UiStrings.MapAbandon, new UiVec(240f, 60f), 16,
                    Place.At(-800f, 452f))
                .Themed(ButtonTheme.Silver);

            // What the room the party just walked into did.
            //
            // Sits UNDER the depth line rather than over the wood, because it
            // is a heading about the descent's state and not a label on
            // anything in the scene. 900 wide at y 364 clears depth's band
            // (387-421) with room to spare; the treasure template's worst-case
            // sample is what E1 measures the box against.
            //
            // Sized for the longest line any room can produce -- the unbuilt
            // shop's apology -- so a placeholder message cannot overflow a box
            // fitted to the short ones.
            var roomMessage = Ui.Label("MapRoomMessageLabel", UiStrings.MapRoomShop,
                new UiVec(900f, 32f), 18, "#C8BBE4", Place.At(0f, 364f))
                .Styled(TypographyRole.Body);

            screen.TitleLabel = title;
            screen.DepthLabel = depth;
            screen.GoldLabel = gold;
            screen.AbandonButton = abandon;
            screen.RoomMessageLabel = roomMessage;

            // The painted forest, repeated. Tiles are 1920 wide and repeat
            // every 1726, so consecutive tiles overlap by 194 -- the art's own
            // clearing pair is what has to line up end to end, not its canvas.
            // the forest tile is 1920 wide but repeats every 1726, so
            // neighbouring tiles overlap by design - the clearings are what
            // tile, not the canvas
            var backdrops = Ui.Pool("MapBackdrops", MapLayout.MaxBackgroundTiles, i => screen.BuildBackdrop(i))
                .AsDecor();

            var trails = Ui.Pool("MapTrails", MapLayout.SegmentCapacity, i => screen.BuildTrailSegment(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var nodes = Ui.Pool("MapNodes", MapLayout.Capacity, i => screen.BuildNode(i))
                .AllowOverlap("an invisible pool container spans the panel by construction and has no graphic to intercept anything");

            var walker = screen.BuildWalker();
            var fog = screen.BuildFog();

            // BACKDROP, then TRAILS, then NODES, then the WALKER, then FOG.
            // uGUI paints siblings in order and this is the whole back-to-front
            // stack: a path crossing over the room it leads to would read as
            // the rooms being behind the map rather than on it, the figure
            // walks IN FRONT of the wood it is walking through, and the fog has
            // to cover the forest it is fading out.
            var content = Ui.Panel("MapContent",
                    Place.Pin(ContentEdge, ContentEdge, UiVec.Zero),
                    UiSize.Fixed(
                        MapLayout.ContentWidth(MapLayout.Columns, UiFrames.Reference.X),
                        UiFrames.Reference.Y),
                    backdrops, trails, nodes, walker, fog)
                .AllowOverflow("the content rect is deliberately wider than the window it sits in - that overflow IS the scroll, and MapViewport clips it");

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
            // The overarching menu, LAST in this scene's children so it draws
            // over everything it can be opened on top of.
            var systemMenu = SystemMenuScreen.Build();
            screen.SystemMenu = systemMenu;

            var shop = ShopScreen.Build();
            screen.Shop = shop;

            // The old paperdoll is gone; the system menu's Character pane is
            // the character screen now. SheetPanel opens the menu instead, so
            // every caller -- C, I, Escape, the hub's building -- reaches the
            // dossier without any of them knowing the screen changed.

            // Viewport FIRST so the headings and the abandon button draw over
            // the scrolling wood rather than under it. The sheet is LAST, so
            // the modal dims the map and everything on it.
            screen.Root = Ui.Panel("MapPanel", UiSize.Fill,
                viewport, title, depth, gold, abandon, roomMessage, systemMenu.Root, shop.Root);

            return screen;
        }

        // One repeat of the painted forest, pinned by its own LEFT edge so tile
        // i starts exactly where the clearing grid says it should.
        private UiNode BuildBackdrop(int index)
        {
            // FILLHEIGHT, NOT FILL, and the width is the tile's own.
            //
            // Fill takes both axes from the parent, and the parent is the whole
            // scrolling leg -- 8824 map-units of it. One repeat of the forest
            // was being stretched across all of it, which showed as a forest
            // magnified about five times. See MapLayout.BackgroundTileWidth.
            //
            // The height still fills, because that axis genuinely is the
            // content's: the wood is as tall as the map is, whatever that
            // turns out to be.
            var tile = Ui.Sprite($"MapBackdrop{index}", BackgroundKey,
                    Place.Pin(ContentEdge, ContentEdge, new UiVec(MapLayout.BackgroundTileX(index), 0f)),
                    UiSize.FillHeight(MapLayout.BackgroundTileWidth))
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

        // The party, standing in the wood. Declared at the entrance's own
        // standing spot and moved from there; sized off Shawn's 540x370 idle,
        // with the controller re-deriving the width from whatever art the
        // actual squad leader turns out to have.
        private UiNode BuildWalker()
        {
            var entrance = MapWalk.Standing(new UiVec(MapLayout.ColumnX(0), MapLayout.RowY(0)));

            var walker = Ui.Sprite("MapWalker", WalkerKey,
                    OnContent(entrance.X, entrance.Y),
                    UiSize.Fixed(MapWalk.FigureHeight * ShawnAspect, MapWalk.FigureHeight))
                .AsDecor()
                .AllowOverflow("the figure stands at the clearing's edge and overhangs the canopy on purpose - so do the trails it walks along, and there is no room inside a 155-wide clearing for a figure half again as wide as it is tall")
                .Inactive();

            Walker = walker;
            return walker;
        }

        // The fade at the end of the wood. Declared where a full-length leg
        // puts it; the controller moves it if a leg is shorter.
        private UiNode BuildFog()
        {
            var label = Ui.Label("MapFogLabel", UiStrings.MapFog, new UiVec(220f, 80f), 15, "#6B6180",
                Place.At(60f, 0f));

            var fog = Ui.Solid("MapFog", "#040503EB",
                    OnContent(MapLayout.FogX(MapLayout.Columns) + MapLayout.FogWidth * 0.5f, 0f),
                    UiSize.Fixed(MapLayout.FogWidth, 680f));

            fog.Children.Add(label);

            Fog = fog;
            return fog;
        }

        // One straight piece of one trail. Declared at the origin with a
        // placeholder size because a segment's real position, length and
        // rotation all depend on which two rooms a leg happened to generate --
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
                    new UiVec(8f, 8f), OnContent(0f, 0f))
                .AsDecor()
                .Inactive();

            segment.Children.Add(core);

            TrailSegments.Add(segment);
            TrailCores.Add(core);
            return segment;
        }

        // The four states a trail can be in, straight from v1. Colours rather
        // than one tint at four alphas: a walked path and an unreachable one
        // differ in what they MEAN, and the eye separates hue faster than it
        // separates opacity.
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

        // Wider than the lines they replace. A road has to be thick enough for
        // the verge to read AS a verge -- at the old 7px an "ahead" trail would
        // have had two pixels of edge and two of core.
        public const float TrailWidthTaken = 24f;
        public const float TrailWidthOpen = 20f;
        public const float TrailWidthAhead = 12f;
        public const float TrailWidthClosed = 7f;

        // The per-state tile tints live in MapController, not here, and
        // deliberately break the convention the trail colours above follow.
        // Nothing in this tree declares a tile's resting colour -- every tile
        // is painted by state on the first Refresh -- and "current" has to be
        // brighter than the sprite's own colour, which a hex string cannot say
        // and a Color above 1.0 can.

        private UiNode BuildNode(int index)
        {
            int depth = index / MapLayout.Rows;
            int slot = index % MapLayout.Rows;

            // The REAL position, not an approximation the controller corrects.
            // Rows snap to painted clearings by slot, so a node's coordinates no
            // longer depend on how many rooms its column turned out to hold and
            // there is nothing left to re-anchor at runtime.
            var place = OnContent(MapLayout.ColumnX(depth), MapLayout.RowY(slot));

            // Declared at the WIDEST a tile can be, because which slot holds the
            // boss is a runtime fact. The controller shrinks it per type; the
            // audit therefore checks a box at least as big as the real one.
            var size = new UiVec(MapLayout.MaxTileWidth, MapLayout.MaxTileHeight);

            // "Name", NOT "Label". UiEmitter generates every button's own
            // caption as "<button>Label", so "MapNode3Label" produced two
            // GameObjects with that name under one parent -- the wiring bound
            // this one and painted it, while every lookup by name silently took
            // the emitter's empty one. Shipped undetected until UiAudit learned
            // to check for it (A4b, 2026-08-11).
            var label = Ui.Label($"MapNode{index}Name", UiString.Runtime, new UiVec(MapLayout.MaxTileWidth, 28f), 13,
                    "#EDE6FF", Place.At(0f, -size.Y * 0.5f - 16f))
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

            // The "you are here" pip. Its own node rather than a tint on the
            // button, so the button keeps saying what KIND of room it is while
            // this says where the party is -- two facts that have to be readable
            // at once.
            var marker = Ui.Solid($"MapNode{index}Marker", "#F2DB9E",
                    Place.At(0f, size.Y * 0.5f + 14f), UiSize.Fixed(18f, 18f))
                .AllowOverflow("the you-are-here pip sits ABOVE its node so the node keeps saying what KIND of room it is")
                .Inactive();

            var button = Ui.Button($"MapNode{index}", UiString.Runtime, size, 12, place);

            // The painted tree IS the button's plate, so the tile the player
            // clicks and the tile they see are one graphic. Without this the
            // emitter would put its shared gold button frame behind every room.
            button.SpriteKey = TreeKey;

            button.Children.Add(icon);
            button.Children.Add(label);
            button.Children.Add(marker);

            NodeButtons.Add(button);
            NodeIcons.Add(icon);
            NodeLabels.Add(label);
            NodeMarkers.Add(marker);

            // Hidden until a leg says otherwise: most of the pool is empty in
            // any given leg, and an inactive node cannot be clicked by accident.
            return button.Inactive();
        }
    }
}
