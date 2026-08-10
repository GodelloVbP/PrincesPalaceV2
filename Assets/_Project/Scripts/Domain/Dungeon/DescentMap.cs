using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Dungeon
{
    // A floor as a BRANCHING DESCENT: columns of rooms left to right, every
    // room linked forward to one or two in the next column, entry on the far
    // left and the boss alone on the far right.
    //
    // A layered DAG rather than the free graph the floor used to be, and the
    // difference is the whole design. A graph lets a player wander, backtrack
    // and eventually see everything, so no choice costs anything. Layers make
    // depth strictly forward: taking one branch means the other is gone, which
    // is what turns "which room" into a decision worth making.
    //
    // Reachability is structural, not checked-and-retried. Every room is
    // seeded with a forward link before extras are added, and column 0 is a
    // single entry, so every room is reachable from the start by construction.
    public sealed class DescentNode
    {
        public int Id;
        public int Depth;

        // Position within its column, 0 at the top. Only a layout hint; the
        // view spaces rooms evenly down its column from this.
        public int Slot;

        public RoomType Type;

        // Ids in the NEXT column this room leads to. Empty for the boss.
        public readonly List<int> Next = new List<int>();
    }

    public sealed class DescentMap
    {
        public readonly List<DescentNode> Nodes = new List<DescentNode>();

        public int DepthCount => Nodes.Count == 0 ? 0 : Nodes.Max(n => n.Depth) + 1;

        public DescentNode Node(int id) => Nodes.FirstOrDefault(n => n.Id == id);

        public IEnumerable<DescentNode> AtDepth(int depth) => Nodes.Where(n => n.Depth == depth).OrderBy(n => n.Slot);

        public DescentNode Entry => Nodes.FirstOrDefault(n => n.Depth == 0);

        public DescentNode Boss => Nodes.FirstOrDefault(n => n.Type == RoomType.Boss);

        // Where a player standing on `id` may go. The empty list means the
        // floor is finished, which is only true of the boss.
        public IReadOnlyList<int> ReachableFrom(int id)
        {
            var node = Node(id);
            return node == null ? Array.Empty<int>() : node.Next;
        }

        public bool CanMove(int fromId, int toId) => ReachableFrom(fromId).Contains(toId);

        // Every node reachable from the entry, walking forward only, PLUS the
        // entry itself — unlike AllReachableFrom this answers "is every room
        // on this floor accounted for", so the entry counts as accounted for
        // without having to be reachable from itself. Used to assert the
        // generator's structural promise rather than trusting it.
        public HashSet<int> ReachableFromEntry()
        {
            if (Entry == null)
            {
                return new HashSet<int>();
            }

            var seen = AllReachableFrom(Entry.Id);
            seen.Add(Entry.Id);
            return seen;
        }

        // Every node reachable by walking forward any number of hops from
        // `id` — INCLUDING the one-hop neighbours ReachableFrom already
        // answers, unlike that method this keeps going past them. `id`
        // itself is never included: this answers "where can I still end up
        // from here", not "am I here".
        //
        // The map view's own state split (open vs. merely ahead) rides this
        // directly: ReachableFrom(id) is "open" (choosable right now), and
        // everything else in this set is "ahead" (still reachable, just not
        // on the very next move).
        public HashSet<int> AllReachableFrom(int id)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int next in ReachableFrom(id))
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }

            while (queue.Count > 0)
            {
                foreach (int next in ReachableFrom(queue.Dequeue()))
                {
                    if (seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }
    }

    public static class DescentMapGenerator
    {
        // A LEG is one map's worth of descent: an entry column the player is
        // standing in, then this many columns of rooms.
        //
        // Eight, so a leg always ends on a forced room — leg 1 on an elite,
        // leg 2 on a boss, and so on without end. The run is one continuous
        // step counter now rather than a series of self-contained floors;
        // what is displayed as "floor" is cosmetic.
        public const int DefaultLegLength = 8;

        // Kept as the old name's value for anything still thinking in
        // columns: a leg of 8 is 9 columns including the entry.
        public const int DefaultDepth = DefaultLegLength + 1;

        // Every 8th step is an elite, every 16th a boss, and the boss rule
        // wins where both apply.
        //
        // Positional overrides rather than weights, which is the same shape
        // the generator already used for the entry and boss columns — the
        // difference is only that the position is now an ABSOLUTE step
        // rather than "first" and "last".
        private const int StepsPerElite = 8;
        private const int StepsPerBoss = 16;

        // Widest a middle column gets. Three reads as a real fork without the
        // columns becoming a wall of rooms the player skims instead of reads.
        public const int MaxColumnWidth = 3;

        // The odds a room gets a SECOND forward link, on top of the one every
        // room is guaranteed. Enough that routes rejoin and the map reads as a
        // web rather than as separate lanes, rare enough that a choice usually
        // still closes something off.
        private const float ExtraLinkChance = 0.34f;

        // What a middle room can be, and how often. Combat is the floor of the
        // run, so it dominates; the rest are the reasons to prefer one branch
        // over another.
        //
        // EliteFight is deliberately NOT in this table. It used to be (weight
        // 14), on the reasoning that a forced elite every 8 steps was a floor
        // rather than the only source — but with no minimum-depth guard, that
        // let one roll as early as the very first column, directly reachable
        // from the entry. A level-1 squad facing two Elite-scaled monsters
        // before earning a single level or a single piece of gear is not a
        // hard start, it is a wall — confirmed by an actual playtest that
        // could not survive the first room. Elites are exclusively the
        // ForcedTypeAt cadence now (every StepsPerElite), so the player's
        // first one always arrives after real levelling room to prepare.
        private static readonly (RoomType Type, int Weight)[] MiddleRooms =
        {
            (RoomType.Fight, 44),
            (RoomType.Event, 14),
            (RoomType.Treasure, 12),
            (RoomType.Unknown, 8),
            (RoomType.Shop, 6),
            (RoomType.Rest, 6),
        };

        // What an absolute step is FORCED to be, or null when it rolls
        // normally.
        //
        // Boss wins where both apply, so step 16 is a boss rather than an
        // elite. Step 0 is the entry a player is standing in rather than a
        // room they chose, so it forces nothing — without that guard the
        // very first column of the run would be a boss.
        public static RoomType? ForcedTypeAt(int absoluteStep)
        {
            if (absoluteStep <= 0)
            {
                return null;
            }

            if (absoluteStep % StepsPerBoss == 0)
            {
                return RoomType.Boss;
            }

            return absoluteStep % StepsPerElite == 0 ? RoomType.EliteFight : (RoomType?)null;
        }

        // One leg of the descent: an entry column, then `legLength` columns
        // of rooms whose absolute steps run startStep+1 .. startStep+legLength.
        //
        // The map is generated a leg at a time rather than a floor at a time
        // because the descent no longer ends — there is always another leg,
        // and "floor" is a label rather than a structure. Legs align to the
        // 8-step grid, so a leg generated at a multiple of 8 always ENDS on
        // its forced room; that is what makes leg 1 finish on an elite and
        // leg 2 on a boss without either being special-cased here.
        public static DescentMap GenerateLeg(SeededRandom random, int startStep, int legLength = DefaultLegLength)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (legLength < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(legLength), legLength,
                    "A leg needs an entry, at least one middle column and a column to end on.");
            }

            var map = new DescentMap();
            int nextId = 0;
            int columnCount = legLength + 1;

            // Column 0 is always a single entry. A column holding a forced
            // Elite or Boss is width 1 as well — a fork into two bosses is
            // not a choice, it is a coin toss. Everything else is
            // 2..MaxColumnWidth wide.
            var columns = new List<List<DescentNode>>();
            for (int d = 0; d < columnCount; d++)
            {
                RoomType? forced = d == 0 ? RoomType.Entry : ForcedTypeAt(startStep + d);

                int width = forced.HasValue ? 1 : random.NextInt(2, MaxColumnWidth + 1);
                var column = new List<DescentNode>();
                for (int slot = 0; slot < width; slot++)
                {
                    column.Add(new DescentNode
                    {
                        Id = nextId++,
                        Depth = d,
                        Slot = slot,
                        Type = forced ?? PickMiddleRoom(random),
                    });
                }

                columns.Add(column);
                map.Nodes.AddRange(column);
            }

            LinkColumns(map, columns, random);
            return map;
        }

        private static RoomType PickMiddleRoom(SeededRandom random)
        {
            int total = MiddleRooms.Sum(r => r.Weight);
            int roll = random.NextInt(0, total);
            foreach (var (type, weight) in MiddleRooms)
            {
                if (roll < weight)
                {
                    return type;
                }

                roll -= weight;
            }

            return RoomType.Fight;
        }

        private static void LinkColumns(DescentMap map, List<List<DescentNode>> columns, SeededRandom random)
        {
            for (int d = 0; d < columns.Count - 1; d++)
            {
                var here = columns[d];
                var next = columns[d + 1];

                // Every room gets a forward link, so nothing is a dead end.
                // Aimed at its positional counterpart in the next column, so
                // links stay short and the map reads as flowing rather than
                // as a tangle crossing itself.
                for (int i = 0; i < here.Count; i++)
                {
                    int target = here.Count == 1
                        ? random.NextInt(0, next.Count)
                        : Math.Min(next.Count - 1, i * next.Count / here.Count);
                    here[i].Next.Add(next[target].Id);
                }

                // Every room in the next column needs an INBOUND link, or it
                // is unreachable and the generator has quietly produced a
                // room nobody can visit.
                foreach (var target in next)
                {
                    if (here.Any(source => source.Next.Contains(target.Id)))
                    {
                        continue;
                    }

                    var nearest = here.OrderBy(source =>
                        Math.Abs(source.Slot * next.Count / Math.Max(1, here.Count) - target.Slot)).First();
                    nearest.Next.Add(target.Id);
                }

                // Then the extras that make routes rejoin.
                foreach (var source in here)
                {
                    if (next.Count <= 1 || !random.NextBool(ExtraLinkChance))
                    {
                        continue;
                    }

                    var candidate = next[random.NextInt(0, next.Count)];
                    if (!source.Next.Contains(candidate.Id))
                    {
                        source.Next.Add(candidate.Id);
                    }
                }

                // Sorted so the view draws links top-to-bottom in a stable
                // order rather than in whatever order they happened to be
                // added.
                foreach (var source in here)
                {
                    source.Next.Sort((a, b) => map.Node(a).Slot.CompareTo(map.Node(b).Slot));
                }
            }
        }
    }
}
