using System;

namespace PrincesPalace.Domain.Combat
{
    // WHICH ROOT ENGINE PAYS A COMBATANT'S PRIMARY POOL (docs/
    // PLAN_BJORN_CONSTELLATIONS.md, section 1 "Engines replace the pool's flat
    // gains" and Phase 2). None is today's behaviour: the pool's authored
    // gainOnAttack / gainOnDamageTaken pay, exactly as before.
    //
    // A plain enum in a file ContentInputHash does not list, on purpose: no
    // content record carries it. The root TalentEffect members that will set
    // it are content-hashed and land with the Unity session; until then
    // nothing sets it and every combatant reads None.
    public enum FuryEngineKind
    {
        None,
        Sentinel,
        Einherjar,
        Juggernaut,
    }

    // THE ENGINE STATE on one combatant, read by FightSession at three seams:
    //
    //   NoteDamageForPools   -- Sentinel (per hit taken) and Einherjar (per
    //                           damaging action) replace the flat gains there
    //   TickPrimaryPool      -- Juggernaut's per-turn income, and its decay off
    //   OpenTurnFor/Reopen   -- the Einherjar action tally starts over
    //
    // Pure arithmetic and a small per-action tally. The session owns when.
    public sealed class FuryEngine
    {
        public FuryEngineKind Kind = FuryEngineKind.None;

        // An engine is set: the pool's flat gainOnAttack / gainOnDamageTaken
        // are not paid for this combatant any more (whichever engine it is --
        // a Sentinel earns nothing for swinging, an Einherjar nothing for
        // being hit, a Juggernaut nothing from hits at all). Fury granted by
        // talent riders from other trees still pays: those call Gain directly
        // and never read these two fields.
        public bool ReplacesFlatGains => Kind != FuryEngineKind.None;

        // Idle decay is the pool's own authored rule for every engine except
        // the Juggernaut's, whose per-turn income is the whole economy.
        public bool SuppressesIdleDecay => Kind == FuryEngineKind.Juggernaut;

        // ---- Sentinel: per hit taken ----------------------------------------------

        // `clamp(floor(raw incoming / maxHP x 150), 3, 25)` -- the SAME helper
        // Shieldwall's per-ally Fury uses (PlantedShield.FuryForAbsorbedHit),
        // so the root's clamp exists in one place. `raw` is the figure before
        // the holder's defences: the session reads it off the ward step
        // (DamagePipeline hands the ward the pre-defence amount).
        public static int SentinelFury(int rawIncoming, int holderMaxHealth) =>
            PlantedShield.FuryForAbsorbedHit(rawIncoming, holderMaxHealth);

        // ---- Einherjar: per damaging action ---------------------------------------

        public const int EinherjarScale = 10;
        public const int EinherjarMin = 5;
        public const int EinherjarMax = 30;

        // `clamp(floor(damage / Attack x 10), 5, 30)`. An Attack of 0 or less
        // reads as 1 (graceful: a debuffed-to-nothing attacker still earns the
        // clamp's ceiling rather than dividing by zero). 0 damage pays 0.
        public static int EinherjarFury(int damage, int attack)
        {
            if (damage <= 0) return 0;

            long scaled = (long)damage * EinherjarScale / Math.Max(1, attack);
            return (int)Math.Max(EinherjarMin, Math.Min(EinherjarMax, scaled));
        }

        // HACK'S EXCEPTION: while set, every hit this combatant deals pays
        // the engine separately instead of the action paying once for its
        // largest hit. Set by the Hack cast for its own action
        // (FightSession.BeginPerHitEngineAction) and cleared when the action
        // settles, so it can never leak into the next one.
        public bool PaysPerHit { get; internal set; }

        // Twin Rampage: while held, hits are tallied but nothing is paid, so
        // the first sweep's Fury does not refill the bar before the second.
        // Released (and paid for the whole action's largest hit) at the end.
        internal bool Holding;

        // The action's tally: its largest hit so far and what the engine has
        // already paid for it. Paying the DIFFERENCE between what the largest
        // hit is worth and what was paid keeps the meter moving on the beat
        // that shows the hit, and still totals exactly one payment for the
        // single largest hit -- the formula is monotone in the hit, so a
        // bigger hit later in a sweep only tops up.
        internal int ActionLargestHit;
        internal int ActionPaid;

        internal void StartAction()
        {
            ActionLargestHit = 0;
            ActionPaid = 0;
            PaysPerHit = false;
            Holding = false;
        }

        // What the engine owes for one more hit of this action, and books it
        // as paid. The caller hands the result to the pool.
        internal int OweForHit(int hit, int attack)
        {
            if (hit <= 0) return 0;

            if (PaysPerHit) return EinherjarFury(hit, attack);

            if (hit > ActionLargestHit) ActionLargestHit = hit;
            if (Holding) return 0;

            return TopUp(attack);
        }

        internal int Release(int attack)
        {
            if (!Holding) return 0;

            Holding = false;
            return TopUp(attack);
        }

        private int TopUp(int attack)
        {
            int owed = EinherjarFury(ActionLargestHit, attack) - ActionPaid;
            if (owed <= 0) return 0;

            ActionPaid += owed;
            return owed;
        }

        // ---- Juggernaut: per turn start -------------------------------------------

        public const int JuggernautBase = 15;

        // `15 x (1 + 3 t^2)`, t = clamp((1 - HP%) / 0.75, 0, 1). Expanded to
        // integers: 15 + 45 t^2 = 15 + 80 x (missing / max)^2 while missing is
        // under three quarters, 60 from there down. Floored.
        //   100% -> 15, 75% -> 20, 50% -> 35, 25% or less -> 60.
        public static int JuggernautFury(int currentHealth, int maxHealth)
        {
            if (maxHealth <= 0) return JuggernautBase;

            long missing = Math.Max(0, Math.Min(maxHealth, maxHealth - Math.Max(0, currentHealth)));
            if (4 * missing >= 3L * maxHealth) return JuggernautBase * 4;

            return JuggernautBase + (int)(80 * missing * missing / ((long)maxHealth * maxHealth));
        }
    }
}
