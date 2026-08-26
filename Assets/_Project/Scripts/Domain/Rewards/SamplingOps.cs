using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Rewards
{
    // The no-repeat draw every reward table in this namespace needs:
    // ModifierTable.PickModifiers and ItemOfferTable.Choose both pick
    // several DISTINCT entries out of a pool using the same
    // draw-an-index/clamp/take-it-out technique, kept here once rather than
    // hand-rolled twice so the two cannot quietly drift.
    public static class SamplingOps
    {
        // Picks up to `count` DISTINCT entries out of `pool`, in the order
        // rolled, MUTATING `pool` as it goes (each pick is removed). Callers
        // that must not disturb their own list -- ModifierTable.PickModifiers
        // takes an IReadOnlyList<T> -- pass a throwaway copy in.
        //
        // Removal-based rather than reject-and-retry: retrying on a repeat
        // would make the number of draws (and so a seeded run's
        // reproducibility) depend on how often the loop got unlucky.
        //
        // Returns fewer than `count` only when `pool` itself is that thin --
        // degrading gracefully rather than repeating an entry.
        public static List<T> SampleWithoutReplacement<T>(List<T> pool, int count, Func<int, int> nextIndex)
        {
            var picked = new List<T>();
            if (pool == null || pool.Count == 0 || nextIndex == null || count <= 0)
            {
                return picked;
            }

            int target = count > pool.Count ? pool.Count : count;
            for (int i = 0; i < target; i++)
            {
                int index = nextIndex(pool.Count);
                if (index < 0 || index >= pool.Count) index = 0;

                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return picked;
        }
    }
}
