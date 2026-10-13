using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Dungeon
{
    public enum RealmOrientation
    {
        // Entry at the top, the heart at the bottom: scrolling down is going
        // deeper (docs/PLAN_RUN_MAP.md section 2, Orientation).
        TopToBottom,
        BottomToTop,
    }

    // Normalised positions for a RealmMap, 0..1 on both axes with y = 0 at the
    // top. The view scales these to its canvas; nothing here knows pixels.
    //
    // Orientation is the one constant. Flipping it changes y and costs art,
    // not code.
    public static class RealmLayout
    {
        public const RealmOrientation Orientation = RealmOrientation.TopToBottom;

        // Lateral positions (0 = left). Pockets hang to alternating sides by
        // stretch so neighbouring pockets never share a side.
        private const float SpineAcross = 0.5f;
        private const float ForkLeftAcross = 0.35f;
        private const float ForkRightAcross = 0.65f;
        private const float PocketLeftAcross = 0.12f;
        private const float PocketRightAcross = 0.88f;

        public readonly struct Placement
        {
            // Longest-route row from the entry (0) to the heart.
            public readonly int Row;

            // Progress along the route, 0 at the entry, 1 at the heart,
            // whatever the orientation.
            public readonly float Along;
            public readonly float Across;
            public readonly float X;
            public readonly float Y;

            public Placement(int row, float along, float across, float x, float y)
            {
                Row = row;
                Along = along;
                Across = across;
                X = x;
                Y = y;
            }
        }

        public sealed class Result
        {
            public readonly Dictionary<int, Placement> Nodes = new Dictionary<int, Placement>();

            // The realm's height in rows: the longest route's room count plus
            // the entry. The view sizes the scrolling canvas from it.
            public int Rows;
        }

        public static Result Place(RealmMap map)
        {
            var result = new Result();
            var rows = new Dictionary<int, int>();
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                if (!rows.ContainsKey(node.Id)) rows[node.Id] = 0;
                foreach (int next in node.Next)
                {
                    int candidate = rows[node.Id] + 1;
                    if (!rows.TryGetValue(next, out int current) || candidate > current) rows[next] = candidate;
                }
            }

            int last = rows.Count == 0 ? 0 : rows.Values.Max();
            result.Rows = last + 1;

            foreach (var node in map.Nodes)
            {
                float along = last == 0 ? 0f : rows[node.Id] / (float)last;
                float across = AcrossOf(map, node);
                float y = Orientation == RealmOrientation.TopToBottom ? along : 1f - along;
                result.Nodes[node.Id] = new Placement(rows[node.Id], along, across, across, y);
            }

            return result;
        }

        private static float AcrossOf(RealmMap map, RealmNode node)
        {
            switch (node.Role)
            {
                case RealmNodeRole.ForkSibling:
                    bool first = map.Nodes.First(n => n.Role == RealmNodeRole.ForkSibling && n.Group == node.Group).Id == node.Id;
                    return first ? ForkLeftAcross : ForkRightAcross;
                case RealmNodeRole.Pocket:
                    return node.Group % 2 == 1 ? PocketLeftAcross : PocketRightAcross;
                default:
                    return SpineAcross;
            }
        }
    }
}
