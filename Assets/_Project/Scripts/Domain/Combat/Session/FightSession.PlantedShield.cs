using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // PHASE 4a, THE SENTINEL'S PLANTED SHIELD -- the session half of
    // PlantedShield (docs/PLAN_BJORN_CONSTELLATIONS.md, "Phase 4 engine
    // seams", 4a). The shield is a ward entry; this file is what makes that
    // entry a planted shield:
    //
    //   PlantShield / BashPlantedShield  -- the wiring's one-liners
    //   ResolveWard                      -- DamagePipeline's ward step, which
    //                                       wraps the status wards with the
    //                                       shield's bookkeeping, Shieldwall's
    //                                       shared pool and the reactive hooks
    //   SettleShieldReactions            -- the post-action seam where the
    //                                       queued answers land
    //   AgePlantedShield                 -- the holder's turn-end clock
    //
    // Nothing here does anything for a combatant who never planted and whose
    // reactive parameters are all 0/false: ResolveWard is then exactly
    // ResolveStatusWards, which is the ward step as it was.
    public sealed partial class FightSession
    {
        // SPELLBREAKER T2'S CALLBACK POINT: a magic hit on a holder with
        // SilenceCasterOnSpellHit (on him or on the shield covering an ally)
        // raises this with (holder, caster) when the action that dealt it
        // settles. The Silence status is content-hashed (StatusEffect.cs) and
        // does not exist yet; the Unity wiring appends it and subscribes here
        // (ApplyStatusTo(caster, Silence, 1, 1) plus its 3-turn per-enemy
        // cooldown). Raised once per caster per action.
        public event Action<CombatantState, CombatantState> SpellHitShieldHolder;

        // What a hit on a holder (or on his wall) owes its attacker. Queued by
        // ResolveWard and paid at the post-action seam, never mid-swing: the
        // attacker is still in the middle of its action when the ward step
        // runs, and a retaliation that killed it there would leave an AOE
        // loop swinging with a corpse. The same placement the plan-1.11
        // Thorned retaliation uses (TriggerPhysicalMoveRetaliation).
        private enum ShieldAnswer
        {
            Shards,
            Thorns,
            Reflect,
            Silence,
        }

        private readonly struct QueuedShieldAnswer
        {
            public readonly CombatantState Holder;
            public readonly CombatantState Attacker;
            public readonly ShieldAnswer Kind;
            public readonly int Amount;
            public readonly DamageType Type;

            public QueuedShieldAnswer(CombatantState holder, CombatantState attacker, ShieldAnswer kind, int amount,
                DamageType type)
            {
                Holder = holder;
                Attacker = attacker;
                Kind = kind;
                Amount = amount;
                Type = type;
            }
        }

        private readonly List<QueuedShieldAnswer> _shieldAnswers = new List<QueuedShieldAnswer>();

        // True while the answers are being dealt: a shard, thorn or reflection
        // is never itself answered (no ping-pong between two holders, and no
        // reflect of a reflect), whatever the target carries.
        private bool _settlingShieldAnswers;

        // ---- the wiring's one-liners ------------------------------------------

        // Plant the Shield (and, with CoversParty set, Shieldwall). Refused --
        // false, nothing changes -- while a shield is down or the re-place
        // wait is open, so the menu can ask CanPlace and the cast can trust it.
        public bool PlantShield(CombatantState holder)
        {
            if (holder == null || !holder.IsAlive) return false;

            var shield = holder.PlantedShield;
            if (!shield.CanPlace) return false;

            bool wall = shield.CoversParty;
            int points = wall ? PlantedShield.ShieldwallPointsFor(holder) : PlantedShield.PointsFor(holder);

            var ward = RaiseWard(holder, points, shield.LifetimeTurns, holder);
            if (ward == null) return false;

            shield.Place(ward, wall, IsHoldersTurn(holder));
            AppendMessage(wall
                ? $"{holder.Name} raises a shieldwall over the party ({points})."
                : $"{holder.Name} plants his shield ({points}).");
            return true;
        }

        // Shield Bash's first half: consumes the planted shield and returns
        // what it absorbed this placement, for the skill's
        // `base + PlantedShield.BashBonus(absorbed, 30)`. Starts the re-place
        // wait -- BashWaitTurns when ShortWaitAfterBash (T2), else the full
        // ReplaceWaitTurns. Returns 0 and does nothing when no shield is down.
        public int BashPlantedShield(CombatantState holder)
        {
            var shield = holder?.PlantedShield;
            if (shield == null || !shield.IsPlaced) return 0;

            int absorbed = shield.AbsorbedThisPlacement;
            holder.Statuses.Remove(shield.Ward);
            shield.End(shield.ShortWaitAfterBash ? shield.BashWaitTurns : shield.ReplaceWaitTurns,
                IsHoldersTurn(holder));
            return absorbed;
        }

        // ---- the ward step -----------------------------------------------------

        // DamagePipeline's resolveWard. In order:
        //
        //   1. The target's own wards (ResolveStatusWards) -- which include the
        //      target's own planted shield or wall, in the ordinary
        //      soonest-to-lapse drain order.
        //   2. If the target is an ally of a Shieldwall holder, what is left
        //      drains the wall. AFTER the ally's own wards: a one-turn ward is
        //      spent before a three-turn wall, the drain-order rule every ward
        //      already follows. An AOE reaches this once per ally it hits, so
        //      the one pool pays for each ally in full.
        //   3. Bookkeeping: what the shield soaked (Shield Bash), a break
        //      (ends the placement, starts the wait, queues shards), Fury.
        //   4. The reactive hooks are QUEUED for the post-action seam.
        private int ResolveWard(CombatantState target, int damage, CombatantState attacker, DamageType type,
            int incoming)
        {
            if (target == null) return Math.Max(0, damage);

            // The blow as it arrived, for the Sentinel engine's "raw incoming"
            // (FightSession.FuryEngines): read by the pool hearing this hit
            // gets, dropped below if the wards leave nothing to hear.
            NoteIncomingHit(target, incoming);

            var own = target.PlantedShield;
            var ownWard = own.IsPlaced ? own.Ward : null;
            int ownBefore = ownWard?.Magnitude ?? 0;

            int remaining = ResolveStatusWards(target, damage);

            // ---- 1. the target's own planted shield / wall
            int ownAbsorbed = ownWard == null ? 0 : ownBefore - ownWard.Magnitude;
            if (ownAbsorbed > 0)
            {
                own.NoteAbsorbed(ownAbsorbed);

                // THE HOLDER HEARS A BLOW HIS SHIELD ATE WHOLE. The pools hear
                // what DealDamage is handed, which is what is left AFTER the
                // wards -- so a hit a ward swallowed entirely has always paid
                // its victim's pool nothing (NoteDamageForPools returns on 0).
                // For the planted shield that is exactly wrong: "damage to his
                // planted shield counts" (plan section 2). Only when nothing is
                // left, so a partly-soaked hit is heard once, by DealDamage.
                // Every other ward keeps the old behaviour.
                if (remaining <= 0) NoteDamageForPools(attacker, target, ownAbsorbed);
            }

            if (ownWard != null && ownWard.Magnitude <= 0)
            {
                ShieldBroke(target, attacker);
            }

            // ---- 2. a Shieldwall covering this ally
            int wallAbsorbed = 0;
            var wallHolder = remaining > 0 ? WallCovering(target) : null;
            if (wallHolder != null)
            {
                wallAbsorbed = DrainWall(wallHolder, target, remaining, attacker);
                remaining -= wallAbsorbed;
            }

            // ---- 4. the reactive hooks, on "him or the shield"
            if (!_settlingShieldAnswers && attacker != null && damage > 0)
            {
                // A hit on the holder himself, planted or not: the whole
                // post-defence figure, shield share and health share together.
                QueueShieldAnswers(target, attacker, damage, type);

                // The part of an ally's hit his wall ate.
                if (wallHolder != null && wallAbsorbed > 0)
                {
                    QueueShieldAnswers(wallHolder, attacker, wallAbsorbed, type);
                }
            }

            // A hit the wards ate whole is not heard by the pools (the planted
            // shield's own hearing above has already consumed the figure), so
            // nothing may read it later.
            if (remaining <= 0) DropIncomingHit(target);

            return remaining;
        }

        // The living holder whose Shieldwall covers `ally`, or null. The
        // holder himself is never "covered" here -- his wall is his own ward
        // and ResolveStatusWards already drained it. A holder who has died
        // takes his wall with him: it ends the moment anyone asks for it.
        //
        // WHO IS COVERED is decided per hit, from the side's living members,
        // so an ally summoned or revived mid-fight is covered from their first
        // hit and a fallen ally simply stops being asked about.
        private CombatantState WallCovering(CombatantState ally)
        {
            if (ally == null || _encounter == null) return null;

            var side = ally.IsPlayerSide ? _encounter.PlayerParty : _encounter.Enemies;
            foreach (var holder in side)
            {
                if (holder == null || ReferenceEquals(holder, ally)) continue;

                var shield = holder.PlantedShield;
                if (!shield.IsPlaced || !shield.IsShieldwall) continue;

                if (!holder.IsAlive)
                {
                    holder.Statuses.Remove(shield.Ward);
                    shield.End(shield.ReplaceWaitTurns, onHoldersTurn: false);
                    continue;
                }

                return holder;
            }

            return null;
        }

        // Spends the wall for one ally's hit and pays the holder's Fury.
        // Returns what the wall absorbed.
        private int DrainWall(CombatantState holder, CombatantState ally, int damage, CombatantState attacker)
        {
            var shield = holder.PlantedShield;
            var ward = shield.Ward;

            int absorbed = Math.Min(ward.Magnitude, damage);
            if (absorbed <= 0) return 0;

            ward.Magnitude -= absorbed;
            shield.NoteAbsorbed(absorbed);
            RecordAbsorbed(absorbed);

            // Shieldwall's Fury: the per-hit clamp, then the per-turn cap.
            int perHit = PlantedShield.FuryForAbsorbedHit(absorbed, holder.MaxHealth);
            int allowed = shield.PartyFuryAllowance(perHit);
            int gained = allowed > 0 ? holder.PrimaryPool?.Gain(allowed) ?? 0 : 0;
            shield.PartyFuryThisTurn += gained;

            AppendMessage($"{holder.Name}'s shieldwall takes {absorbed} for {ally.Name}.");

            if (ward.Magnitude <= 0)
            {
                holder.Statuses.Remove(ward);
                ShieldBroke(holder, attacker);
            }

            return absorbed;
        }

        // A hit emptied the shield: the placement ends, the wait starts, and
        // the shards are owed to whoever broke it.
        private void ShieldBroke(CombatantState holder, CombatantState attacker)
        {
            var shield = holder.PlantedShield;
            if (!shield.IsPlaced) return;

            bool wall = shield.IsShieldwall;
            shield.End(shield.ReplaceWaitTurns, IsHoldersTurn(holder));
            AppendMessage(wall ? $"{holder.Name}'s shieldwall breaks!" : $"{holder.Name}'s planted shield breaks!");

            if (!_settlingShieldAnswers && attacker != null && shield.BreakShardDamage > 0)
            {
                _shieldAnswers.Add(new QueuedShieldAnswer(holder, attacker, ShieldAnswer.Shards,
                    shield.BreakShardDamage, DamageType.Physical));
            }
        }

        private void QueueShieldAnswers(CombatantState holder, CombatantState attacker, int amount, DamageType type)
        {
            var shield = holder.PlantedShield;
            if (ReferenceEquals(holder, attacker) || holder.IsPlayerSide == attacker.IsPlayerSide) return;

            if (type == DamageType.Physical)
            {
                int thorns = PlantedShield.PercentOf(amount, shield.ThornsPercent);
                if (thorns > 0)
                {
                    _shieldAnswers.Add(new QueuedShieldAnswer(holder, attacker, ShieldAnswer.Thorns, thorns, type));
                }

                return;
            }

            int reflected = PlantedShield.PercentOf(amount, shield.ReflectMagicPercent);
            if (reflected > 0)
            {
                _shieldAnswers.Add(new QueuedShieldAnswer(holder, attacker, ShieldAnswer.Reflect, reflected, type));
            }

            if (shield.SilenceCasterOnSpellHit)
            {
                _shieldAnswers.Add(new QueuedShieldAnswer(holder, attacker, ShieldAnswer.Silence, 0, type));
            }
        }

        // ---- the post-action seam ----------------------------------------------

        // Pays everything the action just finished owes the shields it struck.
        // Called beside TriggerPhysicalMoveRetaliation at both of its seams
        // (AdvanceAfterAction for a player action, AutoResolveEnemyTurns for a
        // monster's), with the same `physicalMove` fact: Thornwall answers
        // only a physical hit from a physical MOVE -- a strike or a charge,
        // CombatActions.IsPhysicalMove's reading, which is the nearest thing
        // this combat model has to "melee" -- so a physical-typed arrow or
        // spell is not returned.
        //
        // Each answer is its own hit on the attacker: through DamagePipeline
        // (its defences and wards apply; no dodge, crit or variance -- the
        // figure is an exact share of a blow that already landed), then
        // DealDamage with the holder as the source. The holder did not act
        // (actorActed: false, DealRelicPacket's rule) and scores the kill only
        // on his own turn (Cursed Blood's KillCredit rule). An attacker who
        // died meanwhile is owed nothing.
        private void SettleShieldReactions(bool physicalMove)
        {
            if (_shieldAnswers.Count == 0) return;

            var due = _shieldAnswers.ToList();
            _shieldAnswers.Clear();

            _settlingShieldAnswers = true;
            try
            {
                var silenced = new HashSet<(CombatantState, CombatantState)>();
                foreach (var answer in due)
                {
                    var attacker = answer.Attacker;
                    if (attacker == null || !attacker.IsAlive) continue;

                    switch (answer.Kind)
                    {
                        case ShieldAnswer.Silence:
                            if (silenced.Add((answer.Holder, attacker)))
                            {
                                SpellHitShieldHolder?.Invoke(answer.Holder, attacker);
                            }
                            break;

                        case ShieldAnswer.Thorns:
                            if (physicalMove) DealShieldAnswer(answer, "thorns");
                            break;

                        case ShieldAnswer.Reflect:
                            DealShieldAnswer(answer, "reflection");
                            break;

                        case ShieldAnswer.Shards:
                            DealShieldAnswer(answer, "shards");
                            break;
                    }
                }
            }
            finally
            {
                _settlingShieldAnswers = false;
            }
        }

        private void DealShieldAnswer(QueuedShieldAnswer answer, string what)
        {
            var holder = answer.Holder;
            var attacker = answer.Attacker;

            bool ownsBeat = _recordingBeat == null;
            if (ownsBeat) NewBeat(BeatCause.StatusTick, null, attacker, StageApproach.Hold, null);

            var outcome = DamagePipeline.AfterDefences(
                answer.Amount, answer.Type, attacker,
                affinity: AffinityOf(attacker),
                varianceRange: 0f,
                rng: _rng,
                resolveWard: ResolveWard,
                attacker: holder,
                dodgeAlreadyResolved: true,
                crit: false);

            if (outcome.Damage > 0)
            {
                var credit = IsHoldersTurn(holder) ? KillCredit.Attacker : KillCredit.Nobody;
                DealDamage(holder, attacker, outcome.Damage, answer.Type, credit, actorActed: false);

                if (ownsBeat)
                {
                    _recordingBeat.Amount = outcome.Damage;
                    _recordingBeat.IsHealing = false;
                    SetStance(attacker, attacker.IsAlive ? Stances.Hurt : Stances.Defeated);
                }

                AppendMessage($"{holder.Name}'s {what} strike {attacker.Name} for {outcome.Damage}!");
            }

            if (ownsBeat) CommitOrDropStatusTickBeat();
        }

        // ---- the clock ------------------------------------------------------------

        // The holder's turn ended (AgeEngineWindows). The wait ages first, so
        // a wait this very call opens is not also aged by it; then the
        // lifetime, whose end lifts the shield and opens the wait; then the
        // per-turn Fury cap starts over.
        //
        // Also notices a shield entry something else removed (Shatter, a
        // dispel): the placement ends, not broken -- no shards, the plain wait.
        private void AgePlantedShield(CombatantState holder)
        {
            var shield = holder.PlantedShield;

            shield.ReplaceWait.AgeAtHoldersTurnEnd();
            shield.PartyFuryThisTurn = 0;

            if (!shield.IsPlaced) return;

            var ward = shield.Ward;
            bool expired = shield.Lifetime.AgeAtHoldersTurnEnd();
            bool gone = !holder.Statuses.Contains(ward);
            if (!expired && !gone) return;

            bool wall = shield.IsShieldwall;
            holder.Statuses.Remove(ward);
            shield.End(shield.ReplaceWaitTurns, onHoldersTurn: false);

            if (expired && !gone)
            {
                AppendMessage(wall ? $"{holder.Name} lowers the shieldwall." : $"{holder.Name} lifts his planted shield.");
            }
        }

        // ---- seams for tests ------------------------------------------------------

        // One blow through the real funnel, as an enemy's swing lands:
        // AfterDefences (no dodge, no crit, no variance) with the ward step,
        // then DealDamage. What a test needs to put an attacker's hit on a
        // holder or an ally without staging a whole enemy turn.
        public void StrikeForTest(CombatantState attacker, CombatantState target, int raw, DamageType type)
        {
            var outcome = DamagePipeline.AfterDefences(
                raw, type, target,
                affinity: AffinityOf(target),
                varianceRange: 0f,
                rng: _rng,
                resolveWard: ResolveWard,
                attacker: attacker,
                dodgeAlreadyResolved: true,
                crit: false);

            DealDamage(attacker, target, outcome.Damage, type, KillCredit.Nobody);
        }

        public void SettleShieldReactionsForTest(bool physicalMove) => SettleShieldReactions(physicalMove);
    }
}
