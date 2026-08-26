using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // The RiftTier climb and the which-modifiers pick, mirroring
    // RarityTableTests' structure -- same sampled-distribution technique for
    // the same reason (LootLadder draws a FIXED number of times per call, so
    // a constant stand-in for randomness collapses the result to
    // all-or-nothing rather than measuring the ladder).
    public class ModifierTableTests
    {
        private const int Samples = 40000;

        private static List<RiftTier> SampleRiftTiers(EncounterClass encounter, int favor = 0)
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(20260826);
            var seen = new List<RiftTier>(Samples);
            for (int i = 0; i < Samples; i++)
            {
                seen.Add(ModifierTable.RollRiftTier(encounter, favor, n => rng.NextInt(0, n)));
            }

            return seen;
        }

        private static double Share<T>(List<T> values, Func<T, bool> predicate) =>
            values.Count(predicate) / (double)values.Count;

        // ---- the ladder height itself -------------------------------------

        // Three, not LootLadder.MaxRungs's five -- a content pass that
        // retunes MaxRungs must not silently widen RiftTier past Convergent.
        [Test]
        public void TheRiftTierLadderIsThreeRungs()
        {
            Assert.AreEqual(3, ModifierTable.MaxRungs);
        }

        // ---- RollRiftTier ----------------------------------------------------

        [Test]
        public void NoRollEverExceedsConvergent()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                foreach (var tier in SampleRiftTiers(encounter, favor: 500))
                {
                    Assert.LessOrEqual((int)tier, (int)RiftTier.Convergent,
                        $"{encounter} rolled past the top rung");
                }
            }
        }

        [Test]
        public void RiftTierNeverGoesNegative()
        {
            foreach (var tier in SampleRiftTiers(EncounterClass.Normal))
            {
                Assert.GreaterOrEqual((int)tier, (int)RiftTier.Ordinary);
            }
        }

        // Most rolls are Ordinary -- the ladder is a long tail, same shape as
        // RollPlus's "most drops are unhoned".
        [Test]
        public void MostRollsAreOrdinary()
        {
            var tiers = SampleRiftTiers(EncounterClass.Normal);

            Assert.GreaterOrEqual(Share(tiers, t => t == RiftTier.Ordinary), 0.70,
                "most normal offers should roll no modifier slots at all");
        }

        [Test]
        public void BetterFightsRollHigherRiftTiersMoreOften()
        {
            double normal = SampleRiftTiers(EncounterClass.Normal).Average(t => (int)t);
            double elite = SampleRiftTiers(EncounterClass.Elite).Average(t => (int)t);
            double boss = SampleRiftTiers(EncounterClass.Boss).Average(t => (int)t);

            Assert.Less(normal, elite);
            Assert.Less(elite, boss);
        }

        [Test]
        public void FavorFattensTheRiftTierTailWithoutCollapsingIt()
        {
            var none = SampleRiftTiers(EncounterClass.Normal, favor: 0);
            var lots = SampleRiftTiers(EncounterClass.Normal, favor: 20);

            Assert.Less(Share(none, t => t >= RiftTier.RiftForged), Share(lots, t => t >= RiftTier.RiftForged),
                "Prince's Favor must make a heavily-affixed find more likely");
        }

        [Test]
        public void FavorIsMonotonicForRiftTierToo()
        {
            double previous = -1;
            foreach (int favor in new[] { 0, 5, 10, 20, 40 })
            {
                double share = Share(SampleRiftTiers(EncounterClass.Elite, favor), t => t >= RiftTier.RiftTouched);
                Assert.GreaterOrEqual(share, previous, $"favor {favor} paid out worse than less favor");
                previous = share;
            }
        }

        // Boundary behaviour, pinned rather than sampled -- a source that
        // always reports "succeeded" climbs every rung available; a source
        // that always reports "failed" never leaves the bottom. Neither
        // recomputes LootLadder's formula, they just exercise its two
        // extremes.
        [Test]
        public void AnAlwaysSucceedingSource_AlwaysReachesConvergent()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.AreEqual(RiftTier.Convergent, ModifierTable.RollRiftTier(encounter, 0, _ => 0));
            }
        }

        [Test]
        public void AnAlwaysFailingSource_NeverLeavesOrdinary()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.AreEqual(RiftTier.Ordinary, ModifierTable.RollRiftTier(encounter, 0, _ => 999));
            }
        }

        [Test]
        public void MissingRandomness_IsHandledRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ModifierTable.RollRiftTier(EncounterClass.Boss, 0, null));
            Assert.AreEqual(RiftTier.Ordinary, ModifierTable.RollRiftTier(EncounterClass.Boss, 0, null));
        }

        [Test]
        public void MisbehavingRandomness_IsClampedRatherThanThrowing()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.DoesNotThrow(() => ModifierTable.RollRiftTier(encounter, 0, _ => 99999));
                Assert.DoesNotThrow(() => ModifierTable.RollRiftTier(encounter, 0, _ => -7));
            }
        }

        // ---- PickModifiers -------------------------------------------------

        private static readonly List<string> FivePool = new List<string> { "a", "b", "c", "d", "e" };

        [Test]
        public void PicksExactlyTheRequestedSlotCount()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 3, _ => 0);
            Assert.AreEqual(3, picked.Count);
        }

        [Test]
        public void PickedModifiersAreAlwaysDistinct()
        {
            for (int calls = 0; calls < 5; calls++)
            {
                int n = calls;
                Func<int, int> walking = bound => (n++) % Math.Max(1, bound);
                var picked = ModifierTable.PickModifiers(FivePool, 3, walking);

                CollectionAssert.AllItemsAreUnique(picked);
            }
        }

        // Removal-based selection, pinned: always taking index 0 out of a
        // shrinking pool walks the pool in order. Not a recomputed formula --
        // this is the definition of "always pick the first remaining one".
        [Test]
        public void AlwaysPickingTheFirstRemaining_WalksThePoolInOrder()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 3, _ => 0);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, picked);
        }

        [Test]
        public void ZeroSlots_PicksNothing()
        {
            Assert.IsEmpty(ModifierTable.PickModifiers(FivePool, 0, _ => 0));
        }

        [Test]
        public void RequestingMoreSlotsThanThePool_ReturnsTheWholePoolRatherThanRepeating()
        {
            var picked = ModifierTable.PickModifiers(FivePool, 10, _ => 0);

            Assert.AreEqual(FivePool.Count, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
        }

        [Test]
        public void AnEmptyOrMissingPool_IsHandledRatherThanThrowing()
        {
            Assert.IsEmpty(ModifierTable.PickModifiers(new List<string>(), 3, _ => 0));
            Assert.IsEmpty(ModifierTable.PickModifiers(null, 3, _ => 0));
        }

        [Test]
        public void MissingRandomness_PicksNothing()
        {
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, null));
            Assert.IsEmpty(ModifierTable.PickModifiers(FivePool, 3, null));
        }

        [Test]
        public void OutOfRangeRoll_IsClampedRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, _ => 9999));
            Assert.DoesNotThrow(() => ModifierTable.PickModifiers(FivePool, 3, _ => -5));
        }
    }
}
