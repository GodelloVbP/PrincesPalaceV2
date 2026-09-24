using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.EditModeTests
{
    // RngStreams turns one run seed into many independent reproducible
    // streams. Everything downstream — a resumable run, a fight that replays
    // identically, tests that can pin an outcome instead of retrying until the
    // dice cooperate — rests on these properties holding exactly.
    public class RngStreamsTests
    {
        // The values below are PINNED LITERALS, and they were produced by an
        // independent implementation of the same algorithm rather than by
        // running the code under test and writing down what came out. That
        // distinction is the whole point (CLAUDE.md's fifth gotcha, and
        // AUDIT.md #18): an expectation copied from the implementation agrees
        // with any implementation, including a broken one.
        //
        // These are effectively a serialized format. Every seed anyone has
        // ever played is interpreted through this function, so a change that
        // makes these literals wrong is not a refactor — it renumbers every
        // existing run. If one of these fails, the question is what changed
        // in Derive, not what the new number should be.
        [TestCase(0UL, RngStreams.Leg, 0, 0, 16294208416658607535UL)]
        [TestCase(12345UL, RngStreams.Leg, 1, 0, 14483974800537063282UL)]
        [TestCase(12345UL, RngStreams.Boss, 1, 0, 5846394624376284771UL)]
        [TestCase(12345UL, RngStreams.Fight, 1, 7, 12881891470559542815UL)]
        public void Derive_ProducesItsPinnedValue(ulong runSeed, uint stream, int a, int b, ulong expected)
        {
            Assert.AreEqual(expected, RngStreams.Derive(runSeed, stream, a, b),
                "Derive is a serialized format, not an implementation detail — every run ever seeded is read through it. " +
                "A change here renumbers all of them.");
        }

        [Test]
        public void Derive_IsPure()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(
                    RngStreams.Derive(777UL, RngStreams.Fight, 3, 9),
                    RngStreams.Derive(777UL, RngStreams.Fight, 3, 9),
                    "Same inputs must always give the same seed; anything else means hidden state, which is what this type exists to avoid.");
            }
        }

        // The property that keeps a content change from moving the map. If
        // streams shared a generator, adding an enemy would change how many
        // numbers the boss pick consumed and shift everything after it.
        [Test]
        public void DifferentStreams_AtTheSamePosition_DoNotCollide()
        {
            var streams = new[] { RngStreams.Leg, RngStreams.Boss, RngStreams.Fight, RngStreams.Treasure };
            var seeds = streams.Select(s => RngStreams.Derive(4242UL, s, 5, 5)).ToList();

            CollectionAssert.AllItemsAreUnique(seeds,
                "Two purposes at one position produced the same stream, so they would draw identical numbers in lockstep.");
        }

        // Adjacent positions are the case a player would actually notice — two
        // rooms in a row that "felt the same". Neighbouring inputs must not
        // give neighbouring seeds.
        [Test]
        public void AdjacentPositions_ProduceUnrelatedStreams()
        {
            var byNode = Enumerable.Range(0, 64)
                .Select(node => RngStreams.Derive(31337UL, RngStreams.Fight, 12, node))
                .ToList();
            CollectionAssert.AllItemsAreUnique(byNode, "Adjacent node ids collided.");

            var byStep = Enumerable.Range(0, 64)
                .Select(step => RngStreams.Derive(31337UL, RngStreams.Leg, step))
                .ToList();
            CollectionAssert.AllItemsAreUnique(byStep, "Adjacent steps collided.");

            // Not just distinct — far apart. Sequential seeds would leave
            // SplitMix64 producing visibly similar early draws, which is the
            // failure a plain uniqueness check cannot see.
            for (int i = 1; i < byStep.Count; i++)
            {
                ulong gap = byStep[i] > byStep[i - 1] ? byStep[i] - byStep[i - 1] : byStep[i - 1] - byStep[i];
                Assert.Greater(gap, 1000UL,
                    $"Steps {i - 1} and {i} produced near-identical seeds ({byStep[i - 1]} vs {byStep[i]}), so consecutive rooms would roll near-identical results.");
            }
        }

        [Test]
        public void DifferentRunSeeds_ProduceDifferentStreams()
        {
            var seeds = Enumerable.Range(0, 200)
                .Select(i => RngStreams.Derive((ulong)i, RngStreams.Leg, 0))
                .ToList();

            CollectionAssert.AllItemsAreUnique(seeds, "Two runs seeded differently would generate the same first leg.");
        }

        // The end-to-end property the rest of the codebase actually consumes:
        // open a stream twice, get the same sequence twice.
        [Test]
        public void Open_GivesTheSameSequenceEveryTime()
        {
            List<int> Draw() =>
                Enumerable.Range(0, 20)
                    .Select(_ => RngStreams.Open(555UL, RngStreams.Fight, 2, 3).NextInt(0, 1000))
                    .ToList();

            // Each Open() is a fresh generator on the same derived seed, so
            // every entry is the FIRST draw of that stream and they must all
            // agree — this is what makes "the fight at this node" reproducible
            // without persisting generator state.
            var draws = Draw();
            Assert.AreEqual(1, draws.Distinct().Count(),
                "Re-opening the same stream gave a different first draw, so a resumed fight would not match the one that was interrupted.");

            var again = Draw();
            CollectionAssert.AreEqual(draws, again);
        }

        [Test]
        public void Open_OnDifferentPositions_DivergesImmediately()
        {
            var first = RngStreams.Open(99UL, RngStreams.Fight, 1, 1);
            var second = RngStreams.Open(99UL, RngStreams.Fight, 1, 2);

            var a = Enumerable.Range(0, 10).Select(_ => first.NextInt(0, 10000)).ToList();
            var b = Enumerable.Range(0, 10).Select(_ => second.NextInt(0, 10000)).ToList();

            CollectionAssert.AreNotEqual(a, b, "Two different nodes produced identical fights.");
        }

        // ---- the third position input --------------------------------------
        //
        // The shop needs three coordinates -- step, node, reroll index -- and
        // the extension has to be free for every caller written before it
        // existed, or it renumbers every save in the world.
        //
        // THIS IS THE TEST THAT CATCHES A TIDY. `c` is a separate XOR line
        // precisely because `0 * K` is 0 and `z ^ 0` is `z`; folding it into
        // the multiply chain instead would still "look right" and would move
        // every existing seed. Pinned by property here and by literal below.
        [Test]
        public void OmittingTheThirdCoordinate_IsTheSameAsPassingZero()
        {
            var streams = new[] { RngStreams.Leg, RngStreams.Boss, RngStreams.Fight, RngStreams.Treasure };

            foreach (ulong seed in new ulong[] { 0UL, 1UL, 12345UL, 999999UL, ulong.MaxValue })
            {
                foreach (uint stream in streams)
                {
                    for (int a = 0; a < 6; a++)
                    {
                        for (int b = 0; b < 6; b++)
                        {
                            Assert.AreEqual(
                                RngStreams.Derive(seed, stream, a, b),
                                RngStreams.Derive(seed, stream, a, b, 0),
                                $"Adding the third coordinate moved the seed for ({seed}, {stream}, {a}, {b}) -- " +
                                "which renumbers every run ever seeded through that position.");
                        }
                    }
                }
            }
        }

        // Same provenance as the four-argument literals above: produced by an
        // independent implementation of the algorithm, not copied out of a
        // failing run. The first row is the pre-existing Fight literal read
        // through the five-argument overload, so a change that only broke the
        // DEFAULT would still be caught; the rest pin the three shop streams
        // at a non-zero third coordinate.
        [TestCase(12345UL, RngStreams.Fight, 1, 7, 0, 12881891470559542815UL)]
        [TestCase(12345UL, RngStreams.Fight, 1, 7, 3, 15148803583783660044UL)]
        [TestCase(999UL, RngStreams.ShopGear, 12, 4, 0, 8559632178966823291UL)]
        [TestCase(999UL, RngStreams.ShopBooks, 12, 4, 0, 13513051749714980753UL)]
        [TestCase(999UL, RngStreams.ShopRelics, 12, 4, 1, 17972045689052808779UL)]
        public void Derive_WithAThirdCoordinate_ProducesItsPinnedValue(
            ulong runSeed, uint stream, int a, int b, int c, ulong expected)
        {
            Assert.AreEqual(expected, RngStreams.Derive(runSeed, stream, a, b, c));
        }

        // A reroll is one coordinate moving, and it has to move the WHOLE
        // shelf -- otherwise the player pays and sees the same cards.
        [Test]
        public void AdjacentRerollCounts_ProduceUnrelatedStreams()
        {
            var byReroll = Enumerable.Range(0, 32)
                .Select(n => RngStreams.Derive(31337UL, RngStreams.ShopGear, 12, 4, n))
                .ToList();

            CollectionAssert.AllItemsAreUnique(byReroll, "Two reroll counts collided, so a paid reroll would return the same shelf.");
        }

        // The three shop shelves are three streams so that rerolling one
        // cannot move another. If they collided, the relic shelf would draw in
        // lockstep with the gear shelf at every node.
        [Test]
        public void TheThreeShopStreams_DoNotCollideAtOnePosition()
        {
            var seeds = new[] { RngStreams.ShopGear, RngStreams.ShopBooks, RngStreams.ShopRelics }
                .Select(s => RngStreams.Derive(4242UL, s, 5, 5, 2))
                .ToList();

            CollectionAssert.AllItemsAreUnique(seeds);
        }

        // New streams take a NEW NUMBER; reusing one renumbers every run that
        // was ever seeded through it. Stated as a test because the rule lives
        // in a comment, and a comment cannot fail.
        [Test]
        public void EveryStreamNumberIsDistinct()
        {
            var numbers = new[]
            {
                RngStreams.Leg, RngStreams.Boss, RngStreams.Fight, RngStreams.Treasure,
                RngStreams.ShopGear, RngStreams.ShopBooks, RngStreams.ShopRelics,
                RngStreams.SpellDrop, RngStreams.Event,
            };

            CollectionAssert.AllItemsAreUnique(numbers);
        }

        // A stream number is a serialized format: renumbering Event moves
        // which event every existing seed opens at every node.
        [Test]
        public void TheEventStreamIsNumberNine()
        {
            Assert.AreEqual(9u, RngStreams.Event);
        }
    }
}
