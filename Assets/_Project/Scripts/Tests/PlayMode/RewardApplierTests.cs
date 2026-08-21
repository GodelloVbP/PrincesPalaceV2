using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.PlayModeTests
{
    // Experience reaching the characters.
    //
    // The half of a payout that was computed and thrown away: gold was banked
    // into the run and exp was calculated, reported in the log, and never
    // applied to anybody.
    public class RewardApplierTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reward-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static List<string> Squad() =>
            SaveSlotManager.CurrentSave.ActiveSquad().Select(c => c.definitionId).ToList();

        private static Character First() => SaveSlotManager.CurrentSave.ActiveSquad()[0];

        [Test]
        public void AFieldedCharacterGainsTheExperience()
        {
            int before = First().exp;

            RewardApplier.Apply(new VictoryRewards.Payout(50, 10), Squad());

            Assert.AreEqual(before + 50, First().exp);
        }

        [Test]
        public void EnoughExperienceLevelsThemUp()
        {
            var reward = RewardApplier.Apply(new VictoryRewards.Payout(10000, 0), Squad());

            Assert.Greater(First().level, 1);
            Assert.IsTrue(reward.Characters[0].LevelledUp);
        }

        [Test]
        public void TheReportHoldsBeforeAndAfter()
        {
            // "Before" stops existing the moment AddExperience mutates in place,
            // which is why the report is the return value rather than something
            // the screen reconstructs afterwards.
            int levelBefore = First().level;
            int expBefore = First().exp;

            var reward = RewardApplier.Apply(new VictoryRewards.Payout(30, 0), Squad());
            var row = reward.Characters[0];

            Assert.AreEqual(levelBefore, row.LevelBefore);
            Assert.AreEqual(expBefore, row.ExpBefore);
            Assert.AreEqual(First().exp, row.ExpAfter);
            Assert.AreEqual(30, row.ExpGained);
        }

        [Test]
        public void ADownedCharacterGetsARowAndNothingElse()
        {
            // They used to vanish from the reward entirely, so a party of three
            // came back as two rows with nothing saying why.
            int before = First().exp;

            var reward = RewardApplier.Apply(new VictoryRewards.Payout(50, 0), new List<string>());

            Assert.AreEqual(1, reward.Characters.Count, "the row is still there");
            Assert.IsTrue(reward.Characters[0].IsDowned);
            Assert.AreEqual(0, reward.Characters[0].ExpGained);
            Assert.AreEqual(before, First().exp, "and they gained nothing");
        }

        [Test]
        public void ExperienceIsNotSplitAcrossTheParty()
        {
            // A pre-existing design decision, pinned rather than changed: every
            // fielded character receives the FULL amount.
            var reward = RewardApplier.Apply(new VictoryRewards.Payout(40, 0), Squad());

            foreach (var row in reward.Characters.Where(r => !r.IsDowned))
            {
                Assert.AreEqual(40, row.ExpGained);
            }
        }

        [Test]
        public void RowsComeBackInSquadSlotOrder()
        {
            // v1 iterated a Dictionary here and relied on insertion order
            // holding, so nothing guaranteed party slot 1 was reward row 1.
            var reward = RewardApplier.Apply(new VictoryRewards.Payout(10, 0), Squad());

            CollectionAssert.AreEqual(
                Enumerable.Range(0, reward.Characters.Count).ToArray(),
                reward.Characters.Select(r => r.SlotIndex).ToArray());
        }

        [Test]
        public void TheGoldRidesTheReportButIsTheRunsToBank()
        {
            // Carried so the rewards screen can show one figure, not because
            // this applies it -- gold belongs to the run and is banked there.
            var reward = RewardApplier.Apply(new VictoryRewards.Payout(0, 77), Squad());

            Assert.AreEqual(77, reward.GoldGained);
        }

        [Test]
        public void ALevelSurvivesBeingReadBackFromDisk()
        {
            // The point of applying it at all: a level is meta-progression and
            // outlives the run that earned it.
            RewardApplier.Apply(new VictoryRewards.Payout(10000, 0), Squad());
            int level = First().level;

            SaveSlotManager.Forget();

            Assert.AreEqual(level, First().level);
        }

        [Test]
        public void AnUnwritableSaveReportsAndCarriesOn()
        {
            // Graceful degradation, and the assertion is subtler than it looks.
            // The write does not THROW -- it logs and leaves whatever was on disk
            // untouched -- so the thing under test is that a failed save is
            // survivable, not that it is silent. Unity fails a test on any
            // unhandled error log, so the expectation has to be declared or this
            // passes for the wrong reason and fails for a third one.
            SaveSystem.RootOverride = Path.Combine(_root, "nonexistent", "deeper");
            SaveSlotManager.Forget();

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("could not be written"));

            Assert.DoesNotThrow(() => RewardApplier.Apply(new VictoryRewards.Payout(10, 10), new List<string>()));
        }

        [Test]
        public void ANullFieldedListLeavesEveryoneDowned()
        {
            // The genuinely reachable degenerate case: a fight that somehow
            // fielded nobody still produces a report rather than throwing.
            var reward = RewardApplier.Apply(new VictoryRewards.Payout(50, 0), null);

            Assert.IsNotEmpty(reward.Characters);
            Assert.IsTrue(reward.Characters.All(r => r.IsDowned && r.ExpGained == 0));
        }

        // ---- the reward track gets paid here ------------------------------------
        //
        // THROUGH RewardApplier, not by calling ClaimTrackRewards directly.
        // What is under test is that the game pays the track at all: the claim
        // has exactly one production call site and a test that reached past it
        // would pass just as happily if that site were deleted.
        // architecture_audit.md F17, and AUDIT #46 is what ignoring it costs.

        [Test]
        public void LevellingPaysWhateverTheTrackOwes()
        {
            var character = First();
            Assert.AreEqual(1, character.level, "the fixture did not start at level 1");

            // Enough to cross a long stretch of the track in one payout, which
            // is also the multi-level case: the watermark has to settle at the
            // level actually reached, not one past the first threshold.
            RewardApplier.Apply(new VictoryRewards.Payout(20000, 0), Squad());

            character = First();
            Assert.Greater(character.level, 1, "the fixture did not level up");
            Assert.AreEqual(character.level, character.claimedTrackLevel,
                "the watermark did not follow the level, so the track will pay these levels again");

            int expectedPoints = RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, character.level);
            int expectedFavor = RewardTrack.GrantedBetween(TrackReward.Favor, 1, character.level);

            Assert.AreEqual(expectedPoints, character.unspentStatPoints,
                "the stat points the track owes for those levels were not handed over");
            Assert.AreEqual(expectedFavor, character.earnedFavor,
                "the Favor the track owes for those levels was not handed over");
        }

        // The bug the watermark exists to prevent, from the direction it would
        // actually arrive: a second payout that gains no levels must pay
        // nothing more.
        [Test]
        public void TheSameLevelsAreNeverPaidTwice()
        {
            RewardApplier.Apply(new VictoryRewards.Payout(20000, 0), Squad());

            var character = First();
            int pointsAfterFirst = character.unspentStatPoints;
            int favorAfterFirst = character.earnedFavor;
            int levelAfterFirst = character.level;

            // A trickle: not enough to reach the next level from here.
            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            character = First();
            Assert.AreEqual(levelAfterFirst, character.level, "the fixture levelled again and the test is moot");
            Assert.AreEqual(pointsAfterFirst, character.unspentStatPoints,
                "stat points were paid a second time for levels already claimed");
            Assert.AreEqual(favorAfterFirst, character.earnedFavor,
                "Favor was paid a second time for levels already claimed");
        }

        // A character who reached their level before the track existed has a
        // zero watermark and is owed everything. This is the migration case,
        // and it has to work without a save-version bump because that is the
        // reason the field is additive.
        [Test]
        public void ACharacterLevelledBeforeTheTrackExistedIsPaidWhatTheyAreOwed()
        {
            var character = First();
            character.level = 30;
            character.claimedTrackLevel = 0;
            character.unspentStatPoints = 0;
            character.earnedFavor = 0;

            // Any payout at all. The claim is deliberately NOT gated on exp
            // being gained, or collecting a debt would require earning more.
            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            character = First();
            Assert.AreEqual(30, character.claimedTrackLevel,
                "an unpaid back-catalogue of levels was not settled");
            Assert.AreEqual(RewardTrack.GrantedBetween(TrackReward.StatPoint, 1, 30),
                character.unspentStatPoints);
            Assert.AreEqual(RewardTrack.GrantedBetween(TrackReward.Favor, 1, 30),
                character.earnedFavor);
        }

        // ---- max health reaches the character's real stats ------------------------
        //
        // The failure this exists for is the one AUDIT #53 records for stat
        // points: a reward that is granted, stored, and read by nothing. A
        // number on the save is not a reward.
        [Test]
        public void GrantedMaxHealthActuallyRaisesTheCharactersMaxHealth()
        {
            var character = First();
            character.bonusMaxHealth = 0;
            int before = Content.ContentDatabase.EffectiveStats(character).maxHealth;

            character.bonusMaxHealth = 40;

            Assert.AreEqual(before + 40, Content.ContentDatabase.EffectiveStats(character).maxHealth,
                "bonus max health is stored but never reaches the stats the fight reads");
        }

        [Test]
        public void LevellingPaysTheMaxHealthTheTrackOwes()
        {
            var character = First();
            character.level = 60;
            character.claimedTrackLevel = 0;
            character.bonusMaxHealth = 0;

            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            character = First();
            Assert.AreEqual(RewardTrack.GrantedBetween(TrackReward.MaxHealth, 1, 60),
                character.bonusMaxHealth,
                "the max health the track owes for those levels was not handed over");
            Assert.Greater(character.bonusMaxHealth, 0, "the fixture crossed no max-health node");
        }

        // ---- the experience nodes -------------------------------------------------

        [Test]
        public void ExperienceNodesRaiseWhatAFightIsWorth()
        {
            var character = First();
            character.level = 1;
            character.claimedTrackLevel = 1;
            character.exp = 0;
            character.bonusExpPermille = 200; // +20%

            RewardApplier.Apply(new VictoryRewards.Payout(100, 0), Squad());

            // 120 exp against a level-1 requirement of 109: one level, 11 over.
            character = First();
            Assert.AreEqual(2, character.level, "120 experience did not cross the level-1 threshold of 109");
            Assert.AreEqual(11, character.exp, "the +20% was not applied to the payout");
        }

        [Test]
        public void WithoutTheNodesAFightIsWorthExactlyWhatItPaid()
        {
            var character = First();
            character.bonusExpPermille = 0;
            int before = character.exp;

            RewardApplier.Apply(new VictoryRewards.Payout(50, 0), Squad());

            Assert.AreEqual(before + 50, First().exp);
        }

        // The bonus is read from what the character walked IN with, so a node
        // crossed by this very payout pays out from the next fight rather than
        // retroactively on the one that earned it.
        [Test]
        public void ANodeCrossedByThisPayoutDoesNotBoostThisPayout()
        {
            var character = First();
            character.level = 1;
            character.claimedTrackLevel = 1;
            character.exp = 0;
            character.bonusExpPermille = 0;

            RewardApplier.Apply(new VictoryRewards.Payout(20000, 0), Squad());

            character = First();
            Assert.Greater(character.bonusExpPermille, 0,
                "the fixture crossed no experience node, so this proves nothing");

            // What it would have been worth had the bonus applied to itself.
            Assert.Less(character.exp + LevelCurve.ExpToNextLevel(character.level - 1),
                20000 + 20000 * character.bonusExpPermille / 1000,
                "this payout was boosted by a node it earned on the way past");
        }

        // A respec gives back what was SPENT. Max health was never spent -- the
        // player made no choice about where it went -- so taking it away would
        // be confiscation rather than a refund.
        [Test]
        public void ARespecDoesNotTakeBackGrantedMaxHealth()
        {
            var character = First();
            character.bonusMaxHealth = 50;

            character.Respec(0);

            Assert.AreEqual(50, character.bonusMaxHealth,
                "the respec confiscated max health the player never chose to spend");
        }

        // A downed character earns no exp, so they must also not be paid for
        // levels they already claimed -- but they must still be settled if they
        // are owed something, which is the same unconditional-claim rule from
        // the other side.
        [Test]
        public void ADownedCharacterIsStillSettledButGainsNoLevels()
        {
            var character = First();
            character.level = 15;
            character.claimedTrackLevel = 0;

            // Fielded nobody: everyone is downed and gains zero exp.
            RewardApplier.Apply(new VictoryRewards.Payout(500, 0), new List<string>());

            character = First();
            Assert.AreEqual(15, character.level, "a downed character gained a level");
            Assert.AreEqual(15, character.claimedTrackLevel,
                "a downed character was left holding an unpaid track debt");
        }
    }
}
