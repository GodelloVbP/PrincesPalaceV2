using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
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

        // A normal fight's page that earns no cache, the most common page: its
        // three cards dealt face-up. A Rare+ card earns the cache when it is
        // better than what its wearer wears, so each one's wearer is given the
        // same copy (an equal quality never earns it); Commons and Uncommons
        // never do and keep whatever they were rolled against.
        public static IReadOnlyList<PendingPage> NoCachePage()
        {
            StartRun();
            var pages = OneGearPage();
            var save = SaveSlotManager.CurrentSave;
            foreach (var card in pages[0].Cards)
            {
                if (!card.IsGear || LiveUpgrade.RevealCardOf(save, card).Tier < RevealSchedule.RareTier) continue;
                WearerOf(save, card).equipment.Put(card.Slot,
                    ItemInstance.Of(card.Copy.ItemId, card.Copy.Tier, card.Copy.ModifierIds));
            }

            Assert.IsFalse(RevealSchedule.CacheApplies(RevealCardsOf(save, pages[0]), pages[0].SourceClass),
                "the no-cache fixture earns a cache");
            return pages;
        }

        // The page as the reveal sees it, in page order (the cache rule does
        // not depend on the order).
        public static List<RevealCard> RevealCardsOf(SaveData save, PendingPage page) =>
            page.Cards.Select(c => LiveUpgrade.RevealCardOf(save, c)).ToList();

        private static Character WearerOf(SaveData save, PendingCard card) =>
            save.roster.First(c => c != null && c.definitionId == card.RecipientId);

        // Pity one page short of its cap forces a Legendary on the boss gear
        // page, the top-tier gear a cache will hold.
        public static IReadOnlyList<PendingPage> CacheTopTierPages()
        {
            StartRun();
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            return BossAndTwoAnchors();
        }

        // The boss gear page is the five-card row, every card with a ghost: the
        // item its wearer already wears in the card's slot (C5). The worn items
        // are put straight into the slots, so no equip rule can move or refuse
        // one; each is a matching card from an ordinary page, else the card's
        // own item one tier lower.
        public static IReadOnlyList<PendingPage> FiveGhostsPages()
        {
            StartRun();
            var pages = BossAndTwoAnchors();
            var cards = pages[0].Cards;
            Assert.AreEqual(5, cards.Count, "the boss gear page is no longer five cards");

            // Rolled after the boss page, which it must not disturb, and
            // discarded from the run's pending pages after.
            var wornSource = Settle(
                new List<EncounterClass> { EncounterClass.Normal, EncounterClass.Normal, EncounterClass.Normal },
                nodeId: 3)[0].Cards;
            RunManager.Run.pendingPages = pages.ToList();

            var save = SaveSlotManager.CurrentSave;
            foreach (var card in cards)
            {
                var match = wornSource.FirstOrDefault(c => c.IsGear && c.RecipientId == card.RecipientId && c.Slot == card.Slot);
                var worn = match != null
                    ? ItemInstance.Of(match.Copy.ItemId, match.Copy.Tier, match.Copy.ModifierIds)
                    : ItemInstance.Of(card.Copy.ItemId, System.Math.Max(0, card.Copy.Tier - 1));
                WearerOf(save, card).equipment.Put(card.Slot, worn);
            }

            for (int i = 0; i < cards.Count; i++)
                Assert.IsNotNull(WearerOf(save, cards[i]).equipment.GetInstance(cards[i].Slot), $"card {i}'s wearer wears nothing there");
            return pages;
        }
    }
}
