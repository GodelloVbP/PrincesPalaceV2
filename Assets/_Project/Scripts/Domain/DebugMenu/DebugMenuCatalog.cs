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
    //
    // SubKey is the ONE extra axis a category's sub-filter row matches
    // against, and which field that is depends on the category -- Weapons
    // matches on Tier (the tier chips), Equipment matches on SubKey (its
    // equipSlot ordinal, translated by the controller same as Kind).
    // Consumables and Sets declare no sub-filter, so SubKey is unused and
    // left at -1 for them. One field rather than one per category because a
    // debug row is never asked two sub-filter questions at once -- only the
    // category it belongs to decides which axis is live.
    //
    // SetId is populated only for a Sets-category row, where it names the
    // group GrantSet expands into every piece when clicked. Empty everywhere
    // else, same "empty and absent read the same way" convention the rest of
    // the codebase's optional strings use.
    public readonly struct DebugItem
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Kind;
        public readonly int Tier;
        public readonly DebugCategory Category;
        public readonly int SubKey;
        public readonly string SetId;

        public DebugItem(string id, string name, int kind, int tier, DebugCategory category,
            int subKey = DebugMenuCatalog.SubFilterAll, string setId = "")
        {
            Id = id;
            Name = name;
            Kind = kind;
            Tier = tier;
            Category = category;
            SubKey = subKey;
            SetId = setId ?? "";
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
    // GENERALISED for the category rail (2026-09-23): what used to be one
    // flat kind filter is now a CATEGORY plus one optional SUB-FILTER, the
    // sub-filter's meaning decided by the category rather than restated
    // per-item. RowsPerPage doubled to 24 and laid out 2 columns x 12 rows
    // (Columns/RowsPerColumn below) rather than one column of 12, because
    // ~260 items behind a single-column pager was still a lot of paging even
    // after the category rail narrows the list.
    //
    // Pure and engine-free, so the filter/page arithmetic is EditMode-testable
    // -- the same reason BagView is shaped this way.
    public static class DebugMenuCatalog
    {
        // How many rows one page shows, and how that count is laid out. Read
        // by the screen tree (which builds exactly this many row nodes), the
        // controller, and the count audit, so there is no second place to
        // state any of the three.
        public const int Columns = 2;
        public const int RowsPerColumn = 12;
        public const int RowsPerPage = Columns * RowsPerColumn;

        // The "no sub-filter" sentinel, for both the sub-filter row (All
        // chip) and a DebugItem whose category never sets SubKey.
        public const int SubFilterAll = -1;

        // Sorted by TIER FIRST, then name.
        //
        // Tier is the axis you actually search on in a debug menu -- "give me
        // a tier 4 weapon" is the question, not "give me something starting
        // with M". Sorting by name would scatter the tiers across every page
        // and make paging useless for the one thing the menu is for.
        //
        // The sub-filter axis is category-specific (SubFilterValueOf below):
        // Equipment matches SubKey (its slot), every other category matches
        // Tier -- which for Weapons is exactly the "tier chips" the sub-filter
        // row shows, and for a category with no sub-filter row is a no-op
        // since the controller only ever passes SubFilterAll for those.
        public static IReadOnlyList<DebugItem> Filter(IEnumerable<DebugItem> all, DebugCategory category,
            int subFilter)
        {
            if (all == null) return new List<DebugItem>();

            return all
                .Where(i => i.Category == category)
                .Where(i => subFilter == SubFilterAll || SubFilterValueOf(i) == subFilter)
                .OrderBy(i => i.Tier)
                .ThenBy(i => i.Name, System.StringComparer.Ordinal)
                .ThenBy(i => i.Id, System.StringComparer.Ordinal)
                .ToList();
        }

        private static int SubFilterValueOf(DebugItem item) =>
            item.Category == DebugCategory.Equipment ? item.SubKey : item.Tier;

        // Resources and Tools rows are verbs, not content (DebugCategory's
        // header). For them Tier carries the row's POSITION rather than a
        // tier, so the tier-first sort above lays actions out in the order
        // the controller declared them -- "+100, +1000, +10000" rather than
        // whatever an ordinal name sort makes of those -- and the grant bar's
        // plus/quantity are not applied.
        public static bool IsAction(DebugCategory category) =>
            category == DebugCategory.Resources || category == DebugCategory.Tools;

        // The grant bar's plus stepper. Clamped at both ends rather than
        // wrapping: stepping past +10 landing on +0 would silently undo the
        // thing the last nine presses were for.
        public static int StepPlus(int plus, int direction) =>
            System.Math.Max(0, System.Math.Min(Stats.ItemUpgrade.MaxPlus, plus + direction));

        // Delegated to Paging -- see GlossaryCatalog for why this stopped
        // being written out three times.
        public static int PageCount(int count) => UiKit.Paging.PageCount(count, RowsPerPage);

        public static int ClampPage(int page, int count) => UiKit.Paging.Clamp(page, count, RowsPerPage);

        public static IReadOnlyList<DebugItem> Page(IReadOnlyList<DebugItem> filtered, int page) =>
            UiKit.Paging.Slice(filtered, page, RowsPerPage);
    }
}
