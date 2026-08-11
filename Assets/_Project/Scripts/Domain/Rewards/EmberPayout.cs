using System.Collections.Generic;

namespace PrincesPalace.Domain.Rewards
{
    // What a finished run pays in Embers.
    //
    // ONE RULE: an ember per boss you had never killed before. Not per run, not
    // per floor, not per death. A second kill of the same boss pays nothing,
    // which is what keeps the currency scarce as the roster of bosses grows
    // slowly and the player's skill grows quickly.
    //
    // The consequence is worth stating rather than discovering: the total
    // embers obtainable in the game is FIXED at the number of unique bosses.
    // Talent trees are 21 slots across 3 constellations per character, so
    // unless the boss roster is comparable to 63 x roster size, no player will
    // ever fill every tree. That is a design position -- a permanent, scarce,
    // mutually exclusive choice -- and not an oversight.
    //
    // Pure and engine-free so the settle-up is testable without a save file.
    public static class EmberPayout
    {
        public const int PerUniqueBoss = 1;

        // The bosses in `killedThisRun` that are not already in `alreadyKilled`,
        // in the order they were killed, deduplicated.
        //
        // Returns the IDS rather than a count, because the caller has to add
        // them to the lifetime list and paying without recording would let the
        // same boss pay twice on the next run.
        public static List<string> NewBosses(
            IEnumerable<string> killedThisRun, ICollection<string> alreadyKilled)
        {
            var fresh = new List<string>();
            if (killedThisRun == null) return fresh;

            var seen = new HashSet<string>();

            foreach (var id in killedThisRun)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id)) continue;
                if (alreadyKilled != null && alreadyKilled.Contains(id)) continue;

                fresh.Add(id);
            }

            return fresh;
        }

        public static int EmbersFor(int newBossCount) =>
            newBossCount <= 0 ? 0 : newBossCount * PerUniqueBoss;
    }
}
