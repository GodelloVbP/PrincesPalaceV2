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
        // `instance` names WHICH COPY is being worn. It has to be passed rather
        // than looked up: the bag can hold several stacks of one item id (other
        // plus, other roll, other lot), and only the caller -- which knows which
        // row the player clicked -- can say which one they meant. Taking the
        // whole ItemInstance rather than its fields is what makes provenance
        // travel with the move instead of being a fifth thing to remember.
        public static bool TryEquip(
            EquipmentLoadout loadout,
            List<InventoryEntry> bag,
            ItemInstance instance,
            EquipmentSlot itemSlot,
            bool isEquippable,
            EquipmentSlot? preferredSlot = null)
        {
            if (loadout == null || bag == null || instance == null || string.IsNullOrEmpty(instance.ItemId)) return false;
            if (!isEquippable) return false;

            if (preferredSlot.HasValue && !EquipmentSlots.Accepts(preferredSlot.Value, itemSlot)) return false;

            // The bag's own copy, not the caller's: a caller holding a stale or
            // partial instance (a plain one for a lot copy, say) finds nothing
            // here and changes nothing, and the copy that goes on the body is
            // exactly the one that left the bag.
            var held = InventoryOps.Find(bag, instance);
            if (held == null) return false;
            var worn = held.Instance;

            // Checked before anything is mutated: an equip that cannot be paid
            // for must not half-happen.
            if (!InventoryOps.TryRemoveAt(bag, worn)) return false;

            var slot = preferredSlot ?? loadout.ResolveTargetSlot(itemSlot);

            // ALL of what is being displaced comes back from Put -- the whole
            // instance, read before the slot is overwritten. Taking only the id
            // would put a +5 sword back in the bag as a +0 one -- an
            // item-destroying bug that no count-based assertion would ever
            // catch, because the count is still right. The same holds for a
            // displaced copy's roll and provenance.
            var displaced = loadout.Put(slot, worn);

            if (displaced != null)
            {
                InventoryOps.Add(bag, displaced, 1);
            }

            return true;
        }

        // The positional form, for callers holding an ordinary copy. Kept so
        // the existing callers and tests read as they always did.
        public static bool TryEquip(
            EquipmentLoadout loadout,
            List<InventoryEntry> bag,
            string itemId,
            EquipmentSlot itemSlot,
            bool isEquippable,
            EquipmentSlot? preferredSlot = null,
            int plus = 0,
            List<string> modifierIds = null,
            int riftTier = 0)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return TryEquip(loadout, bag, new ItemInstance(itemId, plus, modifierIds, riftTier),
                itemSlot, isEquippable, preferredSlot);
        }

        // Empties a slot and puts what was in it back in the bag. False when the
        // slot was already empty.
        public static bool TryUnequip(EquipmentLoadout loadout, List<InventoryEntry> bag, EquipmentSlot slot)
        {
            if (loadout == null || bag == null) return false;

            // The whole copy comes back from Put, read before the clear, for
            // the same reason the equip path reads what it displaces.
            var removed = loadout.Put(slot, null);
            if (removed == null) return false;

            InventoryOps.Add(bag, removed, 1);
            return true;
        }
    }
}
