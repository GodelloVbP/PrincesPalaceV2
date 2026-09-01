using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // THE ARCHETYPE'S HALF OF THE GEAR DECISION.
    //
    // Core measures what an item would do; Domain decides whether that is
    // worth wanting. This file exercises only the second half, which is the
    // half that can be tested without content, a save or an engine -- and the
    // half where "aggressive prefers damage" either is true or is a comment.
    public class BotGearWeightsTests
    {
        private static SeededRandom Rng() => new SeededRandom(12345);

        // Deltas shaped so the two archetypes MUST disagree: one is pure
        // offence, the other pure survivability, and neither carries a
        // sweetener that would let a single ranking satisfy both.
        private static StatDeltas Sword => new StatDeltas(offence: 30, health: 0, defence: 0, speed: 0, manaRegen: 0);
        private static StatDeltas Plate => new StatDeltas(offence: 0, health: 60, defence: 20, speed: 0, manaRegen: 0);

        [Test]
        public void AggressiveTakesTheSwordAndDefensiveTakesThePlate()
        {
            var options = new List<StatOption>
            {
                new StatOption(0, "SWORD", Sword),
                new StatOption(1, "PLATE", Plate),
            };

            var aggressive = new GreedyAggressivePolicy();
            var defensive = new GreedyDefensivePolicy();

            Assert.AreEqual(0, aggressive.ChooseStat(options, default, Rng()),
                "an offence-heavy archetype ranked the plate above the sword");
            Assert.AreEqual(1, defensive.ChooseStat(options, default, Rng()),
                "a defence-heavy archetype ranked the sword above the plate");
        }

        // THE WHOLE POINT OF THE ARCHETYPE GAP, in one assertion: if the two
        // greedy policies happened to share a weight vector, every gear pick,
        // every level-up and every talent in the batch would be identical
        // between them and the gap would be measuring only the fight brain.
        [Test]
        public void TheTwoGreedyArchetypesDoNotShareAWeightVector()
        {
            var aggressive = new GreedyAggressivePolicy().Gear;
            var defensive = new GreedyDefensivePolicy().Gear;

            Assert.Greater(aggressive.Score(Sword), aggressive.Score(Plate));
            Assert.Greater(defensive.Score(Plate), defensive.Score(Sword));
        }

        // Lookahead2's edge is supposed to be its FIGHT lookahead and nothing
        // else. A different out-of-fight brain would contaminate the one
        // comparison it exists to make, so it forwards GreedyAggressive's.
        [Test]
        public void Lookahead2SharesGreedyAggressivesOutOfFightPreferences()
        {
            var lookahead = new Lookahead2Policy().Gear;
            var aggressive = new GreedyAggressivePolicy().Gear;

            Assert.AreEqual(aggressive.Score(Sword), lookahead.Score(Sword));
            Assert.AreEqual(aggressive.Score(Plate), lookahead.Score(Plate));
        }

        // RandomLegal must RANK NOTHING, which is not the same as caring about
        // everything equally -- an all-ones vector still puts a +60 health
        // plate above a +30 damage sword, and the fuzzer is the floor the whole
        // report is measured against. Its indifference is what makes Core's
        // evaluator fall through to a uniform draw.
        [Test]
        public void RandomLegalRanksNothingSoEveryLegalCandidateTies()
        {
            var weights = new RandomLegalPolicy().Gear;

            Assert.IsTrue(weights.RanksNothing);
            Assert.AreEqual(0f, weights.Score(Sword));
            Assert.AreEqual(0f, weights.Score(Plate));
            Assert.AreEqual(weights.Score(Sword), weights.Score(Plate));
        }

        [Test]
        public void AWeightVectorThatRanksAnythingIsNotReportedAsIndifferent()
        {
            Assert.IsFalse(GearWeights.Uniform.RanksNothing,
                "Uniform ranks by magnitude and must not be mistaken for having no opinion");
            Assert.IsFalse(new GreedyAggressivePolicy().Gear.RanksNothing);
            Assert.IsFalse(new GreedyDefensivePolicy().Gear.RanksNothing);
        }

        // The scores arrive on the view because ItemOffer cannot carry them --
        // it names an id, a tier and a plus, and nothing about what wearing it
        // would do. When they are there they must WIN over tier/plus, or the
        // whole evaluator is decoration.
        [Test]
        public void ScoredOffersOutrankTierAndPlus()
        {
            var offers = new List<Rewards.ItemOffer>
            {
                // The biggest number on the table, and worthless: tier 5,
                // scored at nothing.
                new Rewards.ItemOffer("gilded_helm", tier: 5, plus: 3),
                new Rewards.ItemOffer("plain_sword", tier: 1, plus: 0),
            };

            var view = new RunView(1f, 0, 1, 0, null).WithOfferScores(new[] { 0f, 250f });

            Assert.AreEqual(1, new GreedyAggressivePolicy().ChooseOffer(offers, view, Rng()));
            Assert.AreEqual(1, new GreedyDefensivePolicy().ChooseOffer(offers, view, Rng()));
        }

        // The fallback is not dead code. RunView.OfferScores is empty whenever
        // the driver had no live save to score against, and a length mismatch
        // would mean the two lists had drifted -- both have to land back on the
        // tier/plus read this method used to be outright.
        [Test]
        public void AnUnscoredOfferFallsBackToTierThenPlus()
        {
            var offers = new List<Rewards.ItemOffer>
            {
                new Rewards.ItemOffer("plain_sword", tier: 1, plus: 0),
                new Rewards.ItemOffer("gilded_helm", tier: 5, plus: 3),
            };

            var noScores = new RunView(1f, 0, 1, 0, null);
            Assert.AreEqual(1, new GreedyAggressivePolicy().ChooseOffer(offers, noScores, Rng()));

            var wrongLength = noScores.WithOfferScores(new[] { 99f });
            Assert.AreEqual(1, new GreedyAggressivePolicy().ChooseOffer(offers, wrongLength, Rng()),
                "a scores list that does not match the offers must be ignored, not indexed into");
        }

        [Test]
        public void NothingToChooseFromComesBackAsMinusOneRatherThanThrowing()
        {
            var empty = new List<TalentOption>();
            var noStats = new List<StatOption>();

            foreach (var name in Archetypes.Names)
            {
                var policy = (IRunPolicy)Archetypes.Create(name);
                Assert.AreEqual(-1, policy.ChooseTalent(empty, default, Rng()), name);
                Assert.AreEqual(-1, policy.ChooseStat(noStats, default, Rng()), name);
            }
        }

        // A talent whose whole effect is a combat rule change derives nothing
        // measurable, so every option on the frontier can legitimately score
        // zero. Declining there would leave the ember unspent forever -- and an
        // unspent ember buys nothing, while the orb taken now is what opens the
        // one above it.
        [Test]
        public void AnAllZeroTalentFrontierIsStillBoughtFrom()
        {
            var options = new List<TalentOption>
            {
                new TalentOption("a", "A", 1, StatDeltas.Zero),
                new TalentOption("b", "B", 1, StatDeltas.Zero),
            };

            foreach (var name in Archetypes.Names)
            {
                var policy = (IRunPolicy)Archetypes.Create(name);
                int index = policy.ChooseTalent(options, default, Rng());
                Assert.That(index, Is.InRange(0, 1), $"{name} declined an affordable orb it could reach");
            }
        }
    }
}
