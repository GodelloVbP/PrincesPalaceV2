using System;

namespace PrincesPalace.Domain.Stats
{
    // The one rounding rule every scaled stat and damage figure in the game
    // uses, so a formula that multiplies a whole-number stat or damage
    // figure by a fraction (StatBlock.Scaled/ScaledForElite, CombatMath's
    // ScaledAttack/ApplyEffectiveness/ApplyStatusEffects, FightController's
    // damage-variance roll and per-hit multipliers) can't silently pick up a
    // different midpoint rule from its neighbour and disagree at the exact
    // .5 tie. AUDIT.md #16 recorded three different conventions coexisting
    // in one pipeline before this existed.
    //
    // Away-from-zero, matching CombatMath's own long-standing choice rather
    // than UnityEngine.Mathf.RoundToInt's to-even (banker's) rounding — see
    // CombatMath.ApplyEffectiveness's worked example (a 25-point packet
    // into a resistance is exactly 12.5) for why the tie case matters here
    // and isn't merely cosmetic.
    public static class Rounding
    {
        public static int AwayFromZero(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
