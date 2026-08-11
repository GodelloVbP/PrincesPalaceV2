using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.DebugMenu
{
    // One row in the debug item picker.
    //
    // Kind is an int rather than ItemKind for the same reason BagItem does it:
    // ItemKind is a Core content enum and this is Domain, which cannot see
    // Core at all. The controller translates at the one place it already has
    // an ItemDefinition in hand.
    public readonly struct DebugItem
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Kind;
        public readonly int Tier;

        public DebugItem(string id, string name, int kind, int tier)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Tier = tier;
        }
    }

    // Filtering and paging for the debug item picker.
    //
    // This exists because the first sketch of the debug menu was a single
    // "give me every item" button, and there are 261 generated item assets
    // against a 20-cell bag -- 14 pages behind prev/next arrows with no way
    // to find anything. Granting from a PICKER instead of dumping into the
    // bag moves the browsing problem to the place that can afford to solve
    // it, and leaves the bag showing only what was deliberately asked for.
    //
    // Pure and engine-free, so the page arithmetic is EditMode-testable --
    // the same reason BagView is shaped this way.
    public static class DebugMenuCatalog
    {
        // How many rows one page shows. Read by the screen tree (which builds
        // exactly this many row nodes), the controller, and the count audit,
        // so there is no second place to state it.
        public const int RowsPerPage = 12;

        // The "no filter" sentinel. Not an ItemKind value because Domain has
        // no ItemKind, and not 0 because 0 is Consumable.
        public const int KindAll = -1;

        // Sorted by TIER FIRST, then name.
        //
        // Tier is the axis you actually search on in a debug menu -- "give me
        // a tier 4 weapon" is the question, not "give me something starting
        // with M". Sorting by name would scatter the tiers across every page
        // and make paging useless for the one thing the menu is for.
        public static IReadOnlyList<DebugItem> Filter(IEnumerable<DebugItem> all, int kind)
        {
            if (all == null) return new List<DebugItem>();

            return all
                .Where(i => kind == KindAll || i.Kind == kind)
                .OrderBy(i => i.Tier)
                .ThenBy(i => i.Name, System.StringComparer.Ordinal)
                .ThenBy(i => i.Id, System.StringComparer.Ordinal)
                .ToList();
        }

        // An empty result is still ONE page, so the pager reads "PAGE 1 OF 1"
        // rather than "PAGE 1 OF 0" -- the same rule BagView.PageCount holds.
        public static int PageCount(int count)
        {
            if (count <= 0) return 1;
            return (count + RowsPerPage - 1) / RowsPerPage;
        }

        public static int ClampPage(int page, int count)
        {
            int last = PageCount(count) - 1;
            if (page < 0) return 0;
            return page > last ? last : page;
        }

        public static IReadOnlyList<DebugItem> Page(IReadOnlyList<DebugItem> filtered, int page)
        {
            var rows = new List<DebugItem>();
            if (filtered == null) return rows;

            int start = ClampPage(page, filtered.Count) * RowsPerPage;
            for (int i = start; i < filtered.Count && rows.Count < RowsPerPage; i++)
            {
                rows.Add(filtered[i]);
            }

            return rows;
        }
    }
}
