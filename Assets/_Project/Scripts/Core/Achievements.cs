using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace
{
    // What this profile has earned.
    //
    // The Core half of the achievement system: Domain owns the RULES (what
    // condition means what, and whether some facts satisfy it), this owns
    // GATHERING the facts, because they live scattered across a SaveData that
    // Domain cannot see.
    //
    // Evaluated on demand rather than stored. An earned-flag written once and
    // then relied on is a flag that can be wrong -- if the ledger says a
    // hundred rooms and the flag says no, the flag loses. The only thing
    // persisted is the raw history the facts are derived from, which is data
    // the game needs anyway.
    public static class Achievements
    {
        // Everything an achievement is allowed to look at, gathered in ONE
        // place. A condition that wants a new fact adds it here and in
        // AchievementFacts, and nowhere else.
        public static AchievementFacts FactsFor(SaveData save)
        {
            if (save == null) return new AchievementFacts();

            int highestLevel = 0;
            foreach (var character in save.roster ?? new List<Character>())
            {
                if (character != null && character.level > highestLevel) highestLevel = character.level;
            }

            // LIFETIME PLUS THE LIVE RUN.
            //
            // The lifetime halves are folded in by RunSettlement before EndRun
            // discards the snapshot; the active run's contribution is added on
            // top so a total ticks up DURING a descent rather than only when it
            // ends. That cannot double-count: once settled, the run is gone, so
            // its contribution is zero and only the fold remains.
            //
            // Deepest step is the exception -- it is a high-water mark, not a
            // sum, so lifetime and current are compared rather than added.
            var run = save.activeRun;

            return new AchievementFacts(
                defeatedBossIds: save.defeatedBossIds ?? new List<string>(),
                highestCharacterLevel: highestLevel,
                roomsCleared: save.lifetimeRoomsCleared + (run?.roomsCleared ?? 0),
                deepestStep: System.Math.Max(save.lifetimeDeepestStep, run?.deepestStep ?? 0),
                totalDamageDealt: save.lifetimeDamageDealt + TotalDamage(run));
        }

        private static long TotalDamage(RunSnapshot run)
        {
            if (run?.ledger == null) return 0;

            long total = 0;
            foreach (var entry in run.ledger)
            {
                if (entry != null) total += entry.TotalDealt;
            }

            return total;
        }

        // The ids this profile has earned. A HashSet because RelicPool asks it
        // "contains?" once per relic per draft.
        public static HashSet<string> EarnedIds(SaveData save)
        {
            var earned = new HashSet<string>();
            var facts = FactsFor(save);

            foreach (var definition in ContentDatabase.Achievements)
            {
                if (definition != null && AchievementProgress.IsEarned(definition.ToResolved(), facts))
                {
                    earned.Add(definition.id);
                }
            }

            return earned;
        }

        public static bool IsEarned(SaveData save, string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return false;

            var definition = ContentDatabase.Achievements.FirstOrDefault(a => a != null && a.id == achievementId);
            return definition != null && AchievementProgress.IsEarned(definition.ToResolved(), FactsFor(save));
        }
    }
}
