using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Dungeon
{
    // Builds a RealmMap from a RealmProfile (docs/PLAN_RUN_MAP.md sections
    // 2-4). Reads no content.
    //
    // Two stages from one stream. First the SHAPE is rolled: which stretches
    // get a pocket and how long, which offsets fork, which pocket rooms and
    // fork siblings hold an anchor, where the shop goes. Then the nodes are
    // built and the rooms that are left are typed from the profile's weights.
    // Placement is constructive, so the rules in RealmMapRules hold by
    // construction; the one fallback that remains is the relax order's last
    // step, filling a prize-less pocket with Treasure.
    //
    // The same (runSeed, legStartStep, profile) always builds the same map with
    // the same node ids, which is what lets a save store only the seed and
    // profile and what keys every per-room rng stream (plan section 3).
    public static class RealmMapGenerator
    {
        private const int InteriorOffsets = 7;

        // Opens the leg stream exactly as DescentMapGenerator.GenerateLegFor
        // does. Both generators draw from their own opened copy, so neither
        // moves the other.
        public static RealmMap GenerateFor(ulong runSeed, int legStartStep, RealmProfile profile)
        {
            return Generate(RngStreams.Open(runSeed, RngStreams.Leg, legStartStep), legStartStep, profile);
        }

        public static RealmMap Generate(SeededRandom rng, int legStartStep, RealmProfile profile)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var plan = RollPlan(rng, profile);
            var map = Build(plan, profile, legStartStep, SeededRandom.FromState(rng.State));
            FillPrizelessPockets(map);

            var violations = RealmMapRules.Validate(map);
            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    "RealmMapGenerator built an invalid map: " + string.Join("; ", violations));
            }

            return map;
        }

        // ---- shape -------------------------------------------------------

        private sealed class Plan
        {
            public readonly SortedDictionary<int, int> PocketLength = new SortedDictionary<int, int>();
            public readonly SortedSet<int> ForkOffsets = new SortedSet<int>();

            // pocket stretch -> room index; fork offset -> sibling index.
            public readonly Dictionary<int, int> PocketAnchor = new Dictionary<int, int>();
            public readonly Dictionary<int, int> ForkAnchor = new Dictionary<int, int>();

            // 0 means no shop.
            public int ShopStretch;
            public int ShopRoom;
        }

        private static Plan RollPlan(SeededRandom rng, RealmProfile profile)
        {
            var plan = new Plan();

            var stretches = Enumerable.Range(1, RealmMap.MaxPockets).ToList();
            rng.Shuffle(stretches);
            int pockets = Clamp(profile.pocketCount, 1, RealmMap.MaxPockets);
            int lengthMin = Math.Max(1, profile.pocketLengthMin);
            int lengthMax = Math.Max(lengthMin, profile.pocketLengthMax);
            foreach (int stretch in stretches.Take(pockets).OrderBy(s => s))
            {
                plan.PocketLength[stretch] = rng.NextInt(lengthMin, lengthMax + 1);
            }

            // Rest before the boss owns offset 7, and a fork there would make
            // it a coin toss between two rests.
            int forkCeiling = profile.restBeforeBoss ? InteriorOffsets - 1 : InteriorOffsets;
            var forkable = Enumerable.Range(1, forkCeiling).ToList();
            rng.Shuffle(forkable);
            foreach (int offset in forkable.Take(Clamp(profile.forkCount, 0, forkCeiling)))
            {
                plan.ForkOffsets.Add(offset);
            }

            // One anchor slot per pocket and per fork: an anchor must be
            // avoidable, and one per group spreads them.
            var slots = new List<(bool Pocket, int Key)>();
            foreach (int stretch in plan.PocketLength.Keys) slots.Add((true, stretch));
            foreach (int offset in plan.ForkOffsets) slots.Add((false, offset));

            var order = Enumerable.Range(0, slots.Count).ToList();
            rng.Shuffle(order);
            foreach (int index in order.Take(Clamp(profile.anchorCount, 0, slots.Count)))
            {
                var slot = slots[index];
                if (slot.Pocket) plan.PocketAnchor[slot.Key] = rng.NextInt(0, plan.PocketLength[slot.Key]);
                else plan.ForkAnchor[slot.Key] = rng.NextInt(0, 2);
            }

            // Anchors first, then the shop in a room they left. The roll is
            // always drawn so a profile edit moves nothing downstream of it.
            bool wantsShop = rng.NextFloat() < profile.shopChance;
            var shopRooms = new List<(int Stretch, int Room)>();
            foreach (var pocket in plan.PocketLength)
            {
                for (int room = 0; room < pocket.Value; room++)
                {
                    if (plan.PocketAnchor.TryGetValue(pocket.Key, out int anchored) && anchored == room) continue;
                    shopRooms.Add((pocket.Key, room));
                }
            }

            if (wantsShop && shopRooms.Count > 0)
            {
                var pick = shopRooms[rng.NextInt(0, shopRooms.Count)];
                plan.ShopStretch = pick.Stretch;
                plan.ShopRoom = pick.Room;
            }

            return plan;
        }

        // ---- nodes -------------------------------------------------------

        private static RealmMap Build(Plan plan, RealmProfile profile, int legStartStep, SeededRandom typeRng)
        {
            var map = new RealmMap { Profile = profile, LegStartStep = legStartStep };
            var fixedTypes = new HashSet<int>();
            var siblingOf = new Dictionary<int, int>();
            int nextId = 0;

            RealmNode Make(int offset, RealmNodeRole role, int group, RoomType type, bool isFixed)
            {
                var node = new RealmNode { Id = nextId++, Offset = offset, Role = role, Group = group, Type = type };
                map.Nodes.Add(node);
                if (isFixed) fixedTypes.Add(node.Id);
                return node;
            }

            var previous = new List<RealmNode> { Make(0, RealmNodeRole.Entry, 0, RoomType.Entry, true) };
            var pendingTails = new List<RealmNode>();

            // Ids ascend along every edge: a level, then its pocket, then the
            // next level.
            for (int offset = 1; offset <= InteriorOffsets; offset++)
            {
                bool fork = plan.ForkOffsets.Contains(offset);
                var level = new List<RealmNode>();
                for (int sibling = 0; sibling < (fork ? 2 : 1); sibling++)
                {
                    bool restHere = profile.restBeforeBoss && offset == InteriorOffsets;
                    bool anchor = fork && plan.ForkAnchor.TryGetValue(offset, out int anchored) && anchored == sibling;
                    RoomType type = restHere ? RoomType.Rest : anchor ? RoomType.EliteFight : RoomType.Fight;
                    level.Add(Make(offset, fork ? RealmNodeRole.ForkSibling : RealmNodeRole.Spine,
                        fork ? offset : 0, type, restHere || anchor));
                }

                if (fork)
                {
                    siblingOf[level[0].Id] = level[1].Id;
                    siblingOf[level[1].Id] = level[0].Id;
                }

                foreach (var src in previous.Concat(pendingTails))
                {
                    foreach (var dst in level) src.Next.Add(dst.Id);
                }

                pendingTails.Clear();
                previous = level;

                if (!plan.PocketLength.TryGetValue(offset, out int length)) continue;

                var pocket = new RealmPocket { Stretch = offset };
                RealmNode tail = null;
                for (int room = 0; room < length; room++)
                {
                    bool anchor = plan.PocketAnchor.TryGetValue(offset, out int anchored) && anchored == room;
                    bool shop = plan.ShopStretch == offset && plan.ShopRoom == room;
                    RoomType type = anchor ? RoomType.EliteFight : shop ? RoomType.Shop : RoomType.Fight;
                    var node = Make(offset + 1, RealmNodeRole.Pocket, offset, type, anchor || shop);
                    pocket.Rooms.Add(node.Id);

                    if (tail == null)
                    {
                        foreach (var src in level) src.Next.Add(node.Id);
                    }
                    else
                    {
                        tail.Next.Add(node.Id);
                    }

                    tail = node;
                }

                map.Pockets.Add(pocket);
                pendingTails.Add(tail);
            }

            var heart = Make(RealmMap.SpineLength, RealmNodeRole.Heart, 0, RoomType.Boss, true);
            foreach (var src in previous) src.Next.Add(heart.Id);

            TypeRooms(map, profile, fixedTypes, siblingOf, typeRng);
            return map;
        }

        private static void TypeRooms(RealmMap map, RealmProfile profile, HashSet<int> fixedTypes,
            Dictionary<int, int> siblingOf, SeededRandom rng)
        {
            var preds = new Dictionary<int, List<RealmNode>>();
            foreach (var node in map.Nodes)
            {
                foreach (int next in node.Next)
                {
                    if (!preds.TryGetValue(next, out var list)) preds[next] = list = new List<RealmNode>();
                    list.Add(node);
                }
            }

            // Rest before the boss is already placed; otherwise one fork
            // sibling may roll Rest.
            bool restUsed = profile.restBeforeBoss;
            var typed = new Dictionary<int, RealmNode>();

            // Id order is topological, so every predecessor is typed first and
            // checking predecessors alone keeps events apart.
            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                typed[node.Id] = node;
                RealmNode sibling = null;
                if (siblingOf.TryGetValue(node.Id, out int siblingId)) typed.TryGetValue(siblingId, out sibling);

                if (!fixedTypes.Contains(node.Id))
                {
                    var allowed = new List<RoomType> { RoomType.Fight, RoomType.Event };
                    if (node.Role == RealmNodeRole.Pocket) allowed.Add(RoomType.Treasure);
                    if (node.Role == RealmNodeRole.ForkSibling && !restUsed) allowed.Add(RoomType.Rest);

                    bool eventBefore = preds.TryGetValue(node.Id, out var before)
                        && before.Any(p => p.Type == RoomType.Event);

                    // Siblings are a choice, so the second never repeats the
                    // first's Event. Two Fights may stand together; they
                    // differ in prize (below).
                    if (eventBefore || (sibling != null && sibling.Type == RoomType.Event))
                    {
                        allowed.Remove(RoomType.Event);
                    }

                    node.Type = PickRoom(profile.waypointWeights, allowed, rng);
                    if (node.Type == RoomType.Rest) restUsed = true;
                }

                if (node.Type == RoomType.Fight)
                {
                    PrizeKind avoid = sibling != null && sibling.Type == RoomType.Fight ? sibling.Prize : PrizeKind.None;
                    node.Prize = PickPrize(profile.prizeWeights, avoid, rng);
                }
                else if (node.IsAnchor)
                {
                    node.Prize = PickPrize(profile.anchorPrizeWeights, PrizeKind.None, rng);
                }
            }
        }

        private static RoomType PickRoom(List<RoomWeight> weights, List<RoomType> allowed, SeededRandom rng)
        {
            int total = 0;
            if (weights != null)
            {
                foreach (var w in weights)
                {
                    if (w.weight > 0 && allowed.Contains(w.type)) total += w.weight;
                }
            }

            if (total <= 0) return RoomType.Fight;

            int roll = rng.NextInt(0, total);
            foreach (var w in weights)
            {
                if (w.weight <= 0 || !allowed.Contains(w.type)) continue;
                if (roll < w.weight) return w.type;
                roll -= w.weight;
            }

            return RoomType.Fight;
        }

        // `avoid` is dropped from the table when anything else is left, so two
        // sibling fights offer different chips.
        private static PrizeKind PickPrize(List<PrizeWeight> weights, PrizeKind avoid, SeededRandom rng)
        {
            if (weights == null) return PrizeKind.None;

            int total = 0;
            int others = 0;
            foreach (var w in weights)
            {
                if (w.weight <= 0 || w.kind == PrizeKind.None) continue;
                total += w.weight;
                if (w.kind != avoid) others += w.weight;
            }

            if (total <= 0) return PrizeKind.None;

            bool dropAvoid = avoid != PrizeKind.None && others > 0;
            int roll = rng.NextInt(0, dropAvoid ? others : total);
            foreach (var w in weights)
            {
                if (w.weight <= 0 || w.kind == PrizeKind.None) continue;
                if (dropAvoid && w.kind == avoid) continue;
                if (roll < w.weight) return w.kind;
                roll -= w.weight;
            }

            return PrizeKind.None;
        }

        // Relax order, last step: a pocket with no anchor, shop or Treasure
        // gets its first room made Treasure. A prize-less pocket holds no
        // placed room, so nothing placed is overwritten.
        private static void FillPrizelessPockets(RealmMap map)
        {
            foreach (var pocket in map.Pockets)
            {
                bool hasPrize = pocket.Rooms.Any(id =>
                {
                    var type = map.Node(id).Type;
                    return type == RoomType.EliteFight || type == RoomType.Shop || type == RoomType.Treasure;
                });
                if (hasPrize) continue;

                var room = map.Node(pocket.Rooms[0]);
                room.Type = RoomType.Treasure;
                room.Prize = PrizeKind.None;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) return min;
            return value < min ? min : value > max ? max : value;
        }
    }
}
