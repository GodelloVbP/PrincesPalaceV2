using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // HONING'S TWO SERVICES (docs/PLAN_ITEMS.md 1.5): the shop sells +1s, a
    // Rest room gives one free instead of the heal. Both write
    // RunSnapshot.slotHoning; prices are pinned literals from
    // ShopPricing.HoneAuthored (21 x (1 + 0.35 x plus)) through ScalePrice.
    public class HoningServicesTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-honing-" + Guid.NewGuid().ToString("N"));
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

        private static ulong SeedWith(RoomType type)
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));
                if (RealmPaths.FirstOfType(map, type) != null) return seed;
            }

            throw new AssertionException($"No seed under 4000 generated a {type} in the first column.");
        }

        private static string Member() => SaveSlotManager.CurrentSave.ActiveSquad().First().definitionId;

        private static void OpenShop()
        {
            RunManager.StartRun(SeedWith(RoomType.Shop));
            var shop = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
            RealmPaths.StandBefore(shop);
            Assert.AreEqual(RunOrchestrator.Arrival.Shop, RunOrchestrator.ArriveAt(shop));
        }

        // The generator puts a Rest only where the realm profile allows one,
        // which realm 0 does not, so the first reachable room is made one.
        private static RealmNode OpenRest()
        {
            RunManager.StartRun(1UL);
            var rest = RunManager.Choices().First();
            rest.Type = RoomType.Rest;
            Assert.AreEqual(RunOrchestrator.Arrival.Rest, RunOrchestrator.ArriveAt(rest));
            return rest;
        }

        // ---- the shop ---------------------------------------------------------

        [Test]
        public void TheShopSellsHoneAtThePriceOfTheSlotsPlusAndTheStep()
        {
            OpenShop();
            var run = RunManager.Run;
            string id = Member();

            run.step = 8;
            Assert.AreEqual(37, RunOrchestrator.HonePriceFor(id, EquipmentSlot.Head), "+0 at step 8");

            run.gold = 1000;
            var first = RunOrchestrator.BuyHone(id, EquipmentSlot.Head);
            Assert.AreEqual(ShopOutcome.Ok, first.Outcome);
            Assert.AreEqual(-37, first.GoldDelta);
            Assert.AreEqual(963, run.gold);
            Assert.AreEqual(1, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Head));

            Assert.AreEqual(49, RunOrchestrator.HonePriceFor(id, EquipmentSlot.Head), "+1 at step 8");

            run.step = 16;
            Assert.AreEqual(66, RunOrchestrator.HonePriceFor(id, EquipmentSlot.Weapon2), "+0 at step 16");
        }

        [Test]
        public void HoneIsPricedBySlotNotByWhatTheSlotHolds()
        {
            OpenShop();
            RunManager.Run.step = 8;
            string id = Member();

            // A filled slot and an empty one cost the same at the same plus.
            Assert.AreEqual(RunOrchestrator.HonePriceFor(id, EquipmentSlot.Weapon1),
                RunOrchestrator.HonePriceFor(id, EquipmentSlot.Necklace));
        }

        [Test]
        public void ARefusedHoneChargesNothing()
        {
            OpenShop();
            var run = RunManager.Run;
            string id = Member();
            run.step = 8;

            run.gold = 36;
            var poor = RunOrchestrator.BuyHone(id, EquipmentSlot.Head);
            Assert.AreEqual(ShopRefusal.NotEnoughGold, poor.Reason);
            Assert.AreEqual(36, run.gold);
            Assert.AreEqual(0, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Head));

            run.gold = 100000;
            for (int i = 0; i < ItemUpgrade.MaxPlus; i++)
            {
                Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.BuyHone(id, EquipmentSlot.Head).Outcome);
            }

            int before = run.gold;
            var capped = RunOrchestrator.BuyHone(id, EquipmentSlot.Head);
            Assert.AreEqual(ShopRefusal.FullyHoned, capped.Reason);
            Assert.AreEqual(before, run.gold);
            Assert.AreEqual(10, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Head));
        }

        [Test]
        public void ABoughtHoneSurvivesAReload()
        {
            OpenShop();
            var run = RunManager.Run;
            string id = Member();
            run.gold = 1000;

            RunOrchestrator.BuyHone(id, EquipmentSlot.Gloves);
            RunOrchestrator.BuyHone(id, EquipmentSlot.Gloves);

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot);
            Assert.AreEqual(2, SlotHoning.PlusOf(onDisk.activeRun.slotHoning, id, EquipmentSlot.Gloves));
        }

        // ---- the Rest room ----------------------------------------------------

        [Test]
        public void ARestRoomOpensUnspentAndHealsNothingOnArrival()
        {
            var rest = OpenRest();
            var run = RunManager.Run;

            Assert.IsTrue(RunOrchestrator.RestIsOpen);
            CollectionAssert.DoesNotContain(run.clearedNodeIds, rest.Id);
            Assert.IsTrue(SaveSystem.Load(SaveSlotManager.CurrentSlot).activeRun.restOpen, "open on disk, so a reload reopens it");
        }

        [Test]
        public void RestingAHoneIsFreeGivesOnePlusAndSpendsTheRoom()
        {
            var rest = OpenRest();
            var run = RunManager.Run;
            string id = Member();
            run.gold = 50;

            Assert.IsTrue(RunOrchestrator.RestHone(id, EquipmentSlot.Torso));

            Assert.AreEqual(1, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Torso));
            Assert.AreEqual(50, run.gold, "free");
            Assert.IsFalse(RunOrchestrator.RestIsOpen);
            CollectionAssert.Contains(run.clearedNodeIds, rest.Id);

            Assert.IsFalse(RunOrchestrator.RestHone(id, EquipmentSlot.Torso), "one pick per Rest");
            Assert.IsFalse(RunOrchestrator.RestHeal(), "and not both");
            Assert.AreEqual(1, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Torso));

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot);
            Assert.AreEqual(1, SlotHoning.PlusOf(onDisk.activeRun.slotHoning, id, EquipmentSlot.Torso));
            Assert.IsFalse(onDisk.activeRun.restOpen);
        }

        [Test]
        public void RestingAHealRestoresThePartyAndGivesNoHone()
        {
            var rest = OpenRest();
            var run = RunManager.Run;
            string id = Member();

            run.currentHealth.Clear();
            run.currentHealth.Add(new RunHealthEntry { characterId = id, hp = 1 });

            Assert.IsTrue(RunOrchestrator.RestHeal());

            Assert.Greater(run.currentHealth.First(e => e.characterId == id).hp, 1);
            Assert.AreEqual(0, SlotHoning.PlusOf(run.slotHoning, id, EquipmentSlot.Torso));
            CollectionAssert.Contains(run.clearedNodeIds, rest.Id);
            Assert.IsFalse(RunOrchestrator.RestHone(id, EquipmentSlot.Torso), "a heal spends the room");
        }

        [Test]
        public void ARestHoneRefusesASlotAtTheCap()
        {
            OpenRest();
            var run = RunManager.Run;
            string id = Member();
            for (int i = 0; i < ItemUpgrade.MaxPlus; i++) SlotHoning.Hone(run.slotHoning, id, EquipmentSlot.Legs);

            Assert.IsFalse(RunOrchestrator.RestHone(id, EquipmentSlot.Legs));
            Assert.IsTrue(RunOrchestrator.RestIsOpen, "a refused pick leaves the room open");
        }
    }
}
