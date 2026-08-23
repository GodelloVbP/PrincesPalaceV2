using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

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
        private bool _killedThisAction;

        // Set when an action is eligible to cash in a banked turn. False for
        // the hold-back action's own resolution, so the action that JUST earned
        // a banked point cannot immediately spend it on itself.
        private bool _actionCanBrave;

        private CombatantState _bloodlustChainActor;
        private int _bloodlustChainCount;
        private CombatantState _trampleChainActor;
        private int _trampleChainCount;

        // Everything after an action resolves: extra turns, then the schedule
        // moves on, then the next actor's turn-start bookkeeping.
        private void AdvanceAfterAction()
        {
            // Read-then-reset up front, unconditionally, so a flag can never
            // leak into a fight that is ending right here or a turn that has
            // not happened yet -- whichever path below runs, it starts clean.
            bool canBrave = _actionCanBrave;
            _actionCanBrave = false;
            bool killedThisAction = _killedThisAction;
            _killedThisAction = false;

            // The action that killed the last enemy ends the fight right here,
            // before any rider could matter -- but the celebration still has to
            // happen, and this is the only path that reaches it.
            if (_encounter.IsOver)
            {
                ResolveVictory();
                ResolveOutcome();
                return;
            }

            TryGrantBrave(canBrave);

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
            GrantTurnStart();

            // Every enemy turn between this action and the player's next one
            // resolves right here, synchronously, before the view has drawn a
            // frame of any of it. The beat queue is what makes that safe.
            AutoResolveEnemyTurns();

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

        // Brave: cashing in a banked action for an immediate extra turn.
        //
        // Granted BEFORE AdvanceTurn -- GrantExtraTurn stacks onto whoever is
        // still Current, and nothing has moved the queue on yet, so the very
        // next advance hands the turn straight back rather than to whoever the
        // schedule says is next.
        private void TryGrantBrave(bool canBrave)
        {
            if (!canBrave) return;

            var actor = _encounter.Current;
            if (actor.IsPlayerSide && actor.IsAlive && actor.BankedActions > 0 && _encounter.GrantExtraTurn(actor))
            {
                actor.BankedActions--;
                AppendMessage($"{actor.Name} spends a banked action to act again!");
            }
        }

        // Trample T3: a kill does not consume the action. Same
        // GrantExtraTurn-before-AdvanceTurn timing as Brave, and its own cap
        // for the same reason Bloodlust has one.
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
            if (kit == null) return false;

            for (int i = 0; i < kit.Relics.Count; i++)
            {
                if (kit.Relics[i].Effect == effect) return true;
            }

            return false;
        }

        // The next actor's turn opens: mana regenerates, statuses tick, and a
        // signature resource fills by its per-turn allowance.
        private void GrantTurnStart()
        {
            if (_encounter == null || _encounter.IsOver) return;

            var actor = _encounter.Current;
            if (actor == null) return;

            RegenerateMana(actor);
            TickStatuses(actor);
            TickCooldowns(actor);

            // Every other duration ticks here too, so "a turn" means the same
            // thing for a ward payout cap, a grace period and a transform as it
            // does for a status.
            TickLambTurnStart(actor);
            TickTransform(actor);

            var signature = actor.Signature;
            if (signature == null) return;

            int owed = SignaturePerTurnFor(actor);
            if (owed <= 0) return;

            if (signature.Gain(owed) < owed)
            {
                AppendMessage($"{actor.Name}'s {signature.DisplayName} is as full as it will get.");
            }
        }

        private void RegenerateMana(CombatantState actor)
        {
            if (actor == null || actor.ManaRegen <= 0) return;
            CombatMath.RestoreMana(actor, actor.ManaRegen);
        }

        // StatusEffects.Tick applies the numbers and reports WHAT happened;
        // turning that into log lines is this layer's job, because the wording
        // belongs to the fight rather than to the status system.
        private void TickStatuses(CombatantState actor)
        {
            if (actor == null) return;

            var report = StatusEffects.Tick(actor);
            if (report.IsEmpty) return;

            if (report.PoisonDamage > 0)
            {
                AppendMessage($"{actor.Name} suffers {report.PoisonDamage} poison damage!");

                // Counted as TAKEN and credited to nobody. The poison was
                // applied turns ago by someone who may now be dead, and
                // back-crediting it would put points in a column the player
                // cannot account for against any blow they watched land.
                RecordUnattributedDamage(actor, report.PoisonDamage);
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

    }
}
