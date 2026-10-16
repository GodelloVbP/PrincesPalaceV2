using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // REAL REWARD PAGES FOR A SCREEN TEST, rolled through the same
    // RewardPages.Settle a won fight uses, against the current save's squad.
    // A Reckoning test asserts what the screen does with a page, so the page is
    // the real thing rather than a hand-built card whose item might not fit.
    internal static class ReckoningPageFixtures
    {
        public const ulong Seed = 639228196442867409UL;

        // A run is started when the save holds none: Settle keys its rolls on
        // the run's seed and Take equips through the run's honing.
        public static IReadOnlyList<PendingPage> Settle(
            IReadOnlyList<EncounterClass> gearChips, IReadOnlyList<EncounterClass> relicChips = null,
            int step = 1, int nodeId = 7, int strength = 0)
        {
            if (SaveSlotManager.CurrentSave.activeRun == null || !SaveSlotManager.CurrentSave.activeRun.hasRun)
            {
                // Opening a realm logs its party weight; a test that asserts no
                // unexpected log must not trip over the fixture's own run.
                LogAssert.Expect(LogType.Log, new Regex(@"\[RealmOpening\]"));
                RunManager.StartRun(Seed);
            }

            var save = SaveSlotManager.CurrentSave;
            var fielded = save.ActiveSquad().Where(c => c != null).Select(c => c.definitionId).ToList();
            return RewardPages.Settle(save, step, nodeId, strength, gearChips,
                relicChips ?? new List<EncounterClass>(), fielded);
        }

        // The page an ordinary won fight leaves: one normal gear chip.
        public static IReadOnlyList<PendingPage> OneGearPage() =>
            Settle(new List<EncounterClass> { EncounterClass.Normal });

        // A boss and two anchors in one settlement, gear and relics both.
        public static IReadOnlyList<PendingPage> BossAndTwoAnchors()
        {
            var chips = new List<EncounterClass> { EncounterClass.Boss, EncounterClass.Elite, EncounterClass.Elite };
            return Settle(chips, chips);
        }

        // ---- the R0 reveal fixtures (docs/PLAN_REWARD_SCREENS.md §6) ----------
        //
        // One home for the three pages the reveal is judged on, so the still
        // captures (ReckoningCaptureTests) and the films (ReckoningRevealFilm,
        // tools/gif.ps1 reveal) show the same pages.

        // The run is started here rather than inside Settle so a fixture can
        // set run state (legendary pity) before the page rolls.
        public static void StartRun()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"\[RealmOpening\]"));
            RunManager.StartRun(Seed);
        }

        // A normal fight's page: three cards, none above the ordinary ladder.
        public static IReadOnlyList<PendingPage> NoCachePage()
        {
            StartRun();
            return OneGearPage();
        }

        // Pity one page short of its cap forces a Legendary on the boss gear
        // page, the top-tier gear a cache will hold.
        public static IReadOnlyList<PendingPage> CacheTopTierPages()
        {
            StartRun();
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            return BossAndTwoAnchors();
        }

        // The boss gear page is the five-card row. A ghost is the item the
        // wearer already wears in the card's slot (C5), so cards 0-3 get their
        // wearer+slot filled and card 4's slot is emptied: the row shows ghost
        // present and ghost absent.
        public static IReadOnlyList<PendingPage> FiveGhostsPages()
        {
            StartRun();
            var pages = BossAndTwoAnchors();
            var cards = pages[0].Cards;
            Assert.AreEqual(5, cards.Count, "the boss gear page is no longer five cards");

            // A second, ordinary page supplies the worn items (any item that
            // fits the slot); rolled after the boss page, which it must not
            // disturb, and discarded from the run's pending pages after.
            var wornSource = Settle(
                new List<EncounterClass> { EncounterClass.Normal, EncounterClass.Normal, EncounterClass.Normal },
                nodeId: 3)[0].Cards;
            RunManager.Run.pendingPages = pages.ToList();

            var save = SaveSlotManager.CurrentSave;
            for (int i = 0; i < cards.Count; i++)
            {
                var who = save.roster.First(c => c != null && c.definitionId == cards[i].RecipientId);
                if (i == cards.Count - 1)
                {
                    EquipmentOps.Unequip(MenuContext.Descent, save, who, cards[i].Slot);
                    Assert.IsNull(who.equipment.GetInstance(cards[i].Slot), "the no-ghost slot is not empty");
                    continue;
                }

                var worn = wornSource.FirstOrDefault(c => c.IsGear && c.RecipientId == cards[i].RecipientId &&
                                                          c.Slot == cards[i].Slot) ?? cards[i];
                var definition = ContentDatabase.DefinitionOf(worn.Copy);
                InventoryOps.Add(save.stockpiledItems, worn.Copy, 1);
                Assert.IsTrue(EquipmentOps.Equip(MenuContext.Descent, save, who, worn.Copy, definition.equipSlot,
                    definition.IsEquippable, cards[i].Slot), $"could not wear card {i}'s slot");
            }

            return pages;
        }
    }
}
