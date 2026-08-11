using System;

namespace PrincesPalace
{
    // One stack of a held item. Matches ItemDefinition.id.
    //
    // A stack is keyed by (itemId, plus), not by itemId alone: a +3 coif and
    // a +5 coif are the same DEFINITION but not the same object, and stacking
    // them would silently upgrade one of them. `count` still means "how many
    // identical ones", it is just that identical now includes how honed they
    // are.
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

        public InventoryEntry()
        {
        }

        public InventoryEntry(string itemId, int count, int plus = 0)
        {
            this.itemId = itemId;
            this.count = count;
            this.plus = plus;
        }
    }
}
