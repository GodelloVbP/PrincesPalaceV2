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
        // Printed rather than pinned to a tight band, since the whole point
        // of this file's report is showing the measured mix.
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

            // Fight's table weight is 52/90 (~58%). The (a)/(c) exclusions
            // and the (b) retype pass both actively push rooms AWAY from
            // Fight whenever it would otherwise cluster or dominate a road,
            // so its measured share necessarily lands under its raw table
            // weight — that is the feature working, not drift. What must
            // still hold is that it stays the largest single bucket by a
            // clear margin, not that it holds a fixed percentage.
            int otherTypesMax = counts.Where(kv => kv.Key != RoomType.Fight).Max(kv => kv.Value);
            Assert.Greater(fightCount, otherTypesMax * 1.5,
                "Fight should still clearly outnumber every other rolled type, even after the constraints thin it out");
            Assert.Greater(fightShare, 0.35, "Fight's share dropped implausibly far below its table weight");
            Assert.Less(fightShare, 0.75, "Fight's share grew implausibly far past its table weight");
            CollectionAssert.DoesNotContain(counts.Keys.ToList(), RoomType.Unknown, "Unknown should never roll");
        }
    }
}
