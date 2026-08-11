using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace
{
    // Add/count/remove over a List<InventoryEntry>, in one place.
    //
    // There are two such lists — RunState.inventory (this run's loot) and
    // SaveData.stockpiledItems (the between-runs stash) — and equipping has
    // to work against whichever one the player is looking at. RunState grew
    // these three methods first; lifting them out is what lets the stash use
    // the identical rules instead of a second, subtly different copy.
    //
    // The rule that actually matters: an entry whose count reaches zero is
    // removed, never left behind. "Absent" and "count 0" must not both be
    // reachable, or the inventory grid would show empty stacks.
    //
    // STACKS ARE KEYED ON (itemId, plus). Two coifs of the same tier but
    // different plus are different objects and must not merge — merging them
    // would either promote the worse one or demote the better one, and both
    // are the sort of quiet item-destroying bug the "every method does both
    // halves of the move or neither" rule in EquipmentService exists to
    // prevent.
    public static class InventoryOps
    {
        public static void Add(List<InventoryEntry> inventory, string itemId, int count = 1, int plus = 0)
        {
            if (inventory == null || string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return;
            }

            var entry = inventory.FirstOrDefault(e => e != null && e.itemId == itemId && e.plus == plus);
            if (entry == null)
            {
                inventory.Add(new InventoryEntry(itemId, count, plus));
                return;
            }

            entry.count += count;
        }

        // How many of this item are held AT ANY PLUS.
        //
        // Summed rather than "the first stack's count", because with plus on
        // the instance one item id can legitimately occupy several stacks —
        // and every caller of this ("do I have a potion", "did equipping take
        // it out of the bag") is asking about the item, not about one
        // particular copy of it.
        public static int Count(List<InventoryEntry> inventory, string itemId)
        {
            if (inventory == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return inventory.Where(e => e != null && e.itemId == itemId).Sum(e => e.count);
        }

        // How many are held at exactly this plus.
        public static int CountAt(List<InventoryEntry> inventory, string itemId, int plus)
        {
            return inventory?.FirstOrDefault(e => e != null && e.itemId == itemId && e.plus == plus)?.count ?? 0;
        }

        // Removes one of the item and returns whether there was one to
        // remove. Takes the LOWEST plus held, so spending a consumable or
        // handing one over never quietly consumes the best copy the player
        // owns. Callers that mean a specific copy pass its plus.
        public static bool TryRemove(List<InventoryEntry> inventory, string itemId)
        {
            var entry = inventory?
                .Where(e => e != null && e.itemId == itemId && e.count > 0)
                .OrderBy(e => e.plus)
                .FirstOrDefault();

            return RemoveOne(inventory, entry);
        }

        // Removes one of exactly this copy.
        public static bool TryRemoveAt(List<InventoryEntry> inventory, string itemId, int plus)
        {
            var entry = inventory?.FirstOrDefault(e => e != null && e.itemId == itemId && e.plus == plus);
            return RemoveOne(inventory, entry);
        }

        private static bool RemoveOne(List<InventoryEntry> inventory, InventoryEntry entry)
        {
            if (entry == null || entry.count <= 0)
            {
                return false;
            }

            entry.count--;
            if (entry.count == 0)
            {
                inventory.Remove(entry);
            }

            return true;
        }
    }
}
