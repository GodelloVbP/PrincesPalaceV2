namespace PrincesPalace.Domain.Events
{
    // The arithmetic of a healPercent / damagePercent effect on one
    // character, apart from the save it is applied to, so both rules are
    // pinnable in EditMode (RunEncounter owns the loop over the squad).
    //
    // The percentage is OF MAXIMUM HEALTH and rounds UP, so a 1% effect on a
    // small maximum still moves one point rather than silently doing nothing.
    public static class EventHealth
    {
        public static int PercentOf(int maxHealth, int percent)
        {
            if (maxHealth <= 0 || percent <= 0) return 0;
            return (maxHealth * percent + 99) / 100;
        }

        // Clamped at the maximum. A downed character (0) is healed like
        // anyone else, the same call RunEncounter.HealPartyToFull makes for a
        // rest: a heal that skipped the fallen would be the one heal that
        // does not answer a bad fight.
        public static int Healed(int current, int maxHealth, int percent)
        {
            int from = current < 0 ? 0 : current;
            int to = from + PercentOf(maxHealth, percent);
            return to > maxHealth ? maxHealth : to;
        }

        // The ONE-MEMBER heal (a healPercent effect naming a character):
        // unlike the party heal above it NEVER REVIVES. A downed character
        // stays at 0 (PLAN_PETTING_ZOO D5: a downed Shawn counts as absent,
        // so a pet cannot stand him back up).
        public static int HealedWithoutRevive(int current, int maxHealth, int percent)
        {
            if (current <= 0) return 0;
            return Healed(current, maxHealth, percent);
        }

        // FLOORED AT 1 AND NEVER KILLS (plan contract 10, assumption 7). A
        // character already at 0 stays at 0 -- flooring them to 1 would make
        // a damage effect a revive.
        public static int Damaged(int current, int maxHealth, int percent)
        {
            if (current <= 0) return current < 0 ? 0 : current;

            int to = current - PercentOf(maxHealth, percent);
            return to < 1 ? 1 : to;
        }
    }
}
