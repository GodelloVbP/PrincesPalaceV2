using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Closing the books on a run.
    //
    // PlayMode because SaveData and SaveSlotManager are Core. The RULE it
    // applies -- an ember per boss never killed before -- is Domain and covered
    // without a scene by EmberPayoutTests; what is left here is that the
    // payout reaches the wallet, that it is recorded in the same pass, and that
    // the run's ledger survives long enough to be read.
    public class RunSettlementTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-settle-" + System.Guid.NewGuid().ToString("N"));
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

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // Who was fielded. RunOrchestrator.SettleFight builds this off the live
        // session; here it is written down, because a fixture that derived it
        // from the ledger it is checking would agree with anything.
        private static readonly string[] Party = { "shawn" };

        private static RunSnapshot Run(int gold = 0, params string[] bosses)
        {
            var run = new RunSnapshot { hasRun = true, gold = gold };
            run.bossesKilled.AddRange(bosses);
            return run;
        }

        [Test]
        public void AFirstBossKillPaysAnEmberToTheSquad()
        {
            // Onto the CHARACTERS who ran, not a shared wallet -- see
            // EmberOwnershipTests for the ownership rule itself.
            int before = Save.ActiveSquad().First().embers;

            var result = RunSettlement.Settle(Save, Run(bosses: "warden"));

            Assert.AreEqual(1, result.EmbersEarned);
            Assert.AreEqual(before + 1, Save.ActiveSquad().First().embers);
            CollectionAssert.Contains(result.NewBosses, "warden");
        }

        [Test]
        public void ThePayoutIsRecordedInTheSamePassThatPaidIt()
        {
            // Paying without recording would let the same boss pay again on the
            // next run. This is the assertion that stops that.
            RunSettlement.Settle(Save, Run(bosses: "warden"));

            CollectionAssert.Contains(Save.defeatedBossIds, "warden");

            int afterFirst = Save.ActiveSquad().First().embers;
            var second = RunSettlement.Settle(Save, Run(bosses: "warden"));

            Assert.AreEqual(0, second.EmbersEarned, "the same boss paid twice");
            Assert.AreEqual(afterFirst, Save.ActiveSquad().First().embers);
        }

        [Test]
        public void TheRecordSurvivesBeingWrittenToDisk()
        {
            RunSettlement.Settle(Save, Run(bosses: "warden"));

            SaveSlotManager.Forget();

            CollectionAssert.Contains(Save.defeatedBossIds, "warden",
                "the lifetime boss list never reached the file, so it would pay again next session");
        }

        [Test]
        public void UnbankedGoldIsReportedAsLost()
        {
            // The wager CurrencyType has always documented and nothing enforced.
            var result = RunSettlement.Settle(Save, Run(gold: 240));

            Assert.AreEqual(240, result.GoldLost);
        }

        [Test]
        public void LostGoldNeverReachesTheBankedWallet()
        {
            // The whole point of the at-risk pile. If this ever fails, retreat
            // and death have become the same decision.
            int banked = Save.wallet.Get(CurrencyType.Gold);

            RunSettlement.Settle(Save, Run(gold: 500));

            Assert.AreEqual(banked, Save.wallet.Get(CurrencyType.Gold));
        }

        [Test]
        public void TheRunsLedgerIsCarriedOutOfTheRunBeforeItIsDiscarded()
        {
            // EndRun replaces the snapshot wholesale, so anything the defeat
            // screen wants has to leave in the settlement or not at all.
            var run = Run();
            run.roomsCleared = 7;
            run.deepestStep = 12;
            run.expEarned = 340;
            run.ledger.Add(new RunLedgerEntry { characterId = "shawn", physicalDealt = 900, damageTaken = 210 });

            var result = RunSettlement.Settle(Save, run);

            Assert.AreEqual(7, result.RoomsCleared);
            Assert.AreEqual(12, result.DeepestStep);
            Assert.AreEqual(340, result.ExpEarned);
            Assert.AreEqual(900, result.Ledger.Single(e => e.characterId == "shawn").physicalDealt);
        }

        // THE HUB'S "ABOUT N FIGHTS TO GO" READS THIS FIELD (RewardTrackController.
        // DepthStep), which is the whole reason Settle writes it here rather
        // than the estimate reaching for the last-ended run itself -- by the
        // time the hub asks, EndRun has already replaced the snapshot Settle
        // read this from.
        [Test]
        public void SettleWritesTheSquadsLastRunDepthOntoTheCharactersWhoRanIt()
        {
            var fielded = Save.ActiveSquad().First();
            Assert.AreEqual(0, fielded.lastRunDeepestStep, "fixture: a fresh character has never run");

            var run = Run();
            run.deepestStep = 40;

            RunSettlement.Settle(Save, run);

            Assert.AreEqual(40, fielded.lastRunDeepestStep);
        }

        // THE LAST RUN, NOT THE DEEPEST EVER -- unlike lifetimeDeepestStep,
        // which only ever climbs. A character who retreats to a shallow
        // retry after a deep failed run should see the hub's fight estimate
        // get cheaper again, not stay priced at their best depth forever.
        [Test]
        public void SettleOverwritesLastRunDepthEvenWhenItIsShallowerThanBefore()
        {
            var fielded = Save.ActiveSquad().First();

            var deep = Run();
            deep.deepestStep = 80;
            RunSettlement.Settle(Save, deep);
            Assert.AreEqual(80, fielded.lastRunDeepestStep);

            var shallow = Run();
            shallow.deepestStep = 5;
            RunSettlement.Settle(Save, shallow);

            Assert.AreEqual(5, fielded.lastRunDeepestStep, "the LAST run, not the best one");
            Assert.AreEqual(80, Save.lifetimeDeepestStep, "the lifetime high-water mark still only climbs");
        }

        [Test]
        public void SettlingANullRunDegradesRatherThanThrowing()
        {
            RunSettlement.Result result = null;

            Assert.DoesNotThrow(() => result = RunSettlement.Settle(Save, null));
            Assert.AreEqual(0, result.EmbersEarned);
            Assert.AreEqual(0, result.GoldLost);
        }

        [Test]
        public void SettlingWithNoSaveStillReportsWhatWasLost()
        {
            // A screen has to be able to say "you lost 240" even if there is
            // nothing to charge it against.
            var result = RunSettlement.Settle(null, Run(gold: 240, bosses: "warden"));

            Assert.AreEqual(240, result.GoldLost);
            Assert.AreEqual(0, result.EmbersEarned, "nothing to pay it onto");
        }

        // ---- what a load hands the settlement -------------------------------------

        // Settle reads exactly two run lists: bossesKilled and ledger. The
        // boss list is the one that is money --
        // an ember per boss never killed before, and the id then goes onto
        // save.defeatedBossIds permanently, where DefeatDistinctBosses counts
        // it. A boss renamed in enemies.json between quitting and resuming
        // would pay for an id nothing can name, and the same boss under its
        // new name would pay again.
        [Test]
        public void ABossContentNoLongerHasIsDroppedByTheLoadRatherThanPaidFor()
        {
            var save = Save;
            save.activeRun = new RunSnapshot { hasRun = true };
            save.activeRun.bossesKilled.Add("a_boss_enemies_json_no_longer_names");

            save.Reconcile();

            Assert.IsEmpty(save.activeRun.bossesKilled,
                "a kill against an id content cannot resolve survived the load");
            Assert.AreEqual(0, RunSettlement.Settle(save, save.activeRun).EmbersEarned,
                "and it was paid an ember on the way out");
        }

        // The other half: the two lists get the ??= guard the other seven get.
        // JsonUtility writes an explicit null for a list a save was written
        // without, and Settle dereferences both.
        [Test]
        public void ARunWhoseBossListAndLedgerAreNullLoadsRatherThanThrowing()
        {
            var save = Save;
            save.activeRun = new RunSnapshot { hasRun = true, bossesKilled = null, ledger = null };

            Assert.DoesNotThrow(() => save.Reconcile());
            Assert.IsNotNull(save.activeRun.bossesKilled);
            Assert.IsNotNull(save.activeRun.ledger);
        }

        // ---- folding a fight into a run ------------------------------------------

        [Test]
        public void FoldingAFightAddsItsColumnsToTheRun()
        {
            var run = Run();
            var fight = new CombatLedger();
            fight.Dealt("shawn", DamageType.Physical, 120);
            fight.Took("shawn", 40, shielded: 12);
            fight.Restored("shawn", 15);
            fight.ScoredKill("shawn");

            RunLedger.Fold(run, fight, Party);
            RunLedger.Fold(run, fight, Party);

            var entry = RunLedger.For(run, "shawn");
            Assert.AreEqual(240, entry.physicalDealt, "two identical fights sum rather than replace");
            Assert.AreEqual(80, entry.damageTaken);
            Assert.AreEqual(24, entry.shielded);
            Assert.AreEqual(30, entry.healed);
            Assert.AreEqual(2, entry.kills);
        }

        // A CombatLedger is BOTH sides of the fight: every enemy swing accrues
        // onto a line keyed by the enemy's definition id. Folding all of it put
        // the monsters into a number the game calls the party's, and this is
        // the sharpest end of it -- lifetimeDamageDealt is what the
        // million_damage achievement counts, so the threshold fired at roughly
        // half the authored figure.
        //
        // Pinned at the FOLD rather than at the sum: the sums are read in four
        // places and cannot tell a character id from an enemy one, while the
        // fold is handed the party that was actually fielded.
        [Test]
        public void AnEnemysOwnLedgerRowIsNotBankedAsDamageTheSquadDealt()
        {
            var run = Run();
            var fight = new CombatLedger();
            fight.Dealt("shawn", DamageType.Physical, 300);
            fight.Dealt("rat", DamageType.Physical, 500);

            RunLedger.Fold(run, fight, Party);
            RunSettlement.Settle(Save, run);

            Assert.AreEqual(300, Save.lifetimeDamageDealt,
                "the rat's 500 was banked as damage the party dealt");
            Assert.AreEqual(0, RunLedger.For(run, "rat").TotalDealt,
                "the enemy got a row on the run's ledger at all");
        }

        [Test]
        public void ACharacterWithNoLineReadsAsZeroesRatherThanNull()
        {
            var entry = RunLedger.For(Run(), "never_fought");

            Assert.IsNotNull(entry);
            Assert.AreEqual(0, entry.TotalDealt);
        }

        [Test]
        public void ALostRoomStillCountsWhatHappenedInItButDoesNotCountAsCleared()
        {
            // The fight you died in is part of the run. Dropping it would make
            // the death screen under-report the most dramatic fight in it.
            var run = Run();

            RunLedger.RecordRoom(run, won: false, expGained: 0, step: 9);

            Assert.AreEqual(0, run.roomsCleared, "a room you died in was not cleared");
            Assert.AreEqual(9, run.deepestStep, "but you still got that deep");

            // RecordRoom does not take gold at all: a lost fight pays
            // nothing because the orchestrator's BankPayout is on the win
            // side of the branch. RunManagerTests.TreasureGoldIsGoldTheRunEarned
            // pins the other half -- that everything which DOES pay counts.
        }

        [Test]
        public void DeepestStepNeverGoesBackwards()
        {
            var run = Run();

            RunLedger.RecordRoom(run, won: true, expGained: 0, step: 14);
            RunLedger.RecordRoom(run, won: true, expGained: 0, step: 3);

            Assert.AreEqual(14, run.deepestStep, "how deep they got, not where they stand");
        }

        [Test]
        public void ABossIsRecordedOnceHoweverManyTimesItIsReported()
        {
            var run = Run();

            RunLedger.RecordBossKill(run, "warden");
            RunLedger.RecordBossKill(run, "warden");

            Assert.AreEqual(1, run.bossesKilled.Count(b => b == "warden"));
        }
    }
}
