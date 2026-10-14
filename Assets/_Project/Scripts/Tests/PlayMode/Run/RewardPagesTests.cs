using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The settlement seam (docs/PLAN_ITEMS.md 1.2): RewardPages.Settle rolls
    // the gear and relic pages off the save, and Take / HoneInstead / Leave
    // apply their outcomes. PlayMode because the pages read ContentDatabase
    // and write the live save.
    public class RewardPagesTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reward-pages-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static readonly EncounterClass[] BossAndTwoAnchors =
            { EncounterClass.Elite, EncounterClass.Boss, EncounterClass.Elite };

        private static SaveData StartedRun()
        {
            RunManager.StartRun(4242UL);
            return SaveSlotManager.CurrentSave;
        }

        private static IReadOnlyList<PendingPage> SettleBoss(SaveData save) =>
            RewardPages.Settle(save, 23, 5, 2, BossAndTwoAnchors, BossAndTwoAnchors, save.ActiveSquadIds());

        private static string Describe(PendingPage page) =>
            string.Join(" | ", page.Cards.Select(c => c.IsGear ? $"{c.RecipientId}:{c.Slot}:{c.Copy}:{c.Rolled}" : c.RelicId));

        [Test]
        public void ABossAndTwoAnchors_SettleIntoOneGearPageThenOneRelicPage()
        {
            var save = StartedRun();
            Assert.IsTrue(RunOrchestrator.AvailableRelicOptions().Any(r => r.Rarity >= RelicRarity.Uncommon),
                "precondition: a fresh save can be offered an Uncommon-or-better relic");

            var pages = SettleBoss(save);

            Assert.AreEqual(2, pages.Count);
            Assert.AreSame(pages, RunManager.Run.pendingPages);

            var gear = pages[0];
            Assert.AreEqual(("boss_gear", RewardPageKind.Gear, 3, 5), (gear.SourceId, gear.Kind, gear.Choices, gear.Cards.Count));
            Assert.IsTrue(gear.Cards.All(c => c.IsGear && save.ActiveSquadIds().Contains(c.RecipientId)), Describe(gear));
            Assert.IsTrue(gear.Cards.All(c => c.Rolled >= Quality.Uncommon), "boss_gear's quality floor");

            var relic = pages[1];
            Assert.AreEqual(("boss_relic", RewardPageKind.Relic, 3), (relic.SourceId, relic.Kind, relic.Choices));
            Assert.IsTrue(relic.Cards.All(c => c.Rarity >= RelicRarity.Uncommon), Describe(relic));
            Assert.LessOrEqual(relic.Cards.Count, 5);
        }

        [Test]
        public void TheSameSettlement_RollsTheSamePages()
        {
            var save = StartedRun();
            string first = Describe(SettleBoss(save)[0]);

            RunManager.Run.legendaryPity = 0;
            RunManager.Run.lastPageBestCommon = false;
            Assert.AreEqual(first, Describe(SettleBoss(save)[0]));
        }

        [Test]
        public void Take_EquipsTheCardOnItsRecipient_AndBagsWhatItDisplaced()
        {
            var save = StartedRun();
            var page = SettleBoss(save)[0];
            var card = page.Cards[0];
            var recipient = save.roster.First(c => c.definitionId == card.RecipientId);
            var before = recipient.equipment.GetInstance(card.Slot);

            Assert.IsTrue(RewardPages.Take(save, page, 0));

            Assert.IsTrue(ItemInstance.SameStack(card.Copy, recipient.equipment.GetInstance(card.Slot)));
            Assert.AreEqual(CardOutcome.Taken, card.Outcome);
            Assert.AreEqual(1, page.Resolved);
            if (before != null)
            {
                Assert.IsTrue(ItemInstance.SameStack(before, card.Displaced));
                Assert.AreEqual(1, InventoryOps.CountAt(save.stockpiledItems, before));
            }

            Assert.IsFalse(RewardPages.Take(save, page, 0), "a card is taken once");
        }

        [Test]
        public void HoneInstead_HonesTheCardsSlotForItsRecipient()
        {
            var save = StartedRun();
            var page = SettleBoss(save)[0];
            var card = page.Cards[1];

            Assert.AreEqual(RewardPages.HoneInsteadResult.Hone, RewardPages.HoneInstead(save, page, 1));

            Assert.AreEqual(1, SlotHoning.PlusOf(RunManager.Run.slotHoning, card.RecipientId, card.Slot));
            Assert.AreEqual(CardOutcome.Honed, card.Outcome);
        }

        [Test]
        public void HoneInstead_WithEveryShownSlotAtMaxPlus_PaysTheSalePrice()
        {
            var save = StartedRun();
            var page = SettleBoss(save)[0];
            foreach (var c in page.Cards)
            {
                while (SlotHoning.CanHone(RunManager.Run.slotHoning, c.RecipientId, c.Slot))
                {
                    SlotHoning.Hone(RunManager.Run.slotHoning, c.RecipientId, c.Slot);
                }
            }

            int gold = RunManager.Run.gold;
            Assert.AreEqual(RewardPages.HoneInsteadResult.Gold, RewardPages.HoneInstead(save, page, 0));

            Assert.Greater(page.Cards[0].GoldPaid, 0);
            Assert.AreEqual(gold + page.Cards[0].GoldPaid, RunManager.Run.gold);
            Assert.AreEqual(ItemUpgrade.MaxPlus,
                SlotHoning.PlusOf(RunManager.Run.slotHoning, page.Cards[0].RecipientId, page.Cards[0].Slot));
        }

        [Test]
        public void Leave_HonesTheFirstCardsSlotOncePerUnusedChoice()
        {
            var save = StartedRun();
            var page = SettleBoss(save)[0];
            var first = page.Cards[0];

            RewardPages.Take(save, page, 2);
            RewardPages.Leave(save, page);

            Assert.AreEqual(2, page.UnusedHones);
            Assert.AreEqual(2, SlotHoning.PlusOf(RunManager.Run.slotHoning, first.RecipientId, first.Slot));
            Assert.AreEqual(0, page.ChoicesLeft);
            Assert.IsTrue(page.Closed);
        }

        [Test]
        public void LeavingARelicPage_PaysEachUnusedChoice()
        {
            var save = StartedRun();
            var relic = SettleBoss(save)[1];
            Assert.IsNotEmpty(relic.Cards, "precondition: the boss relic page shows something");

            RewardPages.Take(save, relic, 0);
            CollectionAssert.Contains(RunManager.Run.relicIds, relic.Cards[0].RelicId);

            int gold = RunManager.Run.gold;
            int paid = RewardPages.Leave(save, relic);

            Assert.Greater(paid, 0);
            Assert.AreEqual(paid % 2, 0, "two unused choices pay the same amount each");
            Assert.AreEqual(gold + paid, RunManager.Run.gold);
        }
    }
}
