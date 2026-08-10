using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // A small, deliberately short table of cross-system payoffs for landing
    // a matching element on top of a status already on the board — the
    // Divinity: Original Sin idea of combining two things for a bonus,
    // translated onto this game's actual statuses rather than a Wet/surface
    // layer it has no equivalent of.
    //
    // ONE reaction, not a matrix, on purpose. A Nature or Poison hit against
    // an already-Poisoned target detonates it: the target takes everything
    // its remaining ticks would have dealt, right now, and the Poison is
    // spent. Kept to exactly this one reaction because it is the one every
    // already-authored piece of content supports without inventing anything
    // new to combo with — Fly's Sting applies Poison, and Shawn's own
    // Nature-typed Attack is a real, already-existing follow-up. A wider
    // table (Fire clearing a Wet status, and so on) would need statuses this
    // game does not have and is a design decision for whoever adds them, not
    // something to speculatively wire ahead of the content that would use it.
    public static class StatusCombos
    {
        // Returns how much bonus damage was dealt (0 if nothing detonated),
        // and applies it directly through CombatMath.ApplyDamage — same as
        // every other source of damage in the game, not a special case the
        // caller has to know how to apply.
        public static int DetonatePoisonIfMatched(CombatantState target, DamageType incomingType)
        {
            if (target == null || (incomingType != DamageType.Nature && incomingType != DamageType.Poison))
            {
                return 0;
            }

            var poison = target.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Poison);
            if (poison == null)
            {
                return 0;
            }

            int bonus = poison.Magnitude * poison.TurnsRemaining;
            target.Statuses.Remove(poison);

            if (bonus > 0)
            {
                CombatMath.ApplyDamage(target, bonus);
            }

            return bonus;
        }
    }
}
