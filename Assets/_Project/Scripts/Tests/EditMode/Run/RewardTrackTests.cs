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

        // STATPOINT IS THE ONLY GRANT NOW (docs/PLAN_REWARD_TRACKS.md §2, P3).
        // MaxHealth used to be one too, back when the interim table had only
        // two reward kinds to pick a filler mix from -- now that it is one of
        // twelve, everything but the one spent, storable quantity is read
        // live instead (RewardTrackDefinition.CollectedTotal).
        [TestCase(TrackReward.StatPoint, true)]
        [TestCase(TrackReward.MaxHealth, false)]
        [TestCase(TrackReward.Respec, false)]
        [TestCase(TrackReward.SecondLife, false)]
        [TestCase(TrackReward.SignatureCapacity, false)]
        [TestCase(TrackReward.ElementalDamagePercent, false)]
        [TestCase(TrackReward.UnlockSkill, false)]
        [TestCase(TrackReward.None, false)]
        public void OnlyStatPointIsAGrant(TrackReward reward, bool isGrant)
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
        //
        // A FILLED-IN ENTRY, not just a reward and an amount -- four of the
        // twelve captions (SignatureCapacity/GainPerTurn/GainOnDamageTaken/
        // Absorbs, ElementalDamagePercent, UnlockSkill) read a baked field
        // (ResourceDisplayName/Against/SkillDisplayName) the plain
        // RewardTrackNames.Of(reward, amount) overload cannot supply, so this
        // walks the enum with an entry that carries all of them rather than
        // leaning on ResourceNameOf/SkillNameOf's blank-string fallbacks,
        // which would pass even for a caption template that forgot to use its
        // own selector.
        [Test]
        public void EveryRewardKindHasSomethingToCallItself()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                var entry = new TrackEntry(reward, 5, against: Stats.DamageType.Nature,
                    skillId: "fixture_skill", skillDisplayName: "Fixture Skill",
                    resourceDisplayName: "Fixture Resource");

                Assert.IsNotEmpty(RewardTrackNames.Of(entry),
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

        // EVERY REWARD KIND THE ENUM KNOWS ABOUT IS ACTUALLY GRANTED used to
        // live here, walking RewardTrack.UnlockLevel over the static table.
        // docs/PLAN_REWARD_TRACKS.md P3 deletes both: the static table is gone
        // (RewardTrackDefinition replaces it, one instance per character) and
        // eight of the twelve reward kinds now exist that only an AUTHORED
        // track grants -- the generated default (RewardTrackDefinition.
        // Default) still only carries four. Asserting "every kind is granted"
        // against the default would be false; weakening it to "against SOME
        // track" needs the shipped sheep/owl tracks P6 authors, which do not
        // exist in this package. P6 restores the honest version,
        // EveryRewardKindIsGrantedBySomeShippedTrack, over real content.

        // ---- the max-health nodes -------------------------------------------------

        // MaxHealth used to be claimed against the watermark exactly once, the
        // same rule stat points follow -- see OnlyStatPointIsAGrant above for
        // why that changed. It is still an IsUnlock kind (IsUnlock is just
        // "not the one grant" now), even though it does not behave like a
        // classic on/off capability: it is read live via CollectedTotal,
        // summed rather than switched on.
        [Test]
        public void MaxHealthIsCollectedLiveRatherThanGranted()
        {
            Assert.IsFalse(RewardTrack.IsGrant(TrackReward.MaxHealth));
            Assert.IsTrue(RewardTrack.IsUnlock(TrackReward.MaxHealth));
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

        // THE INTERIM TABLE'S TWO TOTALS moved to
        // RewardTrackDefinitionTests.TheDefaultTrackPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth
        // (Tests/EditMode/Run/RewardTrackDefinitionTests.cs) now that
        // RewardTrackDefinition.Default owns the table this pinned --
        // RewardTrack.GrantedBetween(MaxHealth, ...) is 0 by construction
        // since P3 (OnlyStatPointIsAGrant above), so the old assertion could
        // not be kept here even reworded.
    }
}
