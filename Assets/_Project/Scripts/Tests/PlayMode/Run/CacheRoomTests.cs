using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // THE CACHE ROOM: arriving opens the cache event, a bank choice moves gold
    // out of the purse, and the run's end pays the banked part to the wallet
    // while the purse is forfeited. Fixture as EventArrivalTests: real map,
    // real content, throwaway save root.
    public class CacheRoomTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static ulong SeedWithACache()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));
                if (RealmPaths.FirstOfType(map, RoomType.Cache) != null) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a cache in the first realm.");
        }

        private static void ArriveAtTheCache(int gold)
        {
            RunManager.StartRun(SeedWithACache());
            var node = RealmPaths.FirstOfType(RunManager.Map, RoomType.Cache);
            RealmPaths.StandBefore(node);
            RunManager.Run.gold = gold;

            Assert.AreEqual(RunOrchestrator.Arrival.Event, RunOrchestrator.ArriveAt(node));
            Assert.AreEqual(RunOrchestrator.CacheEventId, RunManager.Run.eventId);
        }

        private static int BankIndex(int percent)
        {
            var page = ContentDatabase.Events.First(e => e.id == RunOrchestrator.CacheEventId).Data.PageById("cache");
            for (int i = 0; i < page.Choices.Length; i++)
            {
                if (page.Choices[i].Effects.Any(e => e.Kind == EventEffectKind.BankGold && e.Amount == percent)) return i;
            }

            throw new AssertionException($"fixture: the cache page has no {percent}% bank choice");
        }

        [Test]
        public void ArrivingOpensTheCacheAndDoesNotClearTheRoom()
        {
            ArriveAtTheCache(gold: 100);

            var node = RealmPaths.FirstOfType(RunManager.Map, RoomType.Cache);
            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, node.Id);
            Assert.AreEqual("cache", RunManager.Run.eventPageId);
        }

        [Test]
        public void BankingHalfMovesHalfThePurseIntoTheBank()
        {
            ArriveAtTheCache(gold: 101);

            var result = RunOrchestrator.ChooseEventOption(BankIndex(50), "cache");

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual(51, RunManager.Run.gold);
            Assert.AreEqual(50, RunManager.Run.bankedGold);
        }

        [Test]
        public void BankingAllEmptiesThePurse()
        {
            ArriveAtTheCache(gold: 37);

            RunOrchestrator.ChooseEventOption(BankIndex(100), "cache");

            Assert.AreEqual(0, RunManager.Run.gold);
            Assert.AreEqual(37, RunManager.Run.bankedGold);
        }

        [Test]
        public void ANearlyEmptyPurseStillBanksOneCoinAtAQuarter()
        {
            ArriveAtTheCache(gold: 3);

            RunOrchestrator.ChooseEventOption(BankIndex(25), "cache");

            Assert.AreEqual(2, RunManager.Run.gold);
            Assert.AreEqual(1, RunManager.Run.bankedGold);
        }

        [Test]
        public void WithNoGoldNoBankChoiceIsOffered()
        {
            ArriveAtTheCache(gold: 0);

            var result = RunOrchestrator.ChooseEventOption(BankIndex(100), "cache");

            Assert.AreEqual(EventRefusal.Locked, result.Reason);
            Assert.AreEqual(0, RunManager.Run.bankedGold);
        }

        [Test]
        public void ADeathAfterBankingKeepsTheBankAndLosesThePurse()
        {
            ArriveAtTheCache(gold: 200);
            RunOrchestrator.ChooseEventOption(BankIndex(50), "cache");
            int walletBefore = SaveSlotManager.CurrentSave.wallet.gold;

            var settlement = RunManager.EndRun();

            Assert.AreEqual(100, settlement.GoldLost);
            Assert.AreEqual(100, settlement.GoldBanked);
            Assert.AreEqual(walletBefore + 100, SaveSlotManager.CurrentSave.wallet.gold);
        }

        [Test]
        public void TheBankSurvivesAReload()
        {
            ArriveAtTheCache(gold: 80);
            RunOrchestrator.ChooseEventOption(BankIndex(25), "cache");

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot).activeRun;

            Assert.AreEqual(20, onDisk.bankedGold);
            Assert.AreEqual(60, onDisk.gold);
        }
    }
}
