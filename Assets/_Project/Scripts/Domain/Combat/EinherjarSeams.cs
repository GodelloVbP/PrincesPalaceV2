using System;

namespace PrincesPalace.Domain.Combat
{
    // THE EINHERJAR TREE'S ENGINE SEAMS (docs/PLAN_BJORN_CONSTELLATIONS.md,
    // section 3 and "Phase 4 engine seams"): Momentum, Battle Trance and Twin
    // Rampage's rule. Each lives on CombatantState, each is off until the
    // Unity wiring's fight-start pass sets it, and each is read by the
    // session at one seam (FightSession.FuryEngines).

    // MOMENTUM (R: 3/6/9). Stacks per consecutive own turn that dealt damage,
    // +5% crit chance each (CritRules.ChanceFor reads CritChanceBonus). A turn
    // that dealt none resets to 0; a hit taken removes ONE stack.
    public sealed class Momentum
    {
        public const int CritChancePerStack = 5;
        public const int CritDamagePerStack = 5;
        public const int BaseCap = 5;
        public const int ExtendedCap = 8;
        public const int SmallHitPercent = 10;

        // T1. Off = no stacks are ever built and both bonuses read 0.
        public bool Enabled;

        // T2's two halves, separately switchable: the cap goes 5 -> 8, and
        // each stack also adds +5% crit damage.
        public bool ExtendedStackCap;
        public bool CritDamagePerStackBonus;

        // T3: a hit under 10% of his max HP no longer removes a stack.
        public bool IgnoresSmallHits;

        public int Stacks { get; private set; }

        public int Cap => ExtendedStackCap ? ExtendedCap : BaseCap;

        public int CritChanceBonus => Enabled ? Stacks * CritChancePerStack : 0;

        public int CritDamageBonus => Enabled && CritDamagePerStackBonus ? Stacks * CritDamagePerStack : 0;

        // Set by the session when the holder deals damage on his own turn;
        // read and cleared when that turn ends.
        internal bool DealtDamageThisTurn;

        // The holder's turn ended: +1 (to the cap) if it dealt damage, else
        // the chain is broken and the stacks go to 0.
        internal void CloseTurn()
        {
            if (Enabled)
            {
                Stacks = DealtDamageThisTurn ? Math.Min(Cap, Stacks + 1) : 0;
            }

            DealtDamageThisTurn = false;
        }

        // A hit landed on the holder. True when it cost a stack.
        internal bool LoseStackFor(int hit, int maxHealth)
        {
            if (!Enabled || Stacks <= 0 || hit <= 0) return false;
            if (IgnoresSmallHits && (long)hit * 100 < (long)SmallHitPercent * maxHealth) return false;

            Stacks--;
            return true;
        }

        // Headsplitter T3 ("a kill fills Momentum to max stacks").
        public void FillToCap()
        {
            if (Enabled) Stacks = Cap;
        }

        // For a test or a wiring that restores a state; clamped to the cap.
        public void SetStacks(int stacks) => Stacks = Math.Max(0, Math.Min(Cap, stacks));
    }

    // BATTLE TRANCE (R: 13/16/19). At or above ThresholdFury, Percent of each
    // incoming HIT is paid with Fury instead of health, 1 Fury per 1% of max
    // HP. Null on CombatantState = off.
    public sealed class BattleTrance
    {
        public int ThresholdFury = 50;

        // T1 = 20, T2 = 30.
        public int Percent = 20;

        // T2's second half: doubled while transformed. Bjorn's only
        // transformation is Berserk, so "transformed" is read straight off
        // CombatantState.Transformation -- no flag to keep in sync with the
        // form's entry and exit.
        public bool DoublesWhileTransformed;

        // T3: when a soak takes the pool from at-or-above ThresholdFury to
        // below it -- the moment the trance stops soaking -- the holder gains
        // Protect. "When soaking empties his Fury" reads as the trance
        // running dry, since a single soak reaching 0 from 50 needs a hit of
        // most of his max HP.
        public bool ProtectWhenTranceBreaks;
        public int ProtectPercent = 50;
        public int ProtectTurns = 1;

        public BattleTrance(int percent = 20)
        {
            Percent = percent;
        }

        public int PercentFor(CombatantState holder) =>
            DoublesWhileTransformed && holder?.Transformation != null ? 2 * Percent : Percent;

        // The share of `amount` the trance wants to take: floor(amount x pct / 100).
        public static int WantedSoak(int amount, int percent) =>
            amount <= 0 || percent <= 0 ? 0 : (int)((long)amount * Math.Min(100, percent) / 100);

        // 1 Fury per 1% of max HP, rounded UP against the holder (a sliver of
        // a percent still costs a whole point).
        public static int FuryCost(int soak, int maxHealth)
        {
            if (soak <= 0 || maxHealth <= 0) return 0;
            return (int)(((long)soak * 100 + maxHealth - 1) / maxHealth);
        }

        // The most damage `fury` points can pay for.
        public static int SoakCoveredBy(int fury, int maxHealth) =>
            fury <= 0 || maxHealth <= 0 ? 0 : (int)((long)fury * maxHealth / 100);
    }

    // TWIN RAMPAGE (slot 20). Casting the named skill at its full-pool tier
    // while the cooldown is closed runs a second sweep on top: SecondSweepMultiplier
    // x the sweep, a StunTurns stun on every enemy it lands on, no Fury refill
    // between the sweeps, then CooldownTurns of the holder's turns during which
    // the full-pool cast is an ordinary one. Null on CombatantState = off.
    public sealed class TwinRampageRule
    {
        public readonly string SkillId;
        public readonly float SecondSweepMultiplier;
        public readonly int StunTurns;
        public readonly int CooldownTurns;

        public readonly TurnWindow Cooldown = new TurnWindow();

        public TwinRampageRule(string skillId = "rampage", float secondSweepMultiplier = 1f, int stunTurns = 1,
            int cooldownTurns = 5)
        {
            SkillId = skillId ?? "";
            SecondSweepMultiplier = secondSweepMultiplier;
            StunTurns = Math.Max(0, stunTurns);
            CooldownTurns = Math.Max(0, cooldownTurns);
        }

        public bool IsReady => !Cooldown.IsOpen;
    }
}
