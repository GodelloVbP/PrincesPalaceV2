using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // "Make sure a whole road is not just the same, there has to be
    // variation" — the author's own words for this feature. A ROAD is any
    // entry-to-boss path through a leg, following links. Three rules, all
    // enforced deterministically from the seed inside DescentMapGenerator
    // (RollTypes for (a)/(c), EnforceEveryRoadHasVariety for (b)):
    //
    //   (a) no three consecutive ROLLED rooms of the same type along a road
    //       (a forced room breaks a streak but is not counted itself);
    //   (b) every road has at least one non-Fight rolled room;
    //   (c) a column of 2+ nodes is never all one type.
    //
    // A fourth guarantee lives in the same file (EnsureLegHasShop, run
    // after (a)/(b)/(c)) and is tested at the bottom of this class: every
    // leg has at least one Shop node, full stop.
    public class DescentRoadVariationTests
    {
        private const int Seeds = 500;
        private const int LegsPerSeed = 5;

        private static bool IsForced(DescentNode node, int startStep, bool restBeforeBoss)
        {
            return node.Depth == 0 || DescentMapGenerator.ForcedTypeAt(startStep + node.Depth, restBeforeBoss).HasValue;
        }

        // Every entry-to-boss path, following links forward. Small and
        // bounded for a single leg (MaxColumnWidth per column, a handful of
        // columns), so a plain DFS is fine for a test fixture.
        private static IEnumerable<List<DescentNode>> Roads(DescentMap map)
        {
            var stack = new Stack<List<DescentNode>>();
            stack.Push(new List<DescentNode> { map.Entry });

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
                    stack.Push(new List<DescentNode>(path) { map.Node(nextId) });
                }
            }
        }

        private static IEnumerable<(DescentMap Map, int StartStep)> ManyLegs()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                for (int leg = 0; leg < LegsPerSeed; leg++)
                {
                    int startStep = leg * DescentMapGenerator.DefaultLegLength;
                    yield return (DescentMapGenerator.GenerateLeg(new SeededRandom(seed), startStep), startStep);
                }
            }
        }

        [Test]
        public void NoRoadHasThreeConsecutiveRolledRoomsOfTheSameType()
        {
            foreach (var (map, startStep) in ManyLegs())
            {
                foreach (var road in Roads(map))
                {
                    int streak = 0;
                    RoomType? streakType = null;

                    foreach (var node in road)
                    {
                        if (IsForced(node, startStep, false))
                        {
                            streak = 0;
                            streakType = null;
                            continue;
                        }

                        if (node.Type == streakType)
                        {
                            streak++;
                        }
                        else
                        {
                            streak = 1;
                            streakType = node.Type;
                        }

                        Assert.Less(streak, 3,
                            $"start {startStep}: a road ran three consecutive rolled {node.Type} rooms");
                    }
                }
            }
        }

        [Test]
        public void EveryRoadHasAtLeastOneNonFightRolledRoom()
        {
            foreach (var (map, startStep) in ManyLegs())
            {
                foreach (var road in Roads(map))
                {
                    bool hasNonFightRolled = road.Any(n => !IsForced(n, startStep, false) && n.Type != RoomType.Fight);
                    Assert.IsTrue(hasNonFightRolled,
                        $"start {startStep}: a road ({string.Join(",", road.Select(n => n.Type))}) is entirely Fight");
                }
            }
        }

        [Test]
        public void ColumnsOfTwoOrMoreAreNeverAllTheSameType()
        {
            foreach (var (map, _) in ManyLegs())
            {
                for (int d = 0; d < map.DepthCount; d++)
                {
                    var column = map.AtDepth(d).ToList();
                    if (column.Count < 2)
                    {
                        continue;
                    }

                    bool allSame = column.All(n => n.Type == column[0].Type);
                    Assert.IsFalse(allSame,
                        $"depth {d}: a {column.Count}-room column is entirely {column[0].Type}");
                }
            }
        }

        [Test]
        public void TheSameSeedAlwaysGeneratesTheSameRoadVariation()
        {
            string Shape(ulong seed) => string.Join("|",
                DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0).Nodes
                    .Select(n => $"{n.Id}:{n.Type}"));

            for (ulong seed = 0; seed < 20; seed++)
            {
                Assert.AreEqual(Shape(seed), Shape(seed), $"seed {seed} produced different maps on repeat generation");
            }
        }

        // The constraints narrow the pool at roll time and retype the odd
        // road afterward, but neither is meant to reshape the overall mix:
        // Fight should still be the clear majority, and the rest should
        // still land somewhere in the neighbourhood of their table weights.
        // The mix itself is still printed rather than asserted room-by-room
        // -- that is what this file's report is for -- but Fight's OVERALL
        // share is pinned to a band now, not just bounded loosely. It is
        // the one number a retune of DescentMapGenerator.MiddleRooms is
        // meant to land on, and a band with no floor a caller can trust
        // would let that retune silently drift back toward the pre-tune
        // 44% without any test noticing.
        [Test]
        public void TheOverallTypeMixStaysInTheNeighbourhoodOfTheWeights()
        {
            var counts = new Dictionary<RoomType, int>();
            int total = 0;

            foreach (var (map, startStep) in ManyLegs())
            {
                foreach (var node in map.Nodes)
                {
                    if (IsForced(node, startStep, false))
                    {
                        continue;
                    }

                    counts.TryGetValue(node.Type, out int c);
                    counts[node.Type] = c + 1;
                    total++;
                }
            }

            double fightShare = counts.TryGetValue(RoomType.Fight, out int fightCount) ? (double)fightCount / total : 0;
            TestContext.WriteLine($"Rolled-room mix over {Seeds} seeds x {LegsPerSeed} legs ({total} rolled rooms):");
            foreach (var kv in counts.OrderByDescending(kv => kv.Value))
            {
                TestContext.WriteLine($"  {kv.Key}: {kv.Value} ({100.0 * kv.Value / total:F1}%)");
            }

            // Fight's raw table weight (see MiddleRooms) is nowhere near
            // its measured share -- the (a)/(c) exclusions and the (b)
            // retype pass all actively push rooms AWAY from Fight whenever
            // it would otherwise cluster or dominate a road, and MiddleRooms'
            // own comment shows that pushback plateaus the measured share
            // well under 100% no matter how far the table weight is raised.
            // 53-57% is that plateau band, not a fixed target it happened
            // to land on: it is what "measured Fight about 55%" from the
            // retune brief actually cashes out to as a band a test can pin.
            int otherTypesMax = counts.Where(kv => kv.Key != RoomType.Fight).Max(kv => kv.Value);
            Assert.Greater(fightCount, otherTypesMax * 1.5,
                "Fight should still clearly outnumber every other rolled type, even after the constraints thin it out");
            Assert.Greater(fightShare, 0.53, "Fight's measured share dropped below the tuned band (53-57%)");
            Assert.Less(fightShare, 0.57, "Fight's measured share grew past the tuned band (53-57%)");
            CollectionAssert.DoesNotContain(counts.Keys.ToList(), RoomType.Unknown, "Unknown should never roll");
        }

        // ONE SHOP GUARANTEED PER LEG (DescentMapGenerator.EnsureLegHasShop).
        // Measured baseline before this pass existed: Shop was 5.9% of all
        // nodes, and 31% of generated legs (2000 seeds x 5 legs/seed)
        // contained no Shop node on any branch at all. These three tests
        // pin the guarantee that replaced that gap: every leg has one, the
        // node it lands on is never a forced or entry step, and a leg that
        // already rolled a Shop naturally is left untouched.
        [Test]
        public void EveryLegHasAtLeastOneShop_AndItIsNeverOnAForcedOrEntryStep()
        {
            void CheckLegSet(int startStep, bool restBeforeBoss)
            {
                for (ulong seed = 1; seed <= 2000; seed++)
                {
                    var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), startStep,
                        DescentMapGenerator.DefaultLegLength, restBeforeBoss);

                    var shops = map.Nodes.Where(n => n.Type == RoomType.Shop).ToList();
                    Assert.IsNotEmpty(shops,
                        $"start {startStep} restBeforeBoss={restBeforeBoss} seed {seed}: leg has no Shop node");

                    foreach (var shop in shops)
                    {
                        Assert.AreNotEqual(0, shop.Depth,
                            $"start {startStep} seed {seed}: the entry itself was retyped to Shop");
                        Assert.IsFalse(
                            DescentMapGenerator.ForcedTypeAt(startStep + shop.Depth, restBeforeBoss).HasValue,
                            $"start {startStep} seed {seed}: a forced step (elite/boss/rest) was retyped to Shop");
                    }
                }
            }

            CheckLegSet(1, false);
            CheckLegSet(9, true);
        }

        // Seed 47 at startStep 1 rolls three Shop nodes on its own, well
        // before EnsureLegHasShop ever gets a say — its early-return
        // ("leg already has a Shop, do nothing") means this exact sequence
        // is what RollTypes/EnforceEveryRoadHasVariety alone produce.
        // Literal, not recomputed, so a regression that started overwriting
        // an already-satisfied leg would show up here instead of hiding
        // behind the guarantee it was supposed to be a no-op for.
        [Test]
        public void ALegThatAlreadyRolledAShopIsLeftUntouchedByTheGuarantee()
        {
            var map = DescentMapGenerator.GenerateLeg(new SeededRandom(47), 1);

            var actual = map.Nodes.Select(n => $"{n.Id}:{n.Depth}:{n.Slot}:{n.Type}").ToArray();
            var expected = new[]
            {
                "0:0:0:Entry",
                "1:1:0:Fight",
                "2:1:1:Fight",
                "3:1:2:Event",
                "4:2:0:Fight",
                "5:2:1:Fight",
                "6:2:2:Shop",
                "7:3:0:EliteFight",
                "8:4:0:Fight",
                "9:4:1:Event",
                "10:5:0:Fight",
                "11:5:1:Rest",
                "12:6:0:Treasure",
                "13:6:1:Shop",
                "14:7:0:Boss",
                "15:8:0:Fight",
                "16:8:1:Shop",
                "17:8:2:Fight",
            };

            CollectionAssert.AreEqual(expected, actual, "seed 47 startStep 1 should already carry 3 Shop nodes " +
                "before the guarantee runs, and the guarantee must not touch a leg that already has one");
            Assert.AreEqual(3, actual.Count(n => n.EndsWith(":Shop")), "expected exactly 3 Shop nodes on this leg");
        }
    }
}
