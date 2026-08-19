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

        // SAMPLED against a seeded generator, not swept with a constant.
        //
        // The old helpers walked roll 0..99 and handed the SAME value to every
        // draw, which was exact for a one-draw weighted pick. The ladder takes
        // five draws, so a constant makes every rung agree and collapses the
        // result to all-or-nothing -- it measured the stub, not the ladder.
        //
        // Seeded, so it is reproducible; large, so the assertions below are
        // about the distribution rather than about a lucky sample. Expected
        // values are LITERALS measured off the model, never recomputed from
        // the production formula (CLAUDE.md gotcha 5).
        private const int Samples = 40000;

        private static List<int> SampleTiers(EncounterClass encounter, int depthStep, int maxTier, int favor = 0)
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(20260818);
            var seen = new List<int>(Samples);
            for (int i = 0; i < Samples; i++)
            {
                seen.Add(RarityTable.RollTier(encounter, depthStep, maxTier, favor, n => rng.NextInt(0, n)));
            }

            return seen;
        }

        private static List<int> SamplePluses(EncounterClass encounter, int favor = 0)
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(20260818);
            var seen = new List<int>(Samples);
            for (int i = 0; i < Samples; i++)
            {
                seen.Add(RarityTable.RollPlus(encounter, favor, n => rng.NextInt(0, n)));
            }

            return seen;
        }

        private static double Share(List<int> values, Func<int, bool> predicate) =>
            values.Count(predicate) / (double)values.Count;

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
                foreach (int tier in SampleTiers(EncounterClass.Boss, step, maxTier: 10))
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
                foreach (int tier in SampleTiers(EncounterClass.Boss, step, maxTier: 10))
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
            foreach (int tier in SampleTiers(EncounterClass.Elite, depthStep: 0, maxTier: 10))
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

            double normal = SampleTiers(EncounterClass.Normal, Step, 10).Average();
            double elite = SampleTiers(EncounterClass.Elite, Step, 10).Average();
            double boss = SampleTiers(EncounterClass.Boss, Step, 10).Average();

            Assert.Less(normal, elite, "An elite should be worth more than an ordinary fight");
            Assert.Less(elite, boss, "A boss should be worth more than an elite");
        }

        // REPLACES ANormalFight_CentresJustBelowTheDepthsExpectedTier, whose
        // invariant this design deliberately reverses.
        //
        // The old table rolled an offset in [-3, +1], so a normal fight
        // averaged BELOW the depth's tier -- which is exactly why an early run
        // only ever produced tier 0 and 1 and read as stale. The ladder only
        // ever climbs, so the depth's tier is now the FLOOR of what a fight
        // pays rather than the middle of it. What keeps ordinary fights
        // ordinary is that the climb is usually zero.
        [Test]
        public void ANormalFight_UsuallyPaysExactlyTheDepthsTier()
        {
            const int Step = 40;   // expected tier 5, clear of both clamps
            var tiers = SampleTiers(EncounterClass.Normal, Step, 10);
            int expected = RarityTable.FloorTier(Step);

            Assert.GreaterOrEqual(Share(tiers, t => t == expected), 0.70,
                "most ordinary fights should pay exactly the depth's tier - they are the run's texture");
            Assert.GreaterOrEqual(tiers.Min(), expected,
                "the ladder only climbs, so nothing may pay BELOW the depth");
        }

        // The tail that lets a boss actually change a run's power level in
        // one payout, rather than nudging it.
        [Test]
        public void ABoss_CanJumpWellAboveTheDepth()
        {
            var tiers = SampleTiers(EncounterClass.Boss, depthStep: 16, maxTier: 10);

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
                    double average = SampleTiers(encounter, step, maxTier: 10).Average();
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
                foreach (int tier in SampleTiers(encounter, depthStep: 500, maxTier: 10))
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
            foreach (int tier in SampleTiers(EncounterClass.Boss, depthStep: 0, maxTier: 1))
            {
                Assert.AreEqual(1, tier, "With only tiers 0-1 authored, a boss should hand over the tier-1");
            }
        }

        [Test]
        public void AnEmptyCatalogue_IsHandledRatherThanThrowing()
        {
            Assert.AreEqual(0, RarityTable.RollTier(EncounterClass.Boss, 10, maxTier: -1, 0, Counting()));
        }

        [Test]
        public void MisbehavingRandomness_IsClampedRatherThanThrowing()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                Assert.DoesNotThrow(() => RarityTable.RollTier(encounter, 10, 10, 0, _ => 99999));
                Assert.DoesNotThrow(() => RarityTable.RollTier(encounter, 10, 10, 0, _ => -7));
                Assert.DoesNotThrow(() => RarityTable.RollPlus(encounter, 0, _ => 99999));
                Assert.DoesNotThrow(() => RarityTable.RollPlus(encounter, 0, _ => -7));
            }
        }

        [Test]
        public void MissingRandomness_IsHandledRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => RarityTable.RollTier(EncounterClass.Boss, 10, 10, 0, null));
            Assert.DoesNotThrow(() => RarityTable.RollPlus(EncounterClass.Boss, 0, null));
        }

        // ---- plus ---------------------------------------------------------

        // Plus is the long tail: most drops are +0, and that is what keeps a
        // +3 exciting at step 40 as well as at step 4.
        [Test]
        public void MostDropsAreUnhoned()
        {
            var pluses = SamplePluses(EncounterClass.Normal);

            Assert.GreaterOrEqual(Share(pluses, p => p == 0), 0.70,
                "most normal drops should be +0 - that is what keeps a +3 exciting at step 40");
            Assert.LessOrEqual(Share(pluses, p => p == 0), 0.85,
                "if nearly everything is +0 the long tail has stopped existing");
        }

        [Test]
        public void PlusNeverGoesNegativeAndStaysModest()
        {
            foreach (EncounterClass encounter in Enum.GetValues(typeof(EncounterClass)))
            {
                var pluses = SamplePluses(encounter);
                Assert.GreaterOrEqual(pluses.Min(), 0);
                Assert.LessOrEqual(pluses.Max(), 5, $"{encounter} rolled an implausibly honed drop");
            }
        }

        [Test]
        public void BetterFightsRollHonedGearMoreOften()
        {
            double normal = SamplePluses(EncounterClass.Normal).Average();
            double elite = SamplePluses(EncounterClass.Elite).Average();
            double boss = SamplePluses(EncounterClass.Boss).Average();

            Assert.Less(normal, elite);
            Assert.Less(elite, boss);
        }

        // Plus deliberately does not climb with depth — RollPlus takes no
        // depth argument at all. That is enforced by the signature rather
        // than by a test, and is recorded here so that adding one later is a
        // deliberate act: plus becoming depth-sensitive would turn it into a
        // second progression axis competing with tier, and the rarity bands
        // would stop meaning anything.

        // ---- the anti-saturation guarantee ---------------------------------

        // THE PROPERTY THIS WHOLE DESIGN EXISTS FOR.
        //
        // Every shape tried before the ladder let a bonus push mass into the
        // rarest band: the multiplicative version produced the top tier 34% of
        // the time on a floor-1 boss and 67% on a floor-3 normal. A ladder
        // cannot do that, because the top rung needs five consecutive
        // successes -- so even at the hard p-cap, with the best class and
        // absurd Favor, the jackpot stays rare.
        //
        // Pinned as a LITERAL ceiling rather than recomputed, so a future
        // retune that reintroduces saturation fails here rather than shipping.
        [Test]
        public void TheJackpotCanNeverBecomeTheDefault_HoweverGenerousTheOdds()
        {
            var tiers = SampleTiers(EncounterClass.Boss, depthStep: 0, maxTier: 10, favor: 500);
            double topRung = Share(tiers, t => t >= RarityTable.FloorTier(0) + LootLadder.MaxRungs);

            Assert.Less(topRung, 0.08,
                "the top rung has started filling up - a bonus is pushing mass into the rarest band, " +
                "which is the exact fault the ladder replaced");
        }

        [Test]
        public void FavorFattensTheTailWithoutCollapsingIt()
        {
            var none = SamplePluses(EncounterClass.Normal, favor: 0);
            var lots = SamplePluses(EncounterClass.Normal, favor: 20);

            Assert.Less(Share(none, p => p >= 3), Share(lots, p => p >= 3),
                "Prince's Favor must make a run-changing find more likely");
            Assert.Less(Share(lots, p => p >= 3), 0.15,
                "...but never routine - Favor fattens the tail, it does not guarantee it");
        }

        // Favor is the SQUAD'S HIGHEST, never the sum -- otherwise the stat
        // would scale with squad size and the real decision would become
        // "bring more bodies" rather than "bring this character".
        [Test]
        public void FavorIsMonotonic_MoreIsNeverWorse()
        {
            double previous = -1;
            foreach (int favor in new[] { 0, 5, 10, 20, 40 })
            {
                double share = Share(SamplePluses(EncounterClass.Elite, favor), p => p >= 2);
                Assert.GreaterOrEqual(share, previous, $"favor {favor} paid out worse than less favor");
                previous = share;
            }
        }

    }
}
