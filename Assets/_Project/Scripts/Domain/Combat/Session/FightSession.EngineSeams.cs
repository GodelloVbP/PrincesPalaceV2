using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // THE PHASE 4 ENGINE SEAMS (docs/PLAN_BJORN_CONSTELLATIONS.md, "Phase 4
    // engine seams -- built in cloud, wiring handoff"): the session halves of
    // four mechanics whose state lives on CombatantState and whose rules live
    // in small Domain types.
    //
    //   4b Cursed Blood  -- HealConversion,     read in HealAndCount
    //   4c Ignore Pain   -- DelayedDamagePool,  read in LandPacket, the
    //                        turn-start tick and HealAndCount
    //   4d Blood Price   -- BloodPrice,         read in CanAfford / CastCore
    //   4e Unstoppable / Unyielding -- CrowdControlGuard, read in RecordStatus
    //   4a Planted shield / Shieldwall -- PlantedShield, read in ResolveWard;
    //                        its session half is FightSession.PlantedShield.cs
    //   Phase 2 root Fury engines, Momentum, Battle Trance, Twin Rampage --
    //                        FuryEngine / EinherjarSeams; session half
    //                        FightSession.FuryEngines.cs
    //
    // Every one is OFF for every combatant until something sets it, so the
    // fight is unchanged for anyone the Juggernaut content never touches.
    // Nothing in content sets any of them yet: the TalentEffect/SkillEffect
    // members that will are content-hashed and land with the Unity session.
    // The public Open*/Arm* methods below are that wiring's one-liners.
    public sealed partial class FightSession
    {
        // The SpeedBuffs key Unyielding's surge is granted and revoked under.
        // A string rather than a RelicEffect/StatusEffectType, so the HUD's
        // generic speed row (FightHudModel.BuffBadgesFor) names it by itself.
        public const string UnyieldingSpeedSource = "Unyielding";

        // ---- the wiring's one-liners ------------------------------------------

        // Cursed Blood's cast: every heal on `holder` converts for `turns` of
        // their own turns (TurnWindow's clock).
        public void OpenCursedBlood(CombatantState holder, int turns)
        {
            if (holder == null || turns <= 0) return;
            holder.HealConversion.Window.Open(turns, IsHoldersTurn(holder));
            AppendMessage($"{holder.Name}'s blood turns black - no healing will take.");
        }

        // Unbroken's second half.
        public void OpenUnstoppable(CombatantState holder, int turns)
        {
            if (holder == null || turns <= 0) return;
            holder.CrowdControl.Unstoppable.Open(turns, IsHoldersTurn(holder));
        }

        private bool IsHoldersTurn(CombatantState holder) =>
            ReferenceEquals(holder, _encounter?.Current);

        // ---- 4b: heal conversion ---------------------------------------------

        // True when the heal was Cursed Blood's to take -- the caller then
        // restores nothing, whatever this dealt (a full-health holder converts
        // 0 and still restores nothing: the window forbids healing, it does
        // not merely redirect the surplus).
        //
        // THE DAMAGE IS A TYPED HIT through DamagePipeline, then DealDamage:
        // the enemy's resistance to the type and its ward still apply ("Void
        // is rarely resisted", plan section 4), as for any typed rider
        // (ApplyFinalDamage's elemental bonus). No variance, no dodge, no crit
        // -- the figure is an exact measured heal, not a swing.
        //
        // CREDIT: the holder scores a kill only on their own turn. On anyone
        // else's (an ally's heal, an enemy's turn) it is KillCredit.Nobody,
        // for the enemy path's reason in SettleDeath: a rider flag raised
        // outside the holder's own action would hand the CURRENT actor a
        // Trample they did not earn. And the holder did not act, so the pools
        // hear it as DealRelicPacket's does (actorActed: false) -- converting
        // a heal is not a swing that earns gainOnAttack.
        private bool TryConvertHeal(CombatantState target, int amount)
        {
            var conversion = target?.HealConversion;
            if (conversion == null || !conversion.IsActive) return false;

            int effective = HealConversion.EffectiveAmount(target, amount);
            if (effective <= 0) return true;

            var type = conversion.Type;
            AppendMessage($"{target.Name}'s cursed blood turns {effective} healing into {type.ToString().ToLowerInvariant()}!");

            var credit = IsHoldersTurn(target) ? KillCredit.Attacker : KillCredit.Nobody;
            foreach (var enemy in _encounter.OpponentsOf(target).Where(e => e != null && e.IsAlive).ToList())
            {
                var outcome = DamagePipeline.AfterDefences(
                    effective, type, enemy,
                    affinity: AffinityOf(enemy),
                    varianceRange: 0f,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    attacker: target,
                    dodgeAlreadyResolved: true,
                    crit: false);

                if (outcome.Damage <= 0) continue;

                DealDamage(target, enemy, outcome.Damage, type, credit, actorActed: false);
                AppendMessage($"{enemy.Name} takes {outcome.Damage} {type.ToString().ToLowerInvariant()} damage!");
            }

            return true;
        }

        // ---- 4c: delayed damage ----------------------------------------------

        private int DeferIntoDelayedDamage(CombatantState target, int amount, DamageType type)
        {
            var pool = target?.DelayedDamage;
            if (pool == null || !target.IsAlive || amount <= 0) return 0;

            int deferred = pool.Defer(amount, type);
            if (deferred > 0)
            {
                AppendMessage($"{target.Name} ignores {deferred} of the pain - for now.");
            }

            return deferred;
        }

        // Ignore Pain T3's half of HealAndCount. Returns what the pool took.
        private int ReduceDelayedDamageByHeal(CombatantState target, int amount)
        {
            var pool = target?.DelayedDamage;
            if (pool == null) return 0;

            int eased = pool.ReduceByHeal(amount);
            if (eased > 0)
            {
                AppendMessage($"{target.Name}'s healing eases {eased} pending pain.");
            }

            return eased;
        }

        // The holder's turn opened: one installment of every tranche lands, a
        // packet per damage type, through DealStatusTickPacket -- the tick
        // funnel, so it is never deferred again, books the health actually
        // lost, and credits nobody for a death (the blow that caused it was
        // turns ago). The pools hear it once, as a tick's row is heard.
        //
        // Its own beat, a tick-shaped one with no status on it (StatusTick
        // stays null; CombatBeat's header allows that and nothing in the view
        // reads it). Returns whether a beat was committed, so the caller knows
        // its pre-tick snapshot is spent.
        private bool PayDelayedDamage(CombatantState actor, Dictionary<CombatantState, Vitals> pre)
        {
            var pool = actor?.DelayedDamage;
            if (pool == null || !actor.IsAlive || pool.Pending <= 0) return false;

            var due = pool.TakeDue();
            if (due.Count == 0) return false;

            bool ownsBeat = _recordingBeat == null;
            if (ownsBeat) NewBeat(BeatCause.StatusTick, null, actor, StageApproach.Hold, pre);

            int thrown = 0;
            int landed = 0;
            foreach (var (type, amount) in due)
            {
                if (!actor.IsAlive) break;

                int before = actor.CurrentHealth;
                int absorbed = DealStatusTickPacket(actor, amount, type);
                landed += (before - actor.CurrentHealth) + absorbed;
                thrown += amount;
            }

            NoteDamageForPools(null, actor, thrown, isHit: false);

            if (landed > 0)
            {
                if (ownsBeat)
                {
                    _recordingBeat.Amount = landed;
                    _recordingBeat.IsHealing = false;
                    SetStance(actor, Stances.Hurt);
                }

                AppendMessage($"{actor.Name} suffers {landed} delayed damage!");
            }

            if (ownsBeat) CommitOrDropStatusTickBeat();
            return ownsBeat;
        }

        // ---- 4e: crowd control -------------------------------------------------

        // THE ONE APPLICATION-TIME BLOCK, asked by RecordStatus -- the seam
        // under ApplyStatusTo that ApplyChilled's direct callers (the on-hit
        // chill, Lucky Deck) and Court of Whispers' fear also reach, so no CC
        // path walks around it. Asked AFTER HardControlRecoveryBlocks: an
        // attempt that rule already refused never reaches Unyielding.
        private bool CrowdControlAdmits(CombatantState recipient, StatusEffectType type)
        {
            if (recipient == null) return true;

            var guard = recipient.CrowdControl;
            switch (guard.Attempt(type, IsHoldersTurn(recipient)))
            {
                case CrowdControlVerdict.BlockedByUnstoppable:
                    AppendMessage($"{recipient.Name} is unstoppable - the {type} does not take hold.");
                    return false;

                case CrowdControlVerdict.NegatedByUnyielding:
                    SurgeUnyielding(recipient, guard.Unyielding, type);
                    return false;

                default:
                    return true;
            }
        }

        // The negation's reward: +speed for the surge's span (granted untimed
        // and revoked by AgeEngineWindows when the surge window closes, so both
        // halves share TurnWindow's clock), +damage read by AttackBonusFor
        // while the window is open, and T3's Fury.
        private void SurgeUnyielding(CombatantState holder, UnyieldingRule rule, StatusEffectType type)
        {
            RevokeSpeedBuff(holder, UnyieldingSpeedSource);
            if (rule.SpeedPercent > 0) GrantSpeedPercent(holder, UnyieldingSpeedSource, rule.SpeedPercent, 0);

            int fury = rule.FuryGain > 0 ? holder.PrimaryPool?.Gain(rule.FuryGain) ?? 0 : 0;

            AppendMessage(fury > 0
                ? $"{holder.Name} refuses the {type} - and surges, gaining {fury} {holder.PrimaryPool.DisplayName}!"
                : $"{holder.Name} refuses the {type} - and surges!");
        }

        // Called from TickStatusesAtTurnEnd, the AtTurnEnd clock every window
        // runs on (TurnWindow's header).
        private void AgeEngineWindows(CombatantState actor)
        {
            if (actor == null) return;

            if (actor.HealConversion.Window.AgeAtHoldersTurnEnd())
            {
                AppendMessage($"{actor.Name}'s blood runs clean again.");
            }

            var guard = actor.CrowdControl;
            if (guard.Unstoppable.AgeAtHoldersTurnEnd())
            {
                AppendMessage($"{actor.Name} is no longer unstoppable.");
            }

            if (guard.UnyieldingSurge.AgeAtHoldersTurnEnd())
            {
                RevokeSpeedBuff(actor, UnyieldingSpeedSource);
            }

            guard.UnyieldingCooldown.AgeAtHoldersTurnEnd();

            // 4a: the planted shield's lifetime, its re-place wait and
            // Shieldwall's per-turn Fury cap (FightSession.PlantedShield).
            AgePlantedShield(actor);

            // Momentum and Twin Rampage's cooldown (FightSession.FuryEngines).
            AgeFuryEngines(actor);

            // 4f: silence, its per-enemy cooldown, and disarm.
            AgeSuppression(actor);
        }

        // ---- seams for tests ---------------------------------------------------

        public void PayDelayedDamageForTest(CombatantState actor) => PayDelayedDamage(actor, null);

        public int AttackBonusForTest(CombatantState actor) => AttackBonusFor(actor, spendingGift: false);

        // The ward funnel alone (Mending Fleece's break heal rides it), without
        // a whole hit around it -- a real hit's LandPacket runs its own crown
        // check afterwards and would hide whether the HEAL fired it.
        public int ResolveWardForTest(CombatantState target, int damage) =>
            ResolveWard(target, damage, attacker: null, DamageType.Physical, incoming: damage);
    }
}
