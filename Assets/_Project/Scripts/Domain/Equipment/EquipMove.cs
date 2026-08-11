using System.Collections.Generic;

namespace PrincesPalace.Domain.Equipment
{
    // Putting gear on and taking it off.
    //
    // Equipping MOVES the item: it leaves the bag and arrives in the slot, and
    // whatever was already there travels back the other way. Both halves happen
    // or neither does. That is the whole contract, and it is why this is one
    // place rather than two call sites that each remember half of it.
    //
    // Ported from v1's Core/EquipmentService with its two ContentDatabase
    // lookups hoisted into parameters -- `itemSlot` and `isEquippable` are the
    // only things it ever asked content for. With those passed in, the rules are
    // engine-free and content-free, so they live in Domain and are tested in
    // milliseconds instead of through a scene.
    public static class EquipMove
    {
        // `plus` names WHICH COPY is being worn. It has to be passed rather than
        // looked up: the bag can hold several stacks of one item id at different
        // plus levels, and only the caller -- which knows which row the player
        // clicked -- can say which one they meant.
        public static bool TryEquip(
            EquipmentLoadout loadout,
            List<InventoryEntry> bag,
            string itemId,
            EquipmentSlot itemSlot,
            bool isEquippable,
            EquipmentSlot? preferredSlot = null,
            int plus = 0)
        {
            if (loadout == null || bag == null || string.IsNullOrEmpty(itemId)) return false;
            if (!isEquippable) return false;

            if (preferredSlot.HasValue && !EquipmentSlots.Accepts(preferredSlot.Value, itemSlot)) return false;

            // Checked before anything is mutated: an equip that cannot be paid
            // for must not half-happen.
            if (!InventoryOps.TryRemoveAt(bag, itemId, plus)) return false;

            var slot = preferredSlot ?? loadout.ResolveTargetSlot(itemSlot);

            // BOTH HALVES of what is being displaced are read BEFORE Set
            // overwrites the slot. Taking only the id would put a +5 sword back
            // in the bag as a +0 one -- an item-destroying bug that no
            // count-based assertion would ever catch, because the count is
            // still right.
            string displaced = loadout.Get(slot);
            int displacedPlus = loadout.GetPlus(slot);

            loadout.Set(slot, itemId, plus);

            if (displaced.Length > 0)
            {
                InventoryOps.Add(bag, displaced, 1, displacedPlus);
            }

            return true;
        }

        // Empties a slot and puts what was in it back in the bag. False when the
        // slot was already empty.
        public static bool TryUnequip(EquipmentLoadout loadout, List<InventoryEntry> bag, EquipmentSlot slot)
        {
            if (loadout == null || bag == null) return false;

            // Read before the clear, for the same reason the equip path reads
            // the displaced plus before Set.
            int removedPlus = loadout.GetPlus(slot);
            string removed = loadout.Clear(slot);
            if (removed.Length == 0) return false;

            InventoryOps.Add(bag, removed, 1, removedPlus);
            return true;
        }
    }
}
