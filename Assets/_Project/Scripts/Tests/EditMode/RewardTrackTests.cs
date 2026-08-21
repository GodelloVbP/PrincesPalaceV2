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
        [TestCase(25, TrackReward.StartingRelics, 2)]
        [TestCase(30, TrackReward.RestBeforeBoss, 0)]
        [TestCase(40, TrackReward.OfferReroll, 1)]
        [TestCase(45, TrackReward.StartingRelics, 3)]
        [TestCase(50, TrackReward.WiderOffer, 4)]
        [TestCase(60, TrackReward.StartingRelics, 4)]
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

        // Starting relics are the one unlock that arrives in steps, so the
        // answer is "the highest reached", not "the first". A character below
        // the first step still drafts ONE -- the fallback is part of the
        // answer, and getting it wrong takes the draft away entirely.
        [TestCase(1, 1)]
        [TestCase(24, 1)]
        [TestCase(25, 2)]
        [TestCase(44, 2)]
        [TestCase(45, 3)]
        [TestCase(59, 3)]
        [TestCase(60, 4)]
        [TestCase(100, 4)]
        public void StartingRelicsClimbAndDoNotGoBack(int level, int expected)
        {
            Assert.AreEqual(expected, RewardTrack.StartingRelics(level));
        }

        // THE ONE REAL CHAIN on the track: more relics, then the right to pick
        // them. Asserted as an ORDERING rather than as fixed levels, so
        // retuning any of the four keeps the constraint that makes it a chain
        // rather than four unrelated grants.
        [Test]
        public void TheRelicLineClimbsBeforeItLetsYouChoose()
        {
            int previousAmount = 0;
            int lastStep = 0;

            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                var entry = RewardTrack.At(level);
                if (entry.Reward != TrackReward.StartingRelics) continue;

                Assert.Greater(entry.Amount, previousAmount,
                    $"the starting-relic step at level {level} does not increase on the one before it");
                previousAmount = entry.Amount;
                lastStep = level;
            }

            Assert.Greater(lastStep, 0, "the track never grants a starting relic step");
            Assert.Greater(previousAmount, RewardTrack.BaseStartingRelics,
                "every starting-relic step grants what a level-1 character already has");
            Assert.Less(lastStep, RewardTrack.UnlockLevel(TrackReward.ChosenStartingRelics),
                "the track lets you choose your starting relics before it finishes handing them out");
        }

        [Test]
        public void ASecondLifeExistsBeforeItCanRefresh()
        {
            Assert.Less(RewardTrack.UnlockLevel(TrackReward.SecondLife),
                RewardTrack.UnlockLevel(TrackReward.SecondLifeRefresh),
                "the track refreshes a second life before granting one");
        }

        // ---- what the player is told --------------------------------------------

        // EVERY reward kind has a name, checked by walking the enum rather than
        // by listing them. The failure this catches is the one that cannot be
        // seen in a diff: a new TrackReward compiles, pays out correctly, and
        // displays as an empty string.
        [Test]
        public void EveryRewardKindHasSomethingToCallItself()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                Assert.IsNotEmpty(RewardTrackNames.Of(reward, 1),
                    $"{reward} has no display name, so it would show as a blank line");
            }
        }

        [Test]
        public void NothingIsCalledAnythingAtAll()
        {
            Assert.IsEmpty(RewardTrackNames.Of(TrackReward.None, 0));
        }

        // Singular and plural are different sentences. "1 STAT POINTS" is the
        // kind of thing that ships.
        [Test]
        public void CountsReadAsEnglish()
        {
            Assert.AreEqual("A STAT POINT", RewardTrackNames.Of(TrackReward.StatPoint, 1));
            Assert.AreEqual("3 STAT POINTS", RewardTrackNames.Of(TrackReward.StatPoint, 3));
            Assert.AreEqual("AN OFFER REROLL", RewardTrackNames.Of(TrackReward.OfferReroll, 1));
        }

        // "What do I get next" is a question about the next REWARD, not the
        // next level -- the filler is not yet dense enough to answer with a
        // level number and have it mean anything.
        [Test]
        public void TheNextRewardSkipsLevelsThatPayNothing()
        {
            int next = RewardTrack.NextRewardLevel(1);

            Assert.Greater(next, 1);
            Assert.IsTrue(RewardTrack.At(next).IsSomething,
                "the next reward level does not actually hold a reward");

            for (int level = 2; level < next; level++)
            {
                Assert.IsFalse(RewardTrack.At(level).IsSomething,
                    $"level {level} pays something but was skipped over");
            }
        }

        [Test]
        public void EveryRewardLevelIsItsOwnNextFromTheOneBefore()
        {
            // Walking the track by NextRewardLevel has to visit every reward.
            // Off-by-one here would hide a milestone from the player entirely.
            int visited = 0;
            for (int level = RewardTrack.NextRewardLevel(0); level > 0; level = RewardTrack.NextRewardLevel(level))
            {
                visited++;
            }

            int actual = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).IsSomething) actual++;
            }

            Assert.AreEqual(actual, visited, "walking the track skipped or repeated a reward");
        }

        [Test]
        public void TheEndOfTheTrackHasNoNext()
        {
            Assert.AreEqual(0, RewardTrack.NextRewardLevel(RewardTrack.MaxLevel));
            Assert.AreEqual(0, RewardTrack.NextRewardLevel(RewardTrack.MaxLevel + 50));
        }

        [Test]
        public void ARewardTheTrackNeverGrantsHasNoUnlockLevel()
        {
            Assert.AreEqual(0, RewardTrack.UnlockLevel(TrackReward.MaxHealth),
                "MaxHealth is not placed yet, so it should report no unlock level");
        }
    }
}
