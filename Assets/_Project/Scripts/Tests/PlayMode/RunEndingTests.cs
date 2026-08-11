using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Ending a run closes its books, whichever way it ends.
    //
    // There are two ways out of a descent -- dying in it, and abandoning it
    // from the map -- and settling used to be the CALLER'S job. Only the defeat
    // path did it, so walking away threw away every ember the bosses in that
    // run had earned and every room it had cleared. Nothing reported it,
    // because a discarded snapshot looks identical whether or not anything read
    // it first.
    //
    // These drive RunManager.EndRun directly, which is now the single door both
    // paths go through.
    public class RunEndingTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-endrun-" + System.Guid.NewGuid().ToString("N"));
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

        // A run that got somewhere: a boss down, rooms cleared, damage dealt.
        private static void GiveTheSaveARunWorthSettling()
        {
            var run = Save.activeRun;
            run.hasRun = true;
            run.roomsCleared = 6;
            run.deepestStep = 11;
            run.gold = 180;
            run.bossesKilled.Add("forest_warden");
            run.ledger.Add(new RunLedgerEntry { characterId = "shawn", physicalDealt = 4200 });
        }

        [Test]
        public void EndingARunPaysTheEmbersItsBossesEarned()
        {
            GiveTheSaveARunWorthSettling();
            int before = Save.ActiveSquad().First().embers;

            var settlement = RunManager.EndRun();

            Assert.AreEqual(1, settlement.EmbersEarned);
            Assert.AreEqual(before + 1, Save.ActiveSquad().First().embers);
            CollectionAssert.Contains(Save.defeatedBossIds, "forest_warden");
        }

        [Test]
        public void EndingARunFoldsItsTotalsIntoTheLifetimeCounters()
        {
            // Achievements read these. Losing them on one of the two exits made
            // "clear a hundred rooms" quietly unreachable for anyone who ever
            // abandoned a descent.
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();

            Assert.AreEqual(6, Save.lifetimeRoomsCleared);
            Assert.AreEqual(11, Save.lifetimeDeepestStep);
            Assert.AreEqual(4200, Save.lifetimeDamageDealt);
            Assert.AreEqual(1, Save.lifetimeRunsEnded);
        }

        [Test]
        public void EndingARunActuallyEndsIt()
        {
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();

            Assert.IsFalse(RunManager.HasRun);
            Assert.IsFalse(Save.activeRun.hasRun);
        }

        [Test]
        public void TheRunsUnbankedGoldIsForfeited()
        {
            GiveTheSaveARunWorthSettling();
            int banked = Save.wallet.gold;

            var settlement = RunManager.EndRun();

            Assert.AreEqual(180, settlement.GoldLost);
            Assert.AreEqual(banked, Save.wallet.gold, "forfeited gold reached the banked wallet");
        }

        [Test]
        public void EndingTwiceDoesNotPayTwice()
        {
            // The second call has an empty snapshot to settle. If it paid
            // again, closing the books would be a money printer rather than a
            // settlement.
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();
            int afterFirst = Save.ActiveSquad().First().embers;
            int roomsAfterFirst = Save.lifetimeRoomsCleared;

            var second = RunManager.EndRun();

            Assert.AreEqual(0, second.EmbersEarned);
            Assert.AreEqual(afterFirst, Save.ActiveSquad().First().embers);
            Assert.AreEqual(roomsAfterFirst, Save.lifetimeRoomsCleared);
        }

        [Test]
        public void EndingARunThatDidNothingIsHarmless()
        {
            var settlement = RunManager.EndRun();

            Assert.AreEqual(0, settlement.EmbersEarned);
            Assert.AreEqual(0, settlement.GoldLost);
            Assert.IsFalse(RunManager.HasRun);
        }

        [Test]
        public void WhatWasSettledSurvivesBeingWrittenToDisk()
        {
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();
            SaveSlotManager.Forget();

            Assert.AreEqual(6, Save.lifetimeRoomsCleared, "the fold never reached the file");
            CollectionAssert.Contains(Save.defeatedBossIds, "forest_warden");
        }
    }
}
