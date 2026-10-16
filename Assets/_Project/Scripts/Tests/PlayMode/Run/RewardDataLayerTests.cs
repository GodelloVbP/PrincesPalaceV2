using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The data layer under the reward screens (docs/PLAN_REWARD_SCREENS.md
    // R1): the settlement record (C12), PreviewLeave and HoningPreview (C13),
    // the shared compare rows (C5) and the live-upgrade predicate. All of it
    // reads; none of it may change a roll, a save or a payout.
    public class RewardDataLayerTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reward-data-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- C12: the settlement record --------------------------------------------

        private static RunOrchestrator.FightSettlement Settle(RoomType type, PrizeKind prize)
        {
            RunManager.StartRun(4242UL);
            var node = RunManager.Map.Nodes.Where(n => n.Type == type).ElementAt(0);
            node.Prize = prize;
            RunManager.Run.currentNodeId = node.Id;
            var session = RunOrchestrator.BuildFight().Session;
            foreach (var enemy in session.Encounter.Enemies) enemy.CurrentHealth = 0;
            if (session.Encounter.Leader != null) session.Encounter.Leader.CurrentHealth = 0;
            session.Begin();
            return RunOrchestrator.SettleFight(session, won: true);
        }

        private static string Describe(CombatReward reward) =>
            string.Join(" | ", reward.Prizes.Select(p =>
                $"{p.Shown}->{p.Paid} {p.Amount} [{string.Join(",", p.Ids)}]"));

        [Test]
        public void AGearChip_IsRecordedAsGearWithNoAmount_BeforeTheFightsOwnGold()
        {
            var settled = Settle(RoomType.Fight, PrizeKind.Item);

            Assert.AreEqual("Item->Item 0 [] | None->Gold 1 []", Describe(settled.Reward));
            Assert.AreEqual(EncounterClass.Normal, settled.Reward.EncounterClass);
            Assert.AreEqual(0, settled.Reward.GoldBefore);
            Assert.IsEmpty(settled.Notices, "the record adds no notice");
            Assert.AreEqual(1, RunManager.Run.gold, "the record pays nothing");
        }

        [Test]
        public void AGoldChip_PaysAndIsRecorded_WithItsNoticeUnchanged()
        {
            var settled = Settle(RoomType.Fight, PrizeKind.Gold);

            Assert.AreEqual("Gold->Gold 32 [] | None->Gold 1 []", Describe(settled.Reward));
            CollectionAssert.AreEqual(new[] { "Prize: 32 gold." }, settled.Notices);
            Assert.AreEqual(33, RunManager.Run.gold);
        }

        [Test]
        public void PotionsAndBooks_RecordTheIdsDrawn()
        {
            Assert.AreEqual("Consumable->Consumable 3 [health_potion,mana_potion,mana_potion] | None->Gold 1 []",
                Describe(Settle(RoomType.Fight, PrizeKind.Consumable).Reward));

            Restore();
            UseAThrowawaySaveRoot();
            var book = Settle(RoomType.Fight, PrizeKind.Book);
            Assert.AreEqual("Book->Book 0 [winters_rebuke] | None->Gold 1 []", Describe(book.Reward));
            CollectionAssert.AreEqual(new[] { "Prize: Winter's Rebuke." }, book.Notices);
        }

        [Test]
        public void ARelicChipOnAnAnchor_IsRecordedAsRelic_ClassElite()
        {
            var settled = Settle(RoomType.EliteFight, PrizeKind.Relic);

            Assert.AreEqual("Relic->Relic 0 [] | None->Gold 3 []", Describe(settled.Reward));
            Assert.AreEqual(EncounterClass.Elite, settled.Reward.EncounterClass);
        }

        [Test]
        public void ANodeWithNoChip_RecordsOnlyTheFightsGold()
        {
            Assert.AreEqual("None->Gold 1 []", Describe(Settle(RoomType.Fight, PrizeKind.None).Reward));
        }

        [Test]
        public void AGearChipPastThePageLimit_IsRecordedAsGoldItPaid()
        {
            RunManager.StartRun(4242UL);
            var run = RunManager.Run;
            var node = RunManager.Map.Nodes.Where(n => n.Type == RoomType.Fight).ElementAt(0);
            node.Prize = PrizeKind.Item;
            var chips = new PageChips();
            for (int i = 0; i < PageChips.MaxChips; i++) chips.Gear.Add(EncounterClass.Normal);
            var record = new List<PaidPrize>();

            var notices = RealmPrizes.Pay(SaveSlotManager.CurrentSave, run, node, chips, record);

            Assert.AreEqual(1, record.Count);
            Assert.AreEqual((PrizeKind.Item, PrizeKind.Gold), (record[0].Shown, record[0].Paid));
            Assert.Greater(record[0].Amount, 0);
            Assert.AreEqual(record[0].Amount, run.gold);
            Assert.AreEqual(1, notices.Count);
            Assert.AreEqual(PageChips.MaxChips, chips.Gear.Count, "no chip was added");
        }

        // ---- C13: PreviewLeave ------------------------------------------------------

        private static readonly EncounterClass[] BossAndTwoAnchors =
            { EncounterClass.Elite, EncounterClass.Boss, EncounterClass.Elite };

        private static IReadOnlyList<PendingPage> Pages(out SaveData save)
        {
            RunManager.StartRun(4242UL);
            save = SaveSlotManager.CurrentSave;
            return RewardPages.Settle(save, 23, 5, 2, BossAndTwoAnchors, BossAndTwoAnchors, save.ActiveSquadIds());
        }

        [Test]
        public void PreviewLeave_NamesTheHonesLeaveApplies_AndChangesNothing()
        {
            var pages = Pages(out var save);
            var page = pages[0];
            RewardPages.Take(save, page, 2);
            string before = UnityEngine.JsonUtility.ToJson(RunManager.Run);

            var preview = RewardPages.PreviewLeave(save, page);

            Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(RunManager.Run));
            Assert.AreEqual(1, preview.Hones.Count);
            Assert.AreEqual(("sheep", EquipmentSlot.Weapon1, 2),
                (preview.Hones[0].RecipientId, preview.Hones[0].Slot, preview.Hones[0].Count));
            Assert.AreEqual(2, preview.HonesFor("sheep", EquipmentSlot.Weapon1));
            Assert.AreEqual(0, preview.HonesFor("owl", EquipmentSlot.Weapon1));
            Assert.AreEqual(0, preview.Gold);

            RewardPages.Leave(save, page);
            Assert.AreEqual(2, SlotHoning.PlusOf(RunManager.Run.slotHoning, "sheep", EquipmentSlot.Weapon1));
            Assert.AreEqual(2, page.UnusedHones);
            Assert.AreEqual(0, page.UnusedGold);
        }

        [Test]
        public void Leave_AtOneBelowMax_HonesOnceThenPaysTheFirstCardsSalePrice()
        {
            var pages = Pages(out var save);
            var page = pages[0];
            for (int i = 0; i < ItemUpgrade.MaxPlus - 1; i++) SlotHoning.Hone(RunManager.Run.slotHoning, "sheep", EquipmentSlot.Weapon1);
            int gold = RunManager.Run.gold;

            var preview = RewardPages.PreviewLeave(save, page);
            Assert.AreEqual(1, preview.HonesFor("sheep", EquipmentSlot.Weapon1));
            Assert.AreEqual(200, preview.Gold, "two choices left over, 100 each");

            Assert.AreEqual(200, RewardPages.Leave(save, page));
            Assert.AreEqual(ItemUpgrade.MaxPlus, SlotHoning.PlusOf(RunManager.Run.slotHoning, "sheep", EquipmentSlot.Weapon1));
            Assert.AreEqual((1, 200), (page.UnusedHones, page.UnusedGold));
            Assert.AreEqual(gold + 200, RunManager.Run.gold);
        }

        [Test]
        public void Leave_WithEverySlotAtMax_PaysEveryChoiceAsGold()
        {
            var pages = Pages(out var save);
            var page = pages[0];
            for (int i = 0; i < ItemUpgrade.MaxPlus; i++) SlotHoning.Hone(RunManager.Run.slotHoning, "sheep", EquipmentSlot.Weapon1);

            Assert.AreEqual(0, RewardPages.PreviewLeave(save, page).Hones.Count);
            Assert.AreEqual(300, RewardPages.Leave(save, page));
        }

        [Test]
        public void PreviewLeave_OnARelicPage_MatchesWhatLeavePays()
        {
            var pages = Pages(out var save);
            var relic = pages[1];

            Assert.AreEqual(522, RewardPages.PreviewLeave(save, relic).Gold);
            Assert.AreEqual(522, RewardPages.Leave(save, relic));
        }

        [Test]
        public void PreviewLeave_OfAClosedPage_PaysNothing()
        {
            var pages = Pages(out var save);
            RewardPages.Leave(save, pages[0]);

            var preview = RewardPages.PreviewLeave(save, pages[0]);
            Assert.AreEqual((0, 0), (preview.Hones.Count, preview.Gold));
        }

        // ---- C13: HoningPreview -----------------------------------------------------

        private static Character ArmedSheep(out SaveData save)
        {
            RunManager.StartRun(4242UL);
            save = SaveSlotManager.CurrentSave;
            var sheep = save.roster.First(c => c.definitionId == "sheep");
            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", tier: 0);
            return sheep;
        }

        [Test]
        public void HoningPreview_RaisesEveryReader_AndLeavesTheSlotUntouched()
        {
            var sheep = ArmedSheep(out _);
            string before = UnityEngine.JsonUtility.ToJson(RunManager.Run);

            Assert.AreEqual(10, ContentDatabase.EquippedWeaponPower(sheep));
            using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 3))
            {
                Assert.AreEqual(3, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
                Assert.AreEqual(0, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon2));
                Assert.AreEqual(15, ContentDatabase.EquippedWeaponPower(sheep));
                Assert.IsEmpty(RunManager.Run.slotHoning, "the preview never writes slotHoning");
            }

            Assert.AreEqual(0, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
            Assert.AreEqual(10, ContentDatabase.EquippedWeaponPower(sheep));
            Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(RunManager.Run));
        }

        [Test]
        public void HoningPreview_IsRemovedWhenTheBodyThrows()
        {
            var sheep = ArmedSheep(out _);

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 2))
                {
                    Assert.AreEqual(2, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
                    throw new System.InvalidOperationException("boom");
                }
            });

            Assert.AreEqual(0, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
            Assert.IsEmpty(RunManager.Run.slotHoning);
        }

        [Test]
        public void NestedPreviews_Stack_AndEachUnwindsAlone()
        {
            var sheep = ArmedSheep(out _);
            SlotHoning.Hone(RunManager.Run.slotHoning, "sheep", EquipmentSlot.Weapon1);

            var outer = HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 1);
            var inner = HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 2);
            Assert.AreEqual(4, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));

            outer.Dispose();
            Assert.AreEqual(3, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1), "out of order: only the outer goes");
            inner.Dispose();
            inner.Dispose();
            Assert.AreEqual(1, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
        }

        [Test]
        public void APreview_StaysWithinZeroAndMaxPlus()
        {
            var sheep = ArmedSheep(out _);

            using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 99))
            {
                Assert.AreEqual(ItemUpgrade.MaxPlus, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
            }

            using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, -5))
            {
                Assert.AreEqual(0, ContentDatabase.HoningOf(sheep, EquipmentSlot.Weapon1));
            }
        }

        // ---- C5: the shared compare rows --------------------------------------------

        [Test]
        public void CompareRows_ListOnlyTheStatsThatMove_ForAHelm()
        {
            var sheep = ArmedSheep(out _);
            var helm = ContentDatabase.Offerable.First(i => i.equipSlot == EquipmentSlot.Head);

            var result = ItemCompareRows.Build(sheep, ItemInstance.Of(helm.id, 5),
                ContentDatabase.EffectiveAbilityScores(sheep), PoolCapacityRule.WisdomDerived, hasSignature: false);

            Assert.AreEqual("leather_coif: MaxHealth 280->292; PhysicalDefense 40->46", helm.id + ": " + string.Join("; ", result.Rows.Select(r => $"{r.Stat} {r.Now}->{r.Next}")));
            Assert.IsTrue(result.Comparison.CandidateIsLive);
        }

        // ---- the live-upgrade predicate ---------------------------------------------

        private static PendingCard Card(ItemInstance copy, bool inert = false) =>
            new PendingCard { Copy = copy, RecipientId = "sheep", Slot = EquipmentSlot.Weapon1, Inert = inert };

        [Test]
        public void LiveUpgrade_AGreaterTierCopy_IsLive_ALesserOneIsNot()
        {
            var sheep = ArmedSheep(out var save);

            Assert.IsTrue(LiveUpgrade.Of(save, Card(ItemInstance.Of("sword", 5))));

            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", tier: 5);
            Assert.IsTrue(GearEvaluator.LargestDelta(sheep, ItemInstance.Of("sword", 0), EquipmentSlot.Weapon1, out _, out int delta));
            Assert.Less(delta, 0, "precondition: the swap is a downgrade");
            Assert.IsFalse(LiveUpgrade.Of(save, Card(ItemInstance.Of("sword", 0))));
        }

        [Test]
        public void WornTier_ReadsTheRecipientsOwnSlot_EmptyIsZero()
        {
            var sheep = ArmedSheep(out var save);
            var card = Card(ItemInstance.Of("sword", 0));

            Assert.AreEqual(1, LiveUpgrade.WornTier(save, card), "a plain sword in Weapon1 is Common: tier 1");

            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", new List<string> { "a", "b" });
            Assert.AreEqual(3, LiveUpgrade.WornTier(save, card), "two affixes is Rare: tier 3");

            card.Slot = EquipmentSlot.Weapon2;
            Assert.AreEqual(0, LiveUpgrade.WornTier(save, card), "Weapon2 is empty and is not Weapon1");

            card.Slot = EquipmentSlot.Weapon1;
            card.RecipientId = "owl";
            Assert.AreEqual(0, LiveUpgrade.WornTier(save, card), "another character's Weapon1 is empty");
        }

        [Test]
        public void LiveUpgrade_WithNoStatChange_IsLiveOnlyForAnAffixTheWornCopyLacks()
        {
            var sheep = ArmedSheep(out var save);
            var withTrait = ItemInstance.Of("sword", 0, new[] { "trait_tinfoil_pipe" });
            Assert.IsFalse(GearEvaluator.LargestDelta(sheep, withTrait, EquipmentSlot.Weapon1, out _, out _),
                "precondition: the trait moves no stat");

            Assert.IsTrue(LiveUpgrade.Of(save, Card(withTrait)), "the worn sword lacks the trait");

            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", new List<string> { "trait_tinfoil_pipe" }, tier: 0);
            Assert.IsFalse(LiveUpgrade.Of(save, Card(withTrait)), "the same trait is already worn");
            Assert.IsFalse(LiveUpgrade.Of(save, Card(ItemInstance.Of("sword", 0))), "an identical copy gains nothing");
        }

        [Test]
        public void LiveUpgrade_AnInertCard_IsNeverLive()
        {
            ArmedSheep(out var save);

            Assert.IsFalse(LiveUpgrade.Of(save, Card(ItemInstance.Of("sword", 5), inert: true)));
        }

        [Test]
        public void LiveUpgrade_ARelic_IsLiveWhenItReachesAMemberWhoLacksTheMechanic()
        {
            RunManager.StartRun(4242UL);
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "owl", "bear" };

            // kinship is borne by the sheep alone.
            Assert.IsTrue(LiveUpgrade.Of(save, new PendingCard { RelicId = "kinship" }));
            Assert.IsFalse(LiveUpgrade.Of(save, new PendingCard { RelicId = "kinship", AddsNothingFor = new List<string> { "sheep" } }));

            // charging_crystal reaches all three.
            Assert.IsTrue(LiveUpgrade.Of(save, new PendingCard { RelicId = "charging_crystal", AddsNothingFor = new List<string> { "sheep", "owl" } }));
            Assert.IsFalse(LiveUpgrade.Of(save, new PendingCard { RelicId = "charging_crystal", AddsNothingFor = new List<string> { "sheep", "owl", "bear" } }));

            save.selectedCharacterIds = new List<string> { "owl", "bear" };
            Assert.IsFalse(LiveUpgrade.Of(save, new PendingCard { RelicId = "kinship" }), "the sheep is not fielded: it reaches nobody");
        }
    }
}
