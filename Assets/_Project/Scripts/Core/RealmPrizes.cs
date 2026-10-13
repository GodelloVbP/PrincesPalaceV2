using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // A fight node's chip against the live run (docs/PLAN_RUN_MAP.md section 2,
    // Prizes; docs/PLAN_ITEMS.md section 1.1): what the map card shows and what
    // settlement pays, from one function so the two cannot disagree.
    //
    // Gold and consumables are paid here. Gear, Book and Relic go to the item
    // pipeline, which pays nothing until item phase I3; a Book or Relic chip
    // whose pool is empty pays gold instead, read live on the card and again at
    // settlement.
    public static class RealmPrizes
    {
        public static MapNodeChip ChipOf(RunSnapshot run, RealmNode node)
        {
            if (run == null || node == null || node.Prize == PrizeKind.None) return default;

            var kind = RealmPrize.Effective(node.Prize, PoolHasAny(run, node.Prize));
            int strength = RealmOpening.StrengthOf(run, node.Id);
            switch (kind)
            {
                case PrizeKind.Gold:
                    return new MapNodeChip(kind, RealmPrize.GoldFor(strength, run.legStartStep + node.Offset));
                case PrizeKind.Consumable:
                    return new MapNodeChip(kind, RealmPrize.ConsumablesFor(strength));
                default:
                    return new MapNodeChip(kind, 0);
            }
        }

        // Pays the chip of the node the party just won. Does not persist:
        // SettleFight folds every write into one. Returns the lines the
        // Reckoning shows for it.
        public static List<string> Pay(SaveData save, RunSnapshot run, RealmNode node)
        {
            var notices = new List<string>();
            var chip = ChipOf(run, node);

            switch (chip.Kind)
            {
                case PrizeKind.Gold:
                    if (RunManager.CreditPayout(chip.Amount)) notices.Add(UiStrings.PrizePaidGold.Format(chip.Amount));
                    break;

                case PrizeKind.Consumable:
                    notices.AddRange(PayConsumables(save, run, node, chip.Amount));
                    break;

                case PrizeKind.Item:
                case PrizeKind.Book:
                case PrizeKind.Relic:
                    HandToItemPipeline(chip.Kind);
                    break;
            }

            return notices;
        }

        // Item phase I3 builds the pages these chips open (PLAN_ITEMS.md 1.2).
        // Until then a Gear, Book or Relic chip pays nothing, and no profile
        // weights them above zero.
        private static void HandToItemPipeline(PrizeKind kind)
        {
        }

        // Each potion drawn from the authored consumables on the Prize
        // stream, so a reload mid-settlement pays the same ones.
        private static IEnumerable<string> PayConsumables(SaveData save, RunSnapshot run, RealmNode node, int count)
        {
            var pool = ContentDatabase.Consumables.Where(item => item != null).ToList();
            if (save == null || pool.Count == 0 || count <= 0) yield break;

            save.stockpiledItems ??= new List<InventoryEntry>();
            var rng = RngStreams.Open(run.runSeed, RngStreams.Prize, run.legStartStep + node.Offset, node.Id);
            for (int i = 0; i < count; i++)
            {
                var item = pool[rng.NextInt(0, pool.Count)];
                InventoryOps.Add(save.stockpiledItems, item.id);
                yield return UiStrings.PrizePaidItem.Format(item.displayName);
            }
        }

        private static bool PoolHasAny(RunSnapshot run, PrizeKind kind)
        {
            switch (kind)
            {
                case PrizeKind.Book: return BookPoolHasAny(run);
                case PrizeKind.Relic: return RunOrchestrator.AvailableRelicOptions().Count > 0;
                default: return true;
            }
        }

        // The spell-drop pool (bookTier > 0) less books already waiting
        // unassigned and books every squad member has learned.
        private static bool BookPoolHasAny(RunSnapshot run)
        {
            var squad = SaveSlotManager.CurrentSave?.ActiveSquad()?
                .Where(c => c != null).Select(c => c.definitionId).ToList() ?? new List<string>();
            var waiting = new HashSet<string>(run.unassignedSpellBooks ?? new List<string>());
            var learned = run.learnedSpells ?? new List<LearnedSpellEntry>();

            return ContentDatabase.Skills
                .Where(s => s != null && s.Data.BookTier > 0 && !waiting.Contains(s.id))
                .Any(s => squad.Count == 0
                          || squad.Any(id => !learned.Any(e => e.characterId == id && e.skillId == s.id)));
        }
    }
}
