using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Page arithmetic, once.
    //
    // BagView, DebugMenuCatalog and GlossaryCatalog each grew their own
    // identical PageCount/ClampPage/Page within a day of each other. The
    // arithmetic is four lines, which is exactly why it kept being rewritten
    // rather than shared -- and it has two off-by-one traps in it that are
    // invisible when wrong:
    //
    //   * an EMPTY list is ONE page, not zero. "PAGE 1 OF 0" is a bug report.
    //   * EXACTLY pageSize entries is one page, not two. A second, blank page
    //     the player can flip to reads as content that failed to load.
    //
    // Three copies means three chances to get those wrong and three places to
    // fix them. One copy, tested once.
    public static class Paging
    {
        public static int PageCount(int itemCount, int pageSize)
        {
            if (pageSize <= 0 || itemCount <= 0) return 1;
            return (itemCount + pageSize - 1) / pageSize;
        }

        public static int Clamp(int page, int itemCount, int pageSize)
        {
            int last = PageCount(itemCount, pageSize) - 1;
            if (page < 0) return 0;
            return page > last ? last : page;
        }

        // The slice belonging to one page. SHORT on the last page rather than
        // padded -- inventing empty rows here is a different job from the
        // screen hiding its leftover cells, and doing both would double up.
        public static List<T> Slice<T>(IReadOnlyList<T> all, int page, int pageSize)
        {
            var rows = new List<T>();
            if (all == null || pageSize <= 0) return rows;

            int start = Clamp(page, all.Count, pageSize) * pageSize;
            for (int i = start; i < all.Count && rows.Count < pageSize; i++)
            {
                rows.Add(all[i]);
            }

            return rows;
        }
    }
}
