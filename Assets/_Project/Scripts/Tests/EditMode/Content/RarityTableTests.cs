using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The tier roll (docs/PLAN_ITEMS.md 1.2 step 5): centre FloorTier(step) +
    // tierOffset, LootLadder climb at the source's tierStep plus 0.006 a
    // Favor point (cap 0.45), up to five rungs, capped at tier 10. Dials are
    // the shipped rewards.json values, typed here as literals.
    public class RarityTableTests
    {
        private const int Samples = 20000;
        private const ulong Seed = 20261014;

        private static ResolvedRewardSource Source(int tierOffset, float tierStep) =>
            new ResolvedRewardSource { TierOffset = tierOffset, TierStep = tierStep };

        private static readonly ResolvedRewardSource Normal = Source(0, 0.22f);
        private static readonly ResolvedRewardSource Anchor = Source(0, 0.30f);
        private static readonly ResolvedRewardSource Boss = Source(1, 0.38f);

        private static ResolvedRewardSource Named(string name) =>
            name == "normal" ? Normal : name == "anchor" ? Anchor : Boss;

        // Climb above the centre, bucketed +0, +1, +2, +3 or more.
        private static int[] Buckets(ResolvedRewardSource source, int depthStep, int favor, int centre)
        {
            var rng = new SeededRandom(Seed);
            var counts = new int[4];
            for (int i = 0; i < Samples; i++)
            {
                int climb = RarityTable.RollTier(source, depthStep, favor, n => rng.NextInt(0, n)) - centre;
                counts[Math.Min(climb, 3)]++;
            }

            return counts;
        }

        [TestCase(0, 0)]
        [TestCase(15, 0)]
        [TestCase(16, 1)]
        [TestCase(80, 5)]
        public void FloorTier_ClimbsOnePerTwoLegs(int step, int expected)
        {
            Assert.AreEqual(expected, RarityTable.FloorTier(step));
        }

        [Test]
        public void FloorTier_NeverGoesNegative()
        {
            Assert.AreEqual(0, RarityTable.FloorTier(-40));
        }

        // THE TABLE, pinned twice: exact counts on one seed (any change to
        // the draw shifts them), and the plan's shares within 1.5 points.
        [TestCase("normal", 2, new[] { 15533, 3472, 787, 208 }, new[] { 0.78, 0.172, 0.038, 0.011 })]
        [TestCase("anchor", 2, new[] { 13951, 4247, 1284, 518 }, new[] { 0.70, 0.21, 0.063, 0.027 })]
        [TestCase("boss", 3, new[] { 12384, 4718, 1841, 1057 }, new[] { 0.62, 0.236, 0.090, 0.055 })]
        public void TierTable_AtFavorZero(string name, int centre, int[] pinned, double[] plan)
        {
            var counts = Buckets(Named(name), depthStep: 40, favor: 0, centre: centre);

            CollectionAssert.AreEqual(pinned, counts, $"{name}: [{string.Join(", ", counts)}]");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(plan[i], counts[i] / (double)Samples, 0.015, $"{name} +{i}");
            }
        }

        // Favor 20 adds 0.12: a normal card climbs at least one tier 34% of
        // the time instead of 22%.
        [Test]
        public void Favor20_ANormalCardClimbsAThirdOfTheTime()
        {
            var counts = Buckets(Normal, depthStep: 40, favor: 20, centre: 2);
            double climbed = 1.0 - counts[0] / (double)Samples;

            Assert.AreEqual(0.34, climbed, 0.015);
        }

        [Test]
        public void FavorStopsAtTheTierCap()
        {
            var counts = Buckets(Normal, depthStep: 40, favor: 500, centre: 2);
            double climbed = 1.0 - counts[0] / (double)Samples;

            Assert.AreEqual(0.45, climbed, 0.015);
        }

        // No class floor: a boss card at step 0 pays its +1 centre, tier 1.
        [Test]
        public void ABossHasNoAbsoluteFloor_OnlyItsCentre()
        {
            var rng = new SeededRandom(Seed);
            var tiers = Enumerable.Range(0, Samples)
                .Select(_ => RarityTable.RollTier(Boss, 0, 0, n => rng.NextInt(0, n)))
                .ToList();

            Assert.AreEqual(1, tiers.Min());
        }

        [Test]
        public void EveryRollCapsAtTierTen()
        {
            var rng = new SeededRandom(Seed);
            for (int i = 0; i < 2000; i++)
            {
                Assert.AreEqual(10, RarityTable.RollTier(Boss, 500, 0, n => rng.NextInt(0, n)));
            }

            Assert.AreEqual(10, RarityTable.RollTier(Normal, 176, 0, _ => 999), "step 176 centres past the art ladder and clamps to its top");
        }

        [Test]
        public void MissingRandomnessPaysTheCentre()
        {
            Assert.AreEqual(3, RarityTable.RollTier(Boss, 40, 0, null));
            Assert.AreEqual(2, RarityTable.RollTier((ResolvedRewardSource)null, 40, 0, null));
        }

        [Test]
        public void MisbehavingRandomnessIsClamped()
        {
            Assert.DoesNotThrow(() => RarityTable.RollTier(Boss, 10, 0, _ => 99999));
            Assert.DoesNotThrow(() => RarityTable.RollTier(Boss, 10, 0, _ => -7));
        }

        // The top rung needs five successes, so even at the cap the jackpot
        // stays rare.
        [Test]
        public void TheJackpotNeverBecomesTheDefault()
        {
            var rng = new SeededRandom(Seed);
            int top = 0;
            for (int i = 0; i < Samples; i++)
            {
                if (RarityTable.RollTier(Boss, 0, 500, n => rng.NextInt(0, n)) >= 1 + LootLadder.MaxRungs) top++;
            }

            Assert.Less(top / (double)Samples, 0.03);
        }
    }
}
