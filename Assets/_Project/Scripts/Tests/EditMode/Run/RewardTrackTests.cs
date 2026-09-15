using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    public class RewardTrackTests
    {
        // THE GENERATED DEFAULT, named directly rather than reached through a
        // character or the screen layer: every test below is about the
        // default table's own arithmetic (RewardTrackDefinition.Default),
        // not about a specific character's authored track, so there is
        // nothing to gain by routing through RewardTracks.For(character)
        // first.
        private static readonly RewardTrackDefinition Default = RewardTrackDefinition.Default("");

        // ---- the milestones -----------------------------------------------------
        //
        // Pinned by level, because a milestone is a promise about a NUMBER the
        // player is counting toward. Moving one is a design change and should
        // have to be typed twice.
        //
        // SEVEN OF THE TEN ARE MaxHealth 15 -- the generated default has no
        // author to give the other seven milestones their own flavour, so it
        // fills them with a plain, always-safe reward instead. The three
        // that are not are the spine every track shares: Respec at 20,
        // SecondLife at 25, StatPoint at 30.
        //
        // The cadence itself moved with progression v2 phase 2 (cap 100 to
        // 40); this is the placeholder track, and phase 4 authors the real
        // ones.
        [TestCase(3, TrackReward.MaxHealth, 15)]
        [TestCase(5, TrackReward.MaxHealth, 15)]
        [TestCase(10, TrackReward.MaxHealth, 15)]
        [TestCase(15, TrackReward.MaxHealth, 15)]
        [TestCase(20, TrackReward.Respec, 0)]
        [TestCase(25, TrackReward.SecondLife, 1)]
        [TestCase(30, TrackReward.StatPoint, 10)]
        [TestCase(35, TrackReward.MaxHealth, 15)]
        [TestCase(38, TrackReward.MaxHealth, 15)]
        [TestCase(40, TrackReward.MaxHealth, 15)]
        public void AMilestoneLandsOnItsLevel(int level, TrackReward reward, int amount)
        {
            var entry = Default.At(level);

            Assert.AreEqual(reward, entry.Reward, $"level {level} does not hold the milestone it should");
            Assert.AreEqual(amount, entry.Amount, $"level {level}'s milestone has the wrong amount");
        }

        // Level 1 is where a character STARTS. Paying it would hand a reward to
        // a brand-new character for having been created.
        [Test]
        public void TheTrackPaysNothingForLevelOne()
        {
            Assert.IsFalse(Default.At(1).IsSomething);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(41)]
        [TestCase(int.MaxValue)]
        public void LevelsOffTheTrackPayNothingRatherThanThrowing(int level)
        {
            Assert.IsFalse(Default.At(level).IsSomething);
        }

        // THE REASON Spread() exists rather than filling from level 2 upward.
        //
        // A naive fill packs everything into the front of the track and leaves
        // the back half -- the half a player grinds hardest for -- empty. That
        // WAS visible as a shortfall (87 filler levels, 50 rewards to put in
        // them) and is not any more: since the cap came down to 40 the default
        // mix sums to exactly the 29 filler levels there are, so what this
        // asserts now is that the whole back half pays rather than that most
        // of it does.
        //
        // Kept rather than deleted, because the shortfall comes straight back
        // the moment a kind is added to the mix without a matching count, and
        // this is the only test that would notice where it landed.
        [Test]
        public void RewardsReachTheBackHalfOfTheTrack()
        {
            int inTheBackHalf = 0;
            for (int level = RewardTrack.MaxLevel / 2 + 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (Default.At(level).IsSomething) inTheBackHalf++;
            }

            Assert.AreEqual(20, inTheBackHalf,
                "the back half of the track is not full, so the filler is packed into the front");
        }

        // ---- grants vs unlocks --------------------------------------------------

        // STATPOINT IS THE ONLY GRANT (docs/PLAN_REWARD_TRACKS.md §2) --
        // spent once against the watermark and stored. Everything else,
        // MaxHealth included, is read live instead
        // (RewardTrackDefinition.CollectedTotal).
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

        // A watermark is half-open at the bottom: everything up to and
        // including `afterLevel` has already been paid.
        [Test]
        public void AClaimedLevelIsNotPaidTwice()
        {
            int all = Default.GrantedBetween(TrackReward.StatPoint, 1, 40);
            int firstHalf = Default.GrantedBetween(TrackReward.StatPoint, 1, 20);
            int secondHalf = Default.GrantedBetween(TrackReward.StatPoint, 20, 40);

            Assert.AreEqual(all, firstHalf + secondHalf,
                "claiming in two steps paid a different total than claiming in one");
        }

        [Test]
        public void AWatermarkPastTheLevelIsOwedNothing()
        {
            Assert.AreEqual(0, Default.GrantedBetween(TrackReward.StatPoint, 50, 20));
        }

        [Test]
        public void AnUnlockIsNeverPaidAsAGrant()
        {
            Assert.AreEqual(0, Default.GrantedBetween(TrackReward.Respec, 1, RewardTrack.MaxLevel),
                "an unlock was counted as a claimable quantity");
        }

        // ---- a capability turns on once and stays on ----------------------------
        //
        // Written as a function of a level because that is HasUnlocked's
        // parameter; what production passes it since P4 is the character's
        // claimedTrackLevel, not their level, so the capability is COLLECTED
        // rather than merely reached (docs/PLAN_REWARD_TRACKS.md §2). The
        // arithmetic under test is the same either way.

        [TestCase(TrackReward.Respec, 19, false)]
        [TestCase(TrackReward.Respec, 20, true)]
        [TestCase(TrackReward.Respec, 39, true)]
        [TestCase(TrackReward.SecondLife, 24, false)]
        [TestCase(TrackReward.SecondLife, 25, true)]
        public void ACapabilityTurnsOnAtItsLevelAndStaysOn(TrackReward reward, int level, bool expected)
        {
            Assert.AreEqual(expected, Default.HasUnlocked(reward, level));
        }

        // HasUnlocked's gate is RewardTrack.IsOneShotCapability, not "every
        // kind but the one grant" -- MaxHealth is neither: it is a total
        // read live through CollectedTotal, so asking HasUnlocked about it
        // must answer false rather than true the moment it clears level 1.
        [Test]
        public void HasUnlockedRefusesAReadLiveTotalLikeMaxHealth()
        {
            Assert.IsFalse(Default.HasUnlocked(TrackReward.MaxHealth, 3));
        }

        // THE SET ContentDatabase.BuildSignatureResource PAYS -- the four
        // kinds meaningless on a character with no signature resource.
        // MaxHealth and StatPoint are named explicitly rather than left to
        // "everything else" because they are the two kinds a signature-
        // resource check is most likely to be confused with (a resource pool
        // and a spendable point both sound like they could gate on the same
        // thing they do not).
        [TestCase(TrackReward.SignatureCapacity, true)]
        [TestCase(TrackReward.SignatureGainPerTurn, true)]
        [TestCase(TrackReward.SignatureGainOnDamageTaken, true)]
        [TestCase(TrackReward.SignatureAbsorbs, true)]
        [TestCase(TrackReward.MaxHealth, false)]
        [TestCase(TrackReward.StatPoint, false)]
        public void OnlyTheFourSignatureResourceKindsAreSignatureRewards(TrackReward reward, bool isSignatureReward)
        {
            Assert.AreEqual(isSignatureReward, RewardTrack.IsSignatureReward(reward));
        }

        // Level 30 grants ten stat points (level 80 before progression v2
        // phase 2 moved the spine). It USED TO be sized to exactly one
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
        public void TheStatPointMilestoneIsTenPoints_AndEveryPointIsWorthTheSameFlatAmount()
        {
            Assert.AreEqual(TrackReward.StatPoint, Default.At(30).Reward);
            Assert.AreEqual(10, Default.At(30).Amount);

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
            int next = Default.NextRewardLevel(1);

            Assert.Greater(next, 1);
            Assert.IsTrue(Default.At(next).IsSomething,
                "the next reward level does not actually hold a reward");

            for (int level = 2; level < next; level++)
            {
                Assert.IsFalse(Default.At(level).IsSomething,
                    $"level {level} pays something but was skipped over");
            }
        }

        [Test]
        public void EveryRewardLevelIsItsOwnNextFromTheOneBefore()
        {
            // Walking the track by NextRewardLevel has to visit every reward.
            // Off-by-one here would hide a milestone from the player entirely.
            int visited = 0;
            for (int level = Default.NextRewardLevel(0); level > 0; level = Default.NextRewardLevel(level))
            {
                visited++;
            }

            int actual = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (Default.At(level).IsSomething) actual++;
            }

            Assert.AreEqual(actual, visited, "walking the track skipped or repeated a reward");
        }

        [Test]
        public void TheEndOfTheTrackHasNoNext()
        {
            Assert.AreEqual(0, Default.NextRewardLevel(RewardTrack.MaxLevel));
            Assert.AreEqual(0, Default.NextRewardLevel(RewardTrack.MaxLevel + 50));
        }

        // ---- the max-health nodes -------------------------------------------------

        // EVERY LEVEL PAYS. The filler counts sum to exactly the number of
        // filler levels, so there is no level between 2 and 100 that hands over
        // nothing -- which is the whole reason the track can be walked without
        // a stretch of it feeling broken.
        [Test]
        public void NoLevelOfTheTrackPaysNothing()
        {
            for (int level = 2; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.IsTrue(Default.At(level).IsSomething, $"level {level} pays nothing");
            }
        }

        // 24 stat points across the whole track: 14 filler plus level 30's ten.
        // Was 50 while the cap was 100.
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
            int points = Default.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel);

            Assert.AreEqual(24, points);
            Assert.Less(points, Stats.AbilityScores.All.Length * 10,
                "the track grants enough points to max every band, so spending them is no longer a choice");
        }

        // The default table's two totals (24 stat points, 135 max health)
        // are pinned in RewardTrackDefinitionTests.
        // TheDefaultTrackPaysTwentyFourStatPointsAndOneHundredThirtyFiveMaxHealth
        // instead of here -- RewardTrackDefinition.Default owns the table,
        // and Default.GrantedBetween(MaxHealth, ...) is 0 by construction
        // (OnlyStatPointIsAGrant above), so a MaxHealth total belongs beside
        // the CollectedTotal read it actually uses.
    }
}
