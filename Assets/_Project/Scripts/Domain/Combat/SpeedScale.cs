using System;

namespace PrincesPalace.Domain.Combat
{
    // Converts a combatant's Speed stat into how fast it charges toward its
    // next turn. Pure maths, no state — the scheduling that uses it lives in
    // TurnOrder.
    //
    // The design brief was "reward speed, with diminishing returns, so you
    // cannot cheese endless turns". Those pull against each other, so this
    // uses TWO independent mechanisms rather than one:
    //
    //   1. A SUB-LINEAR CURVE. rate = (speed / Baseline) ^ Exponent, with
    //      Exponent below 1. At 0.5 that is a square root: doubling your
    //      speed buys ~1.41x the turns, quadrupling buys 2x. Speed always
    //      helps and never stops helping, but each further point buys less
    //      than the last, so dumping everything into it is self-limiting
    //      rather than forbidden.
    //
    //   2. A HARD CEILING. The curve alone still grows without bound — it
    //      only grows slowly — so a sufficiently extreme build could still
    //      reach an arbitrary number of turns per round. MaxRate caps the
    //      ratio outright. This is what actually makes "eternal turn cheese"
    //      impossible rather than merely expensive: no build, however
    //      absurd, gets more than MaxRate turns per baseline turn.
    //
    // A floor matters too. Rate must never reach zero or a slow combatant
    // would never act at all — a soft-lock rather than a disadvantage.
    public static class SpeedScale
    {
        // The speed a "normal" combatant has; charges at exactly 1.0. Sits
        // at the party's own starting speeds so the curve is centred on real
        // characters rather than an arbitrary number.
        public const float BaselineSpeed = 10f;

        // Below 1 gives diminishing returns. 0.5 (square root) is the
        // readable choice: "four times the speed for twice the turns" is a
        // rule a player can hold in their head.
        private const float Exponent = 0.5f;

        // At most this many turns per baseline turn, however high Speed
        // goes. 2.5 lets a genuinely fast build act meaningfully more often
        // without ever lapping a normal one badly enough to remove their
        // input.
        public const float MaxRate = 2.5f;

        // And never slower than this, so a heavy tank still gets turns.
        public const float MinRate = 0.35f;

        public static float TickRate(int speed)
        {
            if (speed <= 0)
            {
                // Non-positive speed is a content error rather than a valid
                // build, but it must not soft-lock the fight.
                return MinRate;
            }

            float raw = (float)Math.Pow(speed / BaselineSpeed, Exponent);
            return Math.Min(MaxRate, Math.Max(MinRate, raw));
        }
    }
}
