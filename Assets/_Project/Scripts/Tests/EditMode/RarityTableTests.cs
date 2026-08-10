using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    public class RarityTableTests
    {
        // A deterministic stand-in for Random.Range(0, n): walks 0, 1, 2, …
        // so a test can sweep every branch of a weighted table rather than
        // sampling it and hoping.
        private static Func<int, int> Counting()
        {
            int next = 0;
            return bound => bound <= 0 ? 0 : next++ % bound;
        }

        // Every possible outcome of a weighted pick, by rolling every single
        // value the weight total can produce. Exhaustive rather than random,
        // so the assertions below are about the DISTRIBUTION and not about
        // whether a sample happened to be lucky.
        private static List<int> AllTiers(EncounterClass encounter, int depthStep, int maxTier)
        {
            var seen = new List<int>();
            for (int roll = 0; roll < 100; roll++)
            {
                int captured = roll;
                seen.Add(RarityTable.RollTier(encounter, depthStep, maxTier, _ => captured));
            }

            return seen;
        }

        private static List<int> AllPluses(EncounterClass encounter)
        {
            var seen = new List<int>();
            for (int roll = 0; roll < 100; roll++)
            {
                int captured = roll;
                seen.Add(RarityTable.RollPlus(encounter, _ => captured));
            }

            return seen;
        }

        // ---- the depth curve --------------------------------------------

        // PINNED literals. One tier per leg of eight steps.
        [TestCase(0, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 1)]
        [TestCase(16, 2)]
        [TestCase(80, 10)]
        public void FloorTier_ClimbsOnePerLeg(int step, int expected)
        {
            Assert.AreEqual(expected, RarityTable.FloorTier(step));
        }

        [Test]
        public void FloorTier_NeverGoesNegative()
        {
            Assert.AreEqual(0, RarityTable.FloorTier(-40));
        }

        // ---- THE guarantee ----------------------------------------------

        // The one hard rule in the file, and the reason it is stated as a
        // floor rather than as a distribution: a boss handing over grey loot
        // reads as the fight having been pointless, and "it was statistically
        // unlikely" repairs nothing when it happens.
        [Test]
        public void ABoss_NeverDropsBelowTierThree_AtAnyDepth()
        {
            foreach (int step in new[] { 0, 1, 3, 7, 8, 16, 40, 200 })
            {
                foreach (int tier in AllTiers(EncounterClass.Boss, step, maxTier: 10))
                {
                    Assert.GreaterOrEqual(tier, 3, $"a boss at step {step} rolled tier {tier}");
                }
            }
        }

        [Test]
        public void ABoss_NeverDropsCommon()
        {
            foreach (int step in new[] { 0, 4, 12, 60 })
            {
                foreach (int tier in AllTiers(EncounterClass.Boss, step, maxTier: 10))
                {
                    Assert.AreNotEqual(Rarity.Common, RarityBands.For(tier),
                        $"a boss at step {step} rolled a Common (tier {tier})");
                }
            }
        }

        [Test]
        public void MinimumRarityFor_StatesTheSameGuaranteeInBands()
        {
            Assert.AreEqual(Rarity.Common, RarityTable.MinimumRarityFor(EncounterClass.Normal));
            Assert.AreEqual(Rarity.Common, RarityTable.MinimumRarityFor(EncounterClass.Elite));
            Assert.AreEqual(Rarity.Uncommon, RarityTable.MinimumRarityFor(EncounterClass.Boss));
        }

        [Test]
        public void AnElite_NeverDropsTierZero()
        {
            foreach (int tier in AllTiers(EncounterClass.Elite, depthStep: 0, maxTier: 10))
            {
                Assert.GreaterOrEqual(tier, 1);
            }
        }

        // ---- the shape of each class ------------------------------------

        // Normal is centred BELOW the depth, elite ON it, boss ABOVE it. The
        // ordering is what makes clearing an elite feel different from
        // clearing the room next to it.
        [Test]
        public void TheThreeClasses_AreOrderedNormalThenEliteThenBoss()
        {
            const int Step = 40;   // expected tier 5, clear of both clamps

            double normal = AllTiers(EncounterClass.Normal, Step, 10).Average();
            double elite = AllTiers(EncounterClass.Elite, Step, 10).Average();
            double boss = AllTiers(EncounterClass.Boss, Step, 10).Average();

            Assert.Less(normal, elite, "An elite should be worth more than an ordinary fight");
            Assert.Less(elite, boss, "A boss should be worth more than an elite");
        }

        [Test]
        public void ANormalFight_CentresJustBelowTheDepthsExpectedTier()
        {
            const int Step = 40;
            double average = AllTiers(EncounterClass.Normal, Step, 10).Average();

            Assert.Less(average, RarityTable.FloorTier(Step),
                "Ordinary fights are the run's texture, not its rewards");
        }

        // The tail that lets a boss actually change a run's power level in
        // one payout, rather than nudging it.
        [Test]
        public void ABoss_CanJumpWellAboveTheDepth()
        {
            var tiers = AllTiers(EncounterClass.Boss, depthStep: 16, maxTier: 10);

            Assert.Greater(tiers.Max(), RarityTable.FloorTier(16) + 2,
                "A boss should have a real chance of a big jump, not just a nudge");
        }

        // Deeper must never pay worse. Checked on averages across the whole
        // ladder rather than on one pair, since the clamps flatten the top.
        [Test]
        public void GoingDeeper_NeverPaysWorse()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                double previous = -1;
                for (int step = 0; step <= 80; step += 8)
                {
                    double average = AllTiers(encounter, step, maxTier: 10).Average();
                    Assert.GreaterOrEqual(average, previous,
                        $"{encounter} pays worse at step {step} than at step {step - 8}");
                    previous = average;
                }
            }
        }

        // ---- clamping ----------------------------------------------------

        [Test]
        public void NoRoll_EverExceedsTheCataloguesCeiling()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                foreach (int tier in AllTiers(encounter, depthStep: 500, maxTier: 10))
                {
                    Assert.LessOrEqual(tier, 10, $"{encounter} asked for a tier the catalogue does not have");
                }
            }
        }

        // A catalogue too short to honour the boss floor gets the best it
        // has rather than nothing — degrading gracefully beats refusing to
        // pay out, which is the house posture on missing content.
        [Test]
        public void ACatalogueShorterThanTheBossFloor_PaysItsBestRatherThanFailing()
        {
            foreach (int tier in AllTiers(EncounterClass.Boss, depthStep: 0, maxTier: 1))
            {
                Assert.AreEqual(1, tier, "With only tiers 0-1 authored, a boss should hand over the tier-1");
            }
        }

        [Test]
        public void AnEmptyCatalogue_IsHandledRatherThanThrowing()
        {
            Assert.AreEqual(0, RarityTable.RollTier(EncounterClass.Boss, 10, maxTier: -1, Counting()));
        }

        [Test]
        public void MisbehavingRandomness_IsClampedRatherThanThrowing()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.DoesNotThrow(() => RarityTable.RollTier(encounter, 10, 10, _ => 99999));
                Assert.DoesNotThrow(() => RarityTable.RollTier(encounter, 10, 10, _ => -7));
                Assert.DoesNotThrow(() => RarityTable.RollPlus(encounter, _ => 99999));
                Assert.DoesNotThrow(() => RarityTable.RollPlus(encounter, _ => -7));
            }
        }

        [Test]
        public void MissingRandomness_IsHandledRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => RarityTable.RollTier(EncounterClass.Boss, 10, 10, null));
            Assert.DoesNotThrow(() => RarityTable.RollPlus(EncounterClass.Boss, null));
        }

        // ---- plus ---------------------------------------------------------

        // Plus is the long tail: most drops are +0, and that is what keeps a
        // +3 exciting at step 40 as well as at step 4.
        [Test]
        public void MostDropsAreUnhoned()
        {
            var pluses = AllPluses(EncounterClass.Normal);

            Assert.AreEqual(70, pluses.Count(p => p == 0),
                "70 of every 100 normal drops should be +0");
        }

        [Test]
        public void PlusNeverGoesNegativeAndStaysModest()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                var pluses = AllPluses(encounter);
                Assert.GreaterOrEqual(pluses.Min(), 0);
                Assert.LessOrEqual(pluses.Max(), 5, $"{encounter} rolled an implausibly honed drop");
            }
        }

        [Test]
        public void BetterFightsRollHonedGearMoreOften()
        {
            double normal = AllPluses(EncounterClass.Normal).Average();
            double elite = AllPluses(EncounterClass.Elite).Average();
            double boss = AllPluses(EncounterClass.Boss).Average();

            Assert.Less(normal, elite);
            Assert.Less(elite, boss);
        }

        // Plus deliberately does not climb with depth — RollPlus takes no
        // depth argument at all. That is enforced by the signature rather
        // than by a test, and is recorded here so that adding one later is a
        // deliberate act: plus becoming depth-sensitive would turn it into a
        // second progression axis competing with tier, and the rarity bands
        // would stop meaning anything.
    }
}
