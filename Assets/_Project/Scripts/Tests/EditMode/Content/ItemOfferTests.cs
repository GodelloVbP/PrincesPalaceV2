using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    public class ItemOfferTests
    {
        // A stand-in for a whole set: one piece at every tier.
        private static List<ItemOffer> Pool(int maxTier = 10, int pieces = 5)
        {
            var pool = new List<ItemOffer>();
            for (int piece = 0; piece < pieces; piece++)
            {
                for (int tier = 0; tier <= maxTier; tier++)
                {
                    pool.Add(new ItemOffer($"piece{piece}_p{tier}", tier));
                }
            }

            return pool;
        }

        // Deterministic "randomness": always the first still in the pool.
        // The selection RULE is what is under test, not the shuffling.
        private static readonly System.Func<int, int> AlwaysFirst = _ => 0;

        // What a depth is worth moved OUT of this table and into
        // RarityTable, which knows what killed you as well as how deep you
        // are. What is left here is the one job the name claims: given a
        // target tier, pick three distinct items near it.
        [Test]
        public void ItOffersExactlyThree()
        {
            var offers = ItemOfferTable.Choose(Pool(), targetTier: 2, maxTier: 10, AlwaysFirst);
            Assert.AreEqual(ItemOfferTable.OfferCount, offers.Count);
        }

        // Three copies of one item is a choice in name only.
        [Test]
        public void TheThreeOffersAreAlwaysDistinct()
        {
            for (int target = 0; target <= 11; target++)
            {
                var offers = ItemOfferTable.Choose(Pool(), target, maxTier: 10, AlwaysFirst);
                CollectionAssert.AllItemsAreUnique(offers.Select(o => o.ItemId).ToList(), $"target {target}");
            }
        }

        // The actual scaling promise: a low target must not hand out
        // late-game gear, and a high one must not hand out starter gear.
        [Test]
        public void ALowTarget_OffersLowTierGear()
        {
            var offers = ItemOfferTable.Choose(Pool(), targetTier: 0, maxTier: 10, AlwaysFirst);

            Assert.IsNotEmpty(offers);
            Assert.LessOrEqual(offers.Max(o => o.Tier), ItemOfferTable.TierSpread,
                "A target of 0 should not be offering upgraded gear");
        }

        [Test]
        public void AHighTarget_OffersHighTierGear()
        {
            var offers = ItemOfferTable.Choose(Pool(), targetTier: 10, maxTier: 10, AlwaysFirst);

            Assert.IsNotEmpty(offers);
            Assert.GreaterOrEqual(offers.Min(o => o.Tier), 10 - ItemOfferTable.TierSpread,
                "A deep target should not be offering starter gear");
        }

        [Test]
        public void EveryOffer_SitsWithinTheSpreadOfItsTarget()
        {
            for (int target = 0; target <= 10; target++)
            {
                foreach (var offer in ItemOfferTable.Choose(Pool(), target, maxTier: 10, AlwaysFirst))
                {
                    Assert.LessOrEqual(System.Math.Abs(offer.Tier - target), ItemOfferTable.TierSpread,
                        $"target {target} offered tier {offer.Tier}");
                }
            }
        }

        [Test]
        public void ANegativeTarget_ReadsAsZeroRatherThanReachingBelowTheLadder()
        {
            var offers = ItemOfferTable.Choose(Pool(), targetTier: -4, maxTier: 10, AlwaysFirst);

            Assert.IsNotEmpty(offers);
            Assert.LessOrEqual(offers.Max(o => o.Tier), ItemOfferTable.TierSpread);
        }

        // A pool too thin at the target depth must still fill three slots by
        // reaching further out, rather than quietly making the screen a
        // one-item non-choice.
        [Test]
        public void AThinPoolAtTheTargetDepth_WidensRatherThanOfferingFewer()
        {
            // One piece only, so the target tier has exactly one candidate.
            var thin = Pool(maxTier: 10, pieces: 1);

            var offers = ItemOfferTable.Choose(thin, targetTier: 4, maxTier: 10, AlwaysFirst);

            Assert.AreEqual(ItemOfferTable.OfferCount, offers.Count);
            CollectionAssert.AllItemsAreUnique(offers.Select(o => o.ItemId).ToList());
        }

        // Fewer candidates than slots is the one case where returning short
        // is correct. It must terminate rather than widening forever.
        [Test]
        public void APoolSmallerThanTheOfferCount_ReturnsWhatItHasAndStops()
        {
            var tiny = new List<ItemOffer> { new ItemOffer("only", 0) };

            var offers = ItemOfferTable.Choose(tiny, targetTier: 0, maxTier: 10, AlwaysFirst);

            Assert.AreEqual(1, offers.Count);
        }

        [Test]
        public void AnEmptyPool_IsHandledRatherThanThrowing()
        {
            Assert.IsEmpty(ItemOfferTable.Choose(new List<ItemOffer>(), 0, 10, AlwaysFirst));
            Assert.IsEmpty(ItemOfferTable.Choose(null, 0, 10, AlwaysFirst));
        }

        [Test]
        public void MissingRandomness_IsHandledRatherThanThrowing()
        {
            Assert.IsEmpty(ItemOfferTable.Choose(Pool(), 0, 10, null));
        }

        // Randomness that misbehaves must not index out of the pool.
        [Test]
        public void AnOutOfRangeRoll_IsClampedRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ItemOfferTable.Choose(Pool(), 2, 10, _ => 9999));
            Assert.DoesNotThrow(() => ItemOfferTable.Choose(Pool(), 2, 10, _ => -5));
        }

        // The offer carries the copy's plus alongside its tier, so what the
        // card shows and what the bag receives are the same object.
        [Test]
        public void AnOfferCarriesItsPlusSeparatelyFromItsTier()
        {
            var offer = new ItemOffer("leather_coif_p4", 4).WithPlus(3);

            Assert.AreEqual("leather_coif_p4", offer.ItemId);
            Assert.AreEqual(4, offer.Tier);
            Assert.AreEqual(3, offer.Plus);
            Assert.AreEqual(0, new ItemOffer("leather_coif_p4", 4).Plus, "An offer is unhoned unless said otherwise");
        }

        // A fresh offer is Ordinary with no modifiers -- the same "unrolled
        // by default" posture Plus already has.
        [Test]
        public void AFreshOfferIsOrdinaryWithNoModifiers()
        {
            var offer = new ItemOffer("leather_coif_p4", 4);

            Assert.AreEqual(RiftTier.Ordinary, offer.RiftTier);
            Assert.IsNotNull(offer.Modifiers, "absent modifiers should read as an empty list, never null");
            Assert.IsEmpty(offer.Modifiers);
        }

        // WithPlus and WithModifiers each move ONE axis. Chaining them must
        // not let the second call clobber the first -- that was the actual
        // bug shape this pins against (an early WithPlus implementation that
        // rebuilt the struct from only ItemId/Tier would have silently
        // dropped whatever WithModifiers had just set).
        [Test]
        public void WithPlusAndWithModifiersComposeWithoutClobberingEachOther()
        {
            var offer = new ItemOffer("leather_coif_p4", 4)
                .WithModifiers(RiftTier.Convergent, new List<string> { "fiery_test", "swift_test" })
                .WithPlus(3);

            Assert.AreEqual(3, offer.Plus, "WithPlus after WithModifiers must not lose the plus");
            Assert.AreEqual(RiftTier.Convergent, offer.RiftTier, "WithPlus must not reset the rift tier");
            CollectionAssert.AreEqual(new[] { "fiery_test", "swift_test" }, offer.Modifiers);

            var reordered = new ItemOffer("leather_coif_p4", 4)
                .WithPlus(3)
                .WithModifiers(RiftTier.Convergent, new List<string> { "fiery_test", "swift_test" });

            Assert.AreEqual(3, reordered.Plus, "WithModifiers after WithPlus must not lose the plus");
            Assert.AreEqual(RiftTier.Convergent, reordered.RiftTier);
        }
    }

    public class CharacterRewardTests
    {
        private static CharacterReward Reward(int levelBefore, int expBefore, int levelAfter, int expAfter,
            int expToNextAfter, int gained)
        {
            return new CharacterReward("sheep", "Sheep", levelBefore, expBefore, levelAfter, expAfter, expToNextAfter, gained);
        }

        [Test]
        public void TheBarFillsToTheFractionOfTheNextLevel()
        {
            Assert.AreEqual(0.25f, Reward(1, 0, 1, 25, 100, 25).BarFill01(), 0.0001f);
            Assert.AreEqual(0f, Reward(1, 0, 1, 0, 100, 0).BarFill01(), 0.0001f);
        }

        [Test]
        public void TheBarNeverOverflowsOrGoesNegative()
        {
            Assert.AreEqual(1f, Reward(1, 0, 1, 500, 100, 500).BarFill01(), 0.0001f);
            Assert.AreEqual(0f, Reward(1, 0, 1, -20, 100, 0).BarFill01(), 0.0001f);
        }

        // A broken level curve should not take the rewards screen down with
        // it -- dividing by a zero requirement would.
        [Test]
        public void ANonPositiveRequirement_ReadsAsFullRatherThanDividingByZero()
        {
            Assert.AreEqual(1f, Reward(1, 0, 1, 10, 0, 10).BarFill01(), 0.0001f);
            Assert.AreEqual(1f, Reward(1, 0, 1, 10, -5, 10).BarFill01(), 0.0001f);
        }

        [Test]
        public void LevellingUp_IsReportedFromTheBeforeAndAfterLevels()
        {
            Assert.IsTrue(Reward(1, 90, 2, 10, 200, 120).LevelledUp);
            Assert.IsFalse(Reward(3, 10, 3, 60, 300, 50).LevelledUp);
        }

        // The two-tone bar: resting colour up to where the fight STARTED,
        // bright from there to where it ended. That gap is the whole point --
        // it shows what this fight was worth, not just where the character
        // ended up.
        [Test]
        public void TheBarRemembersWhereTheFightStarted()
        {
            var reward = Reward(levelBefore: 3, expBefore: 40, levelAfter: 3, expAfter: 90, expToNextAfter: 100, gained: 50);

            Assert.AreEqual(0.4f, reward.BarFillBefore01(), 0.0001f);
            Assert.AreEqual(0.9f, reward.BarFill01(), 0.0001f);
        }

        // After a level-up the bar reset, so everything showing really was
        // earned this fight. Carrying the old level's progress across would
        // draw a segment that does not exist on the new level's scale.
        [Test]
        public void AfterALevelUp_TheWholeBarReadsAsThisFightsGain()
        {
            var reward = Reward(levelBefore: 1, expBefore: 90, levelAfter: 2, expAfter: 30, expToNextAfter: 200, gained: 140);

            Assert.AreEqual(0f, reward.BarFillBefore01(), 0.0001f);
            Assert.AreEqual(0.15f, reward.BarFill01(), 0.0001f);
        }

        // The bright segment is drawn between the two, so the "before" can
        // never sit past the "after" or it would draw backwards.
        [Test]
        public void TheStartOfTheBarIsNeverPastItsEnd()
        {
            foreach (var reward in new[]
            {
                Reward(3, 40, 3, 90, 100, 50),
                Reward(1, 90, 2, 30, 200, 140),
                Reward(2, 0, 2, 0, 100, 0),
                Reward(2, 500, 2, 900, 100, 400),
            })
            {
                Assert.LessOrEqual(reward.BarFillBefore01(), reward.BarFill01());
            }
        }

        [Test]
        public void ABrokenLevelCurve_DoesNotDivideByZeroOnEitherSegment()
        {
            var reward = Reward(2, 10, 2, 40, 0, 30);

            Assert.AreEqual(1f, reward.BarFill01(), 0.0001f);
            Assert.AreEqual(1f, reward.BarFillBefore01(), 0.0001f);
        }

        // ---- the level-up sweep ------------------------------------------------

        [Test]
        public void SweepStartIsWhereTheBarActuallyWasBeforeTheFight()
        {
            // BarFillBefore01 answers 0 after a level-up, deliberately -- the
            // level reset the track. That is right for the resting display and
            // wrong as the first frame of a sweep, which has to start where the
            // player left the bar.
            var levelled = new CharacterReward("a", "A",
                levelBefore: 1, expBefore: 60,
                levelAfter: 2, expAfter: 35,
                expToNextAfter: 200, expGained: 175,
                expToNextBefore: 100);

            Assert.AreEqual(0f, levelled.BarFillBefore01(), 0.001f, "the resting display still resets");
            Assert.AreEqual(0.6f, levelled.SweepStart01(), 0.001f, "60 of 100 on the START level");
        }

        [Test]
        public void WithoutALevelUpTheSweepStartsWhereTheRestingBarDoes()
        {
            var flat = new CharacterReward("a", "A",
                levelBefore: 2, expBefore: 40,
                levelAfter: 2, expAfter: 90,
                expToNextAfter: 200, expGained: 50,
                expToNextBefore: 200);

            Assert.AreEqual(flat.BarFillBefore01(), flat.SweepStart01(), 0.001f);
        }

        [Test]
        public void LevelsGainedCountsTheCrossingsTheSweepHasToPlay()
        {
            var one = new CharacterReward("a", "A", 1, 0, 2, 0, 200, 100, expToNextBefore: 100);
            var three = new CharacterReward("a", "A", 1, 0, 4, 0, 400, 900, expToNextBefore: 100);
            var none = new CharacterReward("a", "A", 2, 0, 2, 50, 200, 50, expToNextBefore: 200);

            Assert.AreEqual(1, one.LevelsGained);
            Assert.AreEqual(3, three.LevelsGained);
            Assert.AreEqual(0, none.LevelsGained);
        }

        [Test]
        public void AMissingStartRequirementDegradesToZeroRatherThanDividingByIt()
        {
            // A reward built by an older caller has no expToNextBefore. The
            // sweep then starts empty, which is merely less pretty -- not a
            // division by zero on the results screen.
            var legacy = new CharacterReward("a", "A", 1, 60, 2, 35, 200, 175);

            Assert.AreEqual(0f, legacy.SweepStart01(), 0.001f);
        }

        [Test]
        public void TheSweepStartIsClampedForASaveWithMoreExpThanItsLevelNeeded()
        {
            // Reachable through a content change to the level curve: a save
            // written under a cheaper curve can hold more exp than its level
            // now requires. A bar filling past its own track would draw
            // outside the frame.
            var overfull = new CharacterReward("a", "A",
                levelBefore: 1, expBefore: 500,
                levelAfter: 2, expAfter: 0,
                expToNextAfter: 200, expGained: 100,
                expToNextBefore: 100);

            Assert.AreEqual(1f, overfull.SweepStart01(), 0.001f);
        }
    }
}
