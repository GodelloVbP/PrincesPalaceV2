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
        //
        // NINE OF THE TWELVE ARE MaxHealth 15 -- the interim table's stand-in
        // for the seven reward kinds this package removed (P1 of
        // docs/PLAN_REWARD_TRACKS.md). The three that are not are the spine:
        // Respec at 20, StatPoint at 80, SecondLife at 90.
        [TestCase(10, TrackReward.MaxHealth, 15)]
        [TestCase(20, TrackReward.Respec, 0)]
        [TestCase(25, TrackReward.MaxHealth, 15)]
        [TestCase(30, TrackReward.MaxHealth, 15)]
        [TestCase(40, TrackReward.MaxHealth, 15)]
        [TestCase(45, TrackReward.MaxHealth, 15)]
        [TestCase(50, TrackReward.MaxHealth, 15)]
        [TestCase(60, TrackReward.MaxHealth, 15)]
        [TestCase(70, TrackReward.MaxHealth, 15)]
        [TestCase(80, TrackReward.StatPoint, 10)]
        [TestCase(90, TrackReward.SecondLife, 1)]
        [TestCase(100, TrackReward.MaxHealth, 15)]
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

        // ---- grants vs unlocks --------------------------------------------------

        [TestCase(TrackReward.StatPoint, true)]
        [TestCase(TrackReward.MaxHealth, true)]
        [TestCase(TrackReward.Respec, false)]
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
            Assert.AreEqual(0, RewardTrack.GrantedBetween(TrackReward.Respec, 1, RewardTrack.MaxLevel),
                "an unlock was counted as a claimable quantity");
        }

        // ---- unlocks are pure functions of level --------------------------------

        [TestCase(TrackReward.Respec, 19, false)]
        [TestCase(TrackReward.Respec, 20, true)]
        [TestCase(TrackReward.Respec, 99, true)]
        [TestCase(TrackReward.SecondLife, 89, false)]
        [TestCase(TrackReward.SecondLife, 90, true)]
        public void ACapabilityTurnsOnAtItsLevelAndStaysOn(TrackReward reward, int level, bool expected)
        {
            Assert.AreEqual(expected, RewardTrack.HasUnlocked(reward, level));
        }

        // Level 80 grants ten stat points. It USED TO be sized to exactly one
        // AbilityDerivation.CharacterBand -- the edge past which a point
        // stopped paying a flat rate and started paying the square of the
        // excess, making the tenth point the last "cheap" one. Phase 2 of the
        // balance redesign (D2) deleted that piecewise curve: every derivation
        // is a straight line now, so there is no band edge left for this
        // milestone to be exactly sized to. See RewardTrack.cs's own comment
        // at this milestone for the fuller account.
        //
        // What is left to assert is simpler and still true: every point,
        // inside the old band or past it, is worth exactly the same amount --
        // a flat line has no cliff, by construction. Pinned as a literal
        // (gotcha 5) rather than computed, so a future formula change has to
        // touch this number on purpose.
        [Test]
        public void TheLevelEightyGrantIsTenPoints_AndEveryPointIsWorthTheSameFlatAmount()
        {
            Assert.AreEqual(TrackReward.StatPoint, RewardTrack.At(80).Reward);
            Assert.AreEqual(10, RewardTrack.At(80).Amount);

            int firstStep = HealthAt(1) - HealthAt(0);
            int tenthStep = HealthAt(10) - HealthAt(9);
            int eleventhStep = HealthAt(11) - HealthAt(10);

            Assert.AreEqual(20, firstStep, "Constitution's flat rate is +20 health a point");
            Assert.AreEqual(firstStep, tenthStep,
                "the 10th point is worth a different amount than the first -- the curve is no longer flat");
            Assert.AreEqual(firstStep, eleventhStep,
                "the 11th point is worth a different amount than the first -- a band edge has crept back in");
        }

        private static int HealthAt(int investedConstitution) =>
            Stats.AbilityDerivation.MaxHealthBonus(
                new Stats.AbilityScoreBlock(10, 10, 10 + investedConstitution, 10, 10, 10));

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

        // EVERY REWARD KIND THE ENUM KNOWS ABOUT IS ACTUALLY GRANTED.
        //
        // This replaces a test that asserted the opposite about
        // SignatureAtFightStart -- that it was deliberately unplaced -- which
        // was true while a reward kind existed with no way to pay it out. The
        // author cut it rather than building it, so the enum no longer carries
        // anything the track never grants, and the invariant worth holding is
        // the strong one: a kind nobody can earn is dead weight, and the next
        // one added has to be placed or this fails.
        [Test]
        public void EveryRewardKindTheEnumKnowsAboutIsActuallyGranted()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                Assert.Greater(RewardTrack.UnlockLevel(reward), 0,
                    $"{reward} exists as a reward kind but no level of the track ever grants it");
            }
        }

        // ---- the max-health nodes -------------------------------------------------

        // A quantity, so it is claimed against the watermark exactly once --
        // the same rule stat points follow.
        [Test]
        public void MaxHealthIsAGrantRatherThanACapability()
        {
            Assert.IsTrue(RewardTrack.IsGrant(TrackReward.MaxHealth));
            Assert.IsFalse(RewardTrack.IsUnlock(TrackReward.MaxHealth));
        }

        // EVERY LEVEL PAYS. The filler counts sum to exactly the number of
        // filler levels, so there is no level between 2 and 100 that hands over
        // nothing -- which is the whole reason the track can be walked without
        // a stretch of it feeling broken.
        [Test]
        public void NoLevelOfTheTrackPaysNothing()
        {
            for (int level = 2; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.IsTrue(RewardTrack.At(level).IsSomething, $"level {level} pays nothing");
            }
        }

        // 50 stat points across the whole track: 40 filler plus level 80's ten.
        //
        // Under the 60 that would be six scores' worth of ten points each --
        // ten no longer names a formula band (AbilityDerivation.CharacterBand
        // is gone, Phase 2/D2), but it is still the natural per-score unit
        // this milestone grants, and staying under six of them is the right
        // side of that line: a track generous enough to max every score would
        // leave a fully-levelled character with no decision left about where
        // the last points go.
        [Test]
        public void AFullTrackCannotFillEveryAbilityBand()
        {
            int points = RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel);

            Assert.AreEqual(50, points);
            Assert.Less(points, Stats.AbilityScores.All.Length * 10,
                "the track grants enough points to max every band, so spending them is no longer a choice");
        }

        // THE INTERIM TABLE, PINNED AS THE TWO TOTALS IT PROMISES.
        //
        // P1 of docs/PLAN_REWARD_TRACKS.md retires eight reward kinds and fills
        // their milestones with MaxHealth so the track still pays every level.
        // The interim table is deliberately the eventual generated default
        // (P4's RewardTrackDefinition.Default), so these two literals are the
        // number that must not drift: 40 filler stat points + level 80's ten =
        // 50; 47 filler MaxHealth at 2 each + 9 milestones at 15 each = 229.
        [Test]
        public void TheTrackStillPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth()
        {
            Assert.AreEqual(50, RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel),
                "40 filler singles plus level 80's ten");
            Assert.AreEqual(229, RewardTrack.GrantedBetween(TrackReward.MaxHealth, 1, RewardTrack.MaxLevel),
                "9 milestone nodes at 15 plus 47 filler nodes at 2");
        }
    }
}
