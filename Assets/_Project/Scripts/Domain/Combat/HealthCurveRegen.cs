using System;

namespace PrincesPalace.Domain.Combat
{
    // THICK BLOOD'S REGEN (docs/PLAN_BJORN_CONSTELLATIONS.md section 4, the
    // TalentEffectType.HealthCurveRegen rule): a percent of max health healed
    // at the start of the holder's turn, on the same curve the Juggernaut's
    // Fury income rides. `floor` percent at full health, rising to `ceiling`
    // percent once he is at 25% health or lower, and in between
    //
    //   floor + (ceiling - floor) x t^2,   t = clamp((1 - HP%) / 0.75, 0, 1)
    //
    // so it is modest above half health and steep below, the shape the plan
    // asks for ("modest above half health, steep below").
    //
    // INTEGERS ONLY, floored, and never less than 1 for a positive rule: a
    // pure-arithmetic function the session calls and the tests pin with
    // literals. t^2 = 16 missing^2 / (9 max^2) while under three quarters
    // missing, 1 from there down, exactly as FuryEngine.JuggernautFury expands
    // it.
    public static class HealthCurveRegen
    {
        public static int AmountFor(int currentHealth, int maxHealth, int floorPercent, int ceilingPercent)
        {
            if (maxHealth <= 0 || ceilingPercent <= 0) return 0;

            int floor = Math.Max(0, Math.Min(floorPercent, ceilingPercent));
            long spread = (long)(ceilingPercent - floor) * 100;
            long missing = Math.Max(0, Math.Min(maxHealth, maxHealth - Math.Max(0, currentHealth)));

            long rise = 4 * missing >= 3L * maxHealth
                ? spread
                : spread * 16 * missing * missing / (9L * maxHealth * maxHealth);

            // Hundredths of a percent of max health.
            long hundredths = floor * 100L + rise;
            long amount = maxHealth * hundredths / 10000;
            return (int)Math.Max(1, amount);
        }
    }
}
