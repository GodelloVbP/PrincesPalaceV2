using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    public class RewardTrackTests
    {
        // ---- the milestones -----------------------------------------------------
        //
        // Pinned by level, because a milestone is a promise about a NUMBER the
        // player is counting toward. Moving one is a design change and should
        // have to be typed twice.
        [TestCase(10, TrackReward.Favor, 5)]
        [TestCase(20, TrackReward.Respec, 0)]
        [TestCase(25, TrackReward.RelicSlot, 2)]
        [TestCase(30, TrackReward.RestBeforeBoss, 0)]
        [TestCase(40, TrackReward.OfferReroll, 1)]
        [TestCase(45, TrackReward.RelicSlot, 3)]
        [TestCase(50, TrackReward.WiderOffer, 4)]
        [TestCase(60, TrackReward.TwoStartingRelics, 2)]
        [TestCase(70, TrackReward.ChosenStartingRelics, 0)]
        [TestCase(80, TrackReward.EliteRelicDrop, 0)]
        [TestCase(90, TrackReward.SecondLife, 1)]
        [TestCase(100, TrackReward.SecondLifeRefresh, 0)]
        public void AMilestoneLandsOnItsLevel(int level, TrackReward reward, int amount)
        {
            var entry = RewardTrack.At(level);

            Assert.AreEqual(reward, entry.Reward, $"level {level} does not hold the milestone it should");
            Assert.AreEqual(amount, entry.Amount, $"level {level}'s milestone has the wrong amount");
        }

        // Level 1 is where a character STARTS. Paying it would hand a reward to
        // a brand-new character for having been created.
        [Test]
        public void TheTrackPaysNothingForLevelOne()
        {
            Assert.IsFalse(RewardTrack.At(1).IsSomething);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(101)]
        [TestCase(int.MaxValue)]
        public void LevelsOffTheTrackPayNothingRatherThanThrowing(int level)
        {
            Assert.IsFalse(RewardTrack.At(level).IsSomething);
        }

        // ---- the filler mix -----------------------------------------------------
        //
        // The mix is the design statement ("thirty stat points across the
        // track") and the placement is arithmetic, so what is asserted here is
        // the COUNT, not where any individual one landed. A test that pinned
        // placements would fail on every retune and tell you nothing.
        [TestCase(TrackReward.StatPoint, 30)]
        [TestCase(TrackReward.Favor, 20 + 1)] // 20 filler nodes plus the level-10 milestone
        public void TheTrackHandsOutTheMixItDescribes(TrackReward reward, int expectedNodes)
        {
            int nodes = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).Reward == reward) nodes++;
            }

            Assert.AreEqual(expectedNodes, nodes, $"the track holds the wrong number of {reward} nodes");
        }

        // 20 nodes at 2 each, plus 5 at the level-10 milestone.
        [Test]
        public void AFullTrackIsWorthFortyFiveFavor()
        {
            Assert.AreEqual(45, RewardTrack.GrantedBetween(TrackReward.Favor, 1, RewardTrack.MaxLevel));
        }

        // THE REASON Spread() exists rather than filling from level 2 upward.
        //
        // There are more filler levels (87) than rewards to put in them (50)
        // until the remaining reward kinds are built, so a naive fill would
        // pack everything into levels 2-58 and leave the back half of the track
        // -- the half a player grinds hardest for -- completely empty.
        [Test]
        public void RewardsReachTheBackHalfOfTheTrack()
        {
            int inTheBackHalf = 0;
            for (int level = 51; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).IsSomething) inTheBackHalf++;
            }

            Assert.Greater(inTheBackHalf, 20,
                "the back half of the track is nearly empty, so the filler is packed into the front");
        }

        // Interleaved, not clumped: the mix should not hand out all thirty stat
        // points before the first Favor node.
        [Test]
        public void TheFillerKindsInterleaveRatherThanClumping()
        {
            int firstFavorFiller = 0;
            for (int level = 11; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).Reward == TrackReward.Favor)
                {
                    firstFavorFiller = level;
                    break;
                }
            }

            Assert.Greater(firstFavorFiller, 0, "no Favor filler node was placed at all");
            Assert.Less(firstFavorFiller, 30,
                "the first Favor filler node arrives after level 30, so the kinds are clumped");
        }

        // ---- grants vs unlocks --------------------------------------------------

        [TestCase(TrackReward.StatPoint, true)]
        [TestCase(TrackReward.Favor, true)]
        [TestCase(TrackReward.MaxHealth, true)]
        [TestCase(TrackReward.Respec, false)]
        [TestCase(TrackReward.WiderOffer, false)]
        [TestCase(TrackReward.SecondLife, false)]
        [TestCase(TrackReward.None, false)]
        public void QuantitiesAreGrantsAndCapabilitiesAreNot(TrackReward reward, bool isGrant)
        {
            Assert.AreEqual(isGrant, RewardTrack.IsGrant(reward));
        }

        [Test]
        public void NoneIsNeitherAGrantNorAnUnlock()
        {
            Assert.IsFalse(RewardTrack.IsGrant(TrackReward.None));
            Assert.IsFalse(RewardTrack.IsUnlock(TrackReward.None));
        }

        // A watermark is half-open at the bottom: everything up to and
        // including `afterLevel` has already been paid.
        [Test]
        public void AClaimedLevelIsNotPaidTwice()
        {
            int all = RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, 40);
            int firstHalf = RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, 20);
            int secondHalf = RewardTrack.GrantedBetween(TrackReward.StatPoint, 20, 40);

            Assert.AreEqual(all, firstHalf + secondHalf,
                "claiming in two steps paid a different total than claiming in one");
        }

        [Test]
        public void AWatermarkPastTheLevelIsOwedNothing()
        {
            Assert.AreEqual(0, RewardTrack.GrantedBetween(TrackReward.StatPoint, 50, 20));
        }

        [Test]
        public void AnUnlockIsNeverPaidAsAGrant()
        {
            Assert.AreEqual(0, RewardTrack.GrantedBetween(TrackReward.WiderOffer, 1, RewardTrack.MaxLevel),
                "an unlock was counted as a claimable quantity");
        }

        // ---- unlocks are pure functions of level --------------------------------

        [TestCase(TrackReward.Respec, 19, false)]
        [TestCase(TrackReward.Respec, 20, true)]
        [TestCase(TrackReward.Respec, 99, true)]
        [TestCase(TrackReward.WiderOffer, 49, false)]
        [TestCase(TrackReward.WiderOffer, 50, true)]
        [TestCase(TrackReward.SecondLifeRefresh, 99, false)]
        [TestCase(TrackReward.SecondLifeRefresh, 100, true)]
        public void ACapabilityTurnsOnAtItsLevelAndStaysOn(TrackReward reward, int level, bool expected)
        {
            Assert.AreEqual(expected, RewardTrack.HasUnlocked(reward, level));
        }

        // Relic slots are the one unlock that arrives in steps, so the answer
        // is "the highest reached", not "the first".
        [TestCase(1, 1)]
        [TestCase(24, 1)]
        [TestCase(25, 2)]
        [TestCase(44, 2)]
        [TestCase(45, 3)]
        [TestCase(100, 3)]
        public void RelicSlotsClimbAndDoNotGoBack(int level, int expectedSlots)
        {
            Assert.AreEqual(expectedSlots, RewardTrack.UnlockedAmount(TrackReward.RelicSlot, level, 1));
        }

        // Level 60 hands out two starting relics and cannot mean anything until
        // there are two slots to put them in. Asserted as an ORDERING rather
        // than as two levels, so moving either one keeps the constraint.
        [Test]
        public void TheSecondRelicSlotArrivesBeforeTheSecondStartingRelic()
        {
            int slotTwo = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                var entry = RewardTrack.At(level);
                if (entry.Reward == TrackReward.RelicSlot && entry.Amount >= 2)
                {
                    slotTwo = level;
                    break;
                }
            }

            Assert.Greater(slotTwo, 0, "no second relic slot is granted at all");
            Assert.Less(slotTwo, RewardTrack.UnlockLevel(TrackReward.TwoStartingRelics),
                "the track starts every run with two relics before granting a second slot to hold one");
        }

        [Test]
        public void ASecondLifeExistsBeforeItCanRefresh()
        {
            Assert.Less(RewardTrack.UnlockLevel(TrackReward.SecondLife),
                RewardTrack.UnlockLevel(TrackReward.SecondLifeRefresh),
                "the track refreshes a second life before granting one");
        }

        [Test]
        public void ARewardTheTrackNeverGrantsHasNoUnlockLevel()
        {
            Assert.AreEqual(0, RewardTrack.UnlockLevel(TrackReward.MaxHealth),
                "MaxHealth is not placed yet, so it should report no unlock level");
        }
    }
}
