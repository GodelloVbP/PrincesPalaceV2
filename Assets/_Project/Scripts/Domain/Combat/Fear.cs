using System.Linq;

namespace PrincesPalace.Domain.Combat
{
    // FEAR: Stunned (skips the holder's turn -- StatusEffects.HasStun treats
    // Feared exactly like Stun, see StatusEffectType.Feared's own header)
    // plus Vulnerable (StatusEffects.DamageTakenMultiplier reads the same
    // Magnitude the way it reads a Vulnerable status), for a fixed number of
    // the holder's own turns.
    //
    // ONE AUTHORED NUMBER (VulnerablePercent), not per-caller, so two things
    // that both apply Fear cannot quietly disagree about how vulnerable it
    // makes its target.
    public static class Fear
    {
        // No art yet -- see Marks.IconKey's own comment for the convention.
        public const string IconKey = "Assets/_Project/Art/Items/Relics/Processed/status_feared.png";

        public const int VulnerablePercent = 25;

        public static void Apply(CombatantState target, int durationTurns, CombatantState source = null)
        {
            if (target == null || durationTurns <= 0) return;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Feared, VulnerablePercent, durationTurns, source);
        }

        public static bool IsFeared(CombatantState target)
        {
            return target != null && target.Statuses.Any(s => s.Type == StatusEffectType.Feared);
        }
    }
}
