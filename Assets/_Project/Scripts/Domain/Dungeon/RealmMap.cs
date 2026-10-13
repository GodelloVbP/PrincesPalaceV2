using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Dungeon
{
    // Where a node sits in the realm's shape. The roles are the model:
    // placement rules, the skip heal and the layout all read them instead of
    // re-deriving them from edges.
    public enum RealmNodeRole
    {
        // Offset 0, the room the party stands in.
        Entry,

        // Offsets 1..7 with no sibling.
        Spine,

        // One of two siblings at one offset with identical in- and out-edges.
        ForkSibling,

        // A room in a pocket's chain. Group is the pocket's stretch.
        Pocket,

        // Offset 8, the boss.
        Heart,
    }

    public sealed class RealmNode
    {
        public int Id;

        // 0..8. The difficulty clock is legStartStep + Offset (plan section 3).
        public int Offset;

        public RoomType Type;
        public PrizeKind Prize;
        public RealmNodeRole Role;

        // Fork siblings: their shared offset. Pocket rooms: the stretch. Else 0.
        public int Group;

        // Ids this node leads to. Empty for the heart.
        public readonly List<int> Next = new List<int>();

        // An anchor is an elite on the map that waits at the heart if left
        // alive (plan section 6). RoomType has no separate value: on the map
        // it is the one-enemy elite fight.
        public bool IsAnchor => Type == RoomType.EliteFight;
    }

    // A loop that leaves the spine at offset Stretch and rejoins at
    // Stretch + 1. Rooms are in chain order, all at offset Stretch + 1.
    public sealed class RealmPocket
    {
        public int Stretch;
        public readonly List<int> Rooms = new List<int>();
    }

    // One realm as a forward-only DAG: an 8-step spine (entry, 7 rooms, the
    // heart), forks, and pockets (docs/PLAN_RUN_MAP.md sections 3-4). Keeps the
    // query surface of the DescentMap it replaces.
    public sealed class RealmMap
    {
        public const int SpineLength = 8;
        public const int MaxPockets = 6;

        public RealmProfile Profile;
        public int LegStartStep;

        public readonly List<RealmNode> Nodes = new List<RealmNode>();
        public readonly List<RealmPocket> Pockets = new List<RealmPocket>();

        private Dictionary<int, RealmNode> _byId;

        public RealmNode Entry => Nodes.FirstOrDefault(n => n.Role == RealmNodeRole.Entry);

        public RealmNode Boss => Nodes.FirstOrDefault(n => n.Role == RealmNodeRole.Heart);

        public RealmNode Node(int id)
        {
            if (_byId == null || _byId.Count != Nodes.Count)
            {
                _byId = new Dictionary<int, RealmNode>();
                foreach (var node in Nodes) _byId[node.Id] = node;
            }

            return _byId.TryGetValue(id, out var found) ? found : null;
        }

        // The spine nodes standing at `offset`: one, or two siblings of a fork.
        public IEnumerable<RealmNode> SpineAt(int offset)
        {
            return Nodes.Where(n => n.Offset == offset
                && (n.Role == RealmNodeRole.Entry || n.Role == RealmNodeRole.Spine
                    || n.Role == RealmNodeRole.ForkSibling || n.Role == RealmNodeRole.Heart));
        }

        public RealmPocket PocketAt(int stretch) => Pockets.FirstOrDefault(p => p.Stretch == stretch);

        public IReadOnlyList<int> ReachableFrom(int id)
        {
            var node = Node(id);
            return node == null ? Array.Empty<int>() : node.Next;
        }

        public bool CanMove(int fromId, int toId) => ReachableFrom(fromId).Contains(toId);

        // Every node reachable from the entry, plus the entry itself.
        public HashSet<int> ReachableFromEntry()
        {
            var entry = Entry;
            if (entry == null) return new HashSet<int>();

            var seen = AllReachableFrom(entry.Id);
            seen.Add(entry.Id);
            return seen;
        }

        // Every node reachable by walking forward any number of hops from
        // `id`, excluding `id` itself.
        public HashSet<int> AllReachableFrom(int id)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int next in ReachableFrom(id))
            {
                if (seen.Add(next)) queue.Enqueue(next);
            }

            while (queue.Count > 0)
            {
                foreach (int next in ReachableFrom(queue.Dequeue()))
                {
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }

            return seen;
        }

        // Rooms walked on the shortest and longest entry-to-heart route (the
        // entry is not a room). Nodes are stored in id order along edges, so
        // one backwards pass is a topological order.
        public void RouteLengths(out int shortest, out int longest)
        {
            var order = Nodes.OrderBy(n => n.Id).ToList();
            var min = new Dictionary<int, int>();
            var max = new Dictionary<int, int>();
            foreach (var node in order)
            {
                min[node.Id] = node.Role == RealmNodeRole.Entry ? 0 : int.MaxValue;
                max[node.Id] = node.Role == RealmNodeRole.Entry ? 0 : int.MinValue;
            }

            foreach (var node in order)
            {
                if (min[node.Id] == int.MaxValue) continue;
                foreach (int next in node.Next)
                {
                    if (min[node.Id] + 1 < min[next]) min[next] = min[node.Id] + 1;
                    if (max[node.Id] + 1 > max[next]) max[next] = max[node.Id] + 1;
                }
            }

            var boss = Boss;
            shortest = boss == null ? 0 : min[boss.Id];
            longest = boss == null ? 0 : max[boss.Id];
        }
    }
}
