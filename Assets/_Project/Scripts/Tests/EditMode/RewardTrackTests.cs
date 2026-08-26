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
        [TestCase(80, TrackReward.StatPoint, 10)]
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
        [TestCase(TrackReward.StatPoint, 40 + 1)] // 40 filler nodes plus the level-80 milestone
        [TestCase(TrackReward.Favor, 23 + 1)]     // 23 filler nodes plus the level-10 milestone
        [TestCase(TrackReward.MaxHealth, 15)]
        [TestCase(TrackReward.ExpFind, 7)]
        public void TheTrackHandsOutTheMixItDescribes(TrackReward reward, int expectedNodes)
        {
            int nodes = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).Reward == reward) nodes++;
            }

            Assert.AreEqual(expectedNodes, nodes, $"the track holds the wrong number of {reward} nodes");
        }

        // 23 nodes at 2 each, plus 5 at the level-10 milestone.
        //
        // AND THAT NUMBER IS CHOSEN, not rounded to. LootLadder caps the
        // per-rung chance at 55 Favor for a normal fight; Sheep is authored at
        // 4, so 51 from the track puts her at exactly 55 -- the last Favor that
        // buys anything at all. A 24th node would be worth nothing, which is
        // why there are 23.
        [Test]
        public void AFullTrackTakesSheepToExactlyTheFavorCap()
        {
            const int SheepAuthoredFavor = 4;
            int fromTrack = RewardTrack.GrantedBetween(TrackReward.Favor, 1, RewardTrack.MaxLevel);

            Assert.AreEqual(51, fromTrack);

            float atCap = Rewards.LootLadder.StepChanceFor(
                Rewards.EncounterClass.Normal, fromTrack + SheepAuthoredFavor);
            float oneBelow = Rewards.LootLadder.StepChanceFor(
                Rewards.EncounterClass.Normal, fromTrack + SheepAuthoredFavor - 1);

            Assert.AreEqual(Rewards.LootLadder.MaxStep, atCap, 0.0001f,
                "a fully-levelled Sheep does not reach the cap, so the track is short of Favor");
            Assert.Less(oneBelow, Rewards.LootLadder.MaxStep,
                "she was already capped before the last node, so the track has Favor to spare");
        }

        // The percentage that does work. Gold is discarded at run end
        // (RunSettlement records it as GoldLost) and embers pay 1 per unique
        // boss as an integer, so a percentage of either is arithmetic that
        // never changes an outcome.
        [Test]
        public void AFullTrackIsWorthTwentyOnePercentExperience()
        {
            Assert.AreEqual(210, RewardTrack.GrantedBetween(TrackReward.ExpFind, 1, RewardTrack.MaxLevel),
                "permille, so 210 is +21%");
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

        // ---- rerolls accumulate, relics supersede -------------------------------
        //
        // The two unlocks-with-amounts on the track combine in OPPOSITE ways,
        // and confusing them is silent: rerolls would read 1 instead of 3 and
        // nothing would look broken. That is why both have named accessors
        // rather than callers choosing between UnlockedAmount and
        // UnlockedTotal.
        [TestCase(1, 0)]
        [TestCase(39, 0)]
        [TestCase(40, 1)]
        [TestCase(100, 3)]
        public void RerollsAddUpAcrossTheTrack(int level, int expected)
        {
            Assert.AreEqual(expected, RewardTrack.RerollsPerRun(level));
        }

        [Test]
        public void RerollsAccumulateWhereStartingRelicsReplace()
        {
            // Three reroll nodes worth 1 each -> 3. Three starting-relic steps
            // worth 2, 3 and 4 -> 4, not 9. Asserted together because the
            // distinction only exists in the difference.
            Assert.AreEqual(3, RewardTrack.UnlockedTotal(TrackReward.OfferReroll, RewardTrack.MaxLevel));
            Assert.AreEqual(4, RewardTrack.UnlockedAmount(
                TrackReward.StartingRelics, RewardTrack.MaxLevel, RewardTrack.BaseStartingRelics));

            Assert.AreNotEqual(
                RewardTrack.UnlockedTotal(TrackReward.StartingRelics, RewardTrack.MaxLevel),
                RewardTrack.UnlockedAmount(
                    TrackReward.StartingRelics, RewardTrack.MaxLevel, RewardTrack.BaseStartingRelics),
                "summing and taking the highest agree, so nothing here proves the two are different");
        }

        // The milestone alone would make sum and max identical, which is why
        // the two filler rerolls were added in the same commit that built the
        // reroll rather than left for the authoring pass.
        [Test]
        public void MoreThanOneLevelGrantsAReroll()
        {
            int nodes = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).Reward == TrackReward.OfferReroll) nodes++;
            }

            Assert.AreEqual(3, nodes, "the reroll milestone plus its two filler nodes");
        }

        // NO CAPABILITY ARRIVES BEFORE THE MILESTONE THAT ANNOUNCES IT.
        //
        // Written against every unlock rather than against the reroll, because
        // this is the class of bug and not the instance: the even spread put a
        // filler reroll at level 39, one level before the milestone introducing
        // rerolls, and a player would have met the mechanic before the track
        // claimed to give it to them. Any future unlock with filler nodes has
        // the same failure available to it.
        [Test]
        public void NoUnlockIsHandedOutBeforeItsMilestone()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (!RewardTrack.IsUnlock(reward)) continue;

                int first = 0;
                for (int level = 1; level <= RewardTrack.MaxLevel; level++)
                {
                    if (RewardTrack.At(level).Reward == reward) { first = level; break; }
                }

                if (first == 0) continue;

                Assert.AreEqual(RewardTrack.UnlockLevel(reward), first,
                    $"{reward} first appears at level {first}, which is not where its milestone is");
            }
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

        [Test]
        public void TheTrackHandsOutFifteenMaxHealthNodes()
        {
            int nodes = 0;
            for (int level = 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (RewardTrack.At(level).Reward == TrackReward.MaxHealth) nodes++;
            }

            Assert.AreEqual(15, nodes);
        }

        [Test]
        public void AFullTrackIsWorthOneHundredAndFiftyMaxHealth()
        {
            Assert.AreEqual(150,
                RewardTrack.GrantedBetween(TrackReward.MaxHealth, 1, RewardTrack.MaxLevel));
        }

        // A quantity, so it is claimed against the watermark exactly once --
        // the same rule stat points and Favor follow.
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
    }
}
