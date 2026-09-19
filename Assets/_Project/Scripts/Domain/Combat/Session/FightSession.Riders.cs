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

        private CombatantState _bloodlustChainActor;
        private int _bloodlustChainCount;
        private CombatantState _trampleChainActor;
        private int _trampleChainCount;

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

        // Everything after an action resolves: extra turns, then the schedule
        // moves on, then the next actor's turn-start bookkeeping.
        private void AdvanceAfterAction()
        {
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
            // talent nor the relic promises, and which stacks their two
            // independent caps into a four-attack chain. One kill, one extra
            // action, from whichever source is available.
            bool trampled = false;
            if (killedThisAction)
            {
                trampled = TryGrantTrample(_encounter.Current);
            }
            else
            {
                // An action that killed nothing ends the chain, which is what
                // makes the cap read as "per turn" rather than "per fight".
                // Cleared HERE rather than inside TryGrantTrample, because that
                // method is only reached on a kill, and a counter that only
                // resets on the path that increments it never resets at all.
                _trampleChainActor = null;
                _trampleChainCount = 0;
            }

            if (killedThisAction && !trampled)
            {
                RelicsOnKill(_encounter.Current);
            }
            else
            {
                _bloodlustChainActor = null;
                _bloodlustChainCount = 0;
            }

            // THE END OF THE TURN, and the one clock in the game that runs
            // here rather than at a turn's start -- see StatusEffects' own
            // WARDS header for why a ward is visible on the turn it protects
            // and every other duration is not.
            //
            // SKIPPED WHEN THE SAME ACTOR IS ABOUT TO ACT AGAIN, on exactly
            // the rule the line below already states: an extra action is the
            // SAME turn (AUDIT #113), so a trample chain must not age a ward
            // once per swing.
            if (_grantedExtraTurnTo == null || !ReferenceEquals(_grantedExtraTurnTo, _encounter.Current))
            {
                TickStatusesAtTurnEnd(_encounter.Current);
            }

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
            if (cap <= 0 || !actor.IsPlayerSide || !actor.IsAlive)
            {
                _trampleChainActor = null;
                _trampleChainCount = 0;
                return false;
            }

            int soFar = ReferenceEquals(actor, _trampleChainActor) ? _trampleChainCount : 0;
            if (soFar >= cap || !_encounter.GrantExtraTurn(actor))
            {
                return false;
            }

            _trampleChainActor = actor;
            _trampleChainCount = soFar + 1;
            _grantedExtraTurnTo = actor;
            AppendMessage($"{actor.Name} tramples straight over the body and keeps going!");
            return true;
        }

        // Bloodlust: killing an enemy earns the actor an extra turn on the
        // spot, capped so a good room cannot become an unbounded chain.
        private void TryGrantBloodlust(CombatantState actor)
        {
            int chainSoFar = actor == _bloodlustChainActor ? _bloodlustChainCount : 0;

            if (actor.IsPlayerSide && actor.IsAlive
                && HasRelic(actor, RelicEffect.Bloodlust)
                && chainSoFar < FightTuning.MaxBloodlustChain
                && _encounter.GrantExtraTurn(actor))
            {
                _bloodlustChainActor = actor;
                _bloodlustChainCount = chainSoFar + 1;
                _grantedExtraTurnTo = actor;
                AppendMessage($"{actor.Name}'s Bloodlust surges - one more turn!");
                return;
            }

            _bloodlustChainActor = null;
            _bloodlustChainCount = 0;
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

            if (type == StatusEffectType.Chilled)
            {
                ApplyChilled(recipient, magnitude, turns, source);
                return;
            }

            RecordStatus(recipient, type, magnitude, turns, source);
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

            var report = StatusEffects.Tick(actor);

            // CHILLED'S TEARDOWN IS NOT HERE ANY MORE. It moved to
            // TickStatusesAtTurnEnd with the clock (plan D1): Chilled is an
            // AtTurnEnd status now, so StatusEffects.Tick never reports it
            // expired and a revoke here would be dead code waiting to be read
            // as coverage. The teardown follows whichever clock removed the
            // entry -- that is the general rule, and this is the one status
            // that currently has a teardown at all.
            if (report.IsEmpty) return;

            // GATED ON THE WHOLE TICK, not on the part that reached health. A
            // signature pool spends itself before health does, so a tick a full
            // Wool pool eats outright leaves PoisonDamage at zero -- and
            // reading that as "nothing happened" is how five points of armour
            // got spent with no line, no ledger row and no word to the pools.
            if (report.PoisonDamage > 0 || report.PoisonAbsorbed > 0)
            {
                // THE TICK BECOMES A BEAT, so the stage plays it the way it
                // plays every other blow: the hurt pose, the recoil, the
                // flash tinted with the status's own element, the number.
                // Owner 2026-09-19 -- "poison damage or DoTs are not clear".
                // Until now a tick was log-only, because it never opened one.
                //
                // OPENED BEFORE THE LINES BELOW, so they land ON it rather
                // than being retro-attached to whatever beat happened to be
                // last (AppendMessage's own fallback). That is also what
                // moves the poison line from the end of the PREVIOUS blow to
                // the moment the tick is shown.
                bool ownsBeat = BeginStatusTickBeat(
                    actor, preTick, StatusEffectType.Poison,
                    report.PoisonDamage + report.PoisonAbsorbed, isHealing: false);

                // THE MAGNITUDE, THEN WHAT ATE IT -- the shape the enemy swing
                // already uses ("attacks X for N damage!" followed by "X's Wool
                // soaks M of it."). Printing the health figure instead would
                // announce "suffers 0 poison damage!" for a tick the armour
                // stopped, and leave the soak line with no antecedent for
                // "it".
                AppendMessage(
                    $"{actor.Name} suffers {report.PoisonDamage + report.PoisonAbsorbed} poison damage!");

                if (report.PoisonAbsorbed > 0 && actor.SignaturePool != null)
                {
                    AppendMessage(
                        $"{actor.Name}'s {actor.SignaturePool.DisplayName} soaks {report.PoisonAbsorbed} of it.");
                }

                // Counted as TAKEN and credited to nobody. The poison was
                // applied turns ago by someone who may now be dead, and
                // back-crediting it would put points in a column the player
                // cannot account for against any blow they watched land.
                //
                // Both halves handed over separately, exactly as the funnel's
                // own Ledger.Took call does: what a pool ate was never taken by
                // health, and folding the two into one number would double-count
                // every absorbed point.
                RecordUnattributedDamage(actor, report.PoisonDamage, report.PoisonAbsorbed);

                // And if the tick killed, that death is settled with the same
                // KillCredit.Nobody the comment above argues for -- WRITTEN
                // DOWN rather than left as a missing call. This is the one
                // deliberate exception to "a death raises the rider flag and
                // takes a kill row", and an exception spelled as an absence is
                // indistinguishable from the bug SettleDeath exists to kill.
                // The call is a no-op on the Nobody branch by design; what it
                // buys is that `grep SettleDeath` finds every death decision
                // in the file family, this one included.
                SettleDeath(actor: null, target: actor, credit: KillCredit.Nobody);

                // COMMITTED AFTER THE DEATH IS SETTLED, and that ordering is
                // the point: SettleDeath records no beat of its own, so the
                // tick's beat is the only thing that can show the kill --
                // CommitBeat's snapshot is what FightController.FadeTheFallen
                // reads to fade a body, exactly as it does for a killing
                // swing.
                if (ownsBeat) CommitBeat();

                // The regen beat below, if there is one, opens on live vitals
                // rather than on preTick -- the poison beat has already shown
                // the drop and its Snapshot IS live state, so re-using
                // preTick would make the second beat replay the first one's
                // damage.
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

    }
}
