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
        // Eight, so a leg always ends on a forced BOSS ROOM — "boss every
        // floor" (D6/decision #3) — with a forced elite mid-leg and a forced
        // rest the step before the boss. The run is one continuous step
        // counter now rather than a series of self-contained floors; what is
        // displayed as "floor" is cosmetic.
        public const int DefaultLegLength = 8;

        // Kept as the old name's value for anything still thinking in
        // columns: a leg of 8 is 9 columns including the entry.
        public const int DefaultDepth = DefaultLegLength + 1;

        // Every leg ends on a boss (step ≡ 0 mod StepsPerBoss) and carries a
        // forced elite at its midpoint (step ≡ EliteOffsetInLeg mod
        // StepsPerBoss). Both are POSITIONAL overrides rather than weights,
        // the same shape the generator already used for the entry and boss
        // columns — the difference is only that the position is now an
        // ABSOLUTE step rather than "first" and "last".
        //
        // Was 16 (bosses every OTHER leg); D6 moved it to 8 so every leg
        // closes on its own boss instead of alternating boss/elite legs.
        private const int StepsPerBoss = 8;
        private const int EliteOffsetInLeg = 4;

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
        // ForcedTypeAt cadence now (step ≡ EliteOffsetInLeg within each leg),
        // so the player's first one always arrives after real levelling room
        // to prepare.
        // Unknown ("?") is retired from generation — its weight (8) went to
        // Fight rather than being redistributed across the placeholders, so
        // Event/Treasure/Shop/Rest keep the same ratio to each other they had
        // before. The enum value, its RoomResolution case and its map-icon
        // handling all stay: a saved `currentNodeId` from before this change
        // can still point at one, and it must still resolve.
        // Fight's weight here is NOT its measured share — see
        // EnforceEveryRoadHasVariety just below. Rule (b) retypes roughly
        // one rolled room on every road that would otherwise have come out
        // all-Fight, and how often that fires depends only on how DOMINANT
        // Fight is at roll time, not on the exact table weight past a
        // point. Measured over 500 seeds x 5 legs, raising Fight from the
        // pre-(a)/(b)/(c) table's 52 (57.8% of the table, 44.2% measured)
        // through 90, 200 and up plateaus the measured share at ~55.2% no
        // matter how much higher Fight goes — that ceiling, not the table
        // weight, is what (b) actually lets through. 250 (Event/Treasure/
        // Shop/Rest scaled down to a matching 7/6/3/3, same 14:12:6:6 ratio
        // as before) lands the measured share at 54.6%, comfortably inside
        // the 53-57% band DescentRoadVariationTests pins without sitting on
        // either edge of it.
        //
        // Shop's weight (3, same as Rest) reads generous on paper but is not
        // in practice: measured over 2000 seeds at this table, Shop lands on
        // only 5.9% of rolled nodes, and because a node's odds compound
        // across a whole leg, 31% of generated legs contained ZERO Shop node
        // on ANY branch — a player could reach the boss having never once
        // seen a shop. Raising Shop's table weight would only shrink that
        // number, not remove it, and every point taken from Fight to do it
        // erodes the 53-57% band above. EnsureLegHasShop (below,
        // after EnforceEveryRoadHasVariety) guarantees at least one Shop per
        // leg directly instead: a leg that already rolled one is untouched,
        // and one that did not gets a single node retyped, same shape as
        // EnforceEveryRoadHasVariety's own (b) fix-up.
        private static readonly (RoomType Type, int Weight)[] MiddleRooms =
        {
            (RoomType.Fight, 250),
            (RoomType.Event, 7),
            (RoomType.Treasure, 6),
            (RoomType.Shop, 3),
            (RoomType.Rest, 3),
        };

        // What an absolute step is FORCED to be, or null when it rolls
        // normally.
        //
        // Boss wins where both apply (moot now that StepsPerBoss and the
        // elite/rest residues share one modulus and cannot collide, but kept
        // as the explicit ordering so that stays true by construction rather
        // than by accident). Step 0 is the entry a player is standing in
        // rather than a room they chose, so it forces nothing — without that
        // guard the very first column of the run would be a boss.
        public static RoomType? ForcedTypeAt(int absoluteStep, bool restBeforeBoss = false)
        {
            if (absoluteStep <= 0)
            {
                return null;
            }

            if (absoluteStep % StepsPerBoss == 0)
            {
                return RoomType.Boss;
            }

            // A GUARANTEED REST ON THE STEP BEFORE EACH BOSS -- level 30 of the
            // reward track.
            //
            // Access rather than amount, and that is the whole design: rest
            // already heals the party to FULL (RoomResolution returns
            // Outcome(Kind.Rest, healsPartyToFull: true)), so a "rest heals
            // more" reward would have had nothing to improve. What a player
            // actually lacks is the certainty of getting one before the fight
            // that matters.
            //
            // Cannot collide with the elite cadence: rests land where
            // step % StepsPerBoss == StepsPerBoss - 1 (≡ 7) and elites where
            // step % StepsPerBoss == EliteOffsetInLeg (≡ 4), so no step is
            // ever both. Boss still wins outright, which keeps step 8 a boss
            // rather than anything else.
            //
            // A parameter rather than a lookup, because Domain is engine-free
            // and has no way to ask a save what the squad has earned. The
            // caller threads it in.
            if (restBeforeBoss && absoluteStep % StepsPerBoss == StepsPerBoss - 1)
            {
                return RoomType.Rest;
            }

            return absoluteStep % StepsPerBoss == EliteOffsetInLeg ? RoomType.EliteFight : (RoomType?)null;
        }

        // One leg of the descent: an entry column, then `legLength` columns
        // of rooms whose absolute steps run startStep+1 .. startStep+legLength.
        //
        // The map is generated a leg at a time rather than a floor at a time
        // because the descent no longer ends — there is always another leg,
        // and "floor" is a label rather than a structure. Legs align to the
        // 8-step grid, so a leg generated at a multiple of 8 always ENDS on
        // its forced boss room (and carries its forced elite at the
        // midpoint) without either being special-cased here.
        public static DescentMap GenerateLeg(SeededRandom random, int startStep,
            int legLength = DefaultLegLength, bool restBeforeBoss = false)
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
            //
            // Types are NOT rolled here. Linking only needs widths and
            // positions, never a room's type, so the skeleton is built with
            // forced types set and everything else left as an overwritten
            // placeholder — RollTypes fills the rest in AFTER linking, which
            // is what lets it know a node's actual predecessors instead of
            // guessing at them.
            var columns = new List<List<DescentNode>>();
            for (int d = 0; d < columnCount; d++)
            {
                RoomType? forced = d == 0 ? RoomType.Entry : ForcedTypeAt(startStep + d, restBeforeBoss);

                int width = forced.HasValue ? 1 : random.NextInt(2, MaxColumnWidth + 1);
                var column = new List<DescentNode>();
                for (int slot = 0; slot < width; slot++)
                {
                    column.Add(new DescentNode
                    {
                        Id = nextId++,
                        Depth = d,
                        Slot = slot,
                        Type = forced ?? RoomType.Fight, // placeholder; RollTypes overwrites
                    });
                }

                columns.Add(column);
                map.Nodes.AddRange(column);
            }

            LinkColumns(map, columns, random);
            RollTypes(columns, random, startStep, restBeforeBoss);
            EnforceEveryRoadHasVariety(map, columns, startStep, restBeforeBoss);
            EnsureLegHasShop(map, columns, startStep, restBeforeBoss);
            return map;
        }

        // Rolls every non-forced node's type, column by column, constrained
        // by the two structural rules a road must never break:
        //
        //   (a) no three consecutive ROLLED rooms of the same type along any
        //       road — a forced room (elite/boss/rest) is exempt from the
        //       count but still breaks a streak in progress;
        //   (c) a column of 2+ nodes is never all the same type, or a fork
        //       stops being a choice.
        //
        // Both are enforced by narrowing the weighted table BEFORE rolling
        // (excluding whichever types would violate the rule), not by
        // rolling-and-retrying — so this is O(nodes), no retry loop, and the
        // rng stream advances exactly once per unforced node regardless of
        // how many types got excluded.
        //
        // (b) — every road has at least one non-Fight rolled room — is NOT
        // handled here. It is a whole-road property (every column
        // contributes exactly one node to any given road, so a road is "all
        // Fight" only once every rolled column on it happened to roll Fight)
        // and cannot be guaranteed by a per-node local exclusion the way (a)
        // and (c) can. EnforceEveryRoadHasVariety fixes it afterward.
        private static void RollTypes(List<List<DescentNode>> columns, SeededRandom random, int startStep,
            bool restBeforeBoss)
        {
            // The longest run of consecutive same-rolled-type rooms ending
            // AT this node, along whichever incoming road makes it longest.
            // 0 for a forced (or entry) node: forced rooms reset the streak
            // rather than extend it.
            var runLength = new Dictionary<int, int>();

            for (int d = 0; d < columns.Count; d++)
            {
                var column = columns[d];
                var predecessors = d == 0
                    ? new List<DescentNode>()
                    : columns[d - 1];

                RoomType? uniformSoFar = null;
                bool columnIsUniformSoFar = true;

                for (int slot = 0; slot < column.Count; slot++)
                {
                    var node = column[slot];
                    bool forced = d == 0 || ForcedTypeAt(startStep + d, restBeforeBoss).HasValue;
                    if (forced)
                    {
                        // Type already set on the skeleton (Entry, or
                        // whatever ForcedTypeAt returned).
                        runLength[node.Id] = 0;
                        continue;
                    }

                    var incoming = predecessors.Where(p => p.Next.Contains(node.Id)).ToList();

                    // (a): exclude any type that a predecessor already
                    // carries a 2-long streak of — picking it here would
                    // make three.
                    var excluded = new HashSet<RoomType>();
                    foreach (var p in incoming)
                    {
                        if (runLength.TryGetValue(p.Id, out int len) && len >= 2)
                        {
                            excluded.Add(p.Type);
                        }
                    }

                    // (c): the LAST node of a multi-room column additionally
                    // excludes whatever every earlier sibling in this column
                    // already agreed on, so the column cannot close out
                    // uniform. Skipped if it would leave nothing pickable —
                    // (a) takes priority, since a broken streak is worse
                    // than a same-typed column.
                    bool isClosingSlot = slot == column.Count - 1 && column.Count >= 2;
                    if (isClosingSlot && columnIsUniformSoFar && uniformSoFar.HasValue)
                    {
                        var withColumnRule = new HashSet<RoomType>(excluded) { uniformSoFar.Value };
                        if (MiddleRooms.Any(r => !withColumnRule.Contains(r.Type)))
                        {
                            excluded = withColumnRule;
                        }
                    }

                    var type = PickMiddleRoom(random, excluded);
                    node.Type = type;

                    if (slot == 0)
                    {
                        uniformSoFar = type;
                    }
                    else if (uniformSoFar != type)
                    {
                        columnIsUniformSoFar = false;
                    }

                    int longestMatchingIncoming = 0;
                    foreach (var p in incoming)
                    {
                        if (p.Type == type && runLength.TryGetValue(p.Id, out int len))
                        {
                            longestMatchingIncoming = Math.Max(longestMatchingIncoming, len);
                        }
                    }

                    runLength[node.Id] = longestMatchingIncoming + 1;
                }
            }
        }

        private static RoomType PickMiddleRoom(SeededRandom random, ISet<RoomType> excluded = null)
        {
            var pool = excluded == null || excluded.Count == 0
                ? MiddleRooms
                : MiddleRooms.Where(r => !excluded.Contains(r.Type)).ToArray();

            // Every type excluded is only possible if the caller handed in
            // a set wide enough to swallow the whole table — neither (a)
            // nor (c) above ever does that (each checks the pool is
            // non-empty before committing to the extra exclusion). Falling
            // back to the full table rather than throwing keeps this a
            // bounded, no-retry pick even in a combination nobody
            // anticipated.
            if (pool.Length == 0)
            {
                pool = MiddleRooms;
            }

            int total = pool.Sum(r => r.Weight);
            int roll = random.NextInt(0, total);
            foreach (var (type, weight) in pool)
            {
                if (roll < weight)
                {
                    return type;
                }

                roll -= weight;
            }

            return pool[pool.Length - 1].Type;
        }

        // Highest-weight-first, Fight excluded — the order EnforceEvery
        // RoadHasVariety tries substitute types in.
        private static readonly RoomType[] NonFightByWeightDescending = MiddleRooms
            .Where(r => r.Type != RoomType.Fight)
            .OrderByDescending(r => r.Weight)
            .Select(r => r.Type)
            .ToArray();

        // (b): every road (entry to boss, following links) has at least one
        // non-Fight ROLLED room. RollTypes cannot guarantee this locally —
        // it is a property of the whole road, not of one node's neighbours
        // — so this walks every road after the fact and, for any road that
        // is all Fight, retypes the first rolled node on it for which a
        // substitution exists that does not itself break (a) or (c).
        //
        // Bounded rather than a search: for each violating road, at most
        // (rolled nodes on the road) x (non-Fight types) substitutions are
        // tried, and each is checked, not retried-until-lucky.
        private static void EnforceEveryRoadHasVariety(DescentMap map, List<List<DescentNode>> columns,
            int startStep, bool restBeforeBoss)
        {
            bool IsForced(DescentNode n) => n.Depth == 0 || ForcedTypeAt(startStep + n.Depth, restBeforeBoss).HasValue;

            foreach (var road in EnumerateRoads(map))
            {
                bool hasNonFightRolled = road.Any(n => !IsForced(n) && n.Type != RoomType.Fight);
                if (hasNonFightRolled)
                {
                    continue;
                }

                var rolledOnRoad = road.Where(n => !IsForced(n)).ToList();
                bool fixedRoad = false;

                foreach (var node in rolledOnRoad)
                {
                    foreach (var candidate in NonFightByWeightDescending)
                    {
                        if (!RespectsLocalRules(node, candidate, columns, startStep, restBeforeBoss))
                        {
                            continue;
                        }

                        node.Type = candidate;
                        fixedRoad = true;
                        break;
                    }

                    if (fixedRoad)
                    {
                        break;
                    }
                }

                // No candidate on this road could be swapped without
                // breaking (a) or (c) — leaves the road as the one
                // documented exception to (b) rather than forcing a
                // violation of the other two rules to satisfy this one.
            }
        }

        // ONE SHOP GUARANTEED PER LEG. Shop's table weight (see MiddleRooms)
        // leaves 31% of legs with no Shop node on any branch at all — a
        // per-node weight cannot fix a whole-leg property any more than (b)
        // could be fixed at roll time, so this runs the same way (b) does:
        // after every other pass, only when the leg-wide property is
        // already missing.
        //
        // No-op (byte-identical output) whenever the leg already rolled a
        // Shop naturally, so this only ever touches the legs the 31% figure
        // is about. When it does act, it retypes exactly one non-forced,
        // non-entry node — picked as the FIRST such node (non-Fight
        // candidates before Fight ones, columns in order and then slot
        // within each group) for which RespectsLocalRules holds, so this
        // stays deterministic without spending an extra draw from
        // `random`: nothing after this point in GenerateLeg consumes the
        // rng, and keeping this pass rng-free means it can never perturb
        // any other seeded output.
        //
        // Non-Fight-first, not just first-in-column-order: Fight is over
        // half of every rolled node, so a plain in-order scan converts a
        // Fight room into the guaranteed Shop most of the time, and
        // measured over DescentRoadVariationTests' own 500-seed x 5-leg
        // sample that alone dragged Fight's share from 54.6% to 52.7% —
        // out of the 53-57% band the road-variation tests pin, and by
        // nearly 2 points, not the "well under 1%" a single retype per
        // ~31% of legs would suggest if the retype landed on a random type.
        // Preferring a non-Fight room to give up its slot instead — an
        // Event or Treasure becoming the Shop, which the leg already had
        // in surplus — leaves Fight's share at 54.6%, back inside the band,
        // while the leg-has-a-Shop guarantee itself is unaffected either
        // way.
        private static void EnsureLegHasShop(DescentMap map, List<List<DescentNode>> columns, int startStep,
            bool restBeforeBoss)
        {
            if (map.Nodes.Any(n => n.Type == RoomType.Shop))
            {
                return;
            }

            bool IsForced(DescentNode n) => n.Depth == 0 || ForcedTypeAt(startStep + n.Depth, restBeforeBoss).HasValue;

            var candidates = map.Nodes.Where(n => !IsForced(n)).OrderBy(n => n.Type == RoomType.Fight ? 1 : 0);
            foreach (var node in candidates)
            {
                if (!RespectsLocalRules(node, RoomType.Shop, columns, startStep, restBeforeBoss))
                {
                    continue;
                }

                node.Type = RoomType.Shop;
                return;
            }

            // No candidate node in the whole leg could become Shop without
            // breaking (a) or (c) — leaves the leg as the one documented
            // exception to the guarantee, same shape as
            // EnforceEveryRoadHasVariety's own exception to (b).
        }

        // Whether NODE could legally become CANDIDATE without breaking (a)
        // against either direction along the road or (c) against the rest
        // of its column. Recomputed from the map as it currently stands, so
        // a fix made earlier in the same pass is accounted for.
        //
        // Both directions matter, not just the backward one: retyping NODE
        // is the only thing this pass ever does, but a road runs through it
        // in both directions, and a successor already typed CANDIDATE could
        // turn a now-two-long incoming streak into three just as easily as
        // a predecessor could.
        private static bool RespectsLocalRules(DescentNode node, RoomType candidate,
            List<List<DescentNode>> columns, int startStep, bool restBeforeBoss)
        {
            var column = columns[node.Depth];
            if (column.Count >= 2 && column.All(n => n.Id == node.Id || n.Type == candidate))
            {
                return false; // (c): would make the column uniform
            }

            int backwardStreak = BackwardStreakLength(node, candidate, columns, startStep, restBeforeBoss);
            if (backwardStreak >= 3)
            {
                return false; // (a): a predecessor chain already runs two of this type
            }

            foreach (int nextId in node.Next)
            {
                var successor = columns[node.Depth + 1].First(n => n.Id == nextId);
                bool successorForced = ForcedTypeAt(startStep + successor.Depth, restBeforeBoss).HasValue;
                if (!successorForced && successor.Type == candidate && backwardStreak + 1 >= 3)
                {
                    return false; // (a): would make three going forward into this successor
                }
            }

            return true;
        }

        // The length of the consecutive same-TYPE chain of rolled rooms
        // that would end at NODE if NODE's type were TYPE, walking
        // backward through matching, non-forced predecessors. Capped at 3:
        // callers only ever care whether it reaches 3, not how far past it
        // runs, and capping keeps this bounded regardless of leg length.
        private static int BackwardStreakLength(DescentNode node, RoomType type, List<List<DescentNode>> columns,
            int startStep, bool restBeforeBoss)
        {
            int length = 1;
            var current = node;
            while (length < 3 && current.Depth > 0)
            {
                var prior = columns[current.Depth - 1].FirstOrDefault(p => p.Next.Contains(current.Id) &&
                    p.Type == type && !(p.Depth == 0 || ForcedTypeAt(startStep + p.Depth, restBeforeBoss).HasValue));
                if (prior == null)
                {
                    break;
                }

                length++;
                current = prior;
            }

            return length;
        }

        // Every entry-to-boss path through the leg, following links
        // forward. Bounded by leg width (MaxColumnWidth per column) and
        // length, both small and fixed per leg, so this never approaches
        // being unbounded even though it is exponential in shape.
        private static IEnumerable<List<DescentNode>> EnumerateRoads(DescentMap map)
        {
            var entry = map.Entry;
            if (entry == null)
            {
                yield break;
            }

            var stack = new Stack<List<DescentNode>>();
            stack.Push(new List<DescentNode> { entry });

            while (stack.Count > 0)
            {
                var path = stack.Pop();
                var last = path[path.Count - 1];
                if (last.Next.Count == 0)
                {
                    yield return path;
                    continue;
                }

                foreach (int nextId in last.Next)
                {
                    var extended = new List<DescentNode>(path) { map.Node(nextId) };
                    stack.Push(extended);
                }
            }
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
