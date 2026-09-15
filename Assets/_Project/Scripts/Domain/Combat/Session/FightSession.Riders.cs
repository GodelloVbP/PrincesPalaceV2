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
        // REFRESHES rather than stacks -- StatusEffects.Apply's own rule,
        // the same one every other repeatable buff in this game already
        // relies on, so recasting (re-triggering, here) never compounds.
        // 99 turns is "for the rest of the fight" spelled as a duration, the
        // same convention the Magical Shield relic already uses
        // (FightTuning.MagicalShieldDurationTurns) -- ordinary turn-start
        // ticking must never expire this before ConsumeWard spends it.
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

            int wardPercent = Math.Min(FightTuning.RunicWardMagnitudeCapPercent,
                Rounding.AwayFromZero(actor.CurrentMana * FightTuning.RunicWardConversionRate));
            if (wardPercent <= 0) return;

            StatusEffects.Apply(actor.Statuses, StatusEffectType.Shielded, wardPercent,
                FightTuning.MagicalShieldDurationTurns, actor);
            AppendMessage($"{actor.Name}'s runes catch the leftover mana as a ward.");
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

            var report = StatusEffects.Tick(actor);

            // Chilled's malus is booked in FightSession.SpeedBuffs' own
            // dictionary, not on the status itself -- StatusEffects.Tick just
            // removed the EXPIRED ActiveStatus entry (generic per-status
            // countdown, no special case needed there), but nothing has told
            // Speed yet. Placed ahead of the report.IsEmpty early-return
            // below on purpose, even though Expired containing anything
            // already implies !IsEmpty -- keeping the Speed-honesty step
            // unconditional here means a future change to IsEmpty's own
            // definition can never silently start skipping it.
            if (report.Expired.Contains(StatusEffectType.Chilled))
            {
                RevokeSpeedBuff(actor, StatusEffectType.Chilled);
            }

            if (report.IsEmpty) return;

            // GATED ON THE WHOLE TICK, not on the part that reached health. A
            // signature pool spends itself before health does, so a tick a full
            // Wool pool eats outright leaves PoisonDamage at zero -- and
            // reading that as "nothing happened" is how five points of armour
            // got spent with no line, no ledger row and no word to the pools.
            if (report.PoisonDamage > 0 || report.PoisonAbsorbed > 0)
            {
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
            }

            if (report.RegenHealed > 0)
            {
                AppendMessage($"{actor.Name} regenerates {report.RegenHealed} health.");
                Ledger.Restored(LedgerIdOf(actor), report.RegenHealed);
            }

            foreach (var expired in report.Expired)
            {
                AppendMessage($"{actor.Name}'s {expired} wears off.");
            }
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

    }
}
