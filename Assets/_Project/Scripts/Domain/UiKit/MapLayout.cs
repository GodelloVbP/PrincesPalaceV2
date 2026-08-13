using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // Where a descent node sits on screen.
    //
    // Pure arithmetic over (depth, slot), in Domain for the same reason
    // FightSubmenuLayout is: the SCREEN TREE places the node pool at build time
    // and the CONTROLLER re-anchors it per leg at runtime, and those two must be
    // the same function or the map drifts from what it claims.
    //
    // A leg is DefaultDepth columns of at most MaxColumnWidth rows, so the pool
    // is that product and every position is computable without a map in hand --
    // which is what lets the tree be built and audited before any run exists.
    public static class MapLayout
    {
        public const float NodeSize = 92f;

        // Column pitch is derived from the width the leg has to fit into rather
        // than authored, so a change to DefaultLegLength cannot silently push
        // the last column off the screen.
        public const float TrackWidth = 1500f;
        public const float TrackHeight = 460f;

        public static int Columns => DescentMapGenerator.DefaultDepth;
        public static int Rows => DescentMapGenerator.MaxColumnWidth;

        // The pool's capacity: every slot a leg could possibly use.
        public static int Capacity => Columns * Rows;

        // Which pool entry a (depth, slot) pair owns. Row-major so a column's
        // entries are contiguous, which makes "hide the rest of this column"
        // a range rather than a scan.
        public static int IndexFor(int depth, int slot) => depth * Rows + slot;

        public static float ColumnX(int depth)
        {
            if (Columns <= 1) return 0f;
            return -TrackWidth * 0.5f + TrackWidth * depth / (Columns - 1);
        }

        // Rows are CENTRED on the track, so a column of one sits on the spine
        // and a column of three spreads either side of it. Centring per column
        // rather than top-aligning is what makes a fork read as a fork rather
        // than as a branch hanging off the main line.
        public static float RowY(int slotCount, int slot)
        {
            if (slotCount <= 1) return 0f;

            float pitch = TrackHeight / (Rows - 1);
            float span = pitch * (slotCount - 1);
            return span * 0.5f - pitch * slot;
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
