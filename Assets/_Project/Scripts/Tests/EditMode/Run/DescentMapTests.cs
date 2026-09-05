using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The branching descent's structural promises, checked across many seeds
    // rather than one. A generator is a distribution, not a value: a single
    // lucky seed proves nothing, and every one of these properties is the
    // kind that holds for most seeds and fails for a few.
    public class DescentMapTests
    {
        private const int Seeds = 200;

        // Steps 9..16, so the leg ends on step 16 — a boss. Every structural
        // test below was written against a floor that finished on a boss, and
        // a boss-ending leg is still exactly that; generating from step 8
        // rather than from 0 is the only thing that changed.
        private const int BossEndingStartStep = 8;

        private static DescentMap Generate(ulong seed, int legLength = DescentMapGenerator.DefaultLegLength)
        {
            return DescentMapGenerator.GenerateLeg(new SeededRandom(seed), BossEndingStartStep, legLength);
        }

        [Test]
        public void EveryRoom_IsReachableFromTheEntry_WalkingForwardOnly()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                var reachable = map.ReachableFromEntry();

                var stranded = map.Nodes.Where(n => !reachable.Contains(n.Id)).Select(n => $"{n.Id}@d{n.Depth}").ToList();
                CollectionAssert.IsEmpty(stranded, $"seed {seed} generated rooms nobody can reach");
            }
        }

        // A dead end mid-descent would strand the run with no legal move and
        // nothing but quitting to do about it.
        [Test]
        public void NoRoomIsADeadEnd_ExceptTheBoss()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                int lastDepth = map.DepthCount - 1;

                foreach (var node in map.Nodes.Where(n => n.Depth < lastDepth))
                {
                    Assert.IsNotEmpty(node.Next, $"seed {seed}: room {node.Id} at depth {node.Depth} leads nowhere");
                }
            }
        }

        [Test]
        public void TheBossEndsTheFloor()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                var boss = map.Boss;

                Assert.IsNotNull(boss, $"seed {seed} has no boss");
                Assert.AreEqual(map.DepthCount - 1, boss.Depth, "The boss is the last column");
                Assert.IsEmpty(boss.Next, "Nothing follows the boss");
                Assert.AreEqual(1, map.AtDepth(boss.Depth).Count(), "The boss column holds only the boss");
            }
        }

        [Test]
        public void TheEntryIsASingleRoom()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                Assert.AreEqual(1, map.AtDepth(0).Count(), $"seed {seed}: the descent should start from one room");
                Assert.AreEqual(0, map.Entry.Depth);
            }
        }

        // Links only ever go one column forward. That is what makes depth
        // monotonic and a choice permanent -- a backward or skipping link
        // would quietly reintroduce the wandering this replaced.
        [Test]
        public void EveryLink_GoesExactlyOneColumnForward()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                foreach (var node in map.Nodes)
                {
                    foreach (int nextId in node.Next)
                    {
                        Assert.AreEqual(node.Depth + 1, map.Node(nextId).Depth,
                            $"seed {seed}: room {node.Id} at depth {node.Depth} links to {nextId}");
                    }
                }
            }
        }

        [Test]
        public void NoRoomLinksToTheSameRoomTwice()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                foreach (var node in Generate(seed).Nodes)
                {
                    CollectionAssert.AllItemsAreUnique(node.Next, $"seed {seed}: room {node.Id} has a duplicate link");
                }
            }
        }

        // The point of the whole layout: somewhere on the floor there is a
        // real fork. A descent that never branches is a corridor.
        [Test]
        public void MostFloorsOfferARealChoiceSomewhere()
        {
            int branching = 0;
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                if (Generate(seed).Nodes.Any(n => n.Next.Count > 1))
                {
                    branching++;
                }
            }

            Assert.Greater(branching, Seeds * 0.9, "Almost every floor should fork somewhere");
        }

        [Test]
        public void MiddleColumnsAreNeverWiderThanTheCap()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                for (int d = 0; d < map.DepthCount; d++)
                {
                    Assert.LessOrEqual(map.AtDepth(d).Count(), DescentMapGenerator.MaxColumnWidth, $"seed {seed}, depth {d}");
                }
            }
        }

        // The one middle column that is exempt is the forced elite (D6:
        // every leg carries one at its midpoint, step ≡ 4 mod 8) — a forced
        // column is always a single room by design (AForcedColumn_IsAlways
        // ASingleRoom in DescentLegTests), same as the boss already excluded
        // by this loop's own range.
        [Test]
        public void MiddleColumnsAlwaysOfferAtLeastTwoRooms()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = Generate(seed);
                for (int d = 1; d < map.DepthCount - 1; d++)
                {
                    var column = map.AtDepth(d).ToList();
                    if (column.Count == 1 && column[0].Type == RoomType.EliteFight)
                    {
                        continue;
                    }

                    Assert.GreaterOrEqual(column.Count, 2, $"seed {seed}, depth {d} is a corridor");
                }
            }
        }

        [Test]
        public void TheSameSeedAlwaysGeneratesTheSameFloor()
        {
            var a = Generate(4242);
            var b = Generate(4242);

            CollectionAssert.AreEqual(
                a.Nodes.Select(n => $"{n.Id}:{n.Depth}:{n.Slot}:{n.Type}:{string.Join(",", n.Next)}").ToList(),
                b.Nodes.Select(n => $"{n.Id}:{n.Depth}:{n.Slot}:{n.Type}:{string.Join(",", n.Next)}").ToList());
        }

        [Test]
        public void DifferentSeedsGenerateDifferentFloors()
        {
            var shapes = Enumerable.Range(0, 40)
                .Select(i => string.Join("|", Generate((ulong)i).Nodes.Select(n => $"{n.Depth}{n.Slot}{n.Type}")))
                .Distinct()
                .Count();

            Assert.Greater(shapes, 30, "Forty seeds should not collapse into a handful of floors");
        }

        [Test]
        public void CanMove_OnlyAllowsAnActualLink()
        {
            var map = Generate(7);
            var entry = map.Entry;
            int linked = entry.Next[0];

            Assert.IsTrue(map.CanMove(entry.Id, linked));
            Assert.IsFalse(map.CanMove(entry.Id, map.Boss.Id), "The entry does not connect straight to the boss");
            Assert.IsFalse(map.CanMove(entry.Id, 9999), "An id that does not exist is not reachable");
        }

        [Test]
        public void ALegTooShortToBranch_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Generate(1, legLength: 1));
            Assert.Throws<ArgumentNullException>(() => DescentMapGenerator.GenerateLeg(null, 0));
        }

        // Every kind of room should actually turn up. A weight table that
        // silently never produces one of its entries is dead content.
        //
        // EliteFight is excluded on purpose — it is no longer in the weighted
        // table at all (see MiddleRooms' own comment), only ForcedTypeAt's
        // cadence, and every leg this fixture generates (8..16) DOES cross
        // that forced-elite step now (D6: step ≡ 4 mod 8, so step 12 within
        // this range) — it just isn't asserted here because it is not a
        // rolled type. DescentLegTests covers both halves of the elite
        // question directly: that it still shows up at the cadence, and that
        // it never shows up anywhere else.
        [Test]
        public void EveryMiddleRoomType_ShowsUpAcrossManySeeds()
        {
            var seen = Enumerable.Range(0, Seeds)
                .SelectMany(i => Generate((ulong)i).Nodes)
                .Where(n => n.Depth > 0 && n.Type != RoomType.Boss)
                .Select(n => n.Type)
                .Distinct()
                .ToList();

            foreach (var expected in new[] { RoomType.Fight, RoomType.Event, RoomType.Treasure, RoomType.Shop, RoomType.Rest })
            {
                CollectionAssert.Contains(seen, expected, $"{expected} never appeared in {Seeds} floors");
            }
        }

        // Unknown ("?") is retired from generation — its weight moved to
        // Fight. The enum value and its resolution/icon handling stay for a
        // save whose currentNodeId already points at one, but nothing new
        // may ever roll it. Checked across many legs, not just many seeds of
        // one leg, since a positional bug could hide in a later leg.
        [Test]
        public void UnknownNeverGenerates()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                for (int leg = 0; leg < 5; leg++)
                {
                    int startStep = leg * DescentMapGenerator.DefaultLegLength;
                    var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), startStep);

                    CollectionAssert.DoesNotContain(map.Nodes.Select(n => n.Type).ToList(), RoomType.Unknown,
                        $"seed {seed}, leg starting at step {startStep} generated an Unknown room");
                }
            }
        }
    }
}
