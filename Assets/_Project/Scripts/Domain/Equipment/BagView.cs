using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Equipment
{
    // One row of the bag, as the screen needs it.
    //
    // A flattened view rather than the save's own InventoryEntry: the entry
    // knows an id, a count and a plus, and the grid needs a name, a rarity, a
    // slot and an icon too. Core resolves all that from ContentDatabase and
    // hands these over already answered -- the same split FightHudModel's
    // SatchelStack uses, and for the same reason: Domain cannot see content.
    public readonly struct BagItem
    {
        public readonly string Id;
        public readonly string Name;

        // ItemKind is a Core enum, so it crosses as an int. Only ever compared
        // and sorted on here, never interpreted.
        public readonly int Kind;

        public readonly EquipmentSlot Slot;
        public readonly int Tier;
        public readonly int Plus;
        public readonly int Count;
        public readonly string IconPath;
        public readonly bool IsEquippable;

        public BagItem(string id, string name, int kind, EquipmentSlot slot,
                       int tier, int plus, int count, string iconPath, bool isEquippable)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Slot = slot;
            Tier = tier;
            Plus = plus;
            Count = count;
            IconPath = iconPath;
            IsEquippable = isEquippable;
        }
    }

    // What the bag grid shows, and in what order.
    //
    // Pure arithmetic over a list someone else built, so every rule below is
    // pinned by a literal fixture instead of by opening the screen and
    // squinting at it.
    public static class BagView
    {
        // Cells per page. The ONE number the screen tree, the controller and
        // the count audit all read -- v1's grid was sized in the builder and
        // filled in the controller, and the two could disagree.
        public const int CellCount = 20;

        // Ordered so the useful things are on page one.
        //
        // DETERMINISTIC TO THE LAST TIE-BREAK, deliberately. Generated gear
        // means dozens of one-count stacks that differ only by plus, and any
        // pair left unordered would swap places between refreshes -- a grid
        // that reshuffles under the cursor as you equip things.
        public static IReadOnlyList<BagItem> Sorted(IEnumerable<BagItem> items)
        {
            if (items == null) return new List<BagItem>();

            return items
                .OrderByDescending(i => i.IsEquippable)   // wearable things first
                .ThenBy(i => i.Kind)                      // then grouped by kind
                .ThenByDescending(i => i.Tier)            // best first within a kind
                .ThenBy(i => i.Name, System.StringComparer.Ordinal)
                .ThenByDescending(i => i.Plus)            // the honed copy leads
                .ThenBy(i => i.Id, System.StringComparer.Ordinal)
                .ToList();
        }

        // How many pages a bag of this size needs.
        //
        // An EMPTY bag is one page, not zero: the pager reads "PAGE 1 OF 1"
        // over an empty grid, which is a state, where "PAGE 1 OF 0" is a bug
        // report.
        public static int PageCount(int itemCount)
        {
            if (itemCount <= CellCount) return 1;
            return (itemCount + CellCount - 1) / CellCount;
        }

        public static int ClampPage(int page, int itemCount)
        {
            int last = PageCount(itemCount) - 1;
            if (page < 0) return 0;
            return page > last ? last : page;
        }

        // The slice of a sorted list that belongs on one page. Short on the
        // last page rather than padded -- the screen hides the leftover cells,
        // which is a different job from inventing empty rows here.
        public static IReadOnlyList<BagItem> Page(IReadOnlyList<BagItem> sorted, int page)
        {
            if (sorted == null || sorted.Count == 0) return new List<BagItem>();

            int start = ClampPage(page, sorted.Count) * CellCount;
            return sorted.Skip(start).Take(CellCount).ToList();
        }
    }
}
