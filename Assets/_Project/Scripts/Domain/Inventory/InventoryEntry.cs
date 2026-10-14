using System;
using System.Collections.Generic;

namespace PrincesPalace
{
    // One stack of a held item. Matches ItemDefinition.id.
    //
    // A stack is keyed by ItemInstance.SameStack -- (itemId, tier,
    // modifierIds) for an ordinary copy, the lot alone for a caravan copy --
    // not by itemId alone: a tier-3 coif and a tier-5 coif are the same
    // DEFINITION but not the same object, and stacking them would silently
    // upgrade one of them. `count` still means "how many identical ones", it
    // is just that identical includes the tier and which affixes rolled.
    //
    // In PRACTICE, `count` is almost always 1 the moment modifierIds is
    // non-empty: a rolled item is drop-unique, so two entries only merge when
    // two separate drops happened to roll the exact same set of modifiers.
    // Nothing here enforces count == 1 for a modified entry; InventoryOps'
    // merge predicate is the only place that decision is made; see its own
    // header.
    [Serializable]
    public class InventoryEntry
    {
        public string itemId;
        public int count;

        // Which tier of the base this copy is (ItemInstance.Tier). A save
        // written before the field loads 0; such a run is settled on opening
        // and never resumed (docs/PLAN_ITEMS.md 2.5), so no migration reads it.
        public int tier;

        // The rolled affix ids on this stack. Additive-JsonUtility posture: a
        // save written before modifiers existed deserialises this as the empty
        // list the field initializer below already provides, never null. A
        // save that still carries the removed plus and riftTier fields loads
        // with them ignored.
        //
        // A REAL empty List<string>, not left null — matching the same
        // "empty and absent read the same way" convention
        // unlockedTalentIds/EquipmentLoadout use for their own list fields.
        public List<string> modifierIds = new List<string>();

        // Where this copy came from -- see Provenance. The one nested object
        // on an otherwise flat entry; a save written before it existed loads
        // the initializer's ordinary copy.
        public Provenance provenance = new Provenance();

        public InventoryEntry()
        {
        }

        // An entry IS an instance plus how many of it (plan 3.3). Built from
        // one so a move carries every axis, and read back as one so a caller
        // hands the same copy onward without naming its fields.
        public InventoryEntry(ItemInstance instance, int count)
        {
            itemId = instance?.ItemId ?? "";
            this.count = count;
            tier = instance?.Tier ?? 0;
            modifierIds = instance?.ModifierList() ?? new List<string>();
            provenance = instance?.ProvenanceCopy() ?? new Provenance();
        }

        public ItemInstance Instance => ItemInstance.Of(itemId, tier, modifierIds, provenance);

        public InventoryEntry(string itemId, int count, List<string> modifierIds = null, int tier = 0)
        {
            this.itemId = itemId;
            this.count = count;
            this.tier = tier;
            // Copied rather than assigned by reference -- see
            // CollectionOps.CopyOrEmpty for why a caller's own list must not
            // end up shared with this entry's backing list.
            this.modifierIds = CollectionOps.CopyOrEmpty(modifierIds);
        }
    }
}
