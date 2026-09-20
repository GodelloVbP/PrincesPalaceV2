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
    // spent -- ALL of it, since Poison stacks and a target can be carrying
    // several instances at once. Kept to exactly this one reaction because it is the one every
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
        // EVERY INSTANCE, IN ONE CONSUMPTION. Poison stacks as of 2026-09-20
        // (StatusEffects.StackPolicyOf), so "the Poison entry" is no longer a
        // thing: a target can carry three, from three casters, with three
        // clocks. Detonating one and leaving the other two ticking would read
        // to a player as a detonation that did nothing, and would leave the
        // pile detonatable again by the very next Nature hit.
        //
        // ONE FIGURE, ONE RETURN, and therefore one DealDamage and one ledger
        // row upstairs -- the alternative, a bonus per instance, would record
        // the same detonation three times and would round a premium three
        // times over.
        //
        // premiumPercent (plan D2/1.6, milestone B): applied ONCE to the
        // summed worth, never per instance -- three instances each marked up
        // and then added would round three times and quietly pay a different
        // figure for the same board than one instance worth the same total.
        // Defaults to 100, which is every ordinary Nature/Poison hit and is
        // exactly today's arithmetic; Ashen Reckoning is the one caller that
        // passes something else.
        public static int SpendPoisonIfMatched(CombatantState target, DamageType incomingType, int premiumPercent = 100)
        {
            if (target == null || (incomingType != DamageType.Nature && incomingType != DamageType.Poison))
            {
                return 0;
            }

            // TRYSPEND IN A LOOP, not a foreach over the list -- this both
            // reads and REMOVES each instance as it goes, which is what makes
            // "every instance, in one consumption" (D3/1.6) hold for a pile of
            // any size without a second pass to clear what the first one
            // already summed. A zero-magnitude entry is still consumed here,
            // the same as the old RemoveAll-on-the-degenerate-case did: a
            // Poison worth nothing is not a Poison the next hit should find.
            int worth = 0;
            while (StatusEffects.TrySpend(target.Statuses, StatusEffectType.Poison, out var spent))
            {
                worth += spent.Magnitude * spent.TurnsRemaining;
            }

            if (worth <= 0)
            {
                return 0;
            }

            return Rounding.AwayFromZero(worth * premiumPercent / 100f);
        }
    }
}
