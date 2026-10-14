using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Bot
{
    // One map node as the bot reads it: what a player sees on the realm map.
    public readonly struct MapNodeView
    {
        public readonly int Id;
        public readonly RoomType Type;
        public readonly RealmNodeRole Role;

        // The chip the card shows after the empty-pool swap to gold. None for
        // a node that pays no chip.
        public readonly PrizeKind Prize;

        // 1..3 for a fight or anchor, 0 for every other node.
        public readonly int Strength;

        // Rooms still to walk from this node to the heart, on the shortest and
        // the longest route. The heart is 0.
        public readonly int ShortestToHeart;
        public readonly int LongestToHeart;

        // Rooms this node adds over the shortest choice on offer at the
        // current node. 0 for the best-placed choice and for nodes not on offer.
        public readonly int DetourRooms;

        // Anchors met on the route from here to the heart that holds the most,
        // this node included.
        public readonly int AnchorsAhead;

        public MapNodeView(int id, RoomType type, RealmNodeRole role, PrizeKind prize, int strength,
            int shortestToHeart, int longestToHeart, int detourRooms, int anchorsAhead)
        {
            Id = id;
            Type = type;
            Role = role;
            Prize = prize;
            Strength = strength;
            ShortestToHeart = shortestToHeart;
            LongestToHeart = longestToHeart;
            DetourRooms = detourRooms;
            AnchorsAhead = anchorsAhead;
        }

        public bool InPocket => Role == RealmNodeRole.Pocket;
        public bool IsAnchor => Type == RoomType.EliteFight;
    }

    // The realm map flattened for an IRunPolicy (docs/PLAN_RUN_MAP.md section 8,
    // P3): route lengths to the heart, pocket membership, anchors, strength and
    // prize per node, for every node still ahead of the party. Core fills it at
    // each choice point from the live map, the frozen strengths and the chips
    // the cards show; a policy never reads the run itself.
    public sealed class MapView
    {
        private readonly Dictionary<int, MapNodeView> _nodes;
        private readonly Dictionary<int, int[]> _next;

        // Rooms from the node the party stands on to the heart.
        public readonly int ShortestToHeart;
        public readonly int LongestToHeart;

        // Anchors still ahead of the party. An anchor left alive waits at the
        // heart (plan section 6).
        public readonly int AnchorsAhead;

        private MapView(Dictionary<int, MapNodeView> nodes, Dictionary<int, int[]> next,
            int shortest, int longest, int anchors)
        {
            _nodes = nodes;
            _next = next;
            ShortestToHeart = shortest;
            LongestToHeart = longest;
            AnchorsAhead = anchors;
        }

        public bool Has(RealmNode node) => node != null && _nodes.ContainsKey(node.Id);

        public MapNodeView Of(RealmNode node) => _nodes[node.Id];

        // The views of the rooms `node` leads to, empty at the heart.
        public IEnumerable<MapNodeView> After(RealmNode node)
        {
            if (node == null || !_next.TryGetValue(node.Id, out var ids)) yield break;
            foreach (int id in ids) yield return _nodes[id];
        }

        // Memoised walk toward the heart, so no id order is assumed.
        private static void Measure(RealmMap map, RealmNode node,
            Dictionary<int, int> shortest, Dictionary<int, int> longest, Dictionary<int, int> anchors)
        {
            if (shortest.ContainsKey(node.Id)) return;

            int lo = int.MaxValue / 2;
            int hi = 0;
            int anchor = 0;
            foreach (int nextId in node.Next)
            {
                var next = map.Node(nextId);
                if (next == null) continue;
                Measure(map, next, shortest, longest, anchors);
                lo = Math.Min(lo, shortest[nextId] + 1);
                hi = Math.Max(hi, longest[nextId] + 1);
                anchor = Math.Max(anchor, anchors[nextId]);
            }

            if (node.Next.Count == 0 && node.Role == RealmNodeRole.Heart) lo = 0;
            shortest[node.Id] = lo;
            longest[node.Id] = hi;
            anchors[node.Id] = anchor + (node.IsAnchor ? 1 : 0);
        }

        // Builds the view from `current`. `strengthOf` and `prizeOf` are
        // Core's: the frozen strength entry and the card's effective chip.
        public static MapView Build(RealmMap map, RealmNode current,
            Func<RealmNode, int> strengthOf, Func<RealmNode, PrizeKind> prizeOf)
        {
            if (map == null || current == null) return null;

            var shortest = new Dictionary<int, int>();
            var longest = new Dictionary<int, int>();
            var anchors = new Dictionary<int, int>();
            foreach (var node in map.Nodes) Measure(map, node, shortest, longest, anchors);

            var ahead = new HashSet<int>(map.AllReachableFrom(current.Id));
            int bestOffered = current.Next.Count == 0
                ? 0
                : current.Next.Min(id => shortest.TryGetValue(id, out int s) ? s : int.MaxValue / 2);

            var nodes = new Dictionary<int, MapNodeView>();
            var next = new Dictionary<int, int[]>();
            foreach (var node in map.Nodes)
            {
                if (!ahead.Contains(node.Id)) continue;

                bool offered = current.Next.Contains(node.Id);
                int detour = offered ? shortest[node.Id] - bestOffered : 0;
                nodes[node.Id] = new MapNodeView(node.Id, node.Type, node.Role,
                    prizeOf?.Invoke(node) ?? node.Prize, strengthOf?.Invoke(node) ?? 0,
                    shortest[node.Id], longest[node.Id], detour, anchors[node.Id]);
                next[node.Id] = node.Next.Where(ahead.Contains).ToArray();
            }

            return new MapView(nodes, next, shortest[current.Id], longest[current.Id], anchors[current.Id] - (current.IsAnchor ? 1 : 0));
        }
    }
}
