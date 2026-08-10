using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;

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
    }
}
