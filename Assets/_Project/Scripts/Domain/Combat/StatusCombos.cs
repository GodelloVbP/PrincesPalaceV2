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
        // Spends the Poison and reports what its remaining ticks are WORTH.
        // Deals nothing.
        //
        // IT USED TO DEAL IT, straight through CombatMath.ApplyDamage, and
        // that was the whole bug: this is called from inside
        // DamagePipeline.AfterDefences, which is also what every TELEGRAPH
        // preview runs through. A preview passes rng: null and
        // resolveWard: null so it cannot consume a draw or spend a ward --
        // and the combo had no such collaborator to leave out, so preparing
        // an intent for a Poison-typed monster spent a party member's Poison
        // and took the health for it before anything had swung. The damage
        // also never reached FightSession.DealDamage, so it was missing from
        // the ledger and a detonation that felled its target settled no
        // death: no kill row, no rider eligibility.
        //
        // Splitting the two halves is what fixes both at once. The SPEND
        // stays here, where the rule lives; the DAMAGE belongs to whoever
        // owns a damage funnel, which is the session (see
        // FightSession.ResolveDetonation).
        public static int SpendPoisonIfMatched(CombatantState target, DamageType incomingType)
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
            return bonus;
        }
    }
}
