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
    }
}
