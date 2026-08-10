using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class StatBlockTests
    {
        [Test]
        public void Zero_HasEveryStatAtZero()
        {
            var zero = StatBlock.Zero;

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                Assert.AreEqual(0, zero[stat], $"{stat} should be zero on StatBlock.Zero");
            }
        }

        // Guards the one place adding a StatType can silently go wrong: the
        // indexer throwing for a value nobody wired up.
        [Test]
        public void Indexer_CoversEveryStatType()
        {
            var block = new StatBlock(1, 2, 3, 4);

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                Assert.DoesNotThrow(() => _ = block[stat], $"StatBlock's indexer has no case for {stat}");
            }
        }

        [Test]
        public void Indexer_ReturnsTheMatchingField()
        {
            var block = new StatBlock(20, 11, 7, 3);

            Assert.AreEqual(20, block[StatType.MaxHealth]);
            Assert.AreEqual(11, block[StatType.Speed]);
            Assert.AreEqual(7, block[StatType.Attack]);
            Assert.AreEqual(3, block[StatType.Defense]);
        }

        [Test]
        public void Indexer_ThrowsForAnUndefinedStat()
        {
            var block = StatBlock.Zero;
            var bogus = (StatType)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = block[bogus]);
        }

        // The three stats item sets brought in. Named explicitly rather than
        // folded into the constructor test above, so a reader can see the
        // new vocabulary in one place.
        [Test]
        public void Indexer_ReturnsTheThreeSetStats()
        {
            var block = new StatBlock(0, 0, 0, 0, manaRegen: 2, physicalResistance: 5, magicalResistance: 9);

            Assert.AreEqual(2, block[StatType.ManaRegen]);
            Assert.AreEqual(5, block[StatType.PhysicalResistance]);
            Assert.AreEqual(9, block[StatType.MagicalResistance]);
        }

        // ForStat is the authoring bridge — the item-set resolver builds a
        // block from a string name and sums it in, one stat at a time. If a
        // StatType has no case here, a set piece naming that stat throws
        // instead of failing content validation with a message.
        [Test]
        public void ForStat_CoversEveryStatType()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                Assert.DoesNotThrow(() => StatBlock.ForStat(stat, 5), $"StatBlock.ForStat has no case for {stat}");
            }
        }

        [Test]
        public void ForStat_SetsOnlyTheNamedStat()
        {
            var block = StatBlock.ForStat(StatType.PhysicalResistance, 7);

            Assert.AreEqual(new StatBlock(0, 0, 0, 0, physicalResistance: 7), block);
        }

        [Test]
        public void ForStat_RoundTripsThroughTheIndexer()
        {
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                Assert.AreEqual(5, StatBlock.ForStat(stat, 5)[stat], $"{stat} did not round-trip");
            }
        }

        [Test]
        public void Addition_SumsEveryStat()
        {
            var a = new StatBlock(10, 5, 3, 1);
            var b = new StatBlock(4, 2, 6, 8);

            Assert.AreEqual(new StatBlock(14, 7, 9, 9), a + b);
        }

        // Two set pieces on the same character — the exact shape equipping a
        // full set produces, since every piece's bonus is summed the same
        // way a talent's is.
        [Test]
        public void Addition_SumsTheThreeSetStatsToo()
        {
            var hat = new StatBlock(0, 0, 0, 0, manaRegen: 2, physicalResistance: 3, magicalResistance: 7);
            var robe = new StatBlock(0, 0, 0, 0, manaRegen: 1, physicalResistance: 8, magicalResistance: 20);

            var total = hat + robe;

            Assert.AreEqual(3, total.manaRegen);
            Assert.AreEqual(11, total.physicalResistance);
            Assert.AreEqual(27, total.magicalResistance);
        }

        [Test]
        public void Addition_WithZero_LeavesTheBlockUnchanged()
        {
            var block = new StatBlock(12, 9, 4, 2);

            Assert.AreEqual(block, block + StatBlock.Zero);
            Assert.AreEqual(block, StatBlock.Zero + block);
        }

        [Test]
        public void Addition_IsOrderIndependent()
        {
            var a = new StatBlock(3, 1, 4, 1);
            var b = new StatBlock(5, 9, 2, 6);

            Assert.AreEqual(a + b, b + a);
        }

        [Test]
        public void Addition_CanProduceNegatives_SoPenaltiesStack()
        {
            var baseStats = new StatBlock(10, 2, 5, 0);
            var penalty = new StatBlock(0, -5, 0, 0);

            Assert.AreEqual(-3, (baseStats + penalty).speed);
        }

        [Test]
        public void ClampedAtLeast_RaisesStatsBelowTheFloor()
        {
            var block = new StatBlock(10, -4, 3, -1);

            var clamped = block.ClampedAtLeast(0);

            Assert.AreEqual(new StatBlock(10, 0, 3, 0), clamped);
        }

        [Test]
        public void ClampedAtLeast_LeavesStatsAboveTheFloorAlone()
        {
            var block = new StatBlock(10, 4, 3, 1);

            Assert.AreEqual(block, block.ClampedAtLeast(0));
        }

        [Test]
        public void Scaled_MultipliesEveryStatAndRoundsToTheNearestInt()
        {
            var block = new StatBlock(20, 9, 7, 3);

            Assert.AreEqual(new StatBlock(25, 11, 9, 4), block.Scaled(1.25f));
        }

        [Test]
        public void Scaled_ByOne_LeavesTheBlockUnchanged()
        {
            var block = new StatBlock(20, 9, 7, 3);

            Assert.AreEqual(block, block.Scaled(1f));
        }

        // AUDIT.md #16: Scaled must round the same way CombatMath's own
        // damage math does (away-from-zero), not UnityEngine.Mathf's
        // to-even. 5 * 0.5f is an exact tie in float32 (both operands are
        // exactly representable), so this is a genuine test of the tie rule
        // itself rather than a value that happens to land off it either way
        // — to-even would give 2 here, not 3.
        [Test]
        public void Scaled_RoundsAnExactTieAwayFromZero()
        {
            var block = new StatBlock(5, 5, 5, 5);

            Assert.AreEqual(new StatBlock(3, 3, 3, 3), block.Scaled(0.5f));
        }

        [Test]
        public void ScaledForElite_AppliesTheDefenseMultiplierOnlyToDefense()
        {
            var block = new StatBlock(20, 9, 7, 8);

            var scaled = block.ScaledForElite(1.56f, 1.15f, 1.15f);

            Assert.AreEqual((int)Math.Round(20 * 1.56f), scaled.maxHealth);
            Assert.AreEqual((int)Math.Round(9 * 1.56f), scaled.speed);
            Assert.AreEqual((int)Math.Round(7 * 1.15f), scaled.attack);
            Assert.AreEqual((int)Math.Round(8 * 1.15f), scaled.defense);
        }

        [Test]
        public void ScaledForElite_WithEqualMultipliers_MatchesScaled()
        {
            var block = new StatBlock(20, 9, 7, 3, manaRegen: 2, physicalResistance: 4, magicalResistance: 6);

            Assert.AreEqual(block.Scaled(1.3f), block.ScaledForElite(1.3f, 1.3f, 1.3f));
        }

        [Test]
        public void Equality_MatchesOnAllFields()
        {
            var block = new StatBlock(1, 2, 3, 4);

            Assert.AreEqual(block, new StatBlock(1, 2, 3, 4));
            Assert.AreNotEqual(block, new StatBlock(1, 2, 3, 5));
        }

        [Test]
        public void EqualBlocks_ShareAHashCode()
        {
            Assert.AreEqual(new StatBlock(6, 7, 8, 9).GetHashCode(), new StatBlock(6, 7, 8, 9).GetHashCode());
        }

        [Test]
        public void Sum_OfManyBlocks_AccumulatesCorrectly()
        {
            var blocks = Enumerable.Range(1, 4).Select(i => new StatBlock(i, i, i, i));

            var total = blocks.Aggregate(StatBlock.Zero, (acc, b) => acc + b);

            Assert.AreEqual(new StatBlock(10, 10, 10, 10), total);
        }
    }
}
