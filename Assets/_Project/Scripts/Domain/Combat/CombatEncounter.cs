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
        // party's list ORDER is its field formation, and a placement reorders
        // it in place (PlaceAt). Never grows or shrinks. Always exactly the
        // non-null entries of _field, in _field's order -- SyncPartyFromField
        // is the one writer.
        private readonly List<CombatantState> _party;

        // THE PARTY'S SIDE OF THE FIELD, holes included (PLAN_BELLWETHER_KIT
        // 1.1/3.1). Every party member, living or dead, plus a null for each
        // EMPTY SEAT that has somebody standing behind it. A seat is counted
        // off this list: every null and every LIVING member takes the next
        // seat, a corpse takes none.
        //
        // WHY HOLES ARE ENTRIES AND CORPSES ARE SKIPPED. That one counting
        // rule gives both halves of 1.1 with no death hook anywhere: a member
        // falling stops counting, so everyone behind moves forward one seat
        // ("the line closes up"), while an empty seat a Move opened keeps
        // counting until somebody steps into it. Trailing holes are never
        // stored -- a seat past the last entry is simply empty.
        //
        // Seats are STORED (in the position of the holes), ranks are still
        // COMPUTED (LivingRankOf, unchanged). With no hole in front of anyone
        // the two are the same number, which is every full-party fight.
        private readonly List<CombatantState> _field;

        // How many seats the party side has, open or empty. Fixed at three
        // (PartySeat.Count), the stage's own slot count.
        public const int SeatsPerSide = PrincesPalace.Domain.Party.PartySeat.Count;

        public IReadOnlyList<CombatantState> PlayerParty => _party;
        public IReadOnlyList<CombatantState> Enemies => _enemies;

        // The field as FieldSeating reads it: members in formation order with
        // a null per stored empty seat. For the view's per-beat copy
        // (BeatFormation) and for tests; rules ask SeatOf.
        public IReadOnlyList<CombatantState> PartyField => _field;

        public CombatEncounter(IEnumerable<CombatantState> playerParty, IEnumerable<CombatantState> enemies)
        {
            _party = playerParty.ToList();
            _field = new List<CombatantState>(_party);
            _enemies = enemies.ToList();

            if (_party.Count == 0 || _enemies.Count == 0)
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
        // PlaceAt. A stored rank is a second copy of a fact the list already
        // holds. RANK IS NOT SEAT: an empty seat in front of a member counts
        // for SeatOf and not here, which is what keeps a lone character
        // reachable by melee wherever he stands (PLAN_BELLWETHER_KIT 1.1).
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

        // ---- field seats (PLAN_BELLWETHER_KIT 1.1 / 3.1) ---------------------------
        //
        // WHICH SEAT THIS COMBATANT STANDS IN, 0 = front, 1 = middle, 2 = rear
        // (Party.PartySeat). -1 for null, the dead and the absent, the same
        // sentinel LivingRankOf uses.
        //
        // THE PARTY'S IS STORED, AN ENEMY'S IS ITS RANK. Enemies never move,
        // so an enemy's seat is its living rank and nothing new is kept for
        // them. A party member's seat can differ from its rank only when an
        // empty seat stands in front of it -- a lone Shawn who stepped back
        // is in seat 1 or 2 and still rank 0.
        //
        // WHAT READS WHICH. Close-range reach, Provoke and "the front rank"
        // read LivingRankOf (a lone character is always reachable wherever he
        // stands). Seats decide placement on the stage, Move, Palace Passage,
        // Reposition and damage-by-seat effects.
        public int SeatOf(CombatantState combatant)
        {
            if (combatant == null || !combatant.IsAlive) return -1;
            if (!combatant.IsPlayerSide) return LivingRankOf(combatant);

            return FieldSeating.SeatIn(_field, combatant, IsAliveHolder);
        }

        // The living party member in `seat`, or null when it is empty (or out
        // of range).
        public CombatantState OccupantOf(int seat)
        {
            if (seat < 0 || seat >= SeatsPerSide) return null;

            foreach (var member in _party)
            {
                if (SeatOf(member) == seat) return member;
            }

            return null;
        }

        // CAN `member` BE PLACED IN `seat` RIGHT NOW? The one legality rule for
        // every change of field position -- Move, Palace Passage and
        // Reposition all ask this, so none of them can disagree about Rooted
        // or about what an empty seat allows. `occupant` is who would be
        // traded with, null for an empty seat.
        //
        // ROOTED IS READ OFF BOTH ENDS: Rooted means "cannot change field
        // position", and a trade changes two. Member first, so a caller that
        // reports the refusal names the same figure Move always named.
        public PlaceOutcome CanPlaceAt(CombatantState member, int seat, out CombatantState occupant)
        {
            occupant = null;

            int from = member != null && member.IsPlayerSide ? SeatOf(member) : -1;
            if (from < 0) return PlaceOutcome.NotOnTheField;
            if (seat < 0 || seat >= SeatsPerSide || seat == from) return PlaceOutcome.NoSuchSeat;

            if (StatusEffects.HasRooted(member.Statuses)) return PlaceOutcome.MemberRooted;

            occupant = OccupantOf(seat);
            if (occupant != null && StatusEffects.HasRooted(occupant.Statuses)) return PlaceOutcome.OccupantRooted;

            return PlaceOutcome.Placed;
        }

        // PUTS `member` IN `seat`: trades with whoever stands there, or steps
        // into it when it is empty. The ONE writer of field position. Refuses
        // (and changes nothing) for exactly what CanPlaceAt refuses.
        //
        // NEVER TOUCHES _turnOrder, deliberately. Field position and turn
        // order are two different things that both used to be called
        // "position": a Move changes where you stand, not when you act, and
        // wiring it into the schedule would make stepping back also cost (or
        // gain) initiative, which nothing in the design says it should.
        //
        // A CORPSE KEEPS ITS LIST ENTRY. The two field entries swap (member
        // and occupant, or member and the hole), so a dead member's place in
        // PlayerParty never moves -- what MoveCommandTests.CorpsesAreStepped
        // OverAndKeepTheirListSlot has always pinned.
        public PlaceOutcome PlaceAt(CombatantState member, int seat, out CombatantState occupant)
        {
            var outcome = CanPlaceAt(member, seat, out occupant);
            if (outcome != PlaceOutcome.Placed) return outcome;

            DropExcessHoles();

            int from = _field.IndexOf(member);
            int to = FieldIndexOfSeat(seat);

            _field[from] = _field[to];
            _field[to] = member;

            TrimTrailingHoles();
            SyncPartyFromField();
            return PlaceOutcome.Placed;
        }

        // Two party members trade field entries by PlayerParty index, with no
        // rule applied -- the raw mechanism under PlaceAt's trade, kept public
        // for fixtures that need to rewrite the formation directly. Game code
        // changes position through PlaceAt only.
        public bool SwapPartySlots(int a, int b)
        {
            if (a == b) return false;
            if (a < 0 || b < 0 || a >= _party.Count || b >= _party.Count) return false;

            int fa = _field.IndexOf(_party[a]);
            int fb = _field.IndexOf(_party[b]);

            var held = _field[fa];
            _field[fa] = _field[fb];
            _field[fb] = held;

            SyncPartyFromField();
            return true;
        }

        private static readonly Func<CombatantState, bool> IsAliveHolder = c => c.IsAlive;

        // The _field index that seat `seat` is counted at, appending holes
        // when the seat lies past the last stored entry (an implicit empty
        // seat becomes an explicit one).
        private int FieldIndexOfSeat(int seat)
        {
            while (true)
            {
                int counted = 0;
                for (int i = 0; i < _field.Count; i++)
                {
                    var entry = _field[i];
                    if (entry != null && !entry.IsAlive) continue;
                    if (counted == seat) return i;
                    counted++;
                }

                _field.Add(null);
            }
        }

        // A revival (Second Life) can bring back a member whose seat a hole
        // was also holding, leaving more holders than seats. The frontmost
        // surplus holes go -- the same ones FieldSeating skips when counting,
        // so this only makes the stored list agree with what was read.
        private void DropExcessHoles()
        {
            int excess = FieldSeating.ExcessHoles(_field, IsAliveHolder);
            for (int i = 0; i < _field.Count && excess > 0;)
            {
                if (_field[i] == null) { _field.RemoveAt(i); excess--; }
                else i++;
            }
        }

        private void TrimTrailingHoles()
        {
            while (_field.Count > 0 && _field[_field.Count - 1] == null) _field.RemoveAt(_field.Count - 1);
        }

        // In place, never a new list: PlayerParty hands out _party itself and
        // callers hold it across a whole fight.
        private void SyncPartyFromField()
        {
            _party.Clear();
            foreach (var entry in _field)
            {
                if (entry != null) _party.Add(entry);
            }
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

        // WHICH ROUND THE SCHEDULE IS IN, 1 from Start. A round is how long a
        // baseline-speed combatant takes to earn one turn (TurnOrder's own
        // comment at the increment), so it can move by more than one between
        // two turns when everyone on the field is slow. FightSession fires its
        // round-start hook off this and catches up every round it skipped.
        public int Round => _turnOrder.Round;

        // THE FIGHT WAS OUTLASTED, not won by killing. Set once, by
        // EndBySurvival, and never cleared. It is the one way a fight can be
        // over with both sides still standing, which is why IsOver and
        // PlayerWon read it rather than leaving the caller to remember it.
        public bool EndedBySurvival { get; private set; }

        // Ends the fight as a win for the party with enemies still standing --
        // an event fight's round limit (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
        // 3.2). Refused (false) once the fight is already over, so a kill or a
        // wipe that landed first keeps its own ending.
        public bool EndBySurvival()
        {
            if (IsOver) return false;

            EndedBySurvival = true;
            return true;
        }

        public bool IsOver => EndedBySurvival || !LivingPlayerParty.Any() || !LivingEnemies.Any();

        // A SURVIVED FIGHT IS A WIN, with a living enemy on the field. Code
        // downstream that needs "every enemy is dead" must ask LivingEnemies,
        // not this (risk R3 in that plan).
        public bool PlayerWon => IsOver && LivingPlayerParty.Any() && (EndedBySurvival || !LivingEnemies.Any());

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
