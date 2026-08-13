using System;

namespace PrincesPalace.Domain.Dungeon
{
    // How much harder the dungeon gets as a run goes deeper.
    //
    // THIS DID NOT EXIST. Before the descent became continuous, a floor-9
    // fight was statistically identical to a floor-1 fight: the same enemy
    // pool, the same baseStats, the same 1-2 count. The only multiplier in
    // the whole game was FightController's EliteStatMultiplier. That was
    // survivable while a run was seven columns and a boss; an infinite map
    // without a curve is an infinite corridor of trivial fights, so the leg
    // generator and this file ship together on purpose.
    //
    // AND THEN IT WAS NEVER CALLED. Everything above was written, tested and
    // documented as scaling "health, attack and break shields alike", and the
    // only live caller in the entire project was VictoryRewards. Enemy stats
    // went through no curve at all: the corridor of trivial fights this file
    // exists to prevent is what actually shipped, with a green suite over it.
    // Same shape as AUDIT #42 -- implemented, covered by tests that build
    // their own inputs, and unreachable from play. It is wired now, in
    // FightEncounterAdapter.ToCombatant, and EnemiesGetTougherWithDepth pins
    // the wiring rather than the arithmetic.
    //
    // Pure and engine-free so the curve can be pinned by literal tests
    // rather than sampled by playing.
    public static class DifficultyCurve
    {
        // GEOMETRIC, and the previous comment here argued at length for the
        // opposite. It was right at the time and is worth quoting, because
        // what changed is the premise and not the reasoning:
        //
        //   "A geometric curve is the obvious first instinct and is wrong
        //    here [...] which outruns anything the item ladder can answer (a
        //    fully honed top-tier set is worth roughly 4x a starting one)."
        //
        // The item ladder now answers a great deal more than 4x. GearScaling
        // put tier 10 at 9.31x tier 0, AbilityDerivation squares whatever gear
        // contributes, and the two compose: a fully geared character's DAMAGE
        // grows 374x across the ladder and their HEALTH 57x. Against that, a
        // straight line reaching 5.4x at step 80 is not a difficulty curve, it
        // is a rounding error.
        //
        // TWO RATES, NOT ONE, and that is the substance of the retune. The
        // player's two axes grow at very different speeds, so a single
        // multiplier cannot keep both halves of a fight honest:
        //
        //   enemy HEALTH tracks the player's DAMAGE   (x1.81 a tier, 7.7%/step)
        //   enemy ATTACK tracks the player's HEALTH   (x1.50 a tier, 5.2%/step)
        //
        // Both were MEASURED off AbilityDerivation rather than chosen, by
        // walking a fully-geared character up the tier ladder. Their whole
        // purpose is that hits-to-kill and hits-to-die stay put: a rat dies in
        // 1.3 swings at tier 0 and 1.3 swings at tier 10, a golem in 5 and 5.
        // If either number drifts, one of these two rates is wrong.
        //
        // Held as integer permille per STEP, and applied per step rather than
        // per tier so difficulty climbs smoothly instead of stepping every
        // eighth room. Eight steps of 77 permille compound to 1.81, which is
        // one tier.
        private const int HealthPermillePerStep = 77;
        private const int AttackPermillePerStep = 52;

        // What one step multiplies enemy health by. Exposed for the same
        // reason the old EnemyMultiplier was: "is this curve doing anything"
        // is a question worth being able to ask without an enemy to hand.
        public static float HealthMultiplier(int step) => Multiplier(step, HealthPermillePerStep);

        public static float AttackMultiplier(int step) => Multiplier(step, AttackPermillePerStep);

        // Enemy health, and anything else that is a POOL to be chewed through.
        public static int ScaleHealth(int amount, int step) => Scale(amount, step, HealthPermillePerStep);

        // Enemy attack and defense. Defense rides the attack rate rather than
        // the health one deliberately: it is subtracted from the player's
        // swing, so on the health curve it would outgrow the player's Attack
        // and eventually floor every hit at the max(1, ...) clamp.
        public static int ScaleAttack(int amount, int step) => Scale(amount, step, AttackPermillePerStep);

        // A break shield is a pool, so it chews like health.
        public static int ScaleShield(int amount, int step) => Scale(amount, step, HealthPermillePerStep);

        // Rewards ride the SAME curve as the threat.
        //
        // Not a separate rate, on purpose: if pay lagged difficulty the deep
        // game would quietly become worse value per fight and a player's best
        // move would be to farm shallow rooms forever, which is the opposite
        // of what an endless descent is for. Kept as its own named method
        // anyway so that decoupling them later is a deliberate edit rather
        // than a silent one.
        //
        // On the HEALTH rate, which is the bigger of the two now that there
        // are two -- and worth saying out loud that this makes a step-80 fight
        // pay roughly 370x a step-0 one, against a shop whose prices climb
        // linearly with tier. That is AUDIT #2's economy, an order of
        // magnitude further out. Left coupled rather than quietly rebased,
        // because decoupling pay from threat is exactly the deliberate edit
        // this method exists to make someone type.
        public static int ScaleReward(int amount, int step) => ScaleHealth(amount, step);

        // double all the way through, and Scale uses THIS rather than the float
        // the public accessors return. Rounding a multiplier to float and then
        // multiplying is how a scaled health lands one short of a round number
        // at exactly the depths worth checking -- the same trap ItemUpgrade
        // records for 1.4f.
        private static double RawMultiplier(int step, int permille)
        {
            int clamped = Clamp(step);
            if (clamped <= 0) return 1d;

            double raw = Math.Pow(1d + permille / 1000d, clamped);
            return raw > MaxMultiplier ? MaxMultiplier : raw;
        }

        private static float Multiplier(int step, int permille) => (float)RawMultiplier(step, permille);

        // Applies a curve to one number, floored.
        //
        // double rather than the integer permille arithmetic this used to do,
        // because compounding cannot be expressed as one multiply. Computed
        // once per enemy when a fight is built, not per hit, and every value
        // it produces is pinned by literal-valued tests -- the same trade
        // GearScaling makes and for the same reason.
        private static int Scale(int amount, int step, int permille)
        {
            if (amount == 0 || step <= 0)
            {
                return amount;
            }

            double scaled = amount * RawMultiplier(step, permille);

            // Clamped hard. `step` comes off a save, nothing else bounds it,
            // and a compounding curve overflows int far faster than a linear
            // one did -- 7.7% a step passes two billion around step 300.
            if (scaled > MaxScaledValue) return MaxScaledValue;
            if (scaled < -MaxScaledValue) return -MaxScaledValue;

            return (int)Math.Floor(scaled);
        }

        // A run cannot descend forever in practice, but `step` comes off a
        // save and nothing else bounds it.
        //
        // 200 rather than the old 400: at 7.7% compounding, step 200 is
        // already 3.4 million times a step-0 enemy, and the descent stops
        // getting meaningfully harder well before that because the player's
        // own gear ladder ends at tier 10, which is step 80. Everything past
        // there is the endless descent doing what it is for -- the player's
        // power is capped and the dungeon's is not, so a run ends.
        public const int MaxScaledStep = 200;

        // Ceilings, so a corrupt or hand-edited step cannot overflow the
        // multiplication into a negative enemy -- which is what a wrapped int
        // reads as, and a negative-health enemy dies to nothing.
        private const double MaxMultiplier = 4_000_000d;
        private const int MaxScaledValue = 1_000_000_000;

        private static int Clamp(int step)
        {
            if (step <= 0)
            {
                return 0;
            }

            return step > MaxScaledStep ? MaxScaledStep : step;
        }
    }
}
