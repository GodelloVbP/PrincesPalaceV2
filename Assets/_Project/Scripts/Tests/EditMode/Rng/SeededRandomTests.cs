using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new SeededRandom(12345);
            var b = new SeededRandom(12345);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);

            var sequenceA = new int[20];
            var sequenceB = new int[20];
            for (int i = 0; i < 20; i++)
            {
                sequenceA[i] = a.NextInt(0, int.MaxValue);
                sequenceB[i] = b.NextInt(0, int.MaxValue);
            }

            CollectionAssert.AreNotEqual(sequenceA, sequenceB);
        }

        [Test]
        public void NextInt_RespectsBounds()
        {
            var rng = new SeededRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(5, 10);
                Assert.GreaterOrEqual(value, 5);
                Assert.Less(value, 10);
            }
        }

        [Test]
        public void NextInt_ThrowsWhenMaxNotGreaterThanMin()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 3));
        }

        [Test]
        public void NextFloat_IsWithinZeroToOne()
        {
            var rng = new SeededRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void NextBool_RoughlyMatchesProbability()
        {
            var rng = new SeededRandom(99);
            int trueCount = 0;
            const int trials = 20000;
            for (int i = 0; i < trials; i++)
            {
                if (rng.NextBool(0.3f))
                {
                    trueCount++;
                }
            }

            float ratio = (float)trueCount / trials;
            Assert.That(ratio, Is.EqualTo(0.3f).Within(0.02f));
        }

        [Test]
        public void Choice_ThrowsOnEmptyOrNull()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentException>(() => rng.Choice<int>(null));
            Assert.Throws<ArgumentException>(() => rng.Choice(new List<int>()));
        }

        [Test]
        public void Choice_AlwaysReturnsAnItemFromTheList()
        {
            var rng = new SeededRandom(3);
            var items = new List<string> { "a", "b", "c" };
            for (int i = 0; i < 100; i++)
            {
                Assert.Contains(rng.Choice(items), items);
            }
        }

        [Test]
        public void WeightedChoice_ThrowsOnMismatchedLengths()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentException>(() => rng.WeightedChoice(new List<int> { 1, 2 }, new List<float> { 1f }));
        }

        [Test]
        public void WeightedChoice_ThrowsOnNegativeWeight()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentException>(() => rng.WeightedChoice(new List<int> { 1, 2 }, new List<float> { 1f, -1f }));
        }

        [Test]
        public void WeightedChoice_ThrowsWhenAllWeightsZero()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentException>(() => rng.WeightedChoice(new List<int> { 1, 2 }, new List<float> { 0f, 0f }));
        }

        [Test]
        public void WeightedChoice_HeavilyWeightedItemDominates()
        {
            var rng = new SeededRandom(123);
            var items = new List<string> { "rare", "common" };
            var weights = new List<float> { 1f, 99f };

            int commonCount = 0;
            const int trials = 5000;
            for (int i = 0; i < trials; i++)
            {
                if (rng.WeightedChoice(items, weights) == "common")
                {
                    commonCount++;
                }
            }

            Assert.Greater(commonCount, trials * 0.9);
        }

        [Test]
        public void Shuffle_PreservesAllElements()
        {
            var rng = new SeededRandom(55);
            var list = Enumerable.Range(0, 20).ToList();
            var original = new List<int>(list);

            rng.Shuffle(list);

            CollectionAssert.AreEquivalent(original, list);
        }

        [Test]
        public void Shuffle_IsDeterministicForSameSeed()
        {
            var listA = Enumerable.Range(0, 20).ToList();
            var listB = Enumerable.Range(0, 20).ToList();

            new SeededRandom(8).Shuffle(listA);
            new SeededRandom(8).Shuffle(listB);

            CollectionAssert.AreEqual(listA, listB);
        }

        [Test]
        public void StateCanBeSavedAndRestoredToContinueIdentically()
        {
            var original = new SeededRandom(2024);
            original.NextInt(0, 1000);
            original.NextInt(0, 1000);
            ulong savedState = original.State;

            var restored = SeededRandom.FromState(savedState);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(original.NextInt(0, 1000000), restored.NextInt(0, 1000000));
            }
        }
    }
}
