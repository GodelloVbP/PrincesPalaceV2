using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Dungeon
{
    public readonly struct RealmRuleViolation
    {
        public readonly string Rule;
        public readonly int NodeId;

        public RealmRuleViolation(string rule, int nodeId)
        {
            Rule = rule;
            NodeId = nodeId;
        }

        public override string ToString() => NodeId < 0 ? Rule : Rule + " (node " + NodeId + ")";
    }

    // The structural promises of a RealmMap (docs/PLAN_RUN_MAP.md section 4,
    // "Validator"), checked against the map itself rather than against the
    // generator's intent, so a hand-built or migrated map is held to the same
    // rules. Returns every violation; empty means valid.
    public static class RealmMapRules
    {
        public static List<RealmRuleViolation> Validate(RealmMap map)
        {
            var found = new List<RealmRuleViolation>();
            void Add(string rule, int nodeId = -1) => found.Add(new RealmRuleViolation(rule, nodeId));

            var entry = map.Entry;
            var boss = map.Boss;
            if (entry == null) Add("no entry");
            if (boss == null) Add("no heart");
            if (entry == null || boss == null) return found;

            // Ids and edges.
            var seen = new HashSet<int>();
            foreach (var node in map.Nodes)
            {
                if (!seen.Add(node.Id)) Add("duplicate id", node.Id);
            }

            foreach (var node in map.Nodes)
            {
                if (node.Role != RealmNodeRole.Heart && node.Next.Count == 0) Add("dead end", node.Id);

                foreach (int nextId in node.Next)
                {
                    var next = map.Node(nextId);
                    if (next == null) { Add("edge to a missing node", node.Id); continue; }

                    // Id order is the topological order RouteLengths and the
                    // layout rely on.
                    if (nextId <= node.Id) Add("edge does not ascend in id", node.Id);
                    if (next.Offset < node.Offset) Add("offset decreases along an edge", node.Id);
                    if (node.Role == RealmNodeRole.Entry && next.Offset < 1) Add("entry successor below offset 1", nextId);
                    if (node.Type == RoomType.Event && next.Type == RoomType.Event) Add("events adjacent", node.Id);
                }
            }

            var reachable = map.ReachableFromEntry();
            foreach (var node in map.Nodes)
            {
                if (!reachable.Contains(node.Id)) Add("unreachable", node.Id);
            }

            // Forks: two siblings with identical in- and out-edges.
            foreach (var group in map.Nodes.Where(n => n.Role == RealmNodeRole.ForkSibling).GroupBy(n => n.Group))
            {
                var pair = group.ToList();
                if (pair.Count != 2 || pair.Any(n => n.Offset != group.Key)
                    || !pair[0].Next.OrderBy(i => i).SequenceEqual(pair[1].Next.OrderBy(i => i)))
                {
                    Add("malformed fork", pair[0].Id);
                    continue;
                }

                var inA = map.Nodes.Where(n => n.Next.Contains(pair[0].Id)).Select(n => n.Id).OrderBy(i => i);
                var inB = map.Nodes.Where(n => n.Next.Contains(pair[1].Id)).Select(n => n.Id).OrderBy(i => i);
                if (!inA.SequenceEqual(inB)) Add("malformed fork", pair[0].Id);
            }

            // Pockets: 1..6 stretches, rooms at the rejoin offset, a prize.
            if (map.Pockets.Count < 1) Add("no pocket");
            if (map.Pockets.Count > RealmMap.MaxPockets) Add("too many pockets");
            foreach (var pocket in map.Pockets)
            {
                if (pocket.Stretch < 1 || pocket.Stretch > RealmMap.MaxPockets) Add("pocket on a bad stretch");
                if (pocket.Rooms.Count < 1) Add("empty pocket");

                var rooms = pocket.Rooms.Select(map.Node).ToList();
                if (rooms.Any(r => r == null || r.Role != RealmNodeRole.Pocket || r.Offset != pocket.Stretch + 1
                                   || r.Group != pocket.Stretch))
                {
                    Add("pocket room out of place", pocket.Stretch);
                    continue;
                }

                if (!rooms.Any(r => r.IsAnchor || r.Type == RoomType.Shop || r.Type == RoomType.Treasure
                                   || r.Type == RoomType.Cache))
                {
                    Add("pocket without a prize", rooms[0].Id);
                }

                if (rooms.Count(r => r.IsAnchor) > 1) Add("two anchors in one pocket", rooms[0].Id);
            }

            // Placement by type.
            int shops = 0;
            int rests = 0;
            int caches = 0;
            foreach (var node in map.Nodes)
            {
                if (node.Type == RoomType.Shop)
                {
                    shops++;
                    if (node.Role != RealmNodeRole.Pocket) Add("shop outside a pocket", node.Id);
                }

                if (node.Type == RoomType.Treasure && node.Role != RealmNodeRole.Pocket)
                {
                    Add("treasure outside a pocket", node.Id);
                }

                if (node.Type == RoomType.Cache)
                {
                    caches++;
                    if (node.Role != RealmNodeRole.Pocket) Add("cache outside a pocket", node.Id);
                }

                if (node.Type == RoomType.Rest)
                {
                    rests++;
                    bool beforeBoss = node.Role == RealmNodeRole.Spine && node.Offset == RealmMap.SpineLength - 1;
                    if (!(node.Role == RealmNodeRole.ForkSibling || (map.Profile.restBeforeBoss && beforeBoss)))
                    {
                        Add("rest not on a fork sibling or before the boss", node.Id);
                    }
                }

                if (node.IsAnchor && node.Role != RealmNodeRole.Pocket && node.Role != RealmNodeRole.ForkSibling)
                {
                    Add("anchor outside a pocket or fork", node.Id);
                }
            }

            if (shops > 1) Add("more than one shop");
            if (caches > 1) Add("more than one cache");
            if (rests > 1) Add("more than one rest");
            if (map.Profile.restBeforeBoss && rests != 1) Add("rest before the boss missing");

            foreach (var group in map.Nodes.Where(n => n.IsAnchor && n.Role == RealmNodeRole.ForkSibling)
                         .GroupBy(n => n.Group).Where(g => g.Count() > 1))
            {
                Add("two anchors on one fork", group.First().Id);
            }

            // An anchor is optional only if some route avoids it.
            foreach (var anchor in map.Nodes.Where(n => n.IsAnchor))
            {
                if (!BossReachableWithout(map, entry, boss, anchor.Id)) Add("anchor on a node every route walks", anchor.Id);
            }

            // Every fight node carries a chip when the profile has any weight.
            bool anyPrize = map.Profile.prizeWeights != null
                && map.Profile.prizeWeights.Any(w => w.weight > 0 && w.kind != PrizeKind.None);
            if (anyPrize)
            {
                foreach (var node in map.Nodes.Where(n => n.Type == RoomType.Fight && n.Prize == PrizeKind.None))
                {
                    Add("fight without a prize kind", node.Id);
                }
            }

            if (map.Profile.anchorCount > 0 && !map.Nodes.Any(n => n.IsAnchor)) Add("no anchor");
            return found;
        }

        public static bool IsValid(RealmMap map) => Validate(map).Count == 0;

        private static bool BossReachableWithout(RealmMap map, RealmNode entry, RealmNode boss, int blockedId)
        {
            var visited = new HashSet<int> { entry.Id };
            var queue = new Queue<int>();
            queue.Enqueue(entry.Id);
            while (queue.Count > 0)
            {
                foreach (int next in map.ReachableFrom(queue.Dequeue()))
                {
                    if (next == blockedId || !visited.Add(next)) continue;
                    if (next == boss.Id) return true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}
