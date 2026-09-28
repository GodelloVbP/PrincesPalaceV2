using System;
using System.Collections.Generic;

namespace PrincesPalace
{
    // One stack of a held item. Matches ItemDefinition.id.
    //
    // A stack is keyed by ItemInstance.SameStack -- (itemId, plus,
    // modifierIds, riftTier) for an ordinary copy, the lot alone for a
    // caravan copy -- not by itemId alone: a +3 coif and a +5 coif are the same DEFINITION but not
    // the same object, and stacking them would silently upgrade one of them.
    // `count` still means "how many identical ones", it is just that
    // identical now also includes which affixes rolled and how many slots
    // they filled.
    //
    // In PRACTICE, `count` is almost always 1 the moment modifierIds is
    // non-empty: a rolled item is drop-unique, so two entries only merge when
    // two separate drops happened to roll the exact same set of modifiers at
    // the exact same rift tier -- rare, but not a bug when it happens, the
    // same way two +5 plain swords legitimately stack today. Nothing here
    // enforces count == 1 for a modified entry; InventoryOps' merge predicate
    // is the only place that decision is made; see its own header.
    [Serializable]
    public class InventoryEntry
    {
        public string itemId;
        public int count;

        // How honed this stack is. The tier — which item this is — lives on
        // the definition; this is the other axis, and it is here rather than
        // baked because otherwise every tier would need eleven assets.
        //
        // JsonUtility deserialises a missing int as 0, so a save written
        // before plus existed loads as all-plus-zero. That is the correct
        // reading of such a save and is why there is no migration code.
        public int plus;

        // The rolled affix ids on this stack (Phase A of the item-modifier
        // plan; nothing populates this list yet). Same additive-JsonUtility
        // posture as plus: a save written before modifiers existed
        // deserialises this as the empty list the field initializer below
        // already provides, never null.
        //
        // A REAL empty List<string>, not left null — matching the same
        // "empty and absent read the same way" convention
        // unlockedTalentIds/EquipmentLoadout use for their own list fields.
        public List<string> modifierIds = new List<string>();

        // How many modifier slots this stack rolled, 0-3. See
        // EquipmentSlotEntry.riftTier for why this is decoupled from item
        // tier. Zero on a save written before this field existed.
        public int riftTier;

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
            plus = instance?.Plus ?? 0;
            modifierIds = instance?.ModifierList() ?? new List<string>();
            riftTier = instance?.RiftTier ?? 0;
            provenance = instance?.ProvenanceCopy() ?? new Provenance();
        }

        public ItemInstance Instance => new ItemInstance(itemId, plus, modifierIds, riftTier, provenance);

        public InventoryEntry(string itemId, int count, int plus = 0,
            List<string> modifierIds = null, int riftTier = 0)
        {
            this.itemId = itemId;
            this.count = count;
            this.plus = plus;
            // Copied rather than assigned by reference -- see
            // CollectionOps.CopyOrEmpty for why a caller's own list must not
            // end up shared with this entry's backing list.
            this.modifierIds = CollectionOps.CopyOrEmpty(modifierIds);
            this.riftTier = riftTier;
        }
    }
}
