using System.Collections.Generic;
using System.Linq;
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
        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/forest_map_background.png";

        public UiNode Root;

        public List<NodeRef> NodeButtons = new List<NodeRef>();
        public List<NodeRef> NodeLabels = new List<NodeRef>();
        public List<NodeRef> NodeMarkers = new List<NodeRef>();

        // Every straight piece of every trail. Flat rather than nested per
        // link, because the controller walks links in one pass and a jagged
        // array would only make the index arithmetic worse.
        public List<NodeRef> TrailSegments = new List<NodeRef>();

        public NodeRef TitleLabel;
        public NodeRef DepthLabel;
        public NodeRef GoldLabel;
        public NodeRef AbandonButton;

        public static MapScreen Build()
        {
            var screen = new MapScreen();

            var title = Ui.Label("MapTitleLabel", UiStrings.MapTitle, new UiVec(700f, 60f), 40, "#EDE6FF",
                Place.At(0f, 452f));
            var depth = Ui.Label("MapDepthLabel", UiStrings.MapDepth, new UiVec(420f, 34f), 20, "#B8A8D9",
                Place.At(0f, 404f));
            var gold = Ui.Label("MapGoldLabel", UiStrings.MapGold, new UiVec(320f, 40f), 22, "#F2DB9E",
                Place.At(690f, 452f));

            var abandon = Ui.Button("AbandonRunButton", UiStrings.MapAbandon, new UiVec(240f, 60f), 16,
                Place.At(-800f, 452f));

            screen.TitleLabel = title;
            screen.DepthLabel = depth;
            screen.GoldLabel = gold;
            screen.AbandonButton = abandon;

            // The pool is a full-bleed CONTAINER with no graphic of its own -- it
            // is where the nodes live, not something drawn -- so it necessarily
            // spans the headings it shares the panel with. It cannot take their
            // clicks because it has nothing to raycast against.
            var trails = Ui.Pool("MapTrails", MapLayout.SegmentCapacity, i => screen.BuildTrailSegment(i))
                .AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            var nodes = Ui.Pool("MapNodes", MapLayout.Capacity, i => screen.BuildNode(i))
                .AllowOverlap("an invisible pool container spans the panel by construction and has no graphic to intercept anything");

            // TRAILS BEFORE NODES, which is the whole of their draw order: uGUI
            // paints later siblings on top, and a path crossing over the room
            // it leads to would read as the rooms being behind the map rather
            // than on it.
            screen.Root = Ui.Panel("MapPanel", UiSize.Fixed(1920f, 1080f),
                Ui.Sprite("MapBackground", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),
                title, depth, gold, abandon,
                trails, nodes);

            return screen;
        }

        // One quad of one trail. Declared at the origin with a placeholder size
        // because a segment's real position, length and rotation all depend on
        // which two rooms a leg happened to generate -- the same arrangement
        // the fight screen's VFX and damage-popup pools use, and the reason the
        // pool rather than the member carries the audit exemption.
        private UiNode BuildTrailSegment(int index)
        {
            var segment = Ui.Solid($"MapTrail{index}", TrailAheadHex,
                    new UiVec(8f, 8f), Place.At(0f, 0f))
                .AsDecor()
                .Inactive();

            TrailSegments.Add(segment);
            return segment;
        }

        // The four states a trail can be in, straight from v1. Colours rather
        // than one tint at four alphas: a walked path and an unreachable one
        // differ in what they MEAN, and the eye separates hue faster than it
        // separates opacity.
        public const string TrailTakenHex = "#4A2E17";
        public const string TrailOpenHex = "#5A3B21F2";
        public const string TrailAheadHex = "#3B24148C";
        public const string TrailClosedHex = "#2417124D";

        public const float TrailWidthTaken = 15f;
        public const float TrailWidthOpen = 12f;
        public const float TrailWidthAhead = 7f;
        public const float TrailWidthClosed = 4f;

        private UiNode BuildNode(int index)
        {
            int depth = index / MapLayout.Rows;
            int slot = index % MapLayout.Rows;

            // Built at the position a FULL column would put it in. The
            // controller re-anchors to the real column width at runtime through
            // the same MapLayout.RowY, which is the whole reason that function
            // is in Domain rather than in the builder.
            var place = Place.At(MapLayout.ColumnX(depth), MapLayout.RowY(MapLayout.Rows, slot));

            // "Name", NOT "Label". UiEmitter generates every button's own
            // caption as "<button>Label", so "MapNode3Label" produced two
            // GameObjects with that name under one parent -- the wiring bound
            // this one and painted it, while every lookup by name silently took
            // the emitter's empty one. Shipped undetected until UiAudit learned
            // to check for it (A4b, 2026-08-11).
            var label = Ui.Label($"MapNode{index}Name", UiString.Runtime, new UiVec(MapLayout.NodeSize, 28f), 13,
                    "#EDE6FF", Place.At(0f, -MapLayout.NodeSize * 0.5f - 16f))
                .AllowOverflow("the caption sits BELOW its node deliberately - inside, it would cover the room icon it names");

            // The "you are here" pip. Its own node rather than a tint on the
            // button, so the button keeps saying what KIND of room it is while
            // this says where the party is -- two facts that have to be readable
            // at once.
            var marker = Ui.Solid($"MapNode{index}Marker", "#F2DB9E",
                    Place.At(0f, MapLayout.NodeSize * 0.5f + 14f), UiSize.Fixed(18f, 18f))
                .AllowOverflow("the you-are-here pip sits ABOVE its node so the node keeps saying what KIND of room it is")
                .Inactive();

            var button = Ui.Button($"MapNode{index}", UiString.Runtime,
                new UiVec(MapLayout.NodeSize, MapLayout.NodeSize), 12, place);

            button.Children.Add(label);
            button.Children.Add(marker);

            NodeButtons.Add(button);
            NodeLabels.Add(label);
            NodeMarkers.Add(marker);

            // Hidden until a leg says otherwise: most of the pool is empty in
            // any given leg, and an inactive node cannot be clicked by accident.
            return button.Inactive();
        }
    }
}
