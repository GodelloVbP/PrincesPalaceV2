using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The bot's two honing rules (GearEvaluator.TakeRest, HoneAtTheShop).
    // Prices are the step-8 literals HoningServicesTests pins: 37 at +0, 49 at +1.
    public class BotHoningTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-bothone-" + Guid.NewGuid().ToString("N"));
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

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static string Member() => Save.ActiveSquad().First().definitionId;

        // Bare hands score every hone at zero, so the member wears one weapon:
        // honing the main hand is then the only +1 that moves a number.
        private static void WieldAWeapon()
        {
            var weapon = ContentDatabase.Items.First(i => i != null && i.kind == ItemKind.Weapon && i.IsEquippable);
            var character = Save.ActiveSquad().First();
            character.equipment.Clear();
            character.equipment.Set(EquipmentSlot.Weapon1, weapon.id);
        }

        private static void OpenShop()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));
                if (RealmPaths.FirstOfType(map, RoomType.Shop) == null) continue;

                RunManager.StartRun(seed);
                var shop = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
                RealmPaths.StandBefore(shop);
                Assert.AreEqual(RunOrchestrator.Arrival.Shop, RunOrchestrator.ArriveAt(shop));
                return;
            }

            throw new AssertionException("No seed under 4000 generated a Shop.");
        }

        private static void OpenRest()
        {
            RunManager.StartRun(1UL);
            var rest = RunManager.Choices().First();
            rest.Type = RoomType.Rest;
            Assert.AreEqual(RunOrchestrator.Arrival.Rest, RunOrchestrator.ArriveAt(rest));
        }

        // ---- Rest ---------------------------------------------------------------

        [Test]
        public void ARestUnderSeventyPercentHpHeals()
        {
            OpenRest();
            RunManager.Run.currentHealth.Clear();
            RunManager.Run.currentHealth.Add(new RunHealthEntry { characterId = Member(), hp = 1 });

            Assert.AreEqual("heal", GearEvaluator.TakeRest(Save, GearWeights.Aggressive, 0.69f));
            Assert.AreEqual(0, RunManager.Run.slotHoning.Count);
        }

        [Test]
        public void ARestAtSeventyPercentHpHonesTheBestSlotOfTheBestMember()
        {
            OpenRest();
            WieldAWeapon();
            Assert.IsTrue(GearEvaluator.BestHone(Save, GearWeights.Aggressive, out string id, out var slot, out _));

            string taken = GearEvaluator.TakeRest(Save, GearWeights.Aggressive, 0.7f);

            Assert.AreEqual("hone:" + id + ":" + slot, taken);
            Assert.AreEqual("hone:" + Member() + ":Weapon1", taken);
            Assert.AreEqual(1, SlotHoning.PlusOf(RunManager.Run.slotHoning, id, slot));
            Assert.IsFalse(RunOrchestrator.RestIsOpen);
        }

        [Test]
        public void ARestWithEverySlotAtTheCapHeals()
        {
            OpenRest();
            foreach (var member in Save.ActiveSquad())
            {
                foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                {
                    for (int i = 0; i < ItemUpgrade.MaxPlus; i++) SlotHoning.Hone(RunManager.Run.slotHoning, member.definitionId, slot);
                }
            }

            Assert.AreEqual("heal", GearEvaluator.TakeRest(Save, GearWeights.Aggressive, 1f));
        }

        // ---- Shop ---------------------------------------------------------------

        [Test]
        public void TheShopHonesWhileTheGainPerGoldBeatsTheBestCardAndTheGoldHolds()
        {
            OpenShop();
            WieldAWeapon();
            RunManager.Run.step = 8;
            RunManager.Run.gold = 100;

            var bought = GearEvaluator.HoneAtTheShop(Save, GearWeights.Aggressive, () => 0f);

            // 37 + 49 = 86; the third costs more than the 14 left.
            Assert.AreEqual(2, bought.Count);
            StringAssert.EndsWith(":37", bought[0]);
            StringAssert.EndsWith(":49", bought[1]);
            Assert.AreEqual(14, RunManager.Run.gold);
        }

        [Test]
        public void TheShopSkipsAHoneThatALiveCardBeatsPerGold()
        {
            OpenShop();
            RunManager.Run.step = 8;
            RunManager.Run.gold = 1000;

            var bought = GearEvaluator.HoneAtTheShop(Save, GearWeights.Aggressive, () => 1000f);

            Assert.AreEqual(0, bought.Count);
            Assert.AreEqual(1000, RunManager.Run.gold);
        }

        [Test]
        public void TheShopBuysNoHoneBelowItsPrice()
        {
            OpenShop();
            RunManager.Run.step = 8;
            RunManager.Run.gold = 36;

            Assert.AreEqual(0, GearEvaluator.HoneAtTheShop(Save, GearWeights.Aggressive, () => 0f).Count);
            Assert.AreEqual(36, RunManager.Run.gold);
        }
    }
}
