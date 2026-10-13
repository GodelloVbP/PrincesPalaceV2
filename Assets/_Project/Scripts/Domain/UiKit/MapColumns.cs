using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // THE COLUMN PROJECTION (docs/PLAN_RUN_MAP.md section 8, "Column
    // projection (P2)"): the horizontal map screen draws a RealmMap with a
    // node's Offset as its column and the nodes sharing an offset (fork
    // siblings, and the pocket rooms that rejoin there) as rows in it. Every
    // choice from any node sits in its own column or the next, so every
    // choice is on screen beside the party. A throwaway adapter: P4's
    // vertical screen places nodes from RealmLayout and deletes this.
    //
    // Rows within a column are in id order, which the generator assigns along
    // edges: a pocket's rooms (chain order) come before the spine room they
    // rejoin at, so a pocket reads top-down into the spine.
    public sealed class MapColumns
    {
        // Offsets 0..RealmMap.SpineLength.
        public const int Count = RealmMap.SpineLength + 1;

        // The fullest column: a fork's two siblings plus the longest pocket
        // the default profile builds. A designed profile with longer pockets
        // overflows it; Of() reports the overflow rather than drawing over a
        // neighbour.
        public const int MaxRows = 2 + RealmProfiles.MaxDefaultPocketLength;

        private readonly List<RealmNode>[] _columns = new List<RealmNode>[Count];
        private readonly Dictionary<int, int> _rowOf = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _columnOf = new Dictionary<int, int>();

        // Ids that did not fit in MaxRows (or sat past the last column). Empty
        // for every default-profile realm.
        public readonly List<int> Overflow = new List<int>();

        public static MapColumns Of(RealmMap map)
        {
            var columns = new MapColumns();
            for (int c = 0; c < Count; c++) columns._columns[c] = new List<RealmNode>();
            if (map == null) return columns;

            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                int column = node.Offset;
                if (column < 0 || column >= Count || columns._columns[column].Count >= MaxRows)
                {
                    columns.Overflow.Add(node.Id);
                    continue;
                }

                columns._columnOf[node.Id] = column;
                columns._rowOf[node.Id] = columns._columns[column].Count;
                columns._columns[column].Add(node);
            }

            return columns;
        }

        public IReadOnlyList<RealmNode> Column(int column) =>
            column >= 0 && column < Count ? _columns[column] : (IReadOnlyList<RealmNode>)new RealmNode[0];

        public int CountIn(int column) => Column(column).Count;

        public bool Contains(int nodeId) => _columnOf.ContainsKey(nodeId);

        public int ColumnOf(int nodeId) => _columnOf.TryGetValue(nodeId, out int c) ? c : -1;

        public int RowOf(int nodeId) => _rowOf.TryGetValue(nodeId, out int r) ? r : -1;

        // The pool entry a node owns, or -1 when it overflowed.
        public int IndexOf(int nodeId) =>
            Contains(nodeId) ? MapLayout.IndexFor(ColumnOf(nodeId), RowOf(nodeId)) : -1;

        // The node a pool entry shows, or null for an unused entry.
        public RealmNode At(int index)
        {
            if (index < 0) return null;
            int column = index / MapLayout.Rows;
            int row = index % MapLayout.Rows;
            var nodes = Column(column);
            return row < nodes.Count ? nodes[row] : null;
        }

        // Content-local centre of a node's tile.
        public UiVec PositionOf(int nodeId)
        {
            int column = ColumnOf(nodeId);
            if (column < 0) return new UiVec(MapLayout.ColumnX(0), MapLayout.RowY(0, 1));
            return new UiVec(MapLayout.ColumnX(column), MapLayout.RowY(RowOf(nodeId), CountIn(column)));
        }
    }
}
