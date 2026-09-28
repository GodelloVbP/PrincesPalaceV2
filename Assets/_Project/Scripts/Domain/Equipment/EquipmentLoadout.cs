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

        // The rolled affix ids on THIS copy — "Fiery", "Swift", and so on
        // (Phase A of the item-modifier plan; nothing populates this list
        // yet). Same additive-JsonUtility posture as plus: a save written
        // before modifiers existed deserialises this as the empty list the
        // field initializer below already provides, never null, so every
        // reader can foreach it without a guard.
        //
        // A REAL empty List<string>, not left null — JsonUtility needs a
        // concrete instance to write into on load, and "empty" and "absent"
        // must read the same way here for the same reason Get() never
        // returns null for an empty slot.
        public List<string> modifierIds = new List<string>();

        // How many modifier slots this copy rolled, 0-3. Decoupled from item
        // tier (which weapon/armour asset this is) on purpose — a common-tier
        // sword can roll Convergent and a rare one can roll Ordinary, and the
        // glow is meant to read the ROLL, not the base item. Zero on a save
        // written before this field existed, same reasoning as plus and
        // modifierIds above.
        public int riftTier;

        // Where this copy came from -- see Provenance. Travels with the item
        // through every equip and unequip exactly as plus does, because the
        // move reads and writes the whole ItemInstance.
        public Provenance provenance = new Provenance();

        public EquipmentSlotEntry()
        {
        }

        // A slot entry IS a slot plus one instance (plan 3.3).
        public EquipmentSlotEntry(EquipmentSlot slot, ItemInstance instance)
        {
            this.slot = slot;
            Write(instance);
        }

        public ItemInstance Instance => new ItemInstance(itemId, plus, modifierIds, riftTier, provenance);

        // Overwrites every identity field from `instance`, so a slot can never
        // end up holding one copy's plus and another copy's provenance.
        internal void Write(ItemInstance instance)
        {
            itemId = instance?.ItemId ?? "";
            plus = instance?.Plus ?? 0;
            // Copied, not assigned -- see CollectionOps.CopyOrEmpty for why a
            // shared reference here would be the aliasing bug Clone()'s tests
            // exist to catch.
            modifierIds = instance?.ModifierList() ?? new List<string>();
            riftTier = instance?.RiftTier ?? 0;
            provenance = instance?.ProvenanceCopy() ?? new Provenance();
        }

        public EquipmentSlotEntry(EquipmentSlot slot, string itemId, int plus = 0,
            List<string> modifierIds = null, int riftTier = 0)
        {
            this.slot = slot;
            this.itemId = itemId;
            this.plus = plus;
            this.modifierIds = CollectionOps.CopyOrEmpty(modifierIds);
            this.riftTier = riftTier;
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

        // The rolled modifier ids worn in `slot`, or a fresh empty list when
        // the slot is empty. Always a NEW list — the caller can hold onto or
        // mutate what comes back without reaching into the entry's own
        // backing list, same reasoning as Clone().
        //
        // Read BEFORE Set displaces anything, same rule GetPlus's own comment
        // states: by the time Set has returned, the displaced copy's rolled
        // affixes are gone from here.
        public List<string> GetModifierIds(EquipmentSlot slot)
        {
            var entry = Find(slot);
            return entry == null || string.IsNullOrEmpty(entry.itemId)
                ? new List<string>()
                : new List<string>(entry.modifierIds ?? new List<string>());
        }

        // How many modifier slots the copy in `slot` rolled, or 0 when the
        // slot is empty.
        public int GetRiftTier(EquipmentSlot slot)
        {
            var entry = Find(slot);
            return entry == null || string.IsNullOrEmpty(entry.itemId) ? 0 : entry.riftTier;
        }

        // The whole copy worn in `slot`, or null when it is empty. What every
        // move reads BEFORE it displaces anything: one read that cannot
        // forget an axis, where the four getters above each had to be called.
        public ItemInstance GetInstance(EquipmentSlot slot)
        {
            var entry = Find(slot);
            return entry == null || string.IsNullOrEmpty(entry.itemId) ? null : entry.Instance;
        }

        // Puts this copy in `slot` and returns the copy it displaced (null if
        // the slot was free). A null or id-less instance clears the slot.
        public ItemInstance Put(EquipmentSlot slot, ItemInstance instance)
        {
            var previous = GetInstance(slot);
            var entry = Find(slot);

            if (instance == null || string.IsNullOrEmpty(instance.ItemId))
            {
                if (entry != null)
                {
                    slots.Remove(entry);
                }

                return previous;
            }

            if (entry == null)
            {
                slots.Add(new EquipmentSlotEntry(slot, instance));
            }
            else
            {
                entry.Write(instance);
            }

            return previous;
        }

        // Puts `itemId` in `slot`, replacing whatever was there, and returns
        // the id it displaced ("" if the slot was free). Callers use that
        // return value to put the old item back in the bag — which is why
        // this returns it rather than dropping it on the floor, the one way
        // an equip could ever destroy an item.
        //
        // A null/empty itemId clears the slot, so Set and Clear are the same
        // operation and cannot disagree.
        //
        // `modifierIds`/`riftTier` name WHICH ROLL is being worn, the same
        // way `plus` names which honing level is — every caller that reads
        // GetModifierIds/GetRiftTier before displacing has to pass them back
        // in here or the roll is lost the moment the item changes slots.
        //
        // The positional form of Put, for callers holding an ordinary copy:
        // it writes a plain provenance.
        public string Set(EquipmentSlot slot, string itemId, int plus = 0,
            List<string> modifierIds = null, int riftTier = 0)
        {
            string previous = Get(slot);
            Put(slot, string.IsNullOrEmpty(itemId) ? null : new ItemInstance(itemId, plus, modifierIds, riftTier));
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

        // Everything comes off, and nothing comes back.
        //
        // A run's gear does NOT survive the run, the same way its inventory
        // does not -- equipment lives on the character, which is profile-scoped,
        // so without this a run item became permanent the moment it was worn.
        // Inventory was already discarded with the RunSnapshot it sat on, so
        // equipping was the one way to launder a run's loot into the profile.
        //
        // Nothing is returned. The caller is ending a run, not moving items
        // somewhere -- handing back a list here would invite a caller to bank
        // it, which is the behaviour this exists to remove.
        public void Clear() => slots.Clear();

        // Drops every worn id the caller rejects and returns the WHOLE ENTRY --
        // id, plus, and (once anything populates them) the rolled modifierIds
        // and riftTier together.
        //
        // The ids-only overload below loses all of that, and SaveData.Reconcile
        // used it: a +5 orphan came back to the stash as a +0. Exactly the
        // item-destroying bug class EquipMove reads the displaced plus to
        // avoid, sitting in the one path nobody looks at because it only fires
        // when content has been renamed.
        public List<EquipmentSlotEntry> RemoveEntriesWhere(Func<string, bool> shouldRemove)
        {
            var removed = new List<EquipmentSlotEntry>();

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
                    removed.Add(entry);
                    slots.RemoveAt(i);
                }
            }

            removed.Reverse();
            return removed;
        }

        // Ids only. Kept because it has its own callers and its own tests;
        // anything that hands an orphan back to a bag wants the overload above.
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
                    // The EquipmentSlotEntry constructor copies modifierIds
                    // into a NEW list rather than taking the reference, so
                    // this is a real deep copy — mutating the clone's list
                    // can never reach back into `entry.modifierIds`.
                    copy.slots.Add(new EquipmentSlotEntry(entry.slot, entry.Instance));
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
