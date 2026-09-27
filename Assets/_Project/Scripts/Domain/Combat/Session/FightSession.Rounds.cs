using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // How a fight ended, for a caller that has to settle it differently per
    // ending (an event fight's onDefeated / onSurvived / onFell,
    // docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.1).
    public enum FightEndReason
    {
        // Still running -- including a fight a second life just refused to end.
        None,

        // Every enemy is down and the party is not: the ordinary win.
        Defeated,

        // The round limit ran out with the party standing: a win with enemies
        // still on the field. Only ever with RoundLimit > 0.
        Survived,

        // The party is down.
        Fell,
    }

    // ROUNDS, the round limit, the enemy rally and Toll of the Flock
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.2 and 1.4).
    //
    // TurnOrder has always counted rounds; nothing read them. A round here is
    // the scheduler's -- the time a baseline-speed combatant takes to earn a
    // turn -- so it can advance by more than one between two turns, and every
    // round in between still starts, in order.
    //
    // THE ORDER AT A ROUND START is the contract (plan 2.1):
    //   (a) past the limit -> the fight ends Survived, and nothing below fires;
    //   (b) enemy round effects (the rally);
    //   (c) then, back in GrantTurnStart / AdvanceAfterAction, the next actor's
    //       turn opens.
    public sealed partial class FightSession
    {
        // ---- the interface an event fight uses (M2) ------------------------------

        // THE ROUND LIMIT. 0 means none, which is every room fight. Set by the
        // caller BEFORE Begin(), the same shape as SecondLifeCharges and
        // RunWideBonusDamagePercent: the session never reads a run or an event.
        // With a limit of N the fight ends Survived as round N+1 would start,
        // so the party has to live through all N rounds; a kill in round N is
        // still Defeated, because the limit is only checked at a round start.
        public int RoundLimit { get; set; }

        // The round the fight is in, 1 from Begin. Read-only; the HUD's round
        // counter reads this.
        public int Round => _encounter.Round;

        // HOW THE FIGHT ENDED. None while it runs. Computed from the encounter
        // rather than stored, so a second life that refuses the ending leaves
        // it None without anything having to un-set it.
        public FightEndReason EndReason
        {
            get
            {
                if (!_encounter.IsOver) return FightEndReason.None;
                if (_encounter.EndedBySurvival) return FightEndReason.Survived;
                return _encounter.PlayerWon ? FightEndReason.Defeated : FightEndReason.Fell;
            }
        }

        // Raised as each round starts, after the limit check passed and after
        // the enemy round effects, before the next turn opens. RESOLUTION
        // TIME, not presentation: the whole round resolves in one pass before
        // the view draws a frame, so a screen that wants to show a toll reads
        // the beats, not this. For Domain listeners and tests.
        public event Action<int> RoundStarted;

        // ---- the hook ------------------------------------------------------------

        private int _lastRoundStarted;

        // Starts every round the schedule has reached and this session has not
        // started yet, oldest first. Stops the moment one of them ends the fight.
        private void StartPendingRounds()
        {
            while (!_encounter.IsOver && _lastRoundStarted < _encounter.Round)
            {
                _lastRoundStarted++;
                OnRoundStarted(_lastRoundStarted);
            }
        }

        private void OnRoundStarted(int round)
        {
            if (RoundLimit > 0 && round > RoundLimit)
            {
                _encounter.EndBySurvival();
                return;
            }

            RallyEnemies();
            RecordRoundStart(round);

            RoundStarted?.Invoke(round);
        }

        // THE TOLL'S OWN BEAT, for the screen: RoundStarted above fires at
        // resolution time, a whole round before the picture gets there, so the
        // counter, the toll's sound and the overlay's step ride a beat and play
        // when playback reaches it (CombatBeat.RoundStarted).
        //
        // ONLY WITH A ROUND LIMIT. The counter is a round-limit HUD; a room
        // fight shows no rounds, and recording a beat per round there would
        // lengthen every room fight's playback for a picture nobody draws.
        //
        // No nesting: a round that starts while a beat is open (a turn
        // advance inside a recording) is stamped onto that beat rather than
        // replacing it -- the refusal to nest the flock and the status tick
        // make. After the rally, so the beat's snapshot is the round's.
        private void RecordRoundStart(int round)
        {
            if (RoundLimit <= 0) return;

            if (_recordingBeat != null)
            {
                _recordingBeat.RoundStarted = round;
                return;
            }

            NewBeat(BeatCause.RoundStart, null, null, StageApproach.Hold);
            _recordingBeat.RoundStarted = round;
            CommitBeat();
        }

        // ---- the rally -------------------------------------------------------------

        // Every living enemy with an authored rallyPerRound gains one fight-long
        // stack, up to its cap, and its attack bonus is re-summed on the spot so
        // the telegraph's damage preview reads the new figure too, not only the
        // swing (which re-sums anyway, FightSession.Enemies.ResolveEnemyAction).
        private void RallyEnemies()
        {
            foreach (var enemy in _encounter.LivingEnemies.ToList())
            {
                var source = SourceFor(enemy)?.Source;
                if (source == null || !source.HasRally) continue;

                FallingOffStacks.AddStack(enemy, FightTuning.RallyStackKey,
                    RallyStackLifetime, source.RallyMaxStacks);
                RefreshAttackBonus(enemy, spendingGift: false);
            }
        }

        // A rally stack is fight-long. FallingOffStacks ages every list once
        // per holder turn, so a lifetime no fight can use up is how "never
        // falls off" is spelled there. Not a FightTuning *Turns constant: it
        // is not a duration anyone tunes.
        private const int RallyStackLifetime = int.MaxValue;

        // What the rally adds to an actor's attack, in percent: its stacks times
        // its own authored per-stack figure. 0 for anything without a rally --
        // every player, and every shipped monster but the Bellwether.
        private int RallyAttackPercent(CombatantState actor)
        {
            int stacks = FallingOffStacks.Count(actor, FightTuning.RallyStackKey);
            if (stacks <= 0) return 0;

            var source = SourceFor(actor)?.Source;
            return source == null ? 0 : stacks * source.RallyAttackPercentPerStack;
        }

        // How many rally stacks an actor carries. For the HUD and tests.
        public int RallyStacksOf(CombatantState actor) => FallingOffStacks.Count(actor, FightTuning.RallyStackKey);

        // ---- turns opened ----------------------------------------------------------

        // Per fight, per combatant: how many of their turns have OPENED
        // (OpenTurnFor). An extra action inside a turn is not an opened turn.
        private readonly Dictionary<CombatantState, int> _openedTurns = new Dictionary<CombatantState, int>();

        public int OpenedTurnsOf(CombatantState actor) =>
            actor != null && _openedTurns.TryGetValue(actor, out int opened) ? opened : 0;

        // ---- Toll of the Flock -------------------------------------------------------

        public const string TollOfTheFlockLine = "The lost flock charges.";

        // Called as the last step of OpenTurnFor, with that turn already counted.
        // On the bearer's every TollOfTheFlockEvery-th opened turn, if they are
        // still standing: one packet to every living enemy, through the normal
        // defences, credited to nobody -- so a flock kill arms no Trample or
        // Bloodlust and no kill-keyed relic (plan 1.4, Attribution).
        //
        // NO VARIANCE AND NO DODGE (rng null): the relic is a fixed share of the
        // bearer's attack, and a turn start is no place to draw from the run's
        // generator -- the draw would shift every roll after it in the fight.
        private void TollOfTheFlock(CombatantState bearer)
        {
            if (bearer == null || !bearer.IsAlive) return;
            if (!HasRelic(bearer, RelicEffect.TollOfTheFlock)) return;

            int opened = OpenedTurnsOf(bearer);
            if (opened <= 0 || opened % FightTuning.TollOfTheFlockEvery != 0) return;

            var struck = _encounter.OpponentsOf(bearer).ToList();
            if (struck.Count == 0) return;

            int raw = TollOfTheFlockPacket(bearer);
            var type = AttackTypeOf(bearer);

            // Its own beat when nothing else is open, attached to the open one
            // otherwise -- the same refusal to nest the status-tick beat makes.
            bool ownBeat = _recordingBeat == null;
            if (ownBeat)
            {
                NewBeat(BeatCause.RelicTrigger, bearer, struck[0], StageApproach.Hold);
                RecordSpellPresentation(RelicPresentation(bearer, RelicEffect.TollOfTheFlock));
                RecordSplashTargets(struck.Skip(1));
            }

            AppendMessage(TollOfTheFlockLine);

            int largest = 0;
            foreach (var enemy in struck)
            {
                if (!enemy.IsAlive) continue;

                var outcome = DamagePipeline.AfterDefences(
                    raw, bearer, enemy,
                    attackType: type,
                    affinity: AffinityOf(enemy),
                    varianceRange: 0f, rng: null,
                    resolveWard: ResolveWard);

                int landed = outcome.Damage;
                DealDamage(bearer, enemy, landed, type, KillCredit.Nobody);
                SetStance(enemy, enemy.IsAlive ? Stances.Hurt : Stances.Defeated);
                RecordTargetResult(enemy, landed);

                largest = Math.Max(largest, landed);
                if (ownBeat) RecordBeatAmount(largest);

                if (!enemy.IsAlive)
                {
                    AppendMessage($"{enemy.Name} is defeated!");
                }
            }

            if (ownBeat) CommitBeat();
        }

        // The packet before defences: TollOfTheFlockAttackPercent % of the
        // bearer's CURRENT Attack -- a transform's own attack bonus (Black Ram's
        // +50%) is already in it -- times the transform multiplier while a form
        // is worn at this instant. Floored at 1.
        private static int TollOfTheFlockPacket(CombatantState bearer)
        {
            float multiplier = bearer.Transformation != null ? FightTuning.TollOfTheFlockTransformMultiplier : 1f;
            float raw = bearer.Attack * FightTuning.TollOfTheFlockAttackPercent / 100f * multiplier;
            return Math.Max(1, Rounding.AwayFromZero(raw));
        }

        // The bearer's own copy of a relic's authored presentation, or None.
        private SpellPresentation RelicPresentation(CombatantState bearer, RelicEffect effect)
        {
            var kit = KitFor(bearer);
            if (kit == null) return SpellPresentation.None;

            foreach (var relic in kit.Relics)
            {
                if (relic != null && relic.Effect == effect && relic.ReachesCharacter(kit.Id))
                {
                    return relic.Vfx ?? SpellPresentation.None;
                }
            }

            return SpellPresentation.None;
        }
    }
}
