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
        // GEOMETRIC, and an earlier comment here argued at length for the
        // opposite before this curve had ever been wired up to anything. It
        // was right for the game it was written against: a straight line is
        // the honest curve when the item ladder only answers a modest
        // multiple of itself. It stopped being honest once GearScaling and
        // AbilityDerivation started compounding a geared character's power by
        // two-plus orders of magnitude across the ladder -- see
        // FightEncounterAdapter.ToCombatant for where this actually reaches
        // an enemy, wired now rather than measured-and-discarded.
        //
        // TWO RATES, NOT ONE, and that is the substance of the retune. The
        // player's two axes grow at very different speeds, so a single
        // multiplier cannot keep both halves of a fight honest:
        //
        //   enemy HEALTH tracks the player's DAMAGE
        //   enemy ATTACK tracks the player's HEALTH
        //
        // Their whole purpose is that hits-to-kill and hits-to-die stay
        // roughly put across the descent -- if either drifts noticeably, one
        // of these two rates is wrong. The exact multiples were re-measured
        // for the 5B retune below rather than carried forward from an older
        // pre-weapon-model measurement, because the player's own damage
        // source changed entirely under D3/D4 (weapon-driven, not
        // ability-derived) since these rates were first set.
        //
        // Held as integer permille per STEP, and applied per step rather than
        // per tier so difficulty climbs smoothly instead of stepping every
        // eighth room.
        //
        // PHASE 5B (D6) RETUNE: 77 -> 75 health, 52 -> 38 attack, against the
        // §P derived-target table. The attack rate moved the furthest because
        // of D6's other finding, that enemy DEFENSE no longer depth-scales at
        // all (see FightEncounterAdapter.ToCombatant) -- the old 52 permille
        // was tuned for a curve where attack climbed against a defense that
        // was climbing too and partly absorbing it. Against a flat, authored
        // defense the same attack rate overshoots the §P boss-damage-per-hit
        // targets, so it was retuned down. These are pinned tuning values
        // (§T), not derived from a formula here -- when a playtest says they
        // are wrong, they move again, in this file and in BalanceSheetTests,
        // together.
        private const int HealthPermillePerStep = 75;
        private const int AttackPermillePerStep = 38;

        // What one step multiplies enemy health by. Exposed for the same
        // reason the old EnemyMultiplier was: "is this curve doing anything"
        // is a question worth being able to ask without an enemy to hand.
        public static float HealthMultiplier(int step) => Multiplier(step, HealthPermillePerStep);

        public static float AttackMultiplier(int step) => Multiplier(step, AttackPermillePerStep);

        // Enemy health, and anything else that is a POOL to be chewed through.
        public static int ScaleHealth(int amount, int step) => Scale(amount, step, HealthPermillePerStep);

        // Enemy attack.
        //
        // PHASE 5B (D6): enemy DEFENSE no longer rides this curve at all --
        // physicalDefense/magicalDefense are used at their authored, step-0
        // value regardless of depth. This used to scale defense on the same
        // rate as attack (the reasoning: it is subtracted from the player's
        // swing, so scaling it on the steeper health rate would eventually
        // floor every hit at the max(1, ...) clamp), but D6 found that
        // reasoning double-dips: the D_broad/(100+D_broad) mitigation curve
        // (DamagePipeline) is already asymptotic on its own, so ANY defense
        // growth with depth compounds against an already-diminishing-returns
        // curve and runs boss time-to-kill away past floor 4. See
        // FightEncounterAdapter.ToCombatant, the only place enemy stats are
        // assembled for a fight, for where the old scaling call was removed.
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
        // pay roughly 325x a step-0 one, against a shop whose prices climb
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
            // one did -- 7.5% a step passes two billion around step 300.
            if (scaled > MaxScaledValue) return MaxScaledValue;
            if (scaled < -MaxScaledValue) return -MaxScaledValue;

            return (int)Math.Floor(scaled);
        }

        // A run cannot descend forever in practice, but `step` comes off a
        // save and nothing else bounds it.
        //
        // 200 rather than the old 400: at 7.5% compounding, step 200 is
        // already ~1.9 million times a step-0 enemy, and the descent stops
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
