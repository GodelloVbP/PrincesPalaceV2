using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Equipment
{
    // One slot of a character's paperdoll. A list of these rather than eight
    // named string fields for the same reason SaveData holds a list of
    // InventoryEntry: JsonUtility handles [Serializable] classes inside a
    // List fine, and a save written before a slot existed simply has no
    // entry for it rather than needing a migration step per slot.
    [Serializable]
    public class EquipmentSlotEntry
    {
        public EquipmentSlot slot;
        public string itemId;

        // How honed the copy in this slot is. Travels with the item rather
        // than with the definition, so taking a +5 sword off and putting it
        // back on has to preserve the 5 — which is why every equip and
        // unequip path moves this alongside the id.
        //
        // A save written before plus existed deserialises this as 0, which is
        // the correct reading of it and is why no migration step exists.
        public int plus;

        public EquipmentSlotEntry()
        {
        }

        public EquipmentSlotEntry(EquipmentSlot slot, string itemId, int plus = 0)
        {
            this.slot = slot;
            this.itemId = itemId;
            this.plus = plus;
        }
    }

    // What a character is currently wearing: slot -> item id. Engine-free
    // and content-free on purpose — it only ever moves ids around, so every
    // rule in here ("a weapon fits either hand", "equipping into a full slot
    // displaces what was there") is unit-testable in EditMode without a
    // scene, a ScriptableObject, or a running game.
    //
    // Deliberately NOT a Dictionary: this is serialized into the save file
    // and JsonUtility cannot round-trip one. Eight entries max, so the
    // linear scans are free.
    //
    // Empty and absent mean the same thing — an entry with a null/empty
    // itemId is dropped rather than kept as a tombstone, so "is this slot
    // free" has exactly one representation.
    [Serializable]
    public class EquipmentLoadout
    {
        public List<EquipmentSlotEntry> slots = new List<EquipmentSlotEntry>();

        // The item id worn in `slot`, or "" when the slot is empty. Never
        // returns null, so callers can string-compare without guarding.
        public string Get(EquipmentSlot slot)
        {
            var entry = Find(slot);
            return entry == null || string.IsNullOrEmpty(entry.itemId) ? "" : entry.itemId;
        }

        public bool IsEmpty(EquipmentSlot slot)
        {
            return Get(slot).Length == 0;
        }

        // How honed the item in `slot` is, or 0 when the slot is empty.
        //
        // Read BEFORE Set displaces anything: an equip that swaps one sword
        // for another has to put the old one back in the bag at its own plus,
        // and by the time Set has returned, that number is gone.
        public int GetPlus(EquipmentSlot slot)
        {
            var entry = Find(slot);
            return entry == null || string.IsNullOrEmpty(entry.itemId) ? 0 : entry.plus;
        }

        // Puts `itemId` in `slot`, replacing whatever was there, and returns
        // the id it displaced ("" if the slot was free). Callers use that
        // return value to put the old item back in the bag — which is why
        // this returns it rather than dropping it on the floor, the one way
        // an equip could ever destroy an item.
        //
        // A null/empty itemId clears the slot, so Set and Clear are the same
        // operation and cannot disagree.
        public string Set(EquipmentSlot slot, string itemId, int plus = 0)
        {
            string previous = Get(slot);
            var entry = Find(slot);

            if (string.IsNullOrEmpty(itemId))
            {
                if (entry != null)
                {
                    slots.Remove(entry);
                }

                return previous;
            }

            if (entry == null)
            {
                slots.Add(new EquipmentSlotEntry(slot, itemId, plus));
            }
            else
            {
                entry.itemId = itemId;
                entry.plus = plus;
            }

            return previous;
        }

        // Empties `slot` and returns what was in it ("" if nothing).
        public string Clear(EquipmentSlot slot)
        {
            return Set(slot, "");
        }

        // Where an item belonging in `itemSlot` should go: the first slot
        // that accepts it and is currently free, or null when every
        // accepting slot is occupied. Only Weapon1/Weapon2 can ever have
        // more than one candidate.
        public EquipmentSlot? FirstFreeSlotFor(EquipmentSlot itemSlot)
        {
            foreach (var candidate in EquipmentSlots.All)
            {
                if (EquipmentSlots.Accepts(candidate, itemSlot) && IsEmpty(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        // Where an item belonging in `itemSlot` goes when the player didn't
        // pick a slot: a free one if there is one, otherwise the first slot
        // that accepts it — i.e. equipping a third sword replaces Weapon 1
        // rather than silently doing nothing. Never null, because every
        // valid item slot is accepted by at least itself.
        public EquipmentSlot ResolveTargetSlot(EquipmentSlot itemSlot)
        {
            var free = FirstFreeSlotFor(itemSlot);
            if (free.HasValue)
            {
                return free.Value;
            }

            foreach (var candidate in EquipmentSlots.All)
            {
                if (EquipmentSlots.Accepts(candidate, itemSlot))
                {
                    return candidate;
                }
            }

            return itemSlot;
        }

        // The slot holding `itemId`, or null. Two copies of the same item id
        // can legitimately be worn at once (two identical swords, one per
        // hand), so this reports the first — callers that care about both
        // should walk EquippedItemIds instead.
        public EquipmentSlot? FindSlotHolding(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            foreach (var entry in slots)
            {
                if (entry != null && entry.itemId == itemId)
                {
                    return entry.slot;
                }
            }

            return null;
        }

        // Every worn item id, in slot order, skipping empty slots. Includes
        // duplicates when the same id is worn twice — a caller summing stat
        // bonuses must count both hands.
        public List<string> EquippedItemIds()
        {
            var result = new List<string>();
            foreach (var slot in EquipmentSlots.All)
            {
                string id = Get(slot);
                if (id.Length > 0)
                {
                    result.Add(id);
                }
            }

            return result;
        }

        // The same walk, but keeping each slot's plus alongside its id.
        //
        // Anything summing STATS has to use this rather than EquippedItemIds:
        // what a worn item grants depends on how honed that particular copy
        // is, and an id on its own cannot answer that. The id-only version
        // stays for callers that genuinely only need to know what is worn.
        public List<EquipmentSlotEntry> EquippedEntries()
        {
            var result = new List<EquipmentSlotEntry>();
            foreach (var slot in EquipmentSlots.All)
            {
                var entry = Find(slot);
                if (entry != null && !string.IsNullOrEmpty(entry.itemId))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        // Drops every worn id the caller rejects, and returns them. Used by
        // SaveData.Reconcile to strip references to content that no longer
        // exists — same tolerant posture as the rest of Reconcile, and the
        // returned ids are what the caller hands back to the player's bag so
        // a renamed item is not silently deleted.
        public List<string> RemoveWhere(Func<string, bool> shouldRemove)
        {
            var removed = new List<string>();

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var entry = slots[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId))
                {
                    slots.RemoveAt(i);
                    continue;
                }

                if (shouldRemove(entry.itemId))
                {
                    removed.Add(entry.itemId);
                    slots.RemoveAt(i);
                }
            }

            removed.Reverse();
            return removed;
        }

        // A deep copy — a new EquipmentLoadout with its own EquipmentSlotEntry
        // instances, not the same ones. Entries are mutable classes, so a
        // shallow copy of `slots` would let a mutation on the clone (the
        // hover-comparison panel's whole reason for existing: try a swap
        // without touching the real loadout) leak back into the original.
        public EquipmentLoadout Clone()
        {
            var copy = new EquipmentLoadout();
            foreach (var entry in slots)
            {
                if (entry != null)
                {
                    copy.slots.Add(new EquipmentSlotEntry(entry.slot, entry.itemId, entry.plus));
                }
            }

            return copy;
        }

        private EquipmentSlotEntry Find(EquipmentSlot slot)
        {
            foreach (var entry in slots)
            {
                if (entry != null && entry.slot == slot)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
