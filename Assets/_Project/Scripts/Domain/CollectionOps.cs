using System.Collections.Generic;

namespace PrincesPalace
{
    // Small collection helpers shared across Domain that don't belong to any
    // one system.
    public static class CollectionOps
    {
        // A NEW list holding `source`'s contents, or a fresh empty list when
        // `source` is null -- never the same list instance handed back.
        //
        // EquipmentSlotEntry's constructor, EquipmentLoadout.Set's update
        // branch, and InventoryEntry's constructor all need exactly this: a
        // caller handing in its OWN list (e.g. Clone()) must not end up
        // sharing the backing array with the new entry, which is exactly the
        // aliasing bug those types' own Clone()/round-trip tests exist to
        // catch. "Empty" and "absent" reading the same way is the other half
        // of the contract -- every reader can foreach the result without a
        // null guard.
        public static List<string> CopyOrEmpty(List<string> source)
        {
            return source != null ? new List<string>(source) : new List<string>();
        }
    }
}
