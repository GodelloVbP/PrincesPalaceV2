using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // WALKING INTO A SHOP, AND THE SEAM THE SHELF IS ROLLED THROUGH.
    //
    // Two subjects, one fixture, because both need a real map and real
    // content: what ArriveAt answers for a Shop node, and that the shop's
    // gear roll is the SAME three-axis roll the reward path uses.
    public class ShopArrivalTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-arrival-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A seed whose first realm holds a shop, found by generating maps
        // directly rather than by starting thousands of runs -- the generator
        // is pure and reads no content, so this costs nothing and needs no
        // save writes. Shops sit only in pockets, so each test walks the
        // party up to it (RealmPaths.StandBefore). The search is bounded so a
        // profile that removed shops fails here loudly rather than hangs.
        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));
                if (RealmPaths.FirstOfType(map, RoomType.Shop) != null) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        [Test]
        public void ArrivingAtAShopRollsTheShelfAndDoesNotClearTheRoom()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);

            var shop = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
            RealmPaths.StandBefore(shop);
            var arrival = RunOrchestrator.ArriveAt(shop);

            Assert.AreEqual(RunOrchestrator.Arrival.Shop, arrival);

            // A shop is the second room that leads to a screen, so it must
            // NOT clear itself the way every other non-fight room does.
            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, shop.Id);

            Assert.AreEqual(shop.Id, RunManager.Run.shopNodeId);
            Assert.IsNotEmpty(RunManager.Run.shopStock);
            Assert.AreEqual(ShopStock.GearCount + ShopStock.BookCount + ShopStock.RelicCount,
                RunManager.Run.shopStock.Count);
        }

        // The shelf is on disk before the player can see it, which is what
        // makes quitting inside a shop unable to reroll it. Read back off the
        // file rather than off the cached SaveData, since the cache is the
        // thing that would hide a missing write.
        [Test]
        public void TheShelfIsPersistedOnArrival()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);
            var shop = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
            RealmPaths.StandBefore(shop);
            RunOrchestrator.ArriveAt(shop);

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot);

            Assert.AreEqual(shop.Id, onDisk.activeRun.shopNodeId);
            CollectionAssert.AreEqual(
                RunManager.Run.shopStock.Select(e => e.contentId + ":" + e.price).ToList(),
                onDisk.activeRun.shopStock.Select(e => e.contentId + ":" + e.price).ToList());
        }

        [Test]
        public void LeavingAShopClearsTheRoomTheWayEveryOtherRoomDoes()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);
            var shop = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
            RealmPaths.StandBefore(shop);
            RunOrchestrator.ArriveAt(shop);

            RunOrchestrator.LeaveShop();

            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, shop.Id);
            Assert.IsEmpty(RunManager.Run.shopStock);
        }

        // ---- the extracted roll -------------------------------------------------

        // The shop's gear shelf is the same tier-targeted choice, so one
        // seed rolls one shelf.
        [Test]
        public void TheShopGearShelfIsAPureFunctionOfItsStream()
        {
            const ulong Seed = 13579UL;
            const int Depth = 24;

            var shelf = ShopStock.RollGear(ItemOfferRoll.Candidates(), Depth, ItemOfferRoll.MaxTier, Draw(Seed));
            var repeat = ShopStock.RollGear(ItemOfferRoll.Candidates(), Depth, ItemOfferRoll.MaxTier, Draw(Seed));

            CollectionAssert.AreEqual(
                shelf.Select(e => e.contentId + "@" + e.tier + ":" + e.price).ToList(),
                repeat.Select(e => e.contentId + "@" + e.tier + ":" + e.price).ToList());

            Assert.IsTrue(shelf.All(e => e.noOffer || e.price > 0));
        }

        private static Func<int, int> Draw(ulong seed)
        {
            var rng = new SeededRandom(seed);
            return bound => rng.NextInt(0, bound);
        }
    }
}
