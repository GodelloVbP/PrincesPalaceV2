using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The positional rules that turn a series of self-contained floors into
    // one continuous descent. DescentMapTests still covers the structural
    // promises (reachability, widths, links); this covers WHERE the forced
    // rooms land.
    public class DescentLegTests
    {
        private const int Seeds = 60;

        // PINNED. Every 8th step an elite, every 16th a boss, boss wins.
        [TestCase(1, null)]
        [TestCase(7, null)]
        [TestCase(8, RoomType.EliteFight)]
        [TestCase(9, null)]
        [TestCase(16, RoomType.Boss)]
        [TestCase(24, RoomType.EliteFight)]
        [TestCase(32, RoomType.Boss)]
        [TestCase(40, RoomType.EliteFight)]
        [TestCase(48, RoomType.Boss)]
        public void ForcedTypeAt_PutsAnEliteEveryEighthStepAndABossEverySixteenth(int step, RoomType? expected)
        {
            Assert.AreEqual(expected, DescentMapGenerator.ForcedTypeAt(step));
        }

        // ---- level 30: a rest before every boss ----------------------------------

        [TestCase(15, RoomType.Rest)]
        [TestCase(31, RoomType.Rest)]
        [TestCase(47, RoomType.Rest)]
        [TestCase(16, RoomType.Boss)]
        [TestCase(32, RoomType.Boss)]
        [TestCase(8, RoomType.EliteFight)]
        [TestCase(24, RoomType.EliteFight)]
        [TestCase(14, null)]
        [TestCase(17, null)]
        public void TheRestLandsOnTheStepBeforeEachBoss(int step, RoomType? expected)
        {
            Assert.AreEqual(expected, DescentMapGenerator.ForcedTypeAt(step, restBeforeBoss: true));
        }

        // Off by default, so every run that has not earned it is unchanged.
        // Written against the whole cadence rather than one step, because the
        // failure worth catching is the reward leaking into runs that did not
        // earn it.
        [Test]
        public void WithoutTheRewardNothingAboutTheCadenceMoves()
        {
            for (int step = 1; step <= 64; step++)
            {
                Assert.AreEqual(DescentMapGenerator.ForcedTypeAt(step),
                    DescentMapGenerator.ForcedTypeAt(step, restBeforeBoss: false),
                    $"step {step} differs with the reward explicitly off");

                Assert.AreNotEqual(RoomType.Rest, DescentMapGenerator.ForcedTypeAt(step),
                    $"step {step} forces a rest without the reward");
            }
        }

        // THE COLLISION THAT DOES NOT HAPPEN, pinned so a cadence retune cannot
        // introduce it quietly. Rests land where step % 16 == 15 and elites
        // where step % 8 == 0, so no step is ever both -- and a boss wins
        // outright wherever it applies.
        [Test]
        public void ARestNeverStealsAnEliteOrABossStep()
        {
            for (int step = 1; step <= 200; step++)
            {
                var withReward = DescentMapGenerator.ForcedTypeAt(step, restBeforeBoss: true);
                var without = DescentMapGenerator.ForcedTypeAt(step);

                if (without == RoomType.Boss || without == RoomType.EliteFight)
                {
                    Assert.AreEqual(without, withReward,
                        $"the rest reward replaced the {without} at step {step}");
                }
            }
        }

        // And it reaches the generated map, not just the rule.
        [Test]
        public void AGeneratedLegPutsTheRestImmediatelyBeforeItsBoss()
        {
            // A leg starting at 8 runs steps 9..16, so the boss at 16 is its
            // last column and the rest at 15 is the one before.
            var map = DescentMapGenerator.GenerateLeg(new SeededRandom(7), startStep: 8, restBeforeBoss: true);

            var boss = map.Nodes.FirstOrDefault(n => n.Type == RoomType.Boss);
            Assert.IsNotNull(boss, "the leg has no boss, so this test is not testing what it says");

            var before = map.Nodes.Where(n => n.Depth == boss.Depth - 1).ToList();
            Assert.AreEqual(1, before.Count,
                "the column before the boss is not a forced single, so nothing guaranteed anything");
            Assert.AreEqual(RoomType.Rest, before[0].Type,
                "the guaranteed rest is not on the column before the boss");
        }

        // Non-vacuity for the test above: without the reward that same column
        // is a normal forked column of rolled rooms, so the rest it finds is
        // the reward's doing rather than something the weights rolled.
        [Test]
        public void TheSameLegWithoutTheRewardLeavesThatColumnAlone()
        {
            var map = DescentMapGenerator.GenerateLeg(new SeededRandom(7), startStep: 8);

            var boss = map.Nodes.FirstOrDefault(n => n.Type == RoomType.Boss);
            Assert.IsNotNull(boss);

            var before = map.Nodes.Where(n => n.Depth == boss.Depth - 1).ToList();
            Assert.Greater(before.Count, 1,
                "without the reward the column before the boss is already a forced single");
        }

        // Step 0 is the entry the player is standing in rather than a room
        // they chose. Without the guard, 0 % 16 == 0 would make the very
        // first column of the run a boss.
        [Test]
        public void StepZero_ForcesNothing()
        {
            Assert.IsNull(DescentMapGenerator.ForcedTypeAt(0));
            Assert.IsNull(DescentMapGenerator.ForcedTypeAt(-16));
        }

        // Leg 1 ends in an elite, leg 2 in a boss, leg 3 in an elite, and so
        // on without end. None of that is special-cased in the generator —
        // it falls out of the leg length matching the elite period.
        [TestCase(0, RoomType.EliteFight)]
        [TestCase(8, RoomType.Boss)]
        [TestCase(16, RoomType.EliteFight)]
        [TestCase(24, RoomType.Boss)]
        [TestCase(64, RoomType.EliteFight)]
        [TestCase(72, RoomType.Boss)]
        public void EveryLeg_EndsOnItsForcedRoom(int startStep, RoomType expected)
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), startStep);
                var last = map.AtDepth(map.DepthCount - 1).ToList();

                Assert.AreEqual(1, last.Count, $"seed {seed}: the final column should be a single room");
                Assert.AreEqual(expected, last[0].Type, $"seed {seed}: leg from step {startStep}");
            }
        }

        // A fork into two bosses is not a choice, it is a coin toss.
        [Test]
        public void AForcedColumn_IsAlwaysASingleRoom()
        {
            for (ulong seed = 0; seed < Seeds; seed++)
            {
                foreach (int startStep in new[] { 0, 8, 16, 40 })
                {
                    var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), startStep);

                    for (int d = 1; d < map.DepthCount; d++)
                    {
                        if (DescentMapGenerator.ForcedTypeAt(startStep + d).HasValue)
                        {
                            Assert.AreEqual(1, map.AtDepth(d).Count(),
                                $"seed {seed}, leg from {startStep}, column {d} is forced but branches");
                        }
                    }
                }
            }
        }

        // The descent does not end. A boss leg is followed by another leg,
        // which is followed by another — checked far enough out that a rule
        // holding only for the first couple of legs would fail.
        [Test]
        public void TheDescentKeepsGoing_LegAfterLeg()
        {
            var endings = new System.Collections.Generic.List<RoomType>();

            for (int leg = 0; leg < 12; leg++)
            {
                int startStep = leg * DescentMapGenerator.DefaultLegLength;
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom((ulong)leg), startStep);

                Assert.IsNotNull(map.Entry, $"leg {leg} has no entrance");
                endings.Add(map.AtDepth(map.DepthCount - 1).First().Type);
            }

            // Alternating elite, boss, elite, boss …
            for (int i = 0; i < endings.Count; i++)
            {
                Assert.AreEqual(i % 2 == 0 ? RoomType.EliteFight : RoomType.Boss, endings[i],
                    $"leg {i + 1} ended on the wrong room");
            }
        }

        // A leg is one map, and its column count is fixed by its length —
        // the entry plus legLength rooms.
        [Test]
        public void ALegIsItsEntryPlusItsRooms()
        {
            var map = DescentMapGenerator.GenerateLeg(new SeededRandom(3), startStep: 0);

            Assert.AreEqual(DescentMapGenerator.DefaultLegLength + 1, map.DepthCount);
            Assert.AreEqual(RoomType.Entry, map.Entry.Type);
        }

        // Every other middle-room type still rolls normally out of the
        // weighted table.
        [Test]
        public void MidLegRooms_StillRollNormally()
        {
            var midLegTypes = Enumerable.Range(0, Seeds)
                .SelectMany(i => DescentMapGenerator.GenerateLeg(new SeededRandom((ulong)i), startStep: 0).Nodes)
                .Where(n => n.Depth > 0 && !DescentMapGenerator.ForcedTypeAt(n.Depth).HasValue)
                .Select(n => n.Type)
                .Distinct()
                .ToList();

            foreach (var expected in new[] { RoomType.Fight, RoomType.Event, RoomType.Treasure, RoomType.Shop, RoomType.Rest })
            {
                CollectionAssert.Contains(midLegTypes, expected, $"{expected} never appeared mid-leg");
            }

            CollectionAssert.DoesNotContain(midLegTypes, RoomType.Boss,
                "A boss should only ever appear where the step forces one");
        }

        // EliteFight used to ALSO be in the weighted table (mid-leg elites
        // were "extra, not a replacement" for the forced cadence) — reverted
        // after a playtest with a fresh, level-1 squad couldn't survive an
        // Elite rolling as the very first room. It is exclusively
        // ForcedTypeAt's cadence now; this is the mirror of
        // MidLegRooms_StillRollNormally above, guarding the reversal the same
        // way that test guards the rooms that are still meant to roll.
        [Test]
        public void EliteFight_NeverRollsMidLeg_OnlyAtTheForcedCadence()
        {
            var midLegTypes = Enumerable.Range(0, Seeds)
                .SelectMany(i => DescentMapGenerator.GenerateLeg(new SeededRandom((ulong)i), startStep: 0).Nodes)
                .Where(n => n.Depth > 0 && !DescentMapGenerator.ForcedTypeAt(n.Depth).HasValue)
                .Select(n => n.Type)
                .Distinct()
                .ToList();

            CollectionAssert.DoesNotContain(midLegTypes, RoomType.EliteFight,
                "EliteFight should only ever appear where ForcedTypeAt places one");
        }
    }
}
