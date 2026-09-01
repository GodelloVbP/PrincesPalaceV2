using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Combat
{
    // One fight: a fixed set of combatants on each side, ordered by the
    // existing generic TurnOrder<TActor> (initiative = Speed). Dead
    // combatants are never removed from the order — their turn is simply
    // skipped — so PlayerParty/Enemies stay stable lists a UI can keep
    // pointing at for the whole fight, including after it ends.
    public class CombatEncounter
    {
        private readonly TurnOrder<CombatantState> _turnOrder = new TurnOrder<CombatantState>();

        // A LIST, not the IEnumerable it started as, since the roster it
        // backs (Enemies) can now grow mid-fight — see TryAddEnemy. Never
        // shrinks: the same "dead combatants stay in the list, only their
        // turn is skipped" rule this class's own header already states for
        // the encounter as a whole.
        private readonly List<CombatantState> _enemies;

        public IReadOnlyList<CombatantState> PlayerParty { get; }
        public IReadOnlyList<CombatantState> Enemies => _enemies;

        public CombatEncounter(IEnumerable<CombatantState> playerParty, IEnumerable<CombatantState> enemies)
        {
            PlayerParty = playerParty.ToList();
            _enemies = enemies.ToList();

            if (PlayerParty.Count == 0 || _enemies.Count == 0)
            {
                throw new ArgumentException("A CombatEncounter needs at least one combatant on each side.");
            }

            foreach (var combatant in PlayerParty.Concat(_enemies))
            {
                // Speed is used TWICE, for two different jobs. As initiative
                // it decides who opens the fight and breaks exact ties. As a
                // charge rate it decides how OFTEN each combatant acts —
                // which is the thing a fixed rotation could never express,
                // where order varied but every combatant always got exactly
                // the same number of turns.
                _turnOrder.AddCombatant(combatant, combatant.Speed);
                _turnOrder.SetSpeed(combatant, combatant.Speed);
            }

            _turnOrder.Start();

            // The very first entry might already be a dead combatant only if
            // one was created pre-defeated, which never happens in practice —
            // guarded anyway so construction can't hand back a dead Current.
            SkipToNextLivingTurn();
        }

        public CombatantState Current => _turnOrder.Current;
        public bool IsPlayerTurn => Current.IsPlayerSide;

        public IEnumerable<CombatantState> LivingPlayerParty => PlayerParty.Where(c => c.IsAlive);
        public IEnumerable<CombatantState> LivingEnemies => Enemies.Where(c => c.IsAlive);

        // ---- the same two lists, RELATIVE TO WHOEVER IS ACTING -------------------
        //
        // "Every enemy" and "the whole party" are the player's words for them.
        // Skill resolution spelled them that way throughout, which was correct
        // while only the player could cast: DamageAll meant LivingEnemies and
        // HealParty meant LivingPlayerParty, full stop.
        //
        // A monster casting the same skill means the mirror image of both, and
        // hardcoding the sides is what made every enemy ability a scaled basic
        // attack -- there was no way to express "it heals its own side" or "it
        // hits your whole party" without writing a second resolution path.
        //
        // Asked of the ACTOR rather than passed a flag, so a caller cannot get
        // it backwards: there is no argument to swap.
        public IEnumerable<CombatantState> OpponentsOf(CombatantState actor) =>
            actor != null && actor.IsPlayerSide ? LivingEnemies : LivingPlayerParty;

        public IEnumerable<CombatantState> AlliesOf(CombatantState actor) =>
            actor != null && actor.IsPlayerSide ? LivingPlayerParty : LivingEnemies;

        // The frontmost living enemy — the lowest slot index still
        // standing, in the SAME order Enemies was built in and the stage
        // already draws front-to-back (see Domain.Stage.StageLayout, where
        // slot 0 is nearest the camera). Null once every enemy is down.
        //
        // No new field: rank is not state, it falls straight out of the
        // list order every enemy already had from the moment it was added.
        // Melee targeting reads THIS rather than a stored Rank, so a rank
        // never needs to be kept in sync with who is still alive — it just
        // is whoever is still standing nearest the front.
        public CombatantState FrontEnemy => Enemies.FirstOrDefault(e => e.IsAlive);

        // Can a melee attack reach this target? The front-rank rule, which in
        // v1 lived inline in FightController.Actions.cs as a condition wrapped
        // around a UI message. It is a combat rule, not a UI concern, so the
        // rule states itself here and the view asks.
        //
        // Anything that is not an enemy is reachable: this constrains reaching
        // PAST a living front rank, and says nothing about allies or self.
        public bool CanMeleeReach(CombatantState target)
        {
            if (target == null) return false;
            if (target.IsPlayerSide) return true;

            var front = FrontEnemy;
            return front == null || target == front;
        }

        // Adds a combatant to the ENEMY side mid-fight — a summon, so far
        // the only caller. Refuses past `maxSlots` (the stage has exactly
        // that many visual positions on a side; see FightHudSpec.
        // StageSlotsPerSide) rather than accepting a combatant the view has
        // nowhere to put, which would leave it in the encounter, fighting,
        // and permanently invisible.
        //
        // Joins the turn order at the BACK of the current schedule via the
        // same AddCombatant/SetSpeed pair the constructor uses for every
        // starting combatant — it does not act this round, only from the
        // next one its own Speed earns it a turn.
        public bool TryAddEnemy(CombatantState enemy, int maxSlots)
        {
            if (enemy == null || _enemies.Count >= maxSlots) return false;

            _enemies.Add(enemy);
            _turnOrder.AddCombatant(enemy, enemy.Speed);
            _turnOrder.SetSpeed(enemy, enemy.Speed);
            return true;
        }

        public bool IsOver => !LivingPlayerParty.Any() || !LivingEnemies.Any();
        public bool PlayerWon => IsOver && LivingEnemies.Any() == false && LivingPlayerParty.Any();

        // The next `count` turns, starting with whoever is acting right now,
        // for the initiative tracker to display.
        //
        // SIMULATED, not read off a list. This used to walk _turnOrder.Order
        // modulo its length, which assumed the upcoming sequence was a
        // rotation of the roster. Under charge scheduling that assumption is
        // exactly what stops being true: a fast combatant can legitimately
        // appear twice before a slow one appears once, so a rotation would
        // display a future the scheduler is not going to produce. Project()
        // runs the real charge contest forward on copies instead.
        //
        // Skips the defeated, matching AdvanceTurn's own rule — showing a
        // corpse as "up next" would be a lie the player then has to unlearn.
        // They still charge inside the simulation, because they still consume
        // their turn in the real schedule before being skipped.
        //
        // Returns fewer than `count` only when the encounter is already over,
        // which is the one case where "the next turn" genuinely does not
        // exist.
        public IReadOnlyList<CombatantState> UpcomingTurns(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Ask for at least one upcoming turn.");
            }

            if (IsOver)
            {
                return new List<CombatantState>();
            }

            return _turnOrder.Project(count, combatant => combatant.IsAlive);
        }

        // The same projection, previewing what the queue would look like if
        // `pushedActor` had already taken `slots` of push-back -- the hover
        // preview for a skill that carries QueuePushSlots. A thin pass-
        // through onto TurnOrder.ProjectPushed for the same reason
        // UpcomingTurns is one onto Project: nothing here mutates the real
        // schedule, and the queue is the only place that gets to say what a
        // push would actually do to it.
        public IReadOnlyList<CombatantState> UpcomingTurnsPushed(CombatantState pushedActor, int slots, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Ask for at least one upcoming turn.");
            }

            if (IsOver)
            {
                return new List<CombatantState>();
            }

            return _turnOrder.ProjectPushed(pushedActor, slots, count, combatant => combatant.IsAlive);
        }

        // Grants `actor` an immediate extra turn — the same actor is Current
        // again right after this one finishes, instead of the schedule moving
        // on. A thin pass-through onto TurnOrder.GrantExtraTurn: the queue is
        // the only correct place for this (see that method's own comment), so
        // CombatEncounter does not duplicate the bookkeeping, only exposes it.
        //
        // Returns false if `actor` is not (or no longer) in this encounter,
        // so a caller cannot silently grant a turn to someone already removed.
        public bool GrantExtraTurn(CombatantState actor)
        {
            return _turnOrder.GrantExtraTurn(actor);
        }

        // How many extra turns `actor` still has waiting. Grants stack, so
        // this is the only way to tell "one rider fired" from "two fired and
        // one has already been spent" — which is exactly what the
        // Trample/Bloodlust short-circuit turns on, and what a message-only
        // assertion could never distinguish.
        public int PendingExtraTurns(CombatantState actor)
        {
            return _turnOrder.PendingExtraTurns(actor);
        }

        // Knocks `actor` `slots` places later in the queue. Same thin
        // pass-through reasoning as GrantExtraTurn just above: the queue owns
        // its own scheduling, and the initiative tracker reads that queue, so
        // anything layered on top would desynchronise the two.
        public bool PushBack(CombatantState actor, int slots = 1)
        {
            return _turnOrder.PushBack(actor, slots);
        }

        // Hurries `actor` to the front of the queue — the Lamb's Gift: Haste,
        // and the mirror of PushBack above. Same thin pass-through reasoning.
        public bool PullToFront(CombatantState actor)
        {
            return _turnOrder.PullToFront(actor);
        }

        // Re-reads a combatant's Speed into the scheduler's charge rate.
        //
        // Speed was a construction-time constant until a transform could
        // change it mid-fight. Without this, Black Ram Mode's +30% Speed
        // would show on the character plate and change nothing whatsoever
        // about how often he acts — the failure would be entirely invisible,
        // which is the kind worth a named method rather than a comment at a
        // call site.
        public void RefreshSpeed(CombatantState combatant)
        {
            _turnOrder.SetSpeed(combatant, combatant.Speed);
        }

        // Moves to the next combatant whose turn it is, skipping anyone
        // already defeated. Call only when IsOver is false.
        public void AdvanceTurn()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("This encounter is already over.");
            }

            _turnOrder.Advance();
            SkipToNextLivingTurn();
        }

        private void SkipToNextLivingTurn()
        {
            while (!IsOver && !Current.IsAlive)
            {
                _turnOrder.Advance();
            }
        }
    }
}
