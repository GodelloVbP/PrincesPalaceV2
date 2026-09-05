using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class AbilityScoreBlockTests
    {
        [Test]
        public void Zero_HasEveryScoreAtZero()
        {
            var zero = AbilityScoreBlock.Zero;

            foreach (AbilityScore score in Enum.GetValues(typeof(AbilityScore)))
            {
                Assert.AreEqual(0, zero[score], $"{score} should be zero on AbilityScoreBlock.Zero");
            }
        }

        // Guards the one place adding an AbilityScore can silently go wrong:
        // the indexer throwing for a value nobody wired up.
        [Test]
        public void Indexer_CoversEveryAbilityScore()
        {
            var block = new AbilityScoreBlock(1, 2, 3, 4, 5, 6);

            foreach (AbilityScore score in Enum.GetValues(typeof(AbilityScore)))
            {
                Assert.DoesNotThrow(() => _ = block[score], $"AbilityScoreBlock's indexer has no case for {score}");
            }
        }

        [Test]
        public void Indexer_ReturnsTheMatchingField()
        {
            var block = new AbilityScoreBlock(14, 12, 13, 8, 10, 15);

            Assert.AreEqual(14, block[AbilityScore.Strength]);
            Assert.AreEqual(12, block[AbilityScore.Dexterity]);
            Assert.AreEqual(13, block[AbilityScore.Constitution]);
            Assert.AreEqual(8, block[AbilityScore.Wisdom]);
            Assert.AreEqual(10, block[AbilityScore.Intelligence]);
            Assert.AreEqual(15, block[AbilityScore.Charisma]);
        }

        [Test]
        public void Indexer_ThrowsForAnUndefinedScore()
        {
            var block = AbilityScoreBlock.Zero;
            var bogus = (AbilityScore)999;

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = block[bogus]);
        }

        [Test]
        public void Addition_SumsEveryScore()
        {
            var a = new AbilityScoreBlock(10, 5, 3, 1, 2, 4);
            var b = new AbilityScoreBlock(4, 2, 6, 8, 1, 3);

            Assert.AreEqual(new AbilityScoreBlock(14, 7, 9, 9, 3, 7), a + b);
        }

        [Test]
        public void Addition_WithZero_LeavesTheBlockUnchanged()
        {
            var block = new AbilityScoreBlock(12, 9, 4, 2, 6, 8);

            Assert.AreEqual(block, block + AbilityScoreBlock.Zero);
            Assert.AreEqual(block, AbilityScoreBlock.Zero + block);
        }

        [Test]
        public void Addition_IsOrderIndependent()
        {
            var a = new AbilityScoreBlock(3, 1, 4, 1, 5, 9);
            var b = new AbilityScoreBlock(5, 9, 2, 6, 5, 3);

            Assert.AreEqual(a + b, b + a);
        }

        [Test]
        public void Addition_CanProduceNegatives_SoPenaltiesStack()
        {
            var baseScores = new AbilityScoreBlock(10, 2, 5, 0, 4, 6);
            var penalty = new AbilityScoreBlock(0, -5, 0, 0, 0, 0);

            Assert.AreEqual(-3, (baseScores + penalty).dexterity);
        }

        [Test]
        public void ClampedAtLeast_RaisesScoresBelowTheFloor()
        {
            var block = new AbilityScoreBlock(10, -4, 3, -1, 0, -2);

            var clamped = block.ClampedAtLeast(0);

            Assert.AreEqual(new AbilityScoreBlock(10, 0, 3, 0, 0, 0), clamped);
        }

        [Test]
        public void ClampedAtLeast_LeavesScoresAboveTheFloorAlone()
        {
            var block = new AbilityScoreBlock(10, 4, 3, 1, 2, 5);

            Assert.AreEqual(block, block.ClampedAtLeast(0));
        }

        [Test]
        public void Equality_MatchesOnAllFields()
        {
            var block = new AbilityScoreBlock(1, 2, 3, 4, 5, 6);

            Assert.AreEqual(block, new AbilityScoreBlock(1, 2, 3, 4, 5, 6));
            Assert.AreNotEqual(block, new AbilityScoreBlock(1, 2, 3, 4, 5, 7));
        }

        [Test]
        public void EqualBlocks_ShareAHashCode()
        {
            Assert.AreEqual(new AbilityScoreBlock(6, 7, 8, 9, 10, 11).GetHashCode(), new AbilityScoreBlock(6, 7, 8, 9, 10, 11).GetHashCode());
        }

        [Test]
        public void Sum_OfManyBlocks_AccumulatesCorrectly()
        {
            var blocks = Enumerable.Range(1, 4).Select(i => new AbilityScoreBlock(i, i, i, i, i, i));

            var total = blocks.Aggregate(AbilityScoreBlock.Zero, (acc, b) => acc + b);

            Assert.AreEqual(new AbilityScoreBlock(10, 10, 10, 10, 10, 10), total);
        }
    }
}
