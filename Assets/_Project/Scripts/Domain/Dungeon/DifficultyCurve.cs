using System;

namespace PrincesPalace.Domain.Dungeon
{
    // How much harder the dungeon gets as a run goes deeper.
    //
    // Without this, an infinite map is an infinite corridor of trivial
    // fights: the descent has no other source of scaling difficulty, so the
    // leg generator and this file ship together on purpose. Wired in
    // FightEncounterAdapter.ToCombatant, and EnemiesGetTougherWithDepth pins
    // the wiring rather than the arithmetic.
    //
    // Pure and engine-free so the curve can be pinned by literal tests
    // rather than sampled by playing.
    public static class DifficultyCurve
    {
        // Geometric: a straight line is the honest curve when the item
        // ladder only answers a modest multiple of itself, but GearScaling
        // and AbilityDerivation compound a geared character's power by
        // two-plus orders of magnitude across the ladder -- see
        // FightEncounterAdapter.ToCombatant for where this reaches an enemy.
        //
        // Two rates, not one: the player's two axes grow at very different
        // speeds, so a single multiplier cannot keep both halves of a fight
        // honest:
        //
        //   enemy HEALTH tracks the player's DAMAGE
        //   enemy ATTACK tracks the player's HEALTH
        //
        // Their whole purpose is that hits-to-kill and hits-to-die stay
        // roughly put across the descent -- if either drifts noticeably, one
        // of these two rates is wrong.
        //
        // Held as integer permille per step, and applied per step rather than
        // per tier so difficulty climbs smoothly instead of stepping every
        // eighth room.
        //
        // Tuned against the §P derived-target table. The attack rate sits
        // well below the health rate because enemy DEFENSE does not
        // depth-scale at all (see FightEncounterAdapter.ToCombatant): against
        // a flat, authored defense, a steeper attack rate overshoots the §P
        // boss-damage-per-hit targets. These are pinned tuning values (§T),
        // not derived from a formula here -- when a playtest says they are
        // wrong, they move again, in this file and in BalanceSheetTests,
        // together.
        private const int HealthPermillePerStep = 75;
        private const int AttackPermillePerStep = 38;

        // Three rates now, and the third is not a threat rate at all.
        //
        // Experience rides its own rate rather than the health rate
        // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §2) because a
        // level curve cannot be priced against income that compounds as hard
        // as the health rate does without either pricing the early levels
        // out of reach or making the late ones a formality; the model
        // (xp_model.md Part B) puts a deep run at 10,153 on this rate, and a
        // leg-2 death at 710, which is the spread the authored cost table in
        // level_curve.json is written against.
        //
        // 25 rather than 40 because levels should come from playing, not
        // only from depth: 40 permille pays a deep run 23,311 against a
        // leg-5 death's 3,863 (ratio 6.0), 25 pays 10,153 against 2,642
        // (ratio 3.8) -- recorded in the plan's §8.
        //
        // Gold keeps the health rate through ScaleReward below, because the
        // shop's prices are the thing gold is priced against.
        private const int ExpPermillePerStep = 25;

        // What one step multiplies enemy health by. Exposed because "is
        // this curve doing anything" is a question worth being able to ask
        // without an enemy to hand.
        public static float HealthMultiplier(int step) => Multiplier(step, HealthPermillePerStep);

        public static float AttackMultiplier(int step) => Multiplier(step, AttackPermillePerStep);

        // What one step multiplies a fight's EXPERIENCE by. Exposed for the
        // same reason the other two are, and read by LevelCurveTests to state
        // the cost curve's relationship to the income it is racing without
        // restating 1.025 in a second file.
        public static float ExperienceMultiplier(int step) => Multiplier(step, ExpPermillePerStep);

        // Enemy health, and anything else that is a POOL to be chewed through.
        public static int ScaleHealth(int amount, int step) => Scale(amount, step, HealthPermillePerStep);

        // Enemy attack.
        //
        // Enemy DEFENSE does not ride this curve at all --
        // physicalDefense/magicalDefense are used at their authored, step-0
        // value regardless of depth. Scaling defense with depth would
        // double-dip: the D_broad/(100+D_broad) mitigation curve
        // (DamagePipeline) is already asymptotic on its own, so any defense
        // growth with depth compounds against an already-diminishing-returns
        // curve and runs boss time-to-kill away past floor 4. See
        // FightEncounterAdapter.ToCombatant, the only place enemy stats are
        // assembled for a fight.
        public static int ScaleAttack(int amount, int step) => Scale(amount, step, AttackPermillePerStep);

        // ENEMY ATTACK IN A FIGHT WITH A ROUND LIMIT (FightSession.RoundLimit,
        // an event fight's `surviveRounds`) rides its own rate, 5.3% a step,
        // between the attack rate and the health rate.
        //
        // Not the attack rate: a room fight's danger is its length times its
        // attack, and it stays flat across the descent (room-fight damage
        // taken sits at ~5% of party max HP on every floor). A fight that
        // ends after N rounds whatever anyone's health is has no length to
        // grow, so on the attack rate alone the player's toughness (health,
        // defenses, the heals a deeper kit carries) outgrows it.
        //
        // Not the health rate either: 7.5% overshoots the other way. Measured
        // on the Bell (attack 20): Endure 92/77/54/38/10 % on floors 1-5,
        // where the attack rate gave 93/93/94/92/88. The player's toughness
        // grows at roughly 5-6% a step against one hitter, not 7.5%; 5.3% is
        // the rate that held the Bell's Endure between 60% and 85% on all
        // five floors. A pinned tuning value like the two above: when a
        // second round-limited fight says it is wrong, it moves, here and in
        // DifficultyCurveTests, together.
        //
        // roundLimit <= 0 (every room fight) is ScaleAttack exactly.
        public static int ScaleEnemyAttack(int amount, int step, int roundLimit) =>
            roundLimit > 0 ? Scale(amount, step, RoundLimitedAttackPermillePerStep) : ScaleAttack(amount, step);

        private const int RoundLimitedAttackPermillePerStep = 53;

        // A break shield is a pool, so it chews like health.
        public static int ScaleShield(int amount, int step) => Scale(amount, step, HealthPermillePerStep);

        // GOLD. Rides the SAME curve as the threat.
        //
        // Not a separate rate, on purpose: if pay lagged difficulty the deep
        // game would quietly become worse value per fight and a player's best
        // move would be to farm shallow rooms forever, which is the opposite
        // of what an endless descent is for. Kept as its own named method
        // anyway so that decoupling them later is a deliberate edit rather
        // than a silent one.
        //
        // On the health rate -- worth saying out loud that this makes a
        // step-80 fight pay roughly 325x a step-0 one, against a shop whose
        // prices climb linearly with tier. Left coupled rather than quietly
        // rebased, because decoupling pay from threat is exactly the
        // deliberate edit this method exists to make someone type.
        //
        // Experience does not ride this method -- see ScaleExperience below.
        public static int ScaleReward(int amount, int step) => ScaleHealth(amount, step);

        // Experience, on its own rate. The argument above -- that pay must
        // not lag threat or shallow farming wins -- still holds for gold,
        // which is spent inside the run it was earned in, at a shop whose
        // prices climb with depth. It does not hold for experience, which is
        // spent on a track that has to be priced once for a career and
        // cannot be priced against income that multiplies by 325 across a
        // single descent. Levelling still rises with depth here (1.025 a
        // step, 7.2x at step 80), just not fast enough to make the first
        // nine legs of a deep run a rounding error against its last.
        public static int ScaleExperience(int amount, int step) => Scale(amount, step, ExpPermillePerStep);

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
        // double rather than integer permille arithmetic, because
        // compounding cannot be expressed as one multiply. Computed
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
        // 200: at 7.5% compounding, step 200 is
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
