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

        // A LIST for the same reason _enemies is one, plus a second: the
        // party's list ORDER is its field formation now, and Move reorders
        // it in place (SwapPartySlots). Never grows or shrinks -- a Move
        // swaps two slots and nothing else on this side ever adds or removes.
        private readonly List<CombatantState> _party;

        public IReadOnlyList<CombatantState> PlayerParty => _party;
        public IReadOnlyList<CombatantState> Enemies => _enemies;

        public CombatEncounter(IEnumerable<CombatantState> playerParty, IEnumerable<CombatantState> enemies)
        {
            _party = playerParty.ToList();
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

        // The party's own front rank -- the mirror of FrontEnemy, and what
        // the enemy side's melee lands on now that the front-rank rule runs
        // both ways. Null once the whole party is down.
        public CombatantState FrontPartyMember => PlayerParty.FirstOrDefault(c => c.IsAlive);

        // WHERE THIS COMBATANT STANDS, counted among the LIVING on its own
        // side, from the front, in list order. -1 for null, for the dead, and
        // for anyone not in this encounter at all -- one sentinel, because
        // every caller does the same thing with all three answers.
        //
        // COMPUTED, NEVER STORED, and that is the whole design: death
        // compresses the ranks behind the corpse with no bookkeeping to keep
        // in sync, and the list order itself only ever changes through
        // SwapPartySlots. A stored rank is a second copy of a fact the list
        // already holds.
        public int LivingRankOf(CombatantState combatant)
        {
            if (combatant == null || !combatant.IsAlive) return -1;

            var side = combatant.IsPlayerSide ? _party : _enemies;

            int rank = 0;
            for (int i = 0; i < side.Count; i++)
            {
                if (ReferenceEquals(side[i], combatant)) return rank;
                if (side[i].IsAlive) rank++;
            }

            return -1;
        }

        // Two party members trade places on the field. The ONE thing that
        // reorders the party list.
        //
        // NEVER TOUCHES _turnOrder, deliberately. Field position and turn
        // order are two different things that both used to be called
        // "position": a Move changes where you stand, not when you act, and
        // wiring it into the schedule would make stepping back also cost (or
        // gain) initiative, which nothing in the design says it should.
        public bool SwapPartySlots(int a, int b)
        {
            if (a == b) return false;
            if (a < 0 || b < 0 || a >= _party.Count || b >= _party.Count) return false;

            var held = _party[a];
            _party[a] = _party[b];
            _party[b] = held;
            return true;
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

        // The ally-side twin of UpcomingTurnsPushed: what the tracker would
        // read if `pulledActor` were advanced. Nothing here commits anything
        // either -- TurnOrder.ProjectPulled runs on a Snapshot (plan 1.13).
        public IReadOnlyList<CombatantState> UpcomingTurnsPulled(CombatantState pulledActor, int slots, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Ask for at least one upcoming turn.");
            }

            if (IsOver)
            {
                return new List<CombatantState>();
            }

            return _turnOrder.ProjectPulled(pulledActor, slots, count, combatant => combatant.IsAlive);
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

        // Several actors delayed by one cast, every destination measured
        // against the board as it stood before any of them moved -- Gale
        // Scythe (plan 1.9 rule 1). Same thin pass-through reasoning as
        // PushBack above: the queue owns the rule, this only exposes it.
        //
        // Returns how many actually moved, so a caller can say "and each one
        // it hit loses a place" only about the ones that did.
        public int PushBackAll(IReadOnlyList<CombatantState> actors, int slots = 1)
        {
            return _turnOrder.PushBackAll(actors, slots);
        }

        // One actor advanced `slots` places -- Borrowed Moment, and the
        // mirror of PushBack. Distinct from PullToFront, which is the Lamb's
        // "act next" and jumps every level at once.
        public bool PullForward(CombatantState actor, int slots)
        {
            return _turnOrder.PullForward(actor, slots);
        }

        // WHERE A COMBATANT SITS IN THE FORECAST THE TRACKER DRAWS, counting
        // the living only, with index 0 the actor acting right now.
        //
        // THE SAME FILTER UpcomingTurns USES, because this answers a question
        // about the list the player is looking at: an advance is refused for
        // an ally who is already the next NAME ON THE TRACKER, and a corpse
        // the tracker never draws must not be the thing standing between
        // them and position 1.
        public int ForecastPositionOf(CombatantState combatant, int window = 0)
        {
            if (combatant == null || IsOver) return -1;

            return _turnOrder.ForecastPositionOf(combatant, window > 0 ? window : ForecastWindow,
                c => c.IsAlive);
        }

        // WHAT A COMBATANT'S CHARGE IS RIGHT NOW, read-only, straight off the
        // queue.
        //
        // THE UNIT THE DISPLACEMENT CONTRACT IS WRITTEN IN. Every one of
        // 1.9's worked examples states its answer as a charge, and at the
        // start of an encounter a charge is seeded from Speed -- so every
        // combatant sits well under the threshold and one displacement level
        // is a few points, which is real but reorders nobody yet. A test that
        // could only read the ORDER would therefore be blind to a delay that
        // landed on the wrong enemy, or on none.
        public float ChargeOf(CombatantState combatant) => _turnOrder.ChargeOf(combatant);

        // HOW FAR AHEAD THE SCHEDULE HAS TO BE SIMULATED before EVERY living
        // combatant has appeared at least once.
        //
        // WHY IT IS A MULTIPLE AND NOT THE ROSTER SIZE. Charge rates are
        // clamped to [0.35, 2.5] (SpeedScale), so the fastest thing on the
        // field earns at most 2.5/0.35 -- a little over seven -- turns for
        // every one the slowest earns. Eight per combatant plus a couple of
        // turns of slack is therefore an upper bound rather than a guess, and
        // a window that fell short would report "not in the order" for a very
        // slow ally and silently refuse an advance that is perfectly legal.
        //
        // NOT the tracker's own length: the tracker shows what fits on
        // screen, and this is a question about the schedule.
        public int ForecastWindow => System.Math.Max(8, (_party.Count + _enemies.Count) * 8 + 2);

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
