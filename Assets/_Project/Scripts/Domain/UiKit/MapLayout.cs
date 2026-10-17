using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // Where a realm node sits on screen, as numbers (docs/PLAN_RUN_MAP.md
    // section 7). Pure arithmetic in Domain for the same reason
    // FightSubmenuLayout is: the screen tree sizes its pools at build time and
    // the controller reads the same numbers at runtime, and the two must be one
    // function. Which node owns which position is MapGraph.
    //
    // The screen scrolls vertically: the entry sits at the top and the heart at
    // the bottom (RealmLayout), so scrolling down is going deeper.
    //
    // CONTENT-LOCAL COORDINATES, Unity-style: x from the content's horizontal
    // centre (+ right), y from the content's top edge, NEGATIVE going down.
    // The content rect is pinned top-centre, so these are anchoredPosition
    // values as they stand.
    public static class MapLayout
    {
        // The lateral span RealmLayout's 0..1 across maps onto. Narrower than
        // the 1920 canvas so the outermost pocket tile and its prize chip stay
        // on screen.
        public const float SpanWidth = 1500f;

        // The vertical distance between two longest-route rows. Every
        // footprint (a tile with its tag above and caption below) is shorter
        // than this -- the boss, the tallest, by 9 -- so two nodes in
        // neighbouring rows never touch.
        public const float RowPitch = 224f;

        // Row 0's centre below the content top.
        public const float TopMargin = 340f;

        // From the heart's row to the content bottom: the heart's anchors, the
        // queue behind them and the fog.
        public const float HeartDepth = 560f;

        // ---- a node's parts -----------------------------------------------------

        // A room tile is its medallion, which keeps its own proportions inside
        // the box. The tallest shapes (the boss arch, the shield) are about
        // 1.15 high for 1 wide, so the bonus is height only.
        public const float TileWidth = 100f;
        public const float EliteTileWidth = 118f;
        public const float BossTileWidth = 142f;
        public const float TileHeightBonus = 12f;

        public const float MaxTileWidth = BossTileWidth;
        public const float MaxTileHeight = BossTileWidth + TileHeightBonus;

        public static float TileWidthFor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Boss: return BossTileWidth;
                case RoomType.EliteFight: return EliteTileWidth;
                default: return TileWidth;
            }
        }

        public static float TileHeightFor(RoomType type) => TileWidthFor(type) + TileHeightBonus;

        // Room the footprint keeps clear below and above each tile (the heart
        // names reuse CaptionHeight). Spacing, not drawn elements.
        public const float CaptionWidth = 130f;
        public const float CaptionHeight = 28f;
        public const float CaptionGap = 16f;
        public const float MarkerHeight = 26f;
        public const float MarkerGap = 18f;

        // The prize chip stands beside the tile, not on it, so the icon stays
        // readable. Its size follows strength: one step wider and taller per
        // pip.
        public const float ChipGap = 6f;
        public const float ChipIconWidth = 18f;
        public const float ChipBaseWidth = 64f + ChipIconWidth;
        public const float ChipBaseHeight = 24f;
        public const float ChipWidthStep = 10f;
        public const float ChipHeightStep = 4f;

        // The icon is square: its edge is this, one step bigger per pip.
        public static float ChipHeightFor(int strength) =>
            ChipBaseHeight + ChipHeightStep * (ClampStrength(strength) - 1);

        public static readonly float MaxChipWidth = ChipBaseWidth + ChipWidthStep * (NodeStrength.Max - 1);
        public static readonly float MaxChipHeight = ChipBaseHeight + ChipHeightStep * (NodeStrength.Max - 1);

        private static int ClampStrength(int strength) =>
            strength < NodeStrength.Min ? NodeStrength.Min : strength > NodeStrength.Max ? NodeStrength.Max : strength;

        // The pips sit along the tile's top edge, inside it.
        public const float PipSize = 16f;
        // How far the "choosable now" glow ring reaches past the tile on each
        // side; the ring's soft falloff needs this room or it clips to a box.
        public const float GlowPad = 18f;

        // The ring's resting pulse: alpha swings between these as the shared
        // beacon curve (AmbienceCurves.BeaconProgressAt, 0..1) goes round. Well
        // under the hover rim, which is full strength and steady.
        public const float GlowMinAlpha = 0.12f;
        public const float GlowMaxAlpha = 0.38f;
        public const float GlowPeriodSeconds = 2.6f;

        public static float GlowAlpha(float beaconProgress)
        {
            float t = beaconProgress < 0f ? 0f : beaconProgress > 1f ? 1f : beaconProgress;
            return GlowMinAlpha + (GlowMaxAlpha - GlowMinAlpha) * t;
        }
        public const float PipPitch = 20f;

        // The space a node claims, as one box: tile, the clear bands above and
        // below it, and the widest chip on the right (mirrored on the left, which keeps the box
        // symmetric about the node's own x). Two nodes whose boxes do not
        // intersect cannot have overlapping tiles, captions or chips; the
        // layout test holds every default realm to that.
        public static MapRect Footprint(RoomType type, UiVec centre)
        {
            float width = TileWidthFor(type);
            float height = TileHeightFor(type);

            float halfWidth = width * 0.5f + ChipGap + MaxChipWidth;
            if (CaptionWidth * 0.5f > halfWidth) halfWidth = CaptionWidth * 0.5f;

            float above = height * 0.5f + MarkerGap + MarkerHeight * 0.5f;
            float below = height * 0.5f + CaptionGap + CaptionHeight * 0.5f;

            return new MapRect(centre.X - halfWidth, centre.Y - below, centre.X + halfWidth, centre.Y + above);
        }

        // ---- capacity -----------------------------------------------------------

        // The most nodes a default-profile realm holds: the spine, the most
        // forks, and every pocket at its longest. A designed profile past this
        // draws what fits and says so.
        public const int MaxNodes =
            RealmMap.SpineLength + 1 + RealmProfiles.MaxDefaultForks
            + RealmMap.MaxPockets * RealmProfiles.MaxDefaultPocketLength;

        // The most rows the longest route can have: every node on one route.
        public const int MaxRows = MaxNodes;

        // Edges. A node leads to at most three (a fork sibling: the next level
        // twice and a pocket); the fuzz test holds every default realm under it.
        public const int LinkCapacity = MaxNodes * 2;

        public const int RootCapacity = RealmProfiles.MaxDefaultAnchors;

        // ---- the scrolling window -----------------------------------------------

        public static float ContentHeight(int rows) =>
            TopMargin + (rows < 1 ? 0 : rows - 1) * RowPitch + HeartDepth;

        public static float MaxContentHeight => ContentHeight(MaxRows);

        // Where the CURRENT room sits below the viewport's top edge once the
        // camera has caught up: under the top band, with the next rows
        // below it.
        public const float FollowOffset = TopMargin;

        // Auto-scroll, as how far the content has moved UP: positive, clamped
        // so the camera never scrolls off its own content. `currentY` is
        // content-local (negative).
        public static float Scroll(float currentY, float contentHeight, float viewportHeight)
        {
            float max = System.Math.Max(0f, contentHeight - viewportHeight);
            float target = -currentY - FollowOffset;
            if (target < 0f) target = 0f;
            if (target > max) target = max;
            return target;
        }

        // The deepest the camera goes: the heart, for the opening shot.
        public static float ScrollToBottom(float contentHeight, float viewportHeight) =>
            System.Math.Max(0f, contentHeight - viewportHeight);

        // The painted strip, tiled down the leg. One tile is the processed art
        // (tools/ingest_map_art.py: scaled to 1920 wide, then 12% shorter for the
        // seam crossfade: 941x1672 -> 1920x3411 -> 1920x3003), and it tiles
        // top to bottom with no mirroring.
        public const float BackgroundTileWidth = 1920f;
        public const float BackgroundTileHeight = 3003f;

        // The heart's lair: 1672x941 scaled to 1920 wide (1081 tall), bottom-aligned to the
        // content so its centre lands on the heart row (HeartDepth up).
        public const float HeartBackdropWidth = 1920f;
        public const float HeartBackdropHeight = 1081f;

        public static int MaxBackgroundTiles =>
            (int)System.Math.Ceiling(MaxContentHeight / BackgroundTileHeight);

        public static int BackgroundTilesFor(float contentHeight) =>
            contentHeight <= 0f ? 0 : (int)System.Math.Ceiling(contentHeight / BackgroundTileHeight);

        public static float BackgroundTileY(int index) => -index * BackgroundTileHeight;

        // THE HEADER BAND: the top strip the top-right stack (floor and room,
        // gold, overview hint) and the room message occupy. It paints nothing;
        // the overview keeps the realm below it and the first row (TopMargin)
        // stands clear of it.
        public const float HeaderBand = 220f;

        // The overview's zoom: the whole realm below the header band and above
        // the bottom inset, never magnified.
        public const float OverviewTopInset = HeaderBand + 10f;
        public const float OverviewBottomInset = 190f;

        public static float OverviewScale(float contentHeight, float viewportHeight)
        {
            float room = viewportHeight - OverviewTopInset - OverviewBottomInset;
            if (contentHeight <= 0f || room <= 0f) return 1f;
            float fit = room / contentHeight;
            return fit < 1f ? fit : 1f;
        }

        // ---- free scrolling -----------------------------------------------------

        public const float WheelUnits = 140f;
        public const float StickUnitsPerSecond = 1100f;
        public const float KeyUnitsPerSecond = 900f;
        public const float StickDeadZone = 0.25f;

        // How far the content moves UP on screen (positive = deeper, toward the
        // heart) for one frame of input. Wheel up, stick up (the MapScroll axis
        // is inverted in the Input Manager, so up reads positive) and PageUp
        // all show the rooms above; `pageKeys` is +1 for PageDown, -1 for PageUp.
        public static float ScrollUnits(float wheel, float stick, float pageKeys, float deltaSeconds)
        {
            float units = -wheel * WheelUnits;
            if (stick > StickDeadZone || stick < -StickDeadZone) units += -stick * StickUnitsPerSecond * deltaSeconds;
            return units + pageKeys * KeyUnitsPerSecond * deltaSeconds;
        }

        // ---- the trails between rooms -------------------------------------------

        // How many straight pieces a curve is chopped into. uGUI has no line
        // renderer, so a curve IS a row of rotated quads and this count is the
        // whole of its smoothness.
        public const int SegmentsPerLink = 6;

        public const int SegmentCapacity = LinkCapacity * SegmentsPerLink;

        public const int RootSegmentCapacity = RootCapacity * SegmentsPerLink;

        public readonly struct PathSegment
        {
            public readonly UiVec Centre;
            public readonly float Length;
            public readonly float AngleDegrees;

            public PathSegment(UiVec centre, float length, float angleDegrees)
            {
                Centre = centre;
                Length = length;
                AngleDegrees = angleDegrees;
            }
        }

        // A cubic bezier leaving and arriving vertically, with a little
        // lateral jitter so a trail is never ruler-straight. DERIVED FROM THE
        // SEED, never random: the same realm is drawn again on every refresh,
        // and a trail that re-rolled its curve would make the map writhe.
        public static UiVec PathPoint(UiVec from, UiVec to, int seed, float t)
        {
            float dy = to.Y - from.Y;
            float jitter = (seed * 37) % 17 - 8;

            var p1 = new UiVec(from.X + jitter, from.Y + dy * 0.4f);
            var p2 = new UiVec(to.X - jitter, to.Y - dy * 0.4f);

            return Cubic(from, p1, p2, to, t);
        }

        // A root: the same curve with a sideways belly, so a root reads as
        // grown rather than drawn, and never lies on top of a trail.
        public static UiVec RootPoint(UiVec from, UiVec to, int seed, float t)
        {
            float dy = to.Y - from.Y;
            float bend = ((seed * 53) % 23 - 11) * 6f;

            var p1 = new UiVec(from.X + bend, from.Y + dy * 0.25f);
            var p2 = new UiVec(to.X + bend, to.Y - dy * 0.25f);

            return Cubic(from, p1, p2, to, t);
        }

        private static UiVec Cubic(UiVec from, UiVec p1, UiVec p2, UiVec to, float t)
        {
            float u = 1f - t;
            float a = u * u * u;
            float b = 3f * u * u * t;
            float c = 3f * u * t * t;
            float d = t * t * t;

            return new UiVec(
                a * from.X + b * p1.X + c * p2.X + d * to.X,
                a * from.Y + b * p1.Y + c * p2.Y + d * to.Y);
        }

        public static PathSegment SegmentAt(UiVec from, UiVec to, int seed, int index) =>
            Piece(PathPoint(from, to, seed, index / (float)SegmentsPerLink),
                PathPoint(from, to, seed, (index + 1) / (float)SegmentsPerLink));

        public static PathSegment RootSegmentAt(UiVec from, UiVec to, int seed, int index) =>
            Piece(RootPoint(from, to, seed, index / (float)SegmentsPerLink),
                RootPoint(from, to, seed, (index + 1) / (float)SegmentsPerLink));

        // Lengthened by 2 deliberately: consecutive quads meeting exactly at a
        // point leave a hairline gap wherever the curve turns.
        private static PathSegment Piece(UiVec a, UiVec b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);

            return new PathSegment(new UiVec(a.X + dx * 0.5f, a.Y + dy * 0.5f), length + 2f, angle);
        }
    }

    // An axis-aligned box in content-local coordinates (y up).
    public readonly struct MapRect
    {
        public readonly float Left;
        public readonly float Bottom;
        public readonly float Right;
        public readonly float Top;

        public MapRect(float left, float bottom, float right, float top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public static MapRect Around(UiVec centre, float width, float height) =>
            new MapRect(centre.X - width * 0.5f, centre.Y - height * 0.5f,
                centre.X + width * 0.5f, centre.Y + height * 0.5f);

        public float Width => Right - Left;
        public float Height => Top - Bottom;

        // Touching edges do not intersect.
        public bool Intersects(MapRect other) =>
            Left < other.Right && other.Left < Right && Bottom < other.Top && other.Bottom < Top;
    }
}
