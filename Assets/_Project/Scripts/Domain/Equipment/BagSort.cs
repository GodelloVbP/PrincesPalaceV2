using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Equipment
{
    // Which way the pack is ordered, chosen by the player.
    //
    // TWO KEYS. Quality is derived from what rolled on a copy and tier is its
    // own number; honing belongs to the slot, so no bag copy has a plus to
    // order by.
    public enum BagSortKey
    {
        // The default, and the one the pack has always used: wearable things
        // first, grouped by kind, best first within a kind.
        Tier,

        // Alphabetical, for finding a thing you already know the name of.
        Name,
    }

    public static class BagSort
    {
        // In the order the buttons are drawn, so the screen, the controller and
        // the count audit all read one list.
        public static readonly BagSortKey[] All =
        {
            BagSortKey.Tier,
            BagSortKey.Name,
        };

        public static int IndexOf(BagSortKey key)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == key) return i;
            }

            return 0;
        }

        // DETERMINISTIC TO THE LAST TIE-BREAK, whichever key is chosen, and for
        // the reason BagView.Sorted already states: generated gear means dozens
        // of stacks that differ only by tier or roll, and any pair left
        // unordered would swap places between refreshes -- a list that
        // reshuffles under the cursor as you equip things. Every branch below
        // therefore ends on Id.
        public static IReadOnlyList<BagItem> By(IEnumerable<BagItem> items, BagSortKey key) =>
            By(items, i => i, key);

        // The same orders over any row that carries a BagItem: the shop's pack
        // keeps each row's save index beside it, so a sell still hits the entry
        // the row shows.
        public static IReadOnlyList<T> By<T>(IEnumerable<T> rows, System.Func<T, BagItem> item, BagSortKey key)
        {
            if (rows == null) return new List<T>();

            switch (key)
            {
                case BagSortKey.Name:
                    return rows
                        .OrderBy(r => item(r).Name, System.StringComparer.Ordinal)
                        .ThenByDescending(r => item(r).Tier)
                        .ThenBy(r => item(r).Id, System.StringComparer.Ordinal)
                        .ToList();

                // Tier is BagView.Sorted itself rather than a fourth ordering
                // written out again here -- it is the pack's long-standing
                // default and the one every other screen's expectations were
                // built against.
                default:
                    return BagView.Sorted(rows, item);
            }
        }
    }
}
