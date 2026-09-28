using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using UnityEngine;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // M4 OF docs/PLAN_EVENTS_BELL_AND_CARAVAN.md THROUGH THE REAL RUN: save 8,
    // provenance on disk, the Reconcile rule for a worn-out fake, the satchel
    // spending the exact stack pressed, and a fake piece of gear wearing out in
    // SettleFight -- only on a fielded member's body, breaking on the third
    // completed fight with the notice on the settlement.
    //
    // FIXTURE INSTANCES: the caravan that sells fakes is M5, so every lot and
    // fake here is stamped by hand. Room fights are built through
    // RunOrchestrator.BuildFight and settled directly with `won`, the way
    // EventFightRunTests settles where only the settlement is under test.
    // Expected values are literals (CLAUDE.md gotcha 5).
    public class FakeItemRunTests
    {
        private const string Torso = "leather_torso_p0";
        private const string TorsoName = "Ragged Leather Torso";
        private const string Potion = "health_potion";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-fake-items-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            LogAssert.ignoreFailingMessages = false;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static Character Member(string id) => Save.roster.First(c => c.definitionId == id);

        private static ItemInstance FakeTorso(string lot = "lot-a") =>
            new ItemInstance(Torso, provenance: FakeWear.NewFakeGear(lot));

        private static ItemInstance LotPotion(string lot, bool fake) =>
            new ItemInstance(Potion, provenance: new Provenance(lot, fake));

        private static RunOrchestrator.FightSettlement SettleARoomFight()
        {
            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "fixture: the room fight did not build");
            return RunOrchestrator.SettleFight(built.Session, won: true);
        }

        private static SaveData ReloadFromDisk()
        {
            SaveSlotManager.Forget();
            return SaveSlotManager.CurrentSave;
        }

        // ---- save 8 --------------------------------------------------------------------

        [Test]
        public void TheSaveIsVersionEight()
        {
            Assert.AreEqual(8, SaveData.CurrentVersion);
        }

        [Test]
        public void ASaveFromVersionNine_IsRefused()
        {
            var save = SaveData.CreateNew();
            save.version = 9;

            Assert.IsFalse(save.Migrate());
        }

        [Test]
        public void AVersionSevenSave_LoadsAsEight_Unchanged()
        {
            var save = SaveData.CreateNew();
            InventoryOps.Add(save.stockpiledItems, Potion, 2);
            InventoryOps.Add(save.stockpiledItems, Torso, 1, plus: 3);
            save.roster.First(c => c.definitionId == "sheep").equipment.Set(EquipmentSlot.Head, "leather_coif_p0", plus: 1);

            // A version-7 file: no `provenance` anywhere, version 7.
            string json = JsonUtility.ToJson(save);
            json = Regex.Replace(json, ",\"provenance\":\\{[^}]*\\}", "");
            json = json.Replace("\"version\":8", "\"version\":7");
            StringAssert.DoesNotContain("provenance", json, "fixture: the v7 file still names provenance");

            var loaded = JsonUtility.FromJson<SaveData>(json);
            Assert.AreEqual(7, loaded.version, "fixture");

            Assert.IsTrue(loaded.Migrate(), "a version-7 save was refused");
            Assert.AreEqual(8, loaded.version);

            Assert.AreEqual(2, loaded.stockpiledItems.Count);
            Assert.AreEqual(Potion, loaded.stockpiledItems[0].itemId);
            Assert.AreEqual(2, loaded.stockpiledItems[0].count);
            Assert.AreEqual(Torso, loaded.stockpiledItems[1].itemId);
            Assert.AreEqual(3, loaded.stockpiledItems[1].plus);
            Assert.IsFalse(loaded.stockpiledItems.Any(e => e.provenance.HasLot || e.provenance.fake));

            var head = loaded.roster.First(c => c.definitionId == "sheep").equipment.GetInstance(EquipmentSlot.Head);
            Assert.AreEqual("leather_coif_p0", head.ItemId);
            Assert.AreEqual(1, head.Plus);
            Assert.IsFalse(head.HasLot);
        }

        [Test]
        public void Provenance_SurvivesTheSaveFile_InTheBagAndOnTheBody()
        {
            var save = SaveData.CreateNew();
            InventoryOps.Add(save.stockpiledItems, LotPotion("lot-p", fake: true));
            save.roster.First(c => c.definitionId == "sheep").equipment.Put(EquipmentSlot.Torso, FakeTorso("lot-t"));

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.AreEqual("lot-p", loaded.stockpiledItems[0].provenance.lot);
            Assert.IsTrue(loaded.stockpiledItems[0].provenance.fake);
            var worn = loaded.roster.First(c => c.definitionId == "sheep").equipment.GetInstance(EquipmentSlot.Torso);
            Assert.AreEqual("lot-t", worn.Lot);
            Assert.IsTrue(worn.IsFake);
            Assert.AreEqual(3, worn.FightsLeft);
        }

        // ---- Reconcile ------------------------------------------------------------------

        [Test]
        public void Reconcile_RemovesAWornOutFake_FromTheBagAndTheBody_AndNothingElse()
        {
            var save = SaveData.CreateNew();
            InventoryOps.Add(save.stockpiledItems, new ItemInstance(Torso, provenance: new Provenance("lot-0", true, 0)));
            InventoryOps.Add(save.stockpiledItems, new ItemInstance(Torso, provenance: new Provenance("lot-1", true, 1)));
            InventoryOps.Add(save.stockpiledItems, LotPotion("lot-p", fake: true));
            var body = save.roster.First(c => c.definitionId == "sheep").equipment;
            body.Put(EquipmentSlot.Torso, new ItemInstance(Torso, provenance: new Provenance("lot-w", true, 0)));

            save.Reconcile();

            CollectionAssert.AreEqual(new[] { "lot-1", "lot-p" },
                save.stockpiledItems.Select(e => e.provenance.lot).ToArray());
            Assert.AreEqual("", body.Get(EquipmentSlot.Torso), "a worn-out fake is gone, not handed back to the bag");
        }

        // ---- the satchel ----------------------------------------------------------------

        [Test]
        public void TheSatchel_CarriesEachStacksInstance_AndSpendsExactlyThePressedOne()
        {
            RunManager.StartRun(61UL);
            Save.stockpiledItems.Clear();
            InventoryOps.Add(Save.stockpiledItems, Potion, 1);
            InventoryOps.Add(Save.stockpiledItems, LotPotion("lot-f", fake: true));
            InventoryOps.Add(Save.stockpiledItems, LotPotion("lot-g", fake: false));

            var satchel = RunOrchestrator.BuildSatchel();
            Assert.AreEqual(3, satchel.Count, "lots never merge, with each other or the ordinary stack");
            var fake = satchel.Single(s => s.IsFake).Instance;

            RunOrchestrator.SpendConsumable(fake);

            CollectionAssert.AreEqual(new[] { "", "lot-g" }, Save.stockpiledItems.Select(e => e.provenance.lot).ToArray());

            RunOrchestrator.SpendConsumable(new ItemInstance(Potion));

            CollectionAssert.AreEqual(new[] { "lot-g" }, Save.stockpiledItems.Select(e => e.provenance.lot).ToArray(),
                "spending the ordinary potion never takes the genuine lot in its place");
        }

        // ---- wearing out in SettleFight ------------------------------------------------

        [Test]
        public void AWornFieldedFake_BreaksOnTheThirdCompletedFight_NotTheSecond()
        {
            RunManager.StartRun(71UL);
            Member("sheep").equipment.Put(EquipmentSlot.Torso, FakeTorso());

            var first = SettleARoomFight();
            Assert.IsEmpty(first.Notices);
            Assert.AreEqual(2, ReloadFromDisk().roster.First(c => c.definitionId == "sheep")
                .equipment.GetInstance(EquipmentSlot.Torso).FightsLeft, "the countdown is on disk");

            var second = SettleARoomFight();
            Assert.IsEmpty(second.Notices);
            Assert.AreEqual(Torso, Member("sheep").equipment.Get(EquipmentSlot.Torso), "still worn after the second");

            var third = SettleARoomFight();

            CollectionAssert.AreEqual(new[] { "Ragged Leather Torso falls apart.", "No refunds." }, third.Notices.ToArray());
            if (third.Reward != null)
            {
                CollectionAssert.AreEqual(new[] { "Ragged Leather Torso falls apart.", "No refunds." },
                    third.Reward.Notices.ToArray(), "the Reckoning reads them off the reward");
            }

            var reloaded = ReloadFromDisk();
            Assert.AreEqual("", reloaded.roster.First(c => c.definitionId == "sheep").equipment.Get(EquipmentSlot.Torso));
            Assert.AreEqual(0, InventoryOps.Count(reloaded.stockpiledItems, Torso), "no refund to the bag");
        }

        [Test]
        public void AFakeInTheBag_DoesNotWear()
        {
            RunManager.StartRun(71UL);
            Save.stockpiledItems.Clear();
            InventoryOps.Add(Save.stockpiledItems, FakeTorso());

            for (int i = 0; i < 3; i++) Assert.IsEmpty(SettleARoomFight().Notices);

            Assert.AreEqual(1, Save.stockpiledItems.Count);
            Assert.AreEqual(3, Save.stockpiledItems[0].provenance.fightsLeft);
        }

        [Test]
        public void ABenchedMembersFake_DoesNotWear()
        {
            RunManager.StartRun(71UL);
            Save.selectedCharacterIds = new List<string> { "sheep", "bear" };
            Member("owl").equipment.Put(EquipmentSlot.Torso, FakeTorso());

            for (int i = 0; i < 3; i++)
            {
                var built = RunOrchestrator.BuildFight();
                Assert.IsNotNull(built, "fixture: the room fight did not build");
                Assert.AreEqual(2, built.Session.Encounter.PlayerParty.Count, "fixture: Odette is benched");
                Assert.IsEmpty(RunOrchestrator.SettleFight(built.Session, won: true).Notices);
            }

            Assert.AreEqual(3, Member("owl").equipment.GetInstance(EquipmentSlot.Torso).FightsLeft);
        }

        [Test]
        public void AGenuineLotPiece_NeverWears()
        {
            RunManager.StartRun(71UL);
            Member("sheep").equipment.Put(EquipmentSlot.Torso,
                new ItemInstance(Torso, provenance: new Provenance("lot-g")));

            for (int i = 0; i < 4; i++) Assert.IsEmpty(SettleARoomFight().Notices);

            Assert.AreEqual(Torso, Member("sheep").equipment.Get(EquipmentSlot.Torso));
        }
    }
}
