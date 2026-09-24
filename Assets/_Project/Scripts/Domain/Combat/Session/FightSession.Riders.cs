using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // What happens BETWEEN actions: the extra turns an action can earn, and the
    // turn-start bookkeeping the next actor gets.
    //
    // Ported from v1's FightController.Turns.cs. The ordering here is the whole
    // content of the file and it had no test of its own before this one.
    public sealed partial class FightSession
    {
        // Set by an action that killed something, read once by the rider block.
        // WRITTEN IN EXACTLY ONE PLACE -- SettleDeath (FightSession.Ledger.cs),
        // which DealDamage calls for every death it causes. It was assigned at
        // five call sites until 2026-09-06 and one of them had already stopped.
        private bool _killedThisAction;

        // ONE STREAK FOR EVERY EXTRA ACTION A KILL CAN BUY, whichever rider
        // granted it. Trample and Bloodlust each read their own cap against
        // this same count, so the chain is bounded by the larger of the two
        // caps, never their sum. Two counters, one per source, is what let a
        // capped Trample fall through to a Bloodlust whose own count had been
        // held at zero the whole time -- Trample 1 + Bloodlust 2 = a four-attack
        // chain, the exact thing the short-circuit below was written to stop.
        private CombatantState _extraActionChainActor;
        private int _extraActionChainCount;

        // Set by whichever grant fired, read and cleared once by
        // AdvanceAfterAction immediately after AdvanceTurn. It is the only
        // thing that can tell the two kinds of turn apart at that line: both
        // grants call GrantExtraTurn BEFORE the advance, so by the time the
        // queue has moved, "the same actor is Current" is equally true of a
        // trample and of a one-combatant fight.
        //
        // A field rather than a return value because the two grants are not
        // called from the same place -- Bloodlust arrives through
        // RelicsOnKill (FightSession.Relics.cs) -- and threading a bool back
        // out through the relic block would put the fact somewhere it is not
        // about.
        private CombatantState _grantedExtraTurnTo;

        // THORN TITHE'S OPENING TICK, SNAPSHOTTED BEFORE IT CAN BE REMOVED
        // (plan 1.11/2.12). Thorned sits on the AtTick clock like every other
        // DoT (1.4): its final instance is removed inside StatusEffects.Tick
        // on the very turn it fires its last tick, at that turn's OWN START --
        // before the actor has even acted. 2.12 still promises a retaliation
        // on that turn's action, so TickStatuses captures the live instances
        // here, before calling StatusEffects.Tick, and
        // TriggerPhysicalMoveRetaliation reads them back later in the SAME
        // turn. The instance OBJECTS outlive their removal from
        // CombatantState.Statuses -- only the list forgets them -- so their
        // Magnitude is exactly 1.5's stored snapshot, however long after
        // application it is read.
        //
        // Keyed by actor rather than held as a single slot: nothing in this
        // file assumes only one combatant's turn is ever "open" between a
        // turn-start tick and that turn's action (a monster resolves inside
        // one call, but the shape costs nothing to make correct regardless).
        // Cleared to "no entry" the moment a turn-start tick finds no live
        // Thorned, so a combatant who is never Thorned again cannot retaliate
        // off a fight-old snapshot the next time they happen to swing.
        private readonly Dictionary<CombatantState, List<ActiveStatus>> _thornedAtTurnStart =
            new Dictionary<CombatantState, List<ActiveStatus>>();

        // A TURN IS OVER, HOWEVER IT ENDED -- the one place the turn-end clock
        // is wound, sitting immediately before every `_encounter.AdvanceTurn()`
        // in the session.
        //
        // IT USED TO LIVE INSIDE AdvanceAfterAction, and that was a hole with
        // a growing blast radius. AdvanceAfterAction is reached only by the
        // four PLAYER commands (attack, cast, item, move); an enemy's turn and
        // every skipped turn on either side advance through StepToNextTurn
        // instead, and an egg's through AutoResolveEggTurns. So no AtTurnEnd
        // status on a monster ever aged, and no skipped turn aged anything --
        // which was invisible while wards were the only thing on this clock
        // (a monster rarely wears one) and became live the moment plan D1
        // moved Protect, Vulnerable, Chilled, Rooted and Marked onto it in
        // milestone A. Winter's Rebuke's two-turn Chill was permanent;
        // milestone D's two-turn Root would have been permanent AND
        // self-sustaining, because a rooted monster with nothing legal
        // forfeits, and a forfeited turn was exactly the kind that aged
        // nothing. Found by TheSecondOfTwoShackledTurns_IsStillRestricted
        // failing on its THIRD turn.
        //
        // SKIPPED WHEN THE SAME ACTOR IS ABOUT TO ACT AGAIN: an extra action
        // is the SAME turn (AUDIT #113), so a trample chain must not age a
        // ward once per swing.
        //
        // AND SKIPPED FOR A CORPSE. A combatant can die inside its own turn
        // start (a poison tick) and reach the advance without ever acting;
        // ageing its statuses would print "the shield around X fades" over a
        // body. Nothing downstream reads a dead combatant's statuses, so there
        // is nothing to age either.
        private void EndTurnStatusesForCurrent()
        {
            var ending = _encounter?.Current;
            if (ending == null || !ending.IsAlive) return;
            if (_grantedExtraTurnTo != null && ReferenceEquals(_grantedExtraTurnTo, ending)) return;

            TickStatusesAtTurnEnd(ending);
        }

        // Everything after an action resolves: extra turns, then the schedule
        // moves on, then the next actor's turn-start bookkeeping.
        //
        // `physicalMove` IS PLAN 1.11'S POST-ACTION HOOK, for the PLAYER half
        // of it -- see TriggerPhysicalMoveRetaliation's own header for why
        // the hook does not live where the plan first put it, and
        // FightSession.Enemies.AutoResolveEnemyTurns for the monster half,
        // which never reaches this method at all. Every one of the four
        // callers (ExecuteAttack, Move, CastSkillOnPicks, UseItem) already
        // knows at its own call site whether what just happened was a
        // physical move -- Move never is; a plain attack always is
        // (CombatActions.PlainAttackIsPhysicalMove); a cast reads its own
        // skill's authored classification (CombatActions.IsPhysicalMove) -- so
        // this is the one place that fact and "an action just completed"
        // meet, which is exactly 1.11's own definition of when the hook
        // fires. FIRED FIRST, ahead of even the fight-over check below: a
        // retaliation that kills the last enemy must be able to end the
        // fight through the ordinary path, not slip past it.
        private void AdvanceAfterAction(bool physicalMove = false)
        {
            // Enemy actions release this in AutoResolveEnemyTurns. Player
            // actions finish through this funnel instead, so clear the same
            // one-action recovery before any early fight-over return.
            _hardControlRecovery.Remove(_encounter.Current);

            if (physicalMove) TriggerPhysicalMoveRetaliation(_encounter.Current);

            // Read-then-reset up front, unconditionally, so a flag can never
            // leak into a fight that is ending right here or a turn that has
            // not happened yet -- whichever path below runs, it starts clean.
            bool killedThisAction = _killedThisAction;
            _killedThisAction = false;

            // AND THIS TURN'S FREE ACTION, released here for the same reason
            // and on the same principle. A free action (Shawn's Tuck In, the
            // Fragile Lamb's Fleece Ward T3) deliberately does NOT reach this
            // method, so arriving here is exactly "the turn that held the
            // lock is over" -- including the turn an extra action was granted
            // for, which is a new turn and gets its own. See
            // FightSession.Skills' _freeActionTakenBy.
            _freeActionTakenBy = null;

            // The action that killed the last enemy ends the fight right here,
            // before any rider could matter -- but the celebration still has to
            // happen, and this is the only path that reaches it.
            if (_encounter.IsOver)
            {
                ResolveVictory();
                ResolveOutcome();
                return;
            }

            // Trample is tried BEFORE Bloodlust and SHORT-CIRCUITS it, rather
            // than both firing: a Ram wearing the Bloodlust relic would
            // otherwise bank two extra turns for one kill, which neither the
            // talent nor the relic promises. One kill, one extra action, from
            // whichever source still has room -- and both sources count
            // against the ONE streak above, so falling through to Bloodlust
            // once Trample is capped cannot stack their two caps into a
            // four-attack chain either.
            bool granted = false;
            if (killedThisAction)
            {
                var actor = _encounter.Current;
                granted = TryGrantTrample(actor) || RelicsOnKill(actor);
            }

            // Any action that did not earn an extra one ends the streak -- a
            // kill past the cap as much as an action that killed nothing --
            // which is what makes the cap read as "per turn" rather than "per
            // fight". Cleared HERE rather than inside either grant, because a
            // counter that only resets on the path that increments it never
            // resets at all.
            if (!granted)
            {
                _extraActionChainActor = null;
                _extraActionChainCount = 0;
            }

            // THE END OF THE TURN, and the one clock in the game that runs
            // here rather than at a turn's start -- see StatusEffects' own
            // WARDS header for why a ward is visible on the turn it protects
            // and every other duration is not.
            EndTurnStatusesForCurrent();

            _encounter.AdvanceTurn();

            // AUDIT #113: an extra action is the SAME turn, so it re-pays
            // nothing. Read-then-reset here for the same reason
            // _killedThisAction is read that way at the top of this method:
            // whichever branch runs below, the next action starts clean.
            var extraTurnFor = _grantedExtraTurnTo;
            _grantedExtraTurnTo = null;

            if (_encounter != null && !_encounter.IsOver)
            {
                var opening = _encounter.Current;
                if (extraTurnFor != null && ReferenceEquals(opening, extraTurnFor))
                {
                    ReopenTurnFor(opening);
                }
                else
                {
                    OpenTurnFor(opening);
                }
            }

            // Every enemy turn between this action and the player's next one
            // resolves right here, synchronously, before the view has drawn a
            // frame of any of it. The beat queue is what makes that safe.
            AutoResolveEnemyTurns();

            // Phoenix Egg: an egg cannot act, so its holder's own turn (once
            // it comes back around) resolves itself the same way an enemy's
            // does -- see AutoResolveEggTurns' own header.
            AutoResolveEggTurns();

            if (_encounter.IsOver)
            {
                ResolveVictory();
                ResolveOutcome();
                return;
            }

            // Committed only once the turn is genuinely back with the player,
            // so what the nameplates telegraph is the round the player is about
            // to plan for and not one already spent.
            if (_encounter.IsPlayerTurn)
            {
                PrepareEnemyIntents();
            }
        }

        // The Domain half of the fight ending: who is celebrating, what they
        // say, and what the log records. The payout itself is arithmetic and
        // lives in VictoryRewards; applying it to a save is Core's.
        //
        // Runs at most once per fight. A round resolves in a single pass and can
        // reach this from more than one path -- an action that killed the last
        // enemy, and the enemy loop noticing the fight is over -- and a squad
        // that celebrated twice would whoop over itself.
        private bool _victoryResolved;
        private readonly List<string> _victoryVoiceIds = new List<string>();

        // Who should whoop, in party order. Empty until the fight is won.
        public IReadOnlyList<string> VictoryVoiceIds => _victoryVoiceIds;

        private void ResolveVictory()
        {
            if (_victoryResolved || !_encounter.PlayerWon) return;
            _victoryResolved = true;

            foreach (var survivor in _encounter.LivingPlayerParty.ToList())
            {
                // Retro-attached to the last beat, like every other line a turn
                // decides after its beat closed, so the pose is already in place
                // for the frame that ends the fight.
                PoseOnLatestBeat(survivor, Stances.Victory);

                // Collected rather than played: Domain has no audio. Core drains
                // this when the fight ends, and the once-per-fight guard above is
                // what stops a squad whooping twice over itself.
                var kit = KitFor(survivor);
                if (kit != null && !_victoryVoiceIds.Contains(kit.Id))
                {
                    _victoryVoiceIds.Add(kit.Id);
                }
            }

            AppendMessage("Victory!");
        }

        // Trample T3: a kill does not consume the action. GrantExtraTurn is
        // called BEFORE AdvanceTurn -- it stacks onto whoever is still
        // Current, and nothing has moved the queue on yet, so the very next
        // advance hands the turn straight back rather than to whoever the
        // schedule says is next. Its own cap, for the same reason Bloodlust
        // has one.
        private bool TryGrantTrample(CombatantState actor)
        {
            int cap = actor == null ? 0 : actor.Talents.Best(TalentEffectType.ExtraAttackOnKill);
            if (cap <= 0 || !actor.IsPlayerSide || !actor.IsAlive) return false;

            int soFar = ExtraActionsSoFar(actor);
            if (soFar >= cap || !_encounter.GrantExtraTurn(actor)) return false;

            CountExtraAction(actor, soFar);
            AppendMessage($"{actor.Name} tramples straight over the body and keeps going!");
            return true;
        }

        // Bloodlust: killing an enemy earns the actor an extra turn on the
        // spot, capped so a good room cannot become an unbounded chain. The
        // cap is read against the shared streak, so extra actions Trample
        // already granted this turn count toward it.
        private bool TryGrantBloodlust(CombatantState actor)
        {
            if (actor == null || !actor.IsPlayerSide || !actor.IsAlive
                || !HasRelic(actor, RelicEffect.Bloodlust))
            {
                return false;
            }

            int soFar = ExtraActionsSoFar(actor);
            if (soFar >= FightTuning.MaxBloodlustChain || !_encounter.GrantExtraTurn(actor)) return false;

            CountExtraAction(actor, soFar);
            AppendMessage($"{actor.Name}'s Bloodlust surges - one more turn!");
            return true;
        }

        private int ExtraActionsSoFar(CombatantState actor) =>
            ReferenceEquals(actor, _extraActionChainActor) ? _extraActionChainCount : 0;

        private void CountExtraAction(CombatantState actor, int soFar)
        {
            _extraActionChainActor = actor;
            _extraActionChainCount = soFar + 1;
            _grantedExtraTurnTo = actor;
        }

        // From the kit, not from a save-file lookup. v1 read the relic loadout
        // out of GameplayManager.Instance.Save here, which is a large part of
        // why this logic could not leave the controller.
        // EVERY RELIC THEY ARE CARRYING, not just the first one.
        //
        // This read Relics[0] and answered "what is this character's relic
        // effect", which was a fair question while three effects existed and a
        // draft handed out one relic. It silently disabled the second: a character holding Bloodlust and Dual Wield
        // gets whichever the list happens to open with, and nothing anywhere
        // says the other one is inert.
        //
        // Asked as "do they have THIS" rather than "what do they have",
        // because that is the question every call site actually has and it is
        // the only spelling that stays correct as the list grows.
        private bool HasRelic(CombatantState actor, RelicEffect effect)
        {
            var kit = KitFor(actor);
            return kit != null && kit.HasRelic(effect);
        }

        // Hands the turn to whoever is Current and OPENS it. The entry every
        // caller that genuinely moved the schedule on uses: Begin,
        // StepToNextTurn (FightSession.Enemies.cs) and AutoResolveEggTurns
        // (FightSession.RelicMechanics.cs). AdvanceAfterAction does not come
        // through here, because it is the one caller that has to choose --
        // see ReopenTurnFor.
        private void GrantTurnStart()
        {
            if (_encounter == null || _encounter.IsOver) return;

            OpenTurnFor(_encounter.Current);
        }

        // A NEW actor's turn opens: mana regenerates, statuses tick, and a
        // signature resource fills by its per-turn allowance. Everything a
        // turn costs its owner is paid exactly here.
        private void OpenTurnFor(CombatantState actor)
        {
            if (actor == null) return;

            // Mechanic (f): a fresh actor's turn is the turn boundary that
            // actor's own OncePerTurn locks reset against -- see CombatLocks'
            // own header. Scoped to this actor only: another combatant's
            // once-per-turn locks must survive until THEIR turn starts.
            _locks.ResetTurn(actor);

            TickPrimaryPool(actor);
            ApplyRunicWardConversion(actor);
            TickStatuses(actor);
            TickCooldowns(actor);
            TickSpeedBuffs(actor);

            // The necklace is a function of current health rather than an
            // event, so it is recomputed rather than triggered -- see
            // FightSession.Relics.
            RefreshNecklaceSpeed(actor);

            // Every other duration ticks here too, so "a turn" means the same
            // thing for a ward payout cap, a grace period and a transform as it
            // does for a status.
            TickLambTurnStart(actor);
            TickTransform(actor);

            // Phoenix Egg: the shell's own 3-turn clock.
            TickPhoenixEgg(actor);

            var signature = actor.SignaturePool;
            if (signature == null) return;

            int owed = SignaturePerTurnFor(actor);
            if (owed <= 0) return;

            if (signature.Gain(owed) < owed)
            {
                AppendMessage($"{actor.Name}'s {signature.DisplayName} is as full as it will get.");
            }
        }

        // THE SAME ACTOR TAKES ANOTHER ACTION. Trample and Bloodlust buy an
        // extra ACTION, not an extra turn, and this is what that distinction
        // costs in code: a turn is paid for once, when it opens, and an extra
        // action inside it re-pays nothing.
        //
        // AUDIT #113. Until 2026-09-11 an extra turn ran the whole of
        // OpenTurnFor a second time, so a trampling hero was poisoned once per
        // KILL, every status duration he carried aged once per kill, his
        // cooldowns refunded a turn per kill, and Black Ram Mode -- a form
        // talents.json sells as "three turns", and whose prerequisite is the
        // Trample talent itself -- reliably lost two of those three inside one
        // round. The content row was lying about itself, which is the half of
        // this that was never a balance question.
        //
        // WHAT IS PAID AGAIN, and why it is exactly these:
        //
        //   _locks.ResetTurn -- an OncePerTurn lock gates one ACTION's worth
        //   of a relic or a talent, and the extra action is an action. Cleared
        //   by the clearance ledger's row K8, which reasoned about precisely
        //   this pair.
        //
        //   TickPrimaryPool -- the pool's income is per action taken, not per
        //   turn of the clock (K8 again). It is also the one step the
        //   remaining two read, which is what keeps them here:
        //
        //   ApplyRunicWardConversion -- a RECOMPUTE, not an event. Its whole
        //   contract is "whatever mana is sitting unspent RIGHT NOW, including
        //   the regen this very turn-start just granted", so dropping it while
        //   keeping the grant above would leave the ward reading off mana that
        //   no longer exists. It refreshes rather than stacks (StatusEffects.
        //   Apply takes the max), so running it again cannot compound.
        //
        //   RefreshNecklaceSpeed -- likewise a function of current health
        //   rather than an event, and idempotent for the same reason.
        //
        // Everything else is a CLOCK, and a clock that ticks on an action
        // rather than on a turn is the bug: TickStatuses (the poison tick and
        // every duration countdown), TickCooldowns, TickSpeedBuffs,
        // TickLambTurnStart, TickTransform, TickPhoenixEgg, and the signature
        // pool's per-turn gain.
        private void ReopenTurnFor(CombatantState actor)
        {
            if (actor == null) return;

            _locks.ResetTurn(actor);
            TickPrimaryPool(actor);
            ApplyRunicWardConversion(actor);
            RefreshNecklaceSpeed(actor);
        }

        // THE ONE TURN-START TICK FOR THE PRIMARY POOL, and the whole of the
        // gain/decay rule for it. Was RegenerateMana, which only ever added
        // ManaRegen; the pool now owns both halves, so widening this to a
        // resource that decays on an idle turn is authored rather than coded
        // (ResourcePool.TickTurnStart).
        //
        // NOT through CombatMath.RestoreMana, and the difference matters: a
        // per-turn gain is the pool's own income, not a mana effect, so a
        // pool that refuses potions still regenerates whatever it authored.
        //
        // The per-turn gain for the SIGNATURE pool is deliberately still at
        // the bottom of GrantTurnStart rather than folded in here: it is
        // Charisma-scaled and talent-fed (SignaturePerTurnFor), it says
        // something when it overflows, and moving it would change the order
        // two pools fill in for no gain.
        private void TickPrimaryPool(CombatantState actor)
        {
            actor?.PrimaryPool?.TickTurnStart();
        }

        // Runic's mana->Ward conversion: at the start of the wearer's own
        // turn, whatever mana is sitting UNSPENT (including the regen this
        // very turn-start just granted -- "unspent right now", not "unspent
        // before this turn began") becomes a Shielded status. See
        // FightTuning.RunicWardConversionRate's own comment for why the rate
        // is a fixed, deliberately weak constant rather than an authored
        // number, and ModifierEffectType.ManaToWardOnTurnStartPercent's own
        // comment for why this is gated on Has() (a flag) rather than a
        // magnitude.
        //
        // STACKS, WITHOUT A CEILING, and that is the owner's call rather than
        // an oversight (AUDIT #154): it fires at the top of every one of the
        // wearer's turns for free, so a hoarded mana pool that is never spent
        // lays another pool on top of the last one every round, for the whole
        // fight. The ward it lays down carries the relic wards' whole-fight
        // duration (FightTuning.MagicalShieldDurationTurns) rather than the
        // skills' one-turn default, because this is a standing property of the
        // armour and not a window somebody spent a cast opening.
        private void ApplyRunicWardConversion(CombatantState actor)
        {
            if (actor == null || actor.CurrentMana <= 0) return;

            // AND IT HAS TO BE MANA. actor.CurrentMana is the compatibility
            // getter onto PrimaryPool.Current, and CombatantState's own header
            // says what that costs: the getters "will stop being truthful the
            // moment a character's primary pool is not mana... which is why
            // they are getters and not fields". Every WRITE site was made to
            // ask; this READ was not, so on a Fury holder the conversion sized
            // a defensive ward off a rage bar -- highest exactly when he is
            // winning -- and the log said "his runes catch the leftover mana".
            //
            // RestoredByManaEffects is the predicate that already means "this
            // pool is mana-shaped for effects that speak about mana", the same
            // one the potion and the gift read. Not a hardcoded pool id: a
            // second mana-shaped row would be mana to Runic too.
            if (actor.PrimaryPool == null || !actor.PrimaryPool.RestoredByManaEffects) return;

            if (!actor.ModifierEffects.Has(ModifierEffectType.ManaToWardOnTurnStartPercent)) return;

            // NO CEILING, owner 2026-09-16 (AUDIT #154). It used to be capped
            // at RunicWardPointsCap, and the cap made sense while a new ward
            // REPLACED the standing one -- "the conversion tops the pool back
            // up" was true then. Wards stack now and the owner's answer is
            // that a wearer who hoards mana banking points every turn is the
            // design, not a hole in it.
            int wardPoints = Rounding.AwayFromZero(actor.CurrentMana * FightTuning.RunicWardConversionRate);
            if (wardPoints <= 0) return;

            RaiseWard(actor, wardPoints, FightTuning.MagicalShieldDurationTurns, actor);
            AppendMessage($"{actor.Name}'s runes catch the leftover mana as a ward.");
        }

        // THE ENTRIES APPLIED DURING THE TURN THAT IS RUNNING, which do not
        // age at the end of it -- see StatusEffects' WARDS header for the
        // argument and TickAtTurnEnd for where the set is read. Cleared by
        // OpenTurnFor and NOT by ReopenTurnFor, because an extra action is the
        // same turn (AUDIT #113).
        //
        // Holds the ActiveStatus itself rather than the wearer and the type:
        // two chills of the same size on the same clock are told apart by
        // nothing else, and since 2026-09-20 a combatant really can be carrying
        // two of them.
        //
        // It used to be _wardsRaisedThisTurn, filled only by RaiseWard, because
        // a ward was the only thing on the turn-end clock. Five more statuses
        // joined that clock with plan D1 and they need the identical exemption:
        // a Vulnerable a caster puts on THEMSELVES (Court of Whispers, Ashen
        // Reckoning) must not be aged by the end of the turn that applied it.
        private readonly HashSet<ActiveStatus> _statusesAppliedThisTurn = new HashSet<ActiveStatus>();

        // THE ONE PLACE A STATUS IS APPLIED TO A COMBATANT during a fight.
        //
        // StatusEffects.Apply is pure Domain and cannot reach anything a status
        // needs bookkeeping for. Chilled is the standing proof: ApplyChilled is
        // the only path that registers the speed malus, and its own header says
        // so -- so ApplySkillStatus, which called StatusEffects.Apply directly,
        // would have landed an inert Chilled badge the first time a content row
        // authored one. Nothing does today, which is why the gap was latent
        // rather than a bug report.
        //
        // Fixed at the SEAM rather than at the caller: an `if (type == Chilled)`
        // inside ApplySkillStatus would leave the next status with bookkeeping
        // to repeat the bug at the next call site. Every relic, talent, item
        // modifier and skill goes through here; StatusEffects.ApplyWard stays
        // the separate entry point for shields, for the reason its own header
        // gives.
        private void ApplyStatusTo(CombatantState recipient, StatusEffectType type,
            int magnitude, int turns, CombatantState source = null)
        {
            if (recipient == null) return;

            // Keep repeat-control recovery at the shared status seam so an
            // older relic, talent or modifier cannot bypass the spell-level
            // guard by applying Rooted or Feared directly.
            if (HardControlRecoveryBlocks(recipient, type)) return;

            if (type == StatusEffectType.Chilled)
            {
                ApplyChilled(recipient, magnitude, turns, source);
                return;
            }

            // THE NEW-DOT SNAPSHOT (plan 1.5), taken HERE rather than at
            // ResolveAfflict -- the whole reason Censer of Embers and Thorn
            // Tithe cost content and no code (FightSession.Skills
            // .ResolveAfflict's own comment) is that this seam is where
            // Chilled's bookkeeping already lives, so a second damaging
            // status with its own arithmetic joins it rather than teaching
            // ResolveAfflict a third spell-shaped branch. `magnitude` is
            // still the raw authored `intensityStep` on the way in; what is
            // RECORDED is `intensityStep * SkillPowerMultiplierFor(source) *
            // SpellScalingMultiplierFor(source)`, rounded away from zero and
            // floored at 1 -- the exact arithmetic ResolveDamageInstances
            // already uses for a fixed packet. The caster is never consulted
            // again after this line; every later tick and every retaliation
            // reads the stored figure off the entry itself.
            if (type == StatusEffectType.Burn || type == StatusEffectType.Thorned)
            {
                magnitude = SnapshotDotMagnitude(magnitude, source);
            }

            RecordStatus(recipient, type, magnitude, turns, source);
        }

        private bool HardControlRecoveryBlocks(CombatantState recipient, StatusEffectType type) =>
            recipient != null
            && (type == StatusEffectType.Rooted || type == StatusEffectType.Feared)
            && _hardControlRecovery.Contains(recipient);

        // See ApplyStatusTo's own comment for why this runs at the seam
        // rather than at ResolveAfflict. `caster` is who PAID for the cast --
        // null on the one path that authors a status with no caster at all
        // (FightSession.Enemies.ResolveEnemyAction's on-hit `ApplyStatusTo`
        // call, which passes no source), in which case there is no potency to
        // fold in and the authored base stands, still floored at 1.
        private int SnapshotDotMagnitude(int intensityStep, CombatantState caster)
        {
            float multiplier = caster != null
                ? SkillPowerMultiplierFor(caster) * SpellScalingMultiplierFor(caster)
                : 1f;
            return System.Math.Max(1, Rounding.AwayFromZero(intensityStep * multiplier));
        }

        // The application itself plus the turn-end exemption, with no
        // status-specific bookkeeping. Separate from ApplyStatusTo so a
        // dispatch arm that DOES have bookkeeping (ApplyChilled) can reach the
        // application without recursing back through the dispatch.
        private ActiveStatus RecordStatus(CombatantState recipient, StatusEffectType type,
            int magnitude, int turns, CombatantState source)
        {
            var applied = StatusEffects.Apply(recipient.Statuses, type, magnitude, turns, source);
            if (applied != null) _statusesAppliedThisTurn.Add(applied);
            return applied;
        }

        // StatusEffects.Tick applies the numbers and reports WHAT happened;
        // turning that into log lines is this layer's job, because the wording
        // belongs to the fight rather than to the status system.
        private void TickStatuses(CombatantState actor)
        {
            if (actor == null) return;

            // Mechanic (c): every falling-off stack this combatant carries
            // (Cursed Idol's key included) ages by one of THEIR OWN turns
            // here too, the same "holder's own turns" rule every status
            // duration in this file already follows.
            FallingOffStacks.TickAll(actor);

            // Ice Fingernail's own malus has to shrink alongside its
            // stacks -- nothing else re-reads the stack count once a
            // stack falls off on its own clock (as opposed to a fresh
            // attack re-stacking it), so the malus would otherwise
            // outlive every stack that justified it.
            RefreshIceFingernailSpeed(actor);

            // TAKEN BEFORE THE TICK, because the tick SPENDS the health the
            // beats below exist to show being spent. StatusEffects.Tick
            // applies poison and regen in one pass and only then reports what
            // it did, so by the time this method can decide to record
            // anything, live vitals are already the AFTER picture -- and a
            // beat whose PreSnapshot is its own Snapshot drops the health bar
            // the instant the beat opens instead of on the frame the tick
            // lands, which is the exact failure CombatBeat's own header
            // records for the round as a whole.
            //
            // Unconditional, and one small dictionary per turn start is what
            // that costs. Asking first whether a damaging status is present
            // would be a second copy of Tick's own condition, free to
            // disagree with it the day a second DoT lands.
            var preTick = SnapshotVitals();

            // BEFORE THE TICK, and separate from preTick: Thorned's
            // post-action retaliation (plan 1.11/2.12) can fire LATER this
            // same turn, after this very call may already have removed the
            // Thorned instance that pays for it -- Thorned is AtTick like
            // every other DoT (1.4), so its final tick and its removal are
            // the same pass. Snapshotting the live instances here, before
            // StatusEffects.Tick touches anything, is what lets the third
            // affected turn's action still be covered. See
            // TriggerPhysicalMoveRetaliation's own header.
            var thornedNow = StatusEffects.InstancesOf(actor, StatusEffectType.Thorned).ToList();
            if (thornedNow.Count > 0) _thornedAtTurnStart[actor] = thornedNow;
            else _thornedAtTurnStart.Remove(actor);

            var report = StatusEffects.Tick(actor, AffinityOf(actor));

            // CHILLED'S TEARDOWN IS NOT HERE ANY MORE. It moved to
            // TickStatusesAtTurnEnd with the clock (plan D1): Chilled is an
            // AtTurnEnd status now, so StatusEffects.Tick never reports it
            // expired and a revoke here would be dead code waiting to be read
            // as coverage. The teardown follows whichever clock removed the
            // entry -- that is the general rule, and this is the one status
            // that currently has a teardown at all.
            if (report.IsEmpty) return;

            // ONE BEAT PER ROW (plan D5) -- a tick carrying two damage types
            // (Poison and a fresh Burn, say) shows and records both,
            // separately, rather than folding them into one number under one
            // element. GATED ON THE WHOLE ROW, not on the part that reached
            // health: a signature pool spending itself before health does
            // still leaves ToHealth at zero, and reading that as "nothing
            // happened" is how points of armour got spent with no line, no
            // ledger row and no word to the pools.
            //
            // SETTLED AT MOST ONCE FOR THE WHOLE TICK, unlike the beat/message/
            // ledger calls above, which run once per row. Every row's damage
            // already landed inside StatusEffects.Tick, before this loop ever
            // starts, so a target already dead when row 2 is reported did not
            // die twice -- SettleDeath's own guard only refuses a target that
            // is still ALIVE, not one settled a moment ago by row 1 in this
            // same tick, so calling it again here would double the "went
            // down" count for one body. `settled` is this method's own guard
            // for that, the same shape DealDamage's wasAlive/now-dead check
            // enforces on every other damage path.
            bool settled = false;
            foreach (var row in report.Rows)
            {
                int amount = row.ToHealth + row.Absorbed;
                if (amount <= 0) continue;

                // THE TICK BECOMES A BEAT, so the stage plays it the way it
                // plays every other blow: the hurt pose, the recoil, the
                // flash tinted with the status's own element, the number.
                // Owner 2026-09-19 -- "poison damage or DoTs are not clear".
                // Until now a tick was log-only, because it never opened one.
                //
                // OPENED BEFORE THE LINES BELOW, so they land ON it rather
                // than being retro-attached to whatever beat happened to be
                // last (AppendMessage's own fallback). That is also what
                // moves the line from the end of the PREVIOUS blow to the
                // moment the tick is shown.
                bool ownsBeat = BeginStatusTickBeat(actor, preTick, row.Status, amount, isHealing: false);

                // THE MAGNITUDE, THEN WHAT ATE IT -- the shape the enemy swing
                // already uses ("attacks X for N damage!" followed by "X's Wool
                // soaks M of it."). Printing the health figure instead would
                // announce "suffers 0 damage!" for a tick the armour stopped,
                // and leave the soak line with no antecedent for "it".
                AppendMessage($"{actor.Name} suffers {amount} {TickVerb(row.Status)} damage!");

                if (row.Absorbed > 0 && actor.SignaturePool != null)
                {
                    AppendMessage(
                        $"{actor.Name}'s {actor.SignaturePool.DisplayName} soaks {row.Absorbed} of it.");
                }

                // Counted as TAKEN and credited to nobody. The status was
                // applied turns ago by someone who may now be dead, and
                // back-crediting it would put points in a column the player
                // cannot account for against any blow they watched land.
                //
                // Both halves handed over separately, exactly as the funnel's
                // own Ledger.Took call does: what a pool ate was never taken by
                // health, and folding the two into one number would double-count
                // every absorbed point. ONCE PER ROW (plan D5) -- a tick
                // carrying two damage types calls this twice, not once with a
                // summed figure, so the ledger can still tell them apart.
                RecordUnattributedDamage(actor, row.ToHealth, row.Absorbed);

                // And if the tick killed, that death is settled with the same
                // KillCredit.Nobody the comment above argues for -- WRITTEN
                // DOWN rather than left as a missing call. This is the one
                // deliberate exception to "a death raises the rider flag and
                // takes a kill row", and an exception spelled as an absence is
                // indistinguishable from the bug SettleDeath exists to kill.
                // The call is a no-op on the Nobody branch by design; what it
                // buys is that `grep SettleDeath` finds every death decision
                // in the file family, this one included.
                if (!settled && !actor.IsAlive)
                {
                    SettleDeath(actor: null, target: actor, credit: KillCredit.Nobody);
                    settled = true;
                }

                // COMMITTED AFTER THE DEATH IS SETTLED, and that ordering is
                // the point: SettleDeath records no beat of its own, so the
                // tick's beat is the only thing that can show the kill --
                // CommitBeat's snapshot is what FightController.FadeTheFallen
                // reads to fade a body, exactly as it does for a killing
                // swing.
                if (ownsBeat) CommitBeat();

                // The next row's beat, if there is one, opens on live vitals
                // rather than on preTick -- this row's beat has already shown
                // the drop and its Snapshot IS live state, so re-using preTick
                // would make the next beat replay this one's damage.
                preTick = null;
            }

            if (report.RegenHealed > 0)
            {
                // THE SAME MECHANISM, WITH THE HEAL FLASH FALLING OUT OF IT
                // -- FlashOne already branches on IsHealing, so a regen tick
                // gets the green flash and the green number for the cost of
                // the boolean. Its ACTOR is the holder rather than nobody,
                // which is what keeps the recoil and the squash off it
                // (FightBeatPlayer.RecoilOne/Punch both skip a target that is
                // its own actor): a body does not flinch away from its own
                // mending.
                bool ownsBeat = BeginStatusTickBeat(
                    actor, preTick, StatusEffectType.Regen, report.RegenHealed, isHealing: true);

                AppendMessage($"{actor.Name} regenerates {report.RegenHealed} health.");
                Ledger.Restored(LedgerIdOf(actor), report.RegenHealed);

                if (ownsBeat) CommitBeat();
            }

            // DISTINCT, because statuses stack. Three poisons running out on
            // the same tick are three removals and one thing a player needs
            // told; saying it three times reads as a bug in the log rather
            // than as three stacks having lapsed together.
            foreach (var expired in report.Expired.Distinct())
            {
                AppendMessage($"{actor.Name}'s {expired} wears off.");
            }
        }

        // THE WORD A TICK'S OWN LOG LINE USES ("suffers N ___ damage!").
        // Poison keeps its exact original wording; the new DoTs read their
        // own type name lowercased, which is the same "no per-type table
        // beyond what a name already says" rule StatusHud.SlugFor uses for
        // its icon path.
        private static string TickVerb(StatusEffectType type) =>
            type == StatusEffectType.Poison ? "poison" : type.ToString().ToLowerInvariant();

        // PLAN 1.11'S POST-ACTION HOOK, PLACED WHERE A COMPLETED ACTION IS
        // ACTUALLY KNOWN ON BOTH SIDES OF THE FIGHT -- not at the top of
        // AdvanceAfterAction, which is the plan's own first draft and is
        // wrong for an enemy: AdvanceAfterAction is reached only by the four
        // PLAYER commands (see EndTurnStatusesForCurrent's own header for the
        // identical lesson D already paid for). Thorn Tithe is cast on
        // ENEMIES, so a hook that only ever fired for the player would never
        // retaliate at all. Called from two seams instead, one per side:
        // AdvanceAfterAction (this file) for the four player commands, and
        // FightSession.Enemies.AutoResolveEnemyTurns for a monster's own
        // action -- both know, at the point they call this, whether what just
        // happened was a completed PHYSICAL MOVE, which is 1.11's whole
        // trigger condition.
        //
        // SELF-INFLICTED. Thorned punishes its OWN HOLDER for moving, not
        // whoever they moved against -- "whenever it strikes or charges"
        // (2.12's tooltip) names the cursed actor, not their target -- so
        // this reads and damages `actor` itself.
        //
        // NOT AN ACTION. This method is called AFTER the action it reacts to
        // has already fully resolved and its own beat committed; it opens no
        // new one of its own (BeginStatusTickBeat), spends no resource,
        // starts no cooldown and never calls AdvanceAfterAction, CastSkill,
        // ExecuteAttack or Move -- so a retaliation that kills its holder
        // cannot recurse into itself, which is the structural guard 1.11
        // asks for rather than a re-entrancy flag.
        //
        // A REFUSED, FORFEITED OR FREE ACTION NEVER REACHES HERE AT ALL: a
        // refusal returns before either calling seam is reached (1.1), a
        // forfeited turn calls ForfeitTurn rather than an action path, and a
        // free action does not reach AdvanceAfterAction (FightSession.Skills
        // .cs). Nothing needs to check for those cases here because the
        // caller already could not have called this method for one.
        private void TriggerPhysicalMoveRetaliation(CombatantState actor)
        {
            if (actor == null || !actor.IsAlive) return;
            if (!_thornedAtTurnStart.TryGetValue(actor, out var instances) || instances.Count == 0) return;

            var preTick = SnapshotVitals();
            var row = StatusEffects.ApplyDotDamage(actor, StatusEffectType.Thorned, instances, AffinityOf(actor));
            if (!row.HasValue) return;

            int amount = row.Value.ToHealth + row.Value.Absorbed;
            if (amount <= 0) return;

            bool ownsBeat = BeginStatusTickBeat(actor, preTick, StatusEffectType.Thorned, amount, isHealing: false);

            AppendMessage($"{actor.Name}'s thorns lash back for {amount} damage!");

            if (row.Value.Absorbed > 0 && actor.SignaturePool != null)
            {
                AppendMessage(
                    $"{actor.Name}'s {actor.SignaturePool.DisplayName} soaks {row.Value.Absorbed} of it.");
            }

            RecordUnattributedDamage(actor, row.Value.ToHealth, row.Value.Absorbed);
            SettleDeath(actor: null, target: actor, credit: KillCredit.Nobody);

            if (ownsBeat) CommitBeat();
        }

        // A STATUS TICK THE STAGE CAN SEE, or false when something else already
        // owns the beat being recorded.
        //
        // WHY IT DOES NOT GO THROUGH BeginBeat: that method's last line is
        // NotePoolActivity(actor, PoolActivity.Action), the seam that tells a
        // decaying pool "this turn was not idle". A tick is not an action its
        // holder took -- it is something done TO them at the top of a turn
        // they have not spent yet -- and routing it through BeginBeat would
        // quietly stop wool decaying on any turn its owner happened to be
        // poisoned. Everything else a beat needs is CommitBeat's, which this
        // does call, so the snapshot, the turn order, the formation and the
        // hit-cue floor all stay in one place.
        //
        // ACTOR = NULL FOR DAMAGE. Nobody is credited for a tick (see
        // RecordUnattributedDamage), and a null actor is also what makes the
        // victim flinch: FightBeatPlayer skips the recoil and the squash for
        // a target that IS the actor, which is right for a self-heal and
        // wrong for a poison. Every other reader of beat.Actor in the view
        // already guards for null -- the stand-off (CrossesToATarget), the
        // stance phases, the spell placement and the voice lines -- so the
        // one that did not is the damage-type paint, which now asks first
        // (CombatBeat.PaintActorDamageType) and loses to the element declared
        // below.
        //
        // REFUSES TO NEST. _recordingBeat is a single slot, so opening a
        // second beat over an open one would drop the first entirely. No
        // caller does this today (a turn start is between actions), but the
        // failure would be an action silently vanishing from the fight, so it
        // degrades to the old behaviour -- log-only, lines attached to the
        // open beat -- rather than risking that.
        private bool BeginStatusTickBeat(CombatantState victim,
                                         Dictionary<CombatantState, Vitals> pre,
                                         StatusEffectType type, int amount, bool isHealing)
        {
            if (victim == null || amount <= 0 || _recordingBeat != null) return false;

            _recordingBeat = new CombatBeat
            {
                Actor = isHealing ? victim : null,
                Target = victim,
                PreSnapshot = pre ?? SnapshotVitals(),

                // Nothing crosses the stage for a tick: there is no attacker
                // to walk in, and Hold is how the vocabulary says so.
                Approach = StageApproach.Hold,
            };

            RecordBeatAmount(amount, isHealing);

            // THE ELEMENT COMES FROM THE STATUS, not from anyone's weapon --
            // StatusEffects.ElementOf is the one home for that question, and
            // a status that deals damage without declaring one keeps the
            // beat's default rather than inventing a colour.
            var element = StatusEffects.ElementOf(type);
            if (element.HasValue) _recordingBeat.DeclareDamageType(element.Value);

            // The hurt drawing, worn at the impact instant like any other
            // victim's (FightBeatPlayer.PoseVictims) and put back to idle
            // when the beat closes. A heal poses nobody: there is no
            // being-mended drawing, and wearing "hurt" for a regen tick would
            // say the opposite of what happened.
            if (!isHealing) SetStance(victim, Stances.Hurt);

            return true;
        }

        // ---- seams for tests -------------------------------------------------
        //
        // Same reasoning as FightSession.SpeedBuffs' own TickSpeedBuffsForTest:
        // a turn-start tick's observable effect (Chilled's malus reverting
        // exactly on expiry) is real production arithmetic, but driving it
        // through a full round trip means racing this ACTOR's own real turn
        // order against whatever else is in the fight. Calling the same
        // private method directly removes the race without touching what it
        // exercises.
        public void TickStatusesForTest(CombatantState actor) => TickStatuses(actor);

        // The other end of the same turn. Five statuses moved onto this clock
        // with plan D1, and Chilled's speed teardown moved with them, so a test
        // about a standing modifier expiring has to drive THIS rather than the
        // turn-start tick.
        public void TickStatusesAtTurnEndForTest(CombatantState actor) => TickStatusesAtTurnEnd(actor);

        // THE TURN BOUNDARY ITSELF. _statusesAppliedThisTurn is cleared once
        // per turn inside OpenTurnFor (TickLambTurnStart), which a test driving
        // the two tick seams directly never reaches -- so without this a status
        // the fixture applied stays exempt from every turn end forever and the
        // sweep looks broken when it is working exactly as written.
        public void CrossTurnBoundaryForTest() => _statusesAppliedThisTurn.Clear();

        // ApplyChilledForTest's sibling (FightSession.SpeedBuffs.cs): the ONE
        // status-application seam (plan D6), for a test that wants Burn or
        // Thorned's snapshot arithmetic (plan 1.5) without a full skill cast.
        // `source` is the caster whose SkillPowerMultiplier/SpellScaling the
        // snapshot folds in -- pass one to test the scaling, or null to pin
        // the "no caster, no potency" floor.
        public void ApplyStatusToForTest(CombatantState recipient, StatusEffectType type,
            int magnitude, int turns, CombatantState source = null) =>
            ApplyStatusTo(recipient, type, magnitude, turns, source);

        // Thorn Tithe's post-action hook (plan 1.11), driven directly for a
        // test that wants the retaliation's own arithmetic without playing a
        // whole physical action through CastSkill/ExecuteAttack.
        public void TriggerPhysicalMoveRetaliationForTest(CombatantState actor) =>
            TriggerPhysicalMoveRetaliation(actor);

    }
}
