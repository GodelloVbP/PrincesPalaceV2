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
            // came back as two rows with nothing saying why. Squad-of-three is
            // now the live default (SaveData.SquadOfThreeReady), so this test's
            // own "party of three" IS the fielded squad rather than a stand-in
            // for it -- every member should get a row, not a literal one.
            var squad = SaveSlotManager.CurrentSave.ActiveSquad();
            var before = squad.Select(c => c.exp).ToList();

            var reward = RewardApplier.Apply(new VictoryRewards.Payout(50, 0), new List<string>());

            Assert.AreEqual(squad.Count, reward.Characters.Count, "every fielded member still gets a row");
            for (int i = 0; i < reward.Characters.Count; i++)
            {
                Assert.IsTrue(reward.Characters[i].IsDowned);
                Assert.AreEqual(0, reward.Characters[i].ExpGained);
                Assert.AreEqual(before[i], squad[i].exp, "and they gained nothing");
            }
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

        // ---- the reward track is NOT paid here any more --------------------------
        //
        // It used to be, unconditionally, on every payout -- which kept the
        // level and the watermark in lockstep and made the gap between them
        // reachable only through a migration or the debug menu.
        //
        // COLLECTION IS SOMETHING THE PLAYER DOES NOW (reward track design
        // handoff, section 3), so what these pin is the other half of that
        // change: a fight LEVELS a character and leaves the track OWING. What
        // happens when the debt is collected is RewardTrackClaimTests, which
        // goes through the reward track panel for the same reason this file
        // used to go through RewardApplier -- the claim has exactly one
        // production call site, and a test that reached past it would pass just
        // as happily if that site were deleted. architecture_audit.md F17, and
        // AUDIT #46 is what ignoring it costs.

        [Test]
        public void LevellingLeavesTheTrackOwingRatherThanPaying()
        {
            var character = First();
            Assert.AreEqual(1, character.level, "the fixture did not start at level 1");
            Assert.AreEqual(0, character.claimedTrackLevel, "a fresh character starts unpaid at 0");
            Assert.AreEqual(0, character.unspentStatPoints, "the fixture did not start unpaid");

            // Enough to cross a long stretch of the track in one payout, which
            // is also the multi-level case.
            RewardApplier.Apply(new VictoryRewards.Payout(20000, 0), Squad());

            character = First();
            Assert.Greater(character.level, 1, "the fixture did not level up");

            // STILL ZERO. A fresh character's watermark is 0 rather than 1 --
            // the field is additive and defaults, and RewardTrack.UnclaimedCount
            // treats the two the same because level 1 pays nothing either way.
            Assert.AreEqual(0, character.claimedTrackLevel,
                "the watermark moved, so something is still paying the track automatically - " +
                "the whole reward track screen depends on this gap being visible");
            Assert.AreEqual(0, character.unspentStatPoints,
                "stat points arrived without the player collecting them");
        }

        // A second payout does not deepen the debt beyond the levels reached
        // either. The watermark is untouched by this path in both directions.
        [Test]
        public void RepeatedPayoutsNeverMoveTheWatermark()
        {
            RewardApplier.Apply(new VictoryRewards.Payout(20000, 0), Squad());

            // A trickle: not enough to reach the next level from here.
            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            Assert.AreEqual(0, First().claimedTrackLevel);
        }

        // The migration case, from the direction it now arrives: a character
        // who reached their level before the track existed has a zero watermark
        // and is owed everything, and a fight does not quietly settle it. The
        // handoff calls this the expected path rather than an edge case -- it
        // is what the ribbon's comb of gold ticks and the collect-all button
        // are for.
        [Test]
        public void AFightDoesNotSettleACharactersBackCatalogue()
        {
            var character = First();
            character.level = 30;
            character.claimedTrackLevel = 0;
            character.unspentStatPoints = 0;

            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            character = First();
            Assert.AreEqual(0, character.claimedTrackLevel,
                "a fight collected the track on the player's behalf");
            Assert.AreEqual(0, character.unspentStatPoints);

            // And the debt is still THERE to be collected, which is the half
            // that matters: nothing expires.
            Assert.AreEqual(29, RewardTrack.UnclaimedCount(character.level, character.claimedTrackLevel),
                "the levels between 1 and 30 stopped being owed");
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

        // The payout half of the same pair: a fight raises the LEVEL that owes
        // the max health and hands over none of it. That the collection then
        // reaches the character's real stats is the test above, which is the
        // one AUDIT #53 exists for.
        [Test]
        public void LevellingDoesNotHandOverTheMaxHealthTheTrackOwes()
        {
            var character = First();
            character.level = 60;
            character.claimedTrackLevel = 0;
            character.bonusMaxHealth = 0;

            RewardApplier.Apply(new VictoryRewards.Payout(1, 0), Squad());

            Assert.AreEqual(0, First().bonusMaxHealth,
                "max health arrived without the player collecting it");
        }

        // ---- a fight is worth exactly what it paid --------------------------------
        //
        // The reward track used to have an experience-find kind, so a payout
        // could arrive boosted above what the fight actually paid. P1 of
        // docs/PLAN_REWARD_TRACKS.md retired it along with Favor and six other
        // over-arching rewards; RewardApplier now hands `payout.Experience`
        // straight to AddExperience with nothing in between. What is left worth
        // pinning is that the payout is never anything other than what it says.

        [Test]
        public void AFightIsWorthExactlyWhatItPaid()
        {
            int before = First().exp;

            RewardApplier.Apply(new VictoryRewards.Payout(50, 0), Squad());

            Assert.AreEqual(before + 50, First().exp);
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

        // A downed character earns no exp and gains no level, and their debt is
        // left exactly where it was -- neither settled on their behalf nor
        // quietly written off for having sat the fight out.
        [Test]
        public void ADownedCharacterGainsNoLevelsAndKeepsTheirDebt()
        {
            var character = First();
            character.level = 15;
            character.claimedTrackLevel = 0;

            // Fielded nobody: everyone is downed and gains zero exp.
            RewardApplier.Apply(new VictoryRewards.Payout(500, 0), new List<string>());

            character = First();
            Assert.AreEqual(15, character.level, "a downed character gained a level");
            Assert.AreEqual(0, character.claimedTrackLevel,
                "a downed character had their track debt settled for them");
        }
    }
}
