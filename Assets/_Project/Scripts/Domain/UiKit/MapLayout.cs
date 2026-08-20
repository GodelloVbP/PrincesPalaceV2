using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // Where a descent node sits on screen.
    //
    // Pure arithmetic over (depth, slot), in Domain for the same reason
    // FightSubmenuLayout is: the SCREEN TREE places the node pool at build time
    // and the CONTROLLER reads the same numbers at runtime, and those two must
    // be the same function or the map drifts from what it claims.
    //
    // A leg is DefaultDepth columns of at most MaxColumnWidth rows, so the pool
    // is that product and every position is computable without a map in hand --
    // which is what lets the tree be built and audited before any run exists.
    //
    // EVERY x HERE IS CONTENT-LOCAL, measured from the scrolling content rect's
    // own LEFT edge, not from the panel's centre like the rest of the game's
    // screens. The map is the one screen wider than the canvas: nine columns at
    // a fixed pitch is ~7400 units of content behind a 1920 window, and a
    // centre-relative coordinate would have to know the leg's length to mean
    // anything. Nodes are pinned to that left edge (Place.Pin at anchor 0,0.5)
    // so a column's position is the same number whatever the leg does.
    public static class MapLayout
    {
        // THE PAINTED CLEARINGS, measured off forest_map_background.png rather
        // than chosen -- the art is a top-down canopy with holes punched
        // through it, and a room has to stand in a hole. Two clearing columns
        // and three clearing rows per tile of background; the tile repeats
        // every BackgroundPeriod, which is what carries the grid past column 1.
        //
        // This is why the backdrop and the scrolling are one change and not
        // two: the camera pins the current room at ClearingColumnX[0] (see
        // FollowOffset), which is the ONLY reason rooms land on clearings at
        // all. Stretch the art flat behind a centred track, as this screen did
        // until now, and every room stands on canopy.
        public static readonly float[] ClearingColumnX = { 520f, 1383f };
        public static readonly float[] ClearingRowY = { 322f, 11f, -302f };

        public static float ColumnGap => ClearingColumnX[1] - ClearingColumnX[0];

        // A room tile is the painted tree plus its icon. Portrait, not square:
        // trees read taller than wide, so the bonus is height only and the
        // three widths stay widths.
        public const float TileWidth = 100f;
        public const float EliteTileWidth = 118f;
        public const float BossTileWidth = 142f;
        public const float TileHeightBonus = 30f;

        // The widest a tile can be, which is what the declared pool has to
        // reserve: the tree is built before any run exists and cannot know
        // which slot the boss will land in.
        public const float MaxTileWidth = BossTileWidth;
        public const float MaxTileHeight = BossTileWidth + TileHeightBonus;

        public static int Columns => DescentMapGenerator.DefaultDepth;
        public static int Rows => DescentMapGenerator.MaxColumnWidth;

        // The pool's capacity: every slot a leg could possibly use.
        public static int Capacity => Columns * Rows;

        // Which pool entry a (depth, slot) pair owns. Row-major so a column's
        // entries are contiguous, which makes "hide the rest of this column"
        // a range rather than a scan.
        public static int IndexFor(int depth, int slot) => depth * Rows + slot;

        public static float ColumnX(int depth) => ClearingColumnX[0] + depth * ColumnGap;

        // Rows SNAP to a painted clearing by slot index -- they are not spread
        // evenly, and a short column is not centred.
        //
        // This reverses what this file did before, and the reason is the art:
        // MaxColumnWidth is exactly 3 and there are exactly 3 clearing rows, so
        // slot 0/1/2 IS the clearing to stand in. A centred two-room column
        // would sit at two positions the canopy has no holes at -- which is the
        // failure the old comment here ("centring is what makes a fork read as
        // a fork") could not see, because it was written against a backdrop
        // that was being stretched flat and had no clearings to miss.
        //
        // A column of two therefore leaves its unused clearing empty, including
        // the middle one, with no special case for it.
        public static float RowY(int slot)
        {
            if (slot < 0) return ClearingRowY[0];
            return slot < ClearingRowY.Length ? ClearingRowY[slot] : ClearingRowY[ClearingRowY.Length - 1];
        }

        // ---- the scrolling window ----------------------------------------

        // Where the CURRENT room sits, measured from the viewport's own left
        // edge, once the camera has caught up to it.
        //
        // Tied to the left clearing rather than chosen: Scroll() pins the
        // current room to this exact screen offset whatever its depth, so for
        // it to land ON the left painted clearing -- and, by the same fixed
        // ColumnGap stride, for the column it is choosing into to land on the
        // right one -- this has to be the left clearing's own x. Any other
        // value reintroduces the rooms-versus-clearings mismatch.
        public static float FollowOffset => ClearingColumnX[0];

        // The background art paints ONE pair of clearings, so it repeats every
        // two columns. Tiling rather than stretching wider art is what lets a
        // leg of any length stay aligned without needing bigger source art.
        public static float BackgroundPeriod => ColumnGap * 2f;

        // HOW WIDE ONE REPEAT IS DRAWN, in map space.
        //
        // This has to be a fixed number and it is not a screen assumption,
        // which is the distinction that matters here. The tile was declared
        // UiSize.Fill, and while every screen was pinned to 1920 that happened
        // to resolve to 1920 and looked deliberate. Once screens were unpinned
        // (34098f9) "fill" started meaning the parent's REAL width -- and the
        // parent is MapContent, which is the whole scrolling leg. All eight
        // tiles became 8824 wide, so one repeat of the art was stretched across
        // the entire wood and the visible 1920 of it was a 4.6x magnification.
        // That is the "zoomed in forest".
        //
        // A tile that repeats every BackgroundPeriod must be at least
        // BackgroundPeriod wide or the wood has gaps in it, and it cannot be
        // measured off the window, because MapContent is 8824 map-units wide
        // whatever size the window is. So the width belongs here beside the
        // period it has to cover, in the same units.
        //
        // 1920 against 1726 of period: the overlap is deliberate and is what
        // the tile's own AllowOverflow reason describes. The source art is
        // 1672x941, so this is a uniform 1.15x upscale rather than a stretch --
        // 1920x1080 and 1672x941 are both 16:9 to within a pixel.
        public const float BackgroundTileWidth = 1920f;
        public const float BackgroundTileHeight = 1080f;

        // Ceiling rather than an exact fit, so a longer leg (a test-only leg
        // length) warns instead of silently running out of forest.
        public const int MaxBackgroundTiles = 8;

        public static int BackgroundTilesFor(int depthCount) =>
            depthCount <= 0 ? 0 : (depthCount + 1) / 2;

        public static float BackgroundTileX(int index) => index * BackgroundPeriod;

        // How far past the last column the "the wood continues" fade sits.
        public const float FogMargin = 340f;

        // The fog's own width, and the gap it leaves after the last tile.
        public const float FogWidth = 520f;

        // RE-DERIVED, not ported. v1 put the fog's left edge at
        // (depthCount - 1) * ColumnGap + TileSize / 2 -- which is measured from
        // the content's left edge while the last COLUMN is at
        // ClearingColumnX[0] + that same span. The fog therefore started 470
        // units short of the boss and, being 520 wide and opaque, covered it.
        // The last column's own right edge is what "just past the last column"
        // has to mean.
        public static float FogX(int depthCount)
        {
            int last = depthCount <= 0 ? 0 : depthCount - 1;
            return ColumnX(last) + MaxTileWidth * 0.5f + FogMargin;
        }

        // Content has to be wide enough that Scroll() can carry the LAST column
        // all the way to FollowOffset -- not merely far enough to cover the fog
        // sitting past it. Undershooting clamps the auto-scroll short and
        // leaves the deepest rooms parked off the right edge, unclickable and
        // reading as "the map breaks past this point".
        public static float ContentWidth(int depthCount, float viewportWidth)
        {
            // Scrolling the last column to FollowOffset means a scroll of
            // exactly legSpan (ColumnX(last) - FollowOffset), and the clamp in
            // Scroll() allows at most contentWidth - viewportWidth.
            float legSpan = depthCount <= 0 ? 0f : (depthCount - 1) * ColumnGap;
            float toReachLastColumn = legSpan + viewportWidth;
            float toHoldTheFog = FogX(depthCount) + FogWidth;

            float width = toReachLastColumn > toHoldTheFog ? toReachLastColumn : toHoldTheFog;
            return width > viewportWidth ? width : viewportWidth;
        }

        // Auto-scroll, as a CONTENT OFFSET: what to add to content's x so the
        // current room sits FollowOffset in from the viewport's left edge.
        // Clamped at both ends so the camera never scrolls off its own content.
        public static float Scroll(float currentX, float contentWidth, float viewportWidth)
        {
            float maxScroll = System.Math.Max(0f, contentWidth - viewportWidth);
            float target = currentX - FollowOffset;
            if (target < 0f) target = 0f;
            if (target > maxScroll) target = maxScroll;
            return -target;
        }

        // ---- the trails between rooms ------------------------------------

        // How many straight pieces a curve is chopped into. Six is v1's number,
        // and it is where a bezier stops looking like a dogleg. uGUI has no
        // line renderer, so a curve IS a row of rotated quads and this count is
        // the whole of its smoothness.
        public const int SegmentsPerLink = 6;

        // The last column has no outgoing trail, and DescentMapGenerator gives
        // a node one link plus at most one extra -- so this is the real ceiling
        // rather than a guess with headroom.
        public static int LinkCapacity => (Columns - 1) * Rows * 2;

        public static int SegmentCapacity => LinkCapacity * SegmentsPerLink;

        // One straight piece of a trail: where it sits, how long, how turned.
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

        // A cubic bezier with a little jitter, so a trail is never
        // ruler-straight. Two rooms at the same row in adjacent columns would
        // otherwise be joined by a perfectly horizontal bar, and a forest floor
        // does not do that: the jitter is what makes it read as a path trodden
        // between trees rather than as a wiring diagram.
        //
        // DERIVED FROM THE SEED, never random. The same leg gets drawn again on
        // every refresh, and a trail that re-rolled its curve each time would
        // make the whole map writhe whenever anything else changed.
        public static UiVec PathPoint(UiVec from, UiVec to, int seed, float t)
        {
            float dx = to.X - from.X;
            float jitter = (seed * 37) % 17 - 8;

            var p1 = new UiVec(from.X + dx * 0.35f, from.Y + jitter);
            var p2 = new UiVec(to.X - dx * 0.35f, to.Y - jitter);

            float u = 1f - t;
            float a = u * u * u;
            float b = 3f * u * u * t;
            float c = 3f * u * t * t;
            float d = t * t * t;

            return new UiVec(
                a * from.X + b * p1.X + c * p2.X + d * to.X,
                a * from.Y + b * p1.Y + c * p2.Y + d * to.Y);
        }

        // Piece `index` of the curve from `from` to `to`.
        //
        // Lengthened by 2 deliberately: consecutive quads meeting exactly at a
        // point leave a hairline gap wherever the curve turns, and a trail with
        // holes in it reads as broken rather than as curved.
        public static PathSegment SegmentAt(UiVec from, UiVec to, int seed, int index)
        {
            float t0 = index / (float)SegmentsPerLink;
            float t1 = (index + 1) / (float)SegmentsPerLink;

            var a = PathPoint(from, to, seed, t0);
            var b = PathPoint(from, to, seed, t1);

            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);

            return new PathSegment(
                new UiVec(a.X + dx * 0.5f, a.Y + dy * 0.5f),
                length + 2f,
                angle);
        }
    }
}
