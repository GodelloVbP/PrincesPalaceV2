using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    public enum PackEntryKind
    {
        Gear,
        Salve,
        Book,
    }

    // The LOADOUT pack's filter chips, left to right.
    public enum PackFilter
    {
        All,
        Armour,
        Weapons,
        Salves,
        Books,
    }

    // An unassigned spell book as the pack shows it: the run's id plus the
    // name content gives it, which Domain cannot look up for itself.
    public readonly struct PackBook
    {
        public readonly string Id;
        public readonly string Name;

        public PackBook(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    // One cell of the LOADOUT pack: a stack from the save's pack, or a book
    // from the run's unassigned books.
    public readonly struct PackEntry
    {
        public readonly PackEntryKind Kind;

        // Set for gear and salves.
        public readonly BagItem Item;

        // Set for a book.
        public readonly string BookId;
        public readonly string BookName;

        public readonly int Count;

        private PackEntry(PackEntryKind kind, BagItem item, string bookId, string bookName, int count)
        {
            Kind = kind;
            Item = item;
            BookId = bookId;
            BookName = bookName;
            Count = count;
        }

        public static PackEntry OfItem(BagItem item) =>
            new PackEntry(item.IsEquippable ? PackEntryKind.Gear : PackEntryKind.Salve, item, null, null, item.Count);

        public static PackEntry OfBook(string id, string name, int count) =>
            new PackEntry(PackEntryKind.Book, default, id, name, count);

        // The key the cell's icon is found by: the skill's id, or the item's
        // art path (gear art depends on the copy's tier), falling back to
        // its id.
        public string IconId => Kind == PackEntryKind.Book
            ? BookId
            : string.IsNullOrEmpty(Item.IconPath) ? Item.Id : Item.IconPath;

        // What the cell says when it has no art to show (ItemIcons.Apply
        // returned false).
        public string DisplayName => Kind == PackEntryKind.Book ? BookName : Item.Name;

        public bool IsWeapon => Kind == PackEntryKind.Gear && EquipmentSlots.IsWeaponSlot(Item.Slot);
    }

    // THE PACK THE LOADOUT SHOWS (plan C16): gear, salves and the run's
    // unassigned books in one grid. A view only -- the books stay in the run's
    // unassigned list and the items in the save's pack, so nothing about the
    // save changes by showing them together.
    public static class LoadoutPack
    {
        public static readonly PackFilter[] Filters =
        {
            PackFilter.All, PackFilter.Armour, PackFilter.Weapons, PackFilter.Salves, PackFilter.Books,
        };

        public static UiString Label(PackFilter filter)
        {
            switch (filter)
            {
                case PackFilter.All: return UiStrings.PackFilterAll;
                case PackFilter.Armour: return UiStrings.PackFilterArmour;
                case PackFilter.Weapons: return UiStrings.PackFilterWeapons;
                case PackFilter.Salves: return UiStrings.PackFilterSalves;
                case PackFilter.Books: return UiStrings.PackFilterBooks;
                default: throw new System.ArgumentOutOfRangeException(nameof(filter), filter, null);
            }
        }

        // Gear and salves in BagView's order (wearable things first, best
        // first within a kind), then the books by name. Copies of one book
        // are one cell with a count: two copies are interchangeable, and the
        // run takes the first match off its list whichever cell is pressed.
        public static IReadOnlyList<PackEntry> Build(IEnumerable<BagItem> items, IEnumerable<PackBook> books)
        {
            var entries = new List<PackEntry>();

            foreach (var item in BagView.Sorted(items ?? Enumerable.Empty<BagItem>()))
            {
                if (item.Count > 0) entries.Add(PackEntry.OfItem(item));
            }

            var byId = new Dictionary<string, (string Name, int Count)>();
            var order = new List<string>();
            foreach (var book in books ?? Enumerable.Empty<PackBook>())
            {
                if (string.IsNullOrEmpty(book.Id)) continue;

                if (byId.TryGetValue(book.Id, out var seen))
                {
                    byId[book.Id] = (seen.Name, seen.Count + 1);
                    continue;
                }

                byId[book.Id] = (string.IsNullOrEmpty(book.Name) ? book.Id : book.Name, 1);
                order.Add(book.Id);
            }

            foreach (var id in order
                         .OrderBy(id => byId[id].Name, System.StringComparer.Ordinal)
                         .ThenBy(id => id, System.StringComparer.Ordinal))
            {
                entries.Add(PackEntry.OfBook(id, byId[id].Name, byId[id].Count));
            }

            return entries;
        }

        public static bool Matches(PackEntry entry, PackFilter filter)
        {
            switch (filter)
            {
                case PackFilter.All: return true;
                case PackFilter.Armour: return entry.Kind == PackEntryKind.Gear && !entry.IsWeapon;
                case PackFilter.Weapons: return entry.IsWeapon;
                case PackFilter.Salves: return entry.Kind == PackEntryKind.Salve;
                case PackFilter.Books: return entry.Kind == PackEntryKind.Book;
                default: throw new System.ArgumentOutOfRangeException(nameof(filter), filter, null);
            }
        }

        public static IReadOnlyList<PackEntry> Filter(IReadOnlyList<PackEntry> entries, PackFilter filter) =>
            entries.Where(e => Matches(e, filter)).ToList();

        // What an armed gear slot may take from the pack: gear its slot
        // accepts, so a Weapon 1 item is offered to an empty Weapon 2.
        public static bool FitsGearSlot(PackEntry entry, EquipmentSlot slot) =>
            entry.Kind == PackEntryKind.Gear && EquipmentSlots.Accepts(slot, entry.Item.Slot);

        // The chip that stands lit while a slot is armed.
        public static PackFilter FilterFor(EquipmentSlot slot) =>
            EquipmentSlots.IsWeaponSlot(slot) ? PackFilter.Weapons : PackFilter.Armour;
    }
}
