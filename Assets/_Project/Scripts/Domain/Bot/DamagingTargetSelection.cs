using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Bot
{
    // Shared fix for GreedyAggressivePolicy and GreedyDefensivePolicy's
    // "hit back" branch: picking a target first (lowest HP / most
    // threatening) and only THEN filtering candidates down to that
    // target's own Attack/Skill options silently drops Attack whenever the
    // locked target sits outside melee reach, leaving only whatever
    // non-damaging Skill also happens to carry that target's (possibly
    // bogus, see FightAction.LegalActions' per-target loop over a
    // Self/AllEnemies skill) Target.
    //
    // The fix: only rank targets that at least one DAMAGING candidate
    // (EstimateDamage > 0) can actually reach, THEN lock the target and
    // pick the best-estimated damaging action against it. Attack's own
    // floor (CombatMath.ComputeAttackDamage is Max(1, ...)) means a legal
    // Attack always keeps its target eligible here.
    public static class DamagingTargetSelection
    {
        // `damaging`: every legal Attack/Skill action -- the caller's own
        // `legal.Where(a => a.Kind == Attack || a.Kind == Skill)`.
        // `estimateDamage`: the caller's own pre-mitigation ranking read
        // (GreedyAggressivePolicy/GreedyDefensivePolicy.EstimateDamage).
        // `pickTarget`: the caller's own preference order over the
        // ELIGIBLE targets only (lowest HP first for aggressive, most
        // threatening first for defensive) -- everything upstream of "which
        // targets are even reachable by real damage" stays exactly as each
        // archetype already read it.
        //
        // Returns false when no candidate in `damaging` can put a positive
        // number on anyone right now (a kit with nothing but Provoke/Ward/
        // Shatter-with-nothing-to-detonate legal, say) -- the caller's
        // existing non-damaging fallback is what that case is for.
        public static bool TryChooseDamagingAction(
            IReadOnlyList<FightAction> damaging,
            Func<FightAction, int> estimateDamage,
            Func<IEnumerable<CombatantState>, CombatantState> pickTarget,
            out FightAction best)
        {
            best = default;
            if (damaging == null || damaging.Count == 0) return false;

            var eligibleTargets = damaging
                .Select(a => a.Target)
                .Distinct()
                .Where(t => damaging.Any(a => a.Target == t && estimateDamage(a) > 0))
                .ToList();

            if (eligibleTargets.Count == 0) return false;

            var lockedTarget = pickTarget(eligibleTargets);

            int bestEstimate = int.MinValue;
            bool found = false;

            foreach (var candidate in damaging)
            {
                if (candidate.Target != lockedTarget) continue;

                int estimate = estimateDamage(candidate);
                if (!found || estimate > bestEstimate)
                {
                    best = candidate;
                    bestEstimate = estimate;
                    found = true;
                }
            }

            return found;
        }
    }
}
