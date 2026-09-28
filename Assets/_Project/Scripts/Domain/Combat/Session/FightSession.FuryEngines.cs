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
        // Raised when a Battle Trance soak takes its holder's pool to 0, before
        // T3's Protect is applied. A hook for the view (a trance-break cue);
        // nothing in the session subscribes.
        public event Action<CombatantState> BattleTranceEmptied;

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

            GrantPrimary(actor, engine.OweForHit(hit, actor.Attack));
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
        // health-curved income. Runs wherever the pool's income runs --
        // including an extra action's reopen, the rule gainPerTurn already
        // follows ("income per action taken", AUDIT K8).
        private void TickPrimaryPool(CombatantState actor)
        {
            var pool = actor?.PrimaryPool;
            if (pool == null) return;

            var engine = actor.FuryEngine;
            pool.TickTurnStart(allowDecay: !engine.SuppressesIdleDecay);

            if (engine.Kind == FuryEngineKind.Juggernaut && actor.IsAlive)
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

            if (pool.Current == 0)
            {
                BattleTranceEmptied?.Invoke(target);

                if (trance.ProtectWhenEmptied
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

        public void TickPrimaryPoolForTest(CombatantState actor) => TickPrimaryPool(actor);

        public void StartEngineActionForTest(CombatantState actor) => StartEngineAction(actor);

        public void SettleEngineActionForTest(CombatantState actor) => SettleEngineAction(actor);

        // A blow `attacker` deals with actorActed (their own swing), through
        // the real funnel, as StrikeForTest does -- but paying the attacker's
        // side as ApplyAndCountDamage does for a player action.
        public void DealForTest(CombatantState attacker, CombatantState target, int amount) =>
            DealDamage(attacker, target, amount, DamageType.Physical, KillCredit.Nobody);
    }
}
