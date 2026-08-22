namespace PrincesPalace.Domain.Progression
{
    // What happens to current health when the maximum it is measured against
    // moves under it.
    //
    // A run stores current health as an ABSOLUTE number per character, and the
    // maximum is derived from the character -- so anything that changes the
    // maximum changes the fraction without touching the number. Equipping a
    // +20 max health item at 50 of 100 left the run holding 50 against a new
    // maximum of 120, and the bar grew a tail of empty at the end that nothing
    // short of a rest could fill.
    //
    // THE FRACTION IS WHAT IS PRESERVED, not the difference, and the argument
    // is the equip/unequip cycle rather than taste. Adding the delta on the way
    // in and clamping on the way out is a healing exploit: put the item on at
    // 50/100 for 70/120, take it off for 70 clamped to 100, repeat until full.
    // Scaling both ways is symmetric -- a round trip returns exactly what it
    // took, which CarriedHealthTests pins with a literal.
    //
    // PURE, AND IN DOMAIN, so the arithmetic can be tested without a save, a
    // run, or an engine. RunEncounter.ScaleCarriedHealth is the half that knows
    // where the numbers live.
    public static class CarriedHealth
    {
        public static int Rescaled(int current, int previousMax, int newMax)
        {
            // Nothing to preserve, or nothing to preserve it against.
            if (previousMax <= 0 || newMax <= 0 || newMax == previousMax) return current;

            // A DOWNED CHARACTER STAYS DOWN. Zero times any fraction is zero,
            // and the floor below is applied only to someone who was standing
            // -- otherwise a max-health item would be a resurrection.
            if (current <= 0) return current;

            // Rounded rather than truncated. Truncation loses a point on most
            // changes and compounds: a player trying gear on and off would
            // bleed out doing it, one hit point per swap.
            long scaled = ((long)current * newMax + previousMax / 2) / previousMax;

            if (scaled < 1) scaled = 1;
            if (scaled > newMax) scaled = newMax;

            return (int)scaled;
        }
    }
}
