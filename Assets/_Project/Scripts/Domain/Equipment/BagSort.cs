using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Equipment
{
    // Which way the pack is ordered, chosen by the player.
    //
    // THREE KEYS, NOT FOUR. The ask was "tier / rarity / +'s / name", and
    // rarity is not a fourth axis in this game: RarityBands.For(tier) derives
    // the band FROM the tier, so ordering by rarity is ordering by tier with
    // the precision thrown away. Two buttons producing the same list is the
    // same failure as a control that stores nothing -- the player presses it,
    // believes something changed, and is wrong -- so rarity folds into Tier
    // and the tick on each row keeps showing the band.
    public enum BagSortKey
    {
        // The default, and the one the pack has always used: wearable things
        // first, grouped by kind, best first within a kind.
        Tier,

        // Most honed first. The reason this is worth its own key is generated
        // gear: a pack full of one-count stacks that differ only by plus is
        // exactly the case where "which of these is the good one" is hard to
        // see under any other ordering.
        Plus,

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
            BagSortKey.Plus,
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
        // of stacks that differ only by plus, and any pair left unordered would
        // swap places between refreshes -- a list that reshuffles under the
        // cursor as you equip things. Every branch below therefore ends on Id.
        public static IReadOnlyList<BagItem> By(IEnumerable<BagItem> items, BagSortKey key)
        {
            if (items == null) return new List<BagItem>();

            switch (key)
            {
                case BagSortKey.Plus:
                    return items
                        .OrderByDescending(i => i.Plus)
                        .ThenByDescending(i => i.Tier)
                        .ThenBy(i => i.Name, System.StringComparer.Ordinal)
                        .ThenBy(i => i.Id, System.StringComparer.Ordinal)
                        .ToList();

                case BagSortKey.Name:
                    return items
                        .OrderBy(i => i.Name, System.StringComparer.Ordinal)
                        .ThenByDescending(i => i.Plus)
                        .ThenBy(i => i.Id, System.StringComparer.Ordinal)
                        .ToList();

                // Tier is BagView.Sorted itself rather than a fourth ordering
                // written out again here -- it is the pack's long-standing
                // default and the one every other screen's expectations were
                // built against.
                default:
                    return BagView.Sorted(items);
            }
        }
    }
}
