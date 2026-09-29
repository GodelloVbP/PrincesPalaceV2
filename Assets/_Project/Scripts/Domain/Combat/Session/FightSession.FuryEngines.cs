using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // PHASE 2, THE ROOT FURY ENGINES, AND THE EINHERJAR TREE'S SEAMS
    // (docs/PLAN_BJORN_CONSTELLATIONS.md, section 1, section 3, Phase 2 and
    // "Phase 4 engine seams"). The session half of FuryEngine and
    // EinherjarSeams:
    //
    //   PayEnginesForDamage   -- from NoteDamageForPools: Sentinel per hit
    //                            taken, Einherjar per damaging action,
    //                            Momentum's two halves
    //   TickPrimaryPool       -- Juggernaut's per-turn income, decay off
    //   SoakWithBattleTrance  -- from LandPacket, beside Ignore Pain
    //   TryTwinRampage        -- around a DamageAll cast
    //   AgeFuryEngines        -- the holder's turn-end clock
    //
    // Everything here is inert for a combatant whose FuryEngine is None, whose
    // Momentum is not Enabled and whose BattleTrance / TwinRampage are null --
    // which is every combatant until the Unity wiring sets them.
    public sealed partial class FightSession
    {
        // Raised when a Battle Trance soak takes its holder's pool below the
        // threshold (the trance stops soaking), before T3's Protect is
        // applied. A hook for the view (a trance-break cue); nothing in the
        // session subscribes.
        public event Action<CombatantState> BattleTranceBroke;

        // ---- the raw incoming figure (Sentinel) -----------------------------------

        // THE BLOW THE WARD STEP IS HOLDING, as it arrived before the target's
        // defences (DamagePipeline.WardResolver's `incoming`). Written by
        // ResolveWard, read -- and consumed -- by the next pool hearing of that
        // same target, which for a real hit is the DealDamage right after the
        // pipeline (or the planted shield's own hearing when it ate the hit
        // whole). Dropped when the ward step left nothing, because then the
        // pools do not hear the hit at all (a fully warded hit pays nothing,
        // plan 4a's verified rule) and a stale figure must not be read by an
        // unrelated later blow.
        //
        // One slot, not a map: a hit's ward step and its landing are adjacent,
        // and anything that lands with no ward step (splash, a relic packet, a
        // rider) finds no entry for its target and is measured on what it
        // dealt -- which is also its raw figure, since nothing mitigated it.
        private CombatantState _incomingHitTarget;
        private int _incomingHitRaw;

        private void NoteIncomingHit(CombatantState target, int raw)
        {
            _incomingHitTarget = target;
            _incomingHitRaw = raw;
        }

        private void DropIncomingHit(CombatantState target)
        {
            if (ReferenceEquals(_incomingHitTarget, target)) _incomingHitTarget = null;
        }

        private int TakeIncomingRaw(CombatantState target, int fallback)
        {
            if (target == null || !ReferenceEquals(_incomingHitTarget, target)) return fallback;

            _incomingHitTarget = null;
            return _incomingHitRaw;
        }

        // ---- the damage seam ---------------------------------------------------------

        // Called from NoteDamageForPools for every blow the pools hear, with
        // the amount they hear. `isHit` is false for a status tick and a
        // delayed-damage installment.
        private void PayEnginesForDamage(CombatantState actor, CombatantState target, int amount, bool isHit)
        {
            // Consumed for every hit, whoever the target: the slot is a fact
            // about this one blow and must not outlive it.
            int raw = isHit ? TakeIncomingRaw(target, amount) : amount;

            if (actor != null && !ReferenceEquals(actor, target))
            {
                PayEinherjar(actor, amount);

                if (IsHoldersTurn(actor)) actor.Momentum.DealtDamageThisTurn = true;
            }

            if (target == null || !isHit) return;

            PayHoldTheLine(target);

            // SENTINEL: per hit taken, on the raw incoming figure. Hits only
            // -- a poison tick keeps his turn from reading idle (the pools
            // still note the activity) but is not a hit and pays nothing.
            if (target.FuryEngine.Kind == FuryEngineKind.Sentinel)
            {
                GrantPrimary(target, FuryEngine.SentinelFury(raw, target.MaxHealth));
            }

            if (target.Momentum.LoseStackFor(amount, target.MaxHealth))
            {
                AppendMessage($"{target.Name} loses momentum ({target.Momentum.Stacks}).");
            }
        }

        private void PayEinherjar(CombatantState actor, int hit)
        {
            var engine = actor.FuryEngine;
            if (engine.Kind != FuryEngineKind.Einherjar) return;

            int owed = engine.OweForHit(hit, actor.Attack);

            // BLOODFIRE T3: a crit pays its share of the engine again by the
            // node's percent. The flag is consumed here so a later blow of the
            // same action that never went through the crit roll (a splash)
            // cannot inherit it.
            if (actor.LastHitWasCrit && owed > 0)
            {
                owed += owed * actor.Talents.Best(TalentEffectType.CritFuryBonusPercent) / 100;
            }

            actor.LastHitWasCrit = false;
            GrantPrimary(actor, owed);
        }

        // ---- the fight-start pass ---------------------------------------------------------

        // SWITCHES ON every engine seam the combatant's talents grant, once,
        // when the fight begins. A talent set cannot change mid-fight, so this
        // is the one place the tree's state reaches the Domain objects. It only
        // ever turns things ON: a test or a caller that set a seam by hand
        // keeps it.
        //
        // The Juggernaut's arm is FightSession.Juggernaut.cs and the Sentinel's
        // is FightSession.Sentinel.cs.
        private static void ArmEngineSeams(CombatantState actor)
        {
            var talents = actor?.Talents;
            if (talents == null || talents.IsEmpty) return;

            if (talents.Has(TalentEffectType.FuryEngineEinherjar))
            {
                actor.FuryEngine.Kind = FuryEngineKind.Einherjar;
            }

            ArmJuggernautSeams(actor, talents);
            ArmSentinelSeams(actor, talents);

            int momentum = talents.Best(TalentEffectType.MomentumTier);
            if (momentum >= 1) actor.Momentum.Enabled = true;
            if (momentum >= 2)
            {
                actor.Momentum.ExtendedStackCap = true;
                actor.Momentum.CritDamagePerStackBonus = true;
            }

            if (momentum >= 3) actor.Momentum.IgnoresSmallHits = true;

            int trance = talents.Best(TalentEffectType.BattleTranceTier);
            if (trance >= 1)
            {
                actor.BattleTrance = new BattleTrance(trance >= 2 ? 30 : 20)
                {
                    DoublesWhileTransformed = trance >= 2,
                    ProtectWhenTranceBreaks = trance >= 3,
                };
            }

            if (talents.Has(TalentEffectType.TwinRampage) && actor.TwinRampage == null)
            {
                actor.TwinRampage = new TwinRampageRule();
            }
        }

        // ---- the finisher and the Slam's rider ----------------------------------------

        // A finisher's wound bonus (Headsplitter): each 1% of the target's max
        // health already gone adds `damagePerMissingHealthPercent` percent to
        // the raw figure. Integer arithmetic: floor(base x (10000 + rate x
        // missing%) / 10000) with missing% in whole percent, so a target at
        // 20% health and a rate of 100 reads +80%.
        private static int ApplyMissingHealthBonus(int amount, ResolvedSkill skill, CombatantState target)
        {
            int rate = skill.DamagePerMissingHealthPercent;
            if (rate <= 0 || target == null || target.MaxHealth <= 0 || amount <= 0) return amount;

            long missing = target.MaxHealth - System.Math.Max(0, System.Math.Min(target.MaxHealth, target.CurrentHealth));
            long missingPercent = missing * 100 / target.MaxHealth;
            return (int)(amount * (10000L + rate * missingPercent) / 10000L);
        }

        // SLAM T1: a crit on the Fury-tier strike restores Fury.
        private void PaySlamCritFury(CombatantState actor, ResolvedSkill skill)
        {
            int gain = actor.Talents.BestFor(TalentEffectType.SlamCritRestoresFury, skill.Id);
            if (gain <= 0) return;

            GrantPrimary(actor, gain);
        }

        // A killing finisher hands back part of what it spent, and (Headsplitter
        // T3) fills Momentum. Only a cast authored with a refund reads this.
        private void RefundOnKill(CombatantState actor, ResolvedSkill skill, CombatantState target, int spent)
        {
            if (skill.RefundsSpentOnKillPercent <= 0 || target == null || target.IsAlive) return;

            int refund = spent * skill.RefundsSpentOnKillPercent / 100;
            if (refund > 0)
            {
                GrantPrimary(actor, refund);
                AppendMessage($"{actor.Name} takes back {refund} {actor.PrimaryPool?.DisplayName}.");
            }

            if (actor.Talents.Has(TalentEffectType.KillFillsMomentum) && actor.Momentum.Enabled)
            {
                actor.Momentum.FillToCap();
                AppendMessage($"{actor.Name}'s momentum is full ({actor.Momentum.Stacks}).");
            }
        }

        // ---- Fury paid for a kill ---------------------------------------------------------

        // Bloodfire T1 and Berserk T3, at the one place a body is settled:
        // a kill the actor gets credit for refunds Fury, and a kill made while
        // wearing a form refunds the form's own amount on top.
        private void PayKillFury(CombatantState actor, CombatantState target)
        {
            if (actor == null || !actor.IsPlayerSide || actor.PrimaryPool == null) return;

            var talents = actor.Talents;
            int gain = talents.Best(TalentEffectType.FuryOnKill);
            if (actor.Transformation != null) gain += talents.Best(TalentEffectType.FuryOnKillWhileTransformed);
            if (gain <= 0) return;

            GrantPrimary(actor, gain);
            AppendMessage($"{actor.Name} drinks in the kill (+{gain} {actor.PrimaryPool.DisplayName}).");
        }

        // Whether the pool's flat gainOnAttack / gainOnDamageTaken still pay
        // this combatant (no engine set).
        private static bool FlatGainsPay(CombatantState combatant) =>
            combatant != null && !combatant.FuryEngine.ReplacesFlatGains;

        // ---- the action boundary --------------------------------------------------------

        // HACK'S ONE-LINER: for the rest of this action every hit `actor`
        // deals pays the Einherjar engine separately. Called by the Hack cast
        // before it resolves; cleared when the action settles.
        public void BeginPerHitEngineAction(CombatantState actor)
        {
            if (actor != null) actor.FuryEngine.PaysPerHit = true;
        }

        // A fresh action opens (OpenTurnFor / ReopenTurnFor, beside the
        // OncePerTurn locks the flat gainOnAttack uses -- the same boundary).
        private static void StartEngineAction(CombatantState actor) => actor?.FuryEngine.StartAction();

        // The action settled (the two post-action seams). Hack's flag goes,
        // and a held Twin Rampage tally that somehow was not released pays now.
        private void SettleEngineAction(CombatantState actor)
        {
            if (actor == null) return;

            ReleaseHeldEngine(actor);
            actor.FuryEngine.PaysPerHit = false;
        }

        private void ReleaseHeldEngine(CombatantState actor)
        {
            var engine = actor.FuryEngine;
            int owed = engine.Release(actor.Attack);
            if (engine.Kind == FuryEngineKind.Einherjar) GrantPrimary(actor, owed);
        }

        // ---- Juggernaut ------------------------------------------------------------------

        // THE ONE TURN-START TICK FOR THE PRIMARY POOL (was a one-liner in
        // FightSession.Riders). NOT through CombatMath.RestoreMana: a per-turn
        // gain is the pool's own income, not a mana effect, so a pool that
        // refuses potions still regenerates what it authored. The SIGNATURE
        // pool's per-turn gain stays in GrantTurnStart (Charisma-scaled,
        // talent-fed, and it speaks when it overflows).
        //
        // The pool's own gain and decay, with decay
        // switched off under the Juggernaut engine, then that engine's
        // health-curved income -- ONCE PER HIS OWN TURN: an extra action that
        // reopens the turn (Trample, Bloodlust) re-runs the pool's own tick,
        // as gainPerTurn always has (AUDIT K8), but the Juggernaut income is
        // not paid again (`reopened`).
        private void TickPrimaryPool(CombatantState actor, bool reopened = false)
        {
            var pool = actor?.PrimaryPool;
            if (pool == null) return;

            var engine = actor.FuryEngine;

            // No idle drain while a form upkept by this pool runs (its own
            // per-turn drain is the whole upkeep), and BLOODFIRE T2 forgives
            // the first idle drain of the fight.
            bool allowDecay = !engine.SuppressesIdleDecay && !(actor.Transformation?.PrimaryDrainPerTurn > 0);
            if (allowDecay && !engine.FirstIdleTurnSpent && actor.Talents.Has(TalentEffectType.FirstIdleTurnFree)
                && pool.WouldDecayThisTurn)
            {
                engine.FirstIdleTurnSpent = true;
                allowDecay = false;
            }

            pool.TickTurnStart(allowDecay);

            if (engine.Kind == FuryEngineKind.Juggernaut && actor.IsAlive && !reopened)
            {
                pool.Gain(FuryEngine.JuggernautFury(actor.CurrentHealth, actor.MaxHealth));
            }
        }

        // ---- Battle Trance ------------------------------------------------------------

        // Part of a HIT paid with Fury instead of health. In LandPacket after
        // Ignore Pain's deferral and before the egg's lethal check, so the
        // share it takes is of what would land now, after every defence and
        // ward. Returns the damage it took off the hit.
        //
        // Only while the pool is AT OR ABOVE the threshold ("above 50" read as
        // the 50+ every other Fury gate in the plan uses). The cost is 1 Fury
        // per 1% of max HP, rounded up; a soak the pool cannot fully pay for
        // takes what the whole pool covers and spends all of it.
        private int SoakWithBattleTrance(CombatantState target, int amount)
        {
            var trance = target?.BattleTrance;
            var pool = target?.PrimaryPool;
            if (trance == null || pool == null || !target.IsAlive || amount <= 0) return 0;
            if (pool.Current <= 0 || pool.Current < trance.ThresholdFury) return 0;

            int wanted = BattleTrance.WantedSoak(amount, trance.PercentFor(target));
            if (wanted <= 0) return 0;

            int covered = BattleTrance.SoakCoveredBy(pool.Current, target.MaxHealth);
            int soak;
            int cost;
            if (wanted >= covered)
            {
                soak = covered;
                cost = pool.Current;
            }
            else
            {
                soak = wanted;
                cost = Math.Min(pool.Current, BattleTrance.FuryCost(soak, target.MaxHealth));
            }

            if (soak <= 0) return 0;

            pool.SpendUpTo(cost);
            AppendMessage($"{target.Name}'s battle trance takes {soak} of the blow for {cost} {pool.DisplayName}.");

            // A soak only runs from at-or-above the threshold, so ending below
            // it is exactly the crossing: the trance just stopped soaking.
            if (pool.Current < trance.ThresholdFury)
            {
                BattleTranceBroke?.Invoke(target);

                if (trance.ProtectWhenTranceBreaks
                    && ApplyStatusTo(target, StatusEffectType.Protect, trance.ProtectPercent, trance.ProtectTurns, target))
                {
                    AppendMessage($"{target.Name}'s trance breaks - he gains Protect!");
                }
            }

            return soak;
        }

        // ---- Twin Rampage ----------------------------------------------------------------

        // Whether this cast is the empowered one: the rule's skill, cast at a
        // full-pool tier (spend 100%), with the cooldown closed.
        private static bool TwinRampageFires(CombatantState actor, ResolvedSkill skill,
            PoolTierResolution.Result poolTier)
        {
            var rule = actor?.TwinRampage;
            return rule != null && rule.IsReady && skill != null
                   && string.Equals(skill.Id, rule.SkillId, StringComparison.Ordinal)
                   && poolTier.Fired && poolTier.Tier.Spend >= 1f;
        }

        // A DamageAll cast, with Twin Rampage around it when the rule fires:
        //
        //   1. the Einherjar engine HOLDS -- hits are tallied, nothing paid --
        //      so the first sweep's Fury cannot refill the bar before the
        //      second;
        //   2. the first sweep, as cast (its own beat, committed here);
        //   3. the second sweep at SecondSweepMultiplier, in a beat of its own,
        //      then StunTurns of Stun on every enemy it landed on and is still
        //      standing, through ApplyStatusTo (the CC guard and every existing
        //      stun rule apply; no boss is special-cased);
        //   4. the cooldown opens, and the held tally pays ONCE for the whole
        //      action's single largest hit (once per action, section 3).
        //
        // No living enemy after the first sweep: no second sweep, and the
        // empowered version is not spent.
        private void ResolveDamageAllWithTwin(CombatantState actor, ResolvedSkill skill, int resourceSpent,
            PoolTierResolution.Result poolTier)
        {
            if (!TwinRampageFires(actor, skill, poolTier))
            {
                ResolveDamageAll(actor, skill, resourceSpent, poolTier);
                return;
            }

            var rule = actor.TwinRampage;
            actor.FuryEngine.Holding = true;

            try
            {
                ResolveDamageAll(actor, skill, resourceSpent, poolTier);

                if (_encounter.OpponentsOf(actor).Any(e => e != null && e.IsAlive))
                {
                    CommitBeat();
                    AppendMessage($"{actor.Name} does not stop - a second rampage!");

                    var second = new PoolTierResolution.Result(new ResolvedPoolTier(0f, rule.SecondSweepMultiplier), 0);
                    var landed = ResolveDamageAll(actor, skill, resourceSpent, second);

                    if (rule.StunTurns > 0)
                    {
                        foreach (var enemy in landed.Where(e => e != null && e.IsAlive))
                        {
                            if (ApplyStatusTo(enemy, StatusEffectType.Stun, 0, rule.StunTurns, actor))
                            {
                                AppendMessage($"{enemy.Name} is stunned!");
                            }
                        }
                    }

                    rule.Cooldown.Open(rule.CooldownTurns, IsHoldersTurn(actor));
                }
            }
            finally
            {
                ReleaseHeldEngine(actor);
            }
        }

        // ---- the clock ---------------------------------------------------------------------

        // The holder's turn ended (AgeEngineWindows): Momentum builds or breaks,
        // Twin Rampage's cooldown ages.
        private static void AgeFuryEngines(CombatantState actor)
        {
            actor.Momentum.CloseTurn();
            actor.TwinRampage?.Cooldown.AgeAtHoldersTurnEnd();
        }

        // ---- seams for tests ---------------------------------------------------------------

        public void ArmEngineSeamsForTest(CombatantState actor) => ArmEngineSeams(actor);

        public void TickPrimaryPoolForTest(CombatantState actor) => TickPrimaryPool(actor);

        public void ReopenTurnForTest(CombatantState actor) => ReopenTurnFor(actor);

        public void StartEngineActionForTest(CombatantState actor) => StartEngineAction(actor);

        public void SettleEngineActionForTest(CombatantState actor) => SettleEngineAction(actor);

        // A blow `attacker` deals with actorActed (their own swing), through
        // the real funnel, as StrikeForTest does -- but paying the attacker's
        // side as ApplyAndCountDamage does for a player action.
        public void DealForTest(CombatantState attacker, CombatantState target, int amount) =>
            DealDamage(attacker, target, amount, DamageType.Physical, KillCredit.Nobody);
    }
}
