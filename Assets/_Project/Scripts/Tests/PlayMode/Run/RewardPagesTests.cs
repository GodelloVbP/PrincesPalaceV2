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

        // Owner decision Q6: the top quality of any slot carries a trait. The
        // seventh gear page without a Legendary makes its best card one, so
        // priming the pity counter forces a Legendary on every seed.
        // An empty Weapon1 always takes the weapon card; armed members draw
        // across every slot.
        private static void ArmEveryone(SaveData save)
        {
            var weapon = ContentDatabase.Offerable.First(i => i.equipSlot == EquipmentSlot.Weapon1);
            foreach (var member in save.ActiveSquad()) member.equipment.Set(EquipmentSlot.Weapon1, weapon.id, tier: 0);
        }

        private static IEnumerable<PendingCard> LegendaryCards(ulong seed, out SaveData save)
        {
            RunManager.ResetForTests();
            RunManager.StartRun(seed);
            save = SaveSlotManager.CurrentSave;
            ArmEveryone(save);
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            return SettleBoss(save)[0].Cards.Where(c => c.Rolled == Quality.Legendary).ToList();
        }

        [Test]
        public void ALegendaryCard_CarriesOneUsableNonDuplicateTraitFromItsSlotsPool()
        {
            int weapon = 0, armour = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                foreach (var card in LegendaryCards(seed, out var save))
                {
                    var traits = ContentDatabase.TraitRowsOf(card.Copy);
                    Assert.AreEqual(1, traits.Count, $"seed {seed}: {card.Copy}");
                    Assert.AreEqual(Quality.Legendary, ContentDatabase.QualityOf(card.Copy), $"seed {seed}");
                    Assert.AreEqual(2, card.Copy.ModifierIds.Count - 1, $"seed {seed}: two affixes beside the trait");

                    var trait = traits[0].Data;
                    bool isWeapon = EquipmentSlots.IsWeaponSlot(card.Slot);
                    Assert.AreEqual(isWeapon ? Domain.Combat.AffixPool.Weapon : Domain.Combat.AffixPool.Armour, trait.Pool,
                        $"seed {seed}: {trait.Id} on {card.Slot}");
                    if (isWeapon) weapon++; else armour++;

                    var owner = save.roster.First(c => c.definitionId == card.RecipientId);
                    var kit = ContentDatabase.AvailableSkillsFor(owner).Select(k => k.Data.Effect).ToList();
                    Assert.IsTrue(TraitRequirements.AllMet(trait.Requires, kit), $"seed {seed}: {trait.Id} is unusable by {owner.definitionId}");

                    // Not a mechanic the owner already gets from a relic or
                    // from another worn copy.
                    var kind = trait.Effects[0].Kind;
                    foreach (var entry in owner.equipment.EquippedEntries().Where(e => e.slot != card.Slot))
                    {
                        Assert.IsFalse(ContentDatabase.TraitRowsOf(entry.Instance).Any(t => t.Data.Effects[0].Kind == kind),
                            $"seed {seed}: {kind} duplicates a worn copy on {owner.definitionId}");
                    }
                }
            }

            Assert.Greater(weapon, 0, "the sweep must reach a weapon Legendary");
            Assert.Greater(armour, 0, "the sweep must reach an armour Legendary");
        }

        [Test]
        public void ALegendaryPage_IsPinnedOnAFixedSeed()
        {
            LegendaryCards(4242UL, out var save);
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            RunManager.Run.lastPageBestCommon = false;

            Assert.AreEqual(
                "sheep:Weapon2:staff t2 [frostbite,entangling]:Rare | bear:Head:runeplate_runehelm t2 [swift,runic]:Rare | " +
                "owl:Necklace:amulet_pendant t2 [wardrune,bulwark,trait_tinfoil_pipe]:Legendary | " +
                "sheep:Head:vellum_hood t2 [stalwart,bulwark,trait_tinfoil_pipe]:Legendary | " +
                "bear:Gloves:brigandine_gloves t2 [manaflow]:Uncommon",
                Describe(SettleBoss(save)[0]));
        }

        [Test]
        public void ATraitTheOwnerAlreadyGetsFromARelic_IsNotOffered()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                RunManager.ResetForTests();
                RunManager.StartRun(seed);
                var save = SaveSlotManager.CurrentSave;
                // Every weapon trait held as a relic by the whole party would
                // leave none to draw; the one relic below removes exactly one.
                RunManager.Run.relicIds.Add("charging_crystal");
                RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;

                foreach (var card in SettleBoss(save)[0].Cards)
                {
                    Assert.IsFalse(ContentDatabase.TraitRowsOf(card.Copy).Any(t => t.id == "trait_charging_crystal"),
                        $"seed {seed}: {card.Copy} offers a trait its owner already has from the relic");
                }
            }
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
