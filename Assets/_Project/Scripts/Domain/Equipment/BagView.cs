using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

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
        public readonly int Count;
        public readonly string IconPath;
        public readonly bool IsEquippable;

        // The rolled affix ids this STACK carries. Never null -- same "empty
        // and absent read the same way" convention InventoryEntry.modifierIds
        // itself uses, so a caller can foreach ModifierIds without a guard.
        public readonly IReadOnlyList<string> ModifierIds;

        // WHICH COPY this row is, whole. What an equip from the grid hands to
        // EquipMove, so the row's provenance goes onto the body with it rather
        // than being rebuilt from the display fields above (which cannot say
        // which lot a row is). Built from those fields when the caller has no
        // entry to take it from.
        public readonly ItemInstance Instance;

        public BagItem(string id, string name, int kind, EquipmentSlot slot,
                       int tier, int count, string iconPath, bool isEquippable,
                       IReadOnlyList<string> modifierIds = null,
                       ItemInstance instance = null)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Slot = slot;
            Tier = tier;
            Count = count;
            IconPath = iconPath;
            IsEquippable = isEquippable;
            ModifierIds = modifierIds ?? new List<string>();
            Instance = instance ?? ItemInstance.Of(id, tier, modifierIds);
        }
    }

    // What the bag grid shows, and in what order.
    //
    // Pure arithmetic over a list someone else built, so every rule below is
    // pinned by a literal fixture instead of by opening the screen and
    // squinting at it.
    public static class BagView
    {
        // Ordered so the useful things are on page one.
        //
        // DETERMINISTIC TO THE LAST TIE-BREAK, deliberately. Generated gear
        // means dozens of one-count stacks that differ only by plus, and any
        // pair left unordered would swap places between refreshes -- a grid
        // that reshuffles under the cursor as you equip things.
        public static IReadOnlyList<BagItem> Sorted(IEnumerable<BagItem> items) =>
            Sorted(items, i => i);

        // The same order over any row that carries a BagItem, so a screen that
        // must keep a row's save index beside it sorts by this one rule.
        public static IReadOnlyList<T> Sorted<T>(IEnumerable<T> rows, System.Func<T, BagItem> item)
        {
            if (rows == null) return new List<T>();

            return rows
                .OrderByDescending(r => item(r).IsEquippable)   // wearable things first
                .ThenBy(r => item(r).Kind)                      // then grouped by kind
                .ThenByDescending(r => item(r).Tier)            // best first within a kind
                .ThenBy(r => item(r).Name, System.StringComparer.Ordinal)
                .ThenBy(r => item(r).Id, System.StringComparer.Ordinal)
                .ToList();
        }

    }
}
