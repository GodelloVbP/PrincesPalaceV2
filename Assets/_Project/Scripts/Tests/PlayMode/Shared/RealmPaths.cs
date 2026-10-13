using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Finding and reaching rooms on the realm map, for tests that need a
    // particular room in front of the party. Every move goes through
    // RunManager.MoveTo, the authority on adjacency, so a fixture can never
    // stand the party somewhere the map would refuse.
    public static class RealmPaths
    {
        // The realm a fresh run on `seed` opens: realm 0 on the default
        // profile, exactly what RunManager.StartRun generates.
        public static RealmMap FirstRealm(ulong seed) =>
            RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));

        // The first seed under 4000 whose first realm satisfies `wanted`.
        // Computed live, never pinned (the project's seed-search idiom).
        public static ulong SeedWhere(Func<RealmMap, bool> wanted, string what)
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                if (wanted(FirstRealm(seed))) return seed;
            }

            throw new AssertionException("No seed under 4000 has a first realm with " + what + ".");
        }

        // The room first in screen order among the entry's choices: row 0 of
        // column 1, which is the map's pad entry on a fresh run.
        public static RealmNode FirstChoice(RealmMap map) =>
            map.ReachableFrom(map.Entry.Id).Select(map.Node).OrderBy(n => n.Id).First();

        // The live run's first choice in screen order from where it stands:
        // its pad entry. Choices share a column at most with each other, and
        // within a column rows follow id.
        public static RealmNode FirstChoiceFromHere() =>
            RunManager.Choices().OrderBy(n => n.Offset).ThenBy(n => n.Id).First();

        // A room of `type` that opens its pocket (room 0): the first choice
        // from the spine room the pocket leaves, one column over; null when
        // the realm has none.
        public static RealmNode PocketHead(RealmMap map, RoomType type) =>
            map.Pockets.Select(p => map.Node(p.Rooms[0])).FirstOrDefault(n => n.Type == type);

        // A plain Fight standing alone on the spine just past a pocket, so
        // from the room before it the party chooses between the pocket and
        // this fight: a real choice, with the fight second in screen order.
        public static RealmNode FightPastAPocket(RealmMap map) =>
            map.Pockets
                .Select(p => map.SpineAt(p.Stretch + 1).ToList())
                .Where(level => level.Count == 1 && level[0].Type == RoomType.Fight)
                .Select(level => level[0])
                .FirstOrDefault();

        public static RealmNode FirstOfType(RealmMap map, RoomType type) =>
            map.Nodes.Where(n => n.Type == type).OrderBy(n => n.Id).FirstOrDefault();

        // Walks the live run, room by room, to a node from which `target` is
        // one move away, clearing each room passed as if it had been played.
        // The rooms are NOT resolved: no fight, no treasure, no event.
        public static void StandBefore(RealmNode target)
        {
            var map = RunManager.Map;
            Assert.IsNotNull(map, "no realm to walk");

            var path = PathTo(map, RunManager.CurrentNode.Id, target.Id);
            Assert.IsNotNull(path, $"node {target.Id} cannot be reached from {RunManager.CurrentNode.Id}");

            foreach (int id in path.Take(path.Count - 1))
            {
                Assert.IsTrue(RunManager.MoveTo(id), $"MoveTo({id}) refused");
                RunManager.ClearCurrentRoom();
            }
        }

        // Shortest forward path from `fromId` to `toId`, excluding `fromId`.
        private static List<int> PathTo(RealmMap map, int fromId, int toId)
        {
            var parent = new Dictionary<int, int> { [fromId] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(fromId);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == toId) break;
                foreach (int next in map.ReachableFrom(at))
                {
                    if (parent.ContainsKey(next)) continue;
                    parent[next] = at;
                    queue.Enqueue(next);
                }
            }

            if (!parent.ContainsKey(toId)) return null;
            var path = new List<int>();
            for (int at = toId; at != fromId; at = parent[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        // The pool GameObject name a node is drawn on (MapColumns).
        public static string ButtonName(RealmNode node) =>
            "MapNode" + MapColumns.Of(RunManager.Map).IndexOf(node.Id);
    }
}
