using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Rewards;

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
    }
}
