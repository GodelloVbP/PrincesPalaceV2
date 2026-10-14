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

        private static double Share(List<int> values, Func<int, bool> predicate) =>
            values.Count(predicate) / (double)values.Count;

        // ---- the depth curve --------------------------------------------

        // PINNED literals. One tier per TWO legs (16 steps) -- doubled from
        // one leg (8) in the tier rebalance pass so the tier curve climbs at
        // half the old rate; see StepsPerTier's own header for why.
        [TestCase(0, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 0)]
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

        // TIER REBALANCE: "tiers come more slowly" -- pinned as the actual
        // target the brief states in floor terms, not just the depthStep
        // formula those floors translate to. Floor N's own leg starts at
        // depthStep (N-1)*8 (RunDepth.FloorFor's inverse); a NORMAL fight
        // (the common case a run's texture is mostly made of) sampled there
        // must land in the stated tier range at least 80% of the time.
        // Zero Favor -- these are the UNBOOSTED, common-case numbers; see
        // the Favor-driven tests below for how Favor moves them.
        [TestCase(0, 0, 1)]   // floor 1
        [TestCase(16, 1, 2)]  // floor 3
        [TestCase(32, 2, 3)]  // floor 5
        public void NormalFights_LandInTheStatedTierRangeAtLeast80PercentOfTheTime(
            int depthStep, int rangeLow, int rangeHigh)
        {
            var tiers = SampleTiers(EncounterClass.Normal, depthStep, maxTier: 10);

            double inRange = Share(tiers, t => t >= rangeLow && t <= rangeHigh);
            Assert.GreaterOrEqual(inRange, 0.80,
                $"depthStep {depthStep} (floor {depthStep / 8 + 1}) landed in [{rangeLow},{rangeHigh}] only " +
                $"{inRange:P1} of the time");
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
        public void ABoss_NeverDropsBelowTheBossFloor_InAnyBand()
        {
            foreach (int step in new[] { 0, 4, 12, 60 })
            {
                foreach (int tier in SampleTiers(EncounterClass.Boss, step, maxTier: 10))
                {
                    Assert.GreaterOrEqual(tier, 3,
                        $"a boss at step {step} rolled below the boss floor (tier {tier})");
                }
            }
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
            const int Step = 40;   // expected tier 2 (40/16), clear of both clamps

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
            const int Step = 40;   // expected tier 2 (40/16), clear of both clamps
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
            }
        }

        [Test]
        public void MissingRandomness_IsHandledRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => RarityTable.RollTier(EncounterClass.Boss, 10, 10, 0, null));
        }

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

        // ---- the reported bug: a floor-3 boss paying out tier 1 --------------
        //
        // depthStep 23 is the exact step from the report -- floor 3 runs
        // steps 16..23 (RunDepth.FloorFor: floor N starts at
        // (N-1)*RealmMap.SpineLength, SpineLength==8), so 23 is the step
        // before that floor's heart. The bug itself was never in these two rolls -- both
        // already had the numbers to do this (TierFloorFor(Boss)==3;
        // BossStep beats Normal's) -- it was RunOrchestrator.RollOffers
        // never routing a boss kill to EncounterClass.Boss at all (it read
        // IsEliteFight only), so every boss fight rolled as EncounterClass.
        // Normal. See RunOrchestrator.RollOffers' own comment. That seam is
        // Core-side and untestable from Domain; this pins what Domain now
        // guarantees ONCE the caller passes EncounterClass.Boss correctly.
        [Test]
        public void AFloorThreeBoss_PaysAboveTheLowTiers()
        {
            const int Step = 23;
            var tiers = SampleTiers(EncounterClass.Boss, Step, maxTier: 10);

            Assert.GreaterOrEqual(Share(tiers, t => t >= 2), 0.80,
                "a floor-3 boss should clear the common band (tier 1-2) at least 4 times in 5");
            Assert.AreEqual(3, RarityTable.TierFloorFor(EncounterClass.Boss),
                "the absolute boss floor is what actually guarantees the line above");
        }
    }
}
