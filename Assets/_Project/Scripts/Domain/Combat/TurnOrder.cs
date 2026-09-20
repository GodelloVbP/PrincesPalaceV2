using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Combat
{
    // Generic, reusable initiative/turn-order manager. Not tied to any
    // concrete character type so it can be driven by plain test doubles as
    // easily as real gameplay actors.
    public sealed class TurnOrder<TActor>
    {
        private sealed class Entry
        {
            public TActor Actor;
            public int Initiative;

            // Charge-based scheduling. Everyone accumulates toward a shared
            // threshold at a rate derived from Speed; whoever crosses it
            // first acts. This is what lets a fast combatant genuinely take
            // an extra turn or overtake a slow one, which a fixed round-robin
            // over sorted initiative can never express — there, order could
            // differ but the COUNT of turns was always equal.
            public float Charge;
            public float Rate = 1f;
        }

        // Crossing this earns a turn. Arbitrary in itself; only the ratio
        // between it and the rates matters.
        private const float TurnThreshold = 100f;

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry _current;
        private int _round;
        private bool _started;

        public int Round => _round;
        public int Count => _entries.Count;

        public TActor Current
        {
            get
            {
                if (!_started || _current == null)
                {
                    throw new InvalidOperationException("Turn order has not started, or has no combatants.");
                }

                return _current.Actor;
            }
        }

        public IReadOnlyList<TActor> Order => _entries.Select(e => e.Actor).ToList();

        // Speed is the scheduling input. Kept separate from `initiative`
        // (which still breaks ties on the opening turn) so an existing caller
        // that only knows about initiative keeps working: rate defaults to
        // baseline, giving exactly the old equal-turns behaviour.
        public void SetSpeed(TActor actor, int speed)
        {
            var entry = _entries.FirstOrDefault(e => Equals(e.Actor, actor));
            if (entry != null)
            {
                entry.Rate = SpeedScale.TickRate(speed);
            }
        }

        public void AddCombatant(TActor actor, int initiative)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }


            if (_entries.Any(e => Equals(e.Actor, actor)))
            {
                throw new InvalidOperationException("This actor is already in the turn order.");
            }

            // Seeded from initiative for the same reason Start() does it: a
            // reinforcement arriving mid-fight should slot in by how fast it
            // is, not act dead last purely because it joined with an empty
            // gauge. Only matters once the fight is running — before Start()
            // the seeding there covers it.
            _entries.Add(new Entry
            {
                Actor = actor,
                Initiative = initiative,
                Charge = _started ? SeedCharge(initiative) : 0f,
            });
            StableSortDescendingByInitiative();
        }

        public void RemoveCombatant(TActor actor)
        {
            _extraTurns.Remove(actor);
            var entry = _entries.FirstOrDefault(e => Equals(e.Actor, actor));
            if (entry == null)
            {
                return;
            }

            bool wasCurrent = _current == entry;
            int index = _entries.IndexOf(entry);
            _entries.RemoveAt(index);

            if (!wasCurrent)
            {
                return;
            }

            _current = null;

            if (_entries.Count == 0)
            {
                return;
            }

            // The removed actor was current, so hand the turn to whoever
            // the schedule says is next rather than to a list position —
            // under charge scheduling, list order is not turn order.
            _current = index < _entries.Count ? _entries[index] : ChargeUntilNextReady();
        }

        public void Start()
        {
            // Seeded from initiative so the FIRST turn still goes to the
            // highest-initiative combatant, exactly as before. Small relative
            // to the threshold, so it decides the opening and then washes
            // out rather than permanently advantaging anyone.
            //
            // CAPPED BELOW THE THRESHOLD, which is what makes that last
            // sentence true. Uncapped, the seed is a head start measured in
            // the same units as a turn, so any combatant with Speed >= 100
            // started already over the line and took Speed/100 turns before
            // anyone else moved at all — at Speed 10000 that is a hundred
            // consecutive turns, and the opponent never acts. Speed is
            // already rewarded by Rate; the seed only decides who opens.
            foreach (var e in _entries)
            {
                e.Charge = SeedCharge(e.Initiative);
            }

            if (_entries.Count == 0)
            {
                throw new InvalidOperationException("Cannot start a turn order with no combatants.");
            }

            StableSortDescendingByInitiative();
            _round = 1;
            _started = true;

            // The opening actor must SPEND its charge like any other turn.
            // Handing the turn out without deducting left them fully charged,
            // so they immediately won the next contest too and acted twice in
            // a row before anyone else moved.
            _current = _entries[0];
            _current.Charge -= TurnThreshold;
        }

        public TActor Advance()
        {
            if (!_started)
            {
                throw new InvalidOperationException("Call Start() before Advance().");
            }

            if (_entries.Count == 0)
            {
                throw new InvalidOperationException("No combatants remain in the turn order.");
            }

            // An extra turn keeps the SAME actor up rather than moving on,
            // and consumes one grant. Checked before the queue advances so a
            // granted turn happens immediately after the one that earned it,
            // which is what makes it read as 'again' rather than 'sooner'.
            if (_current != null && _extraTurns.TryGetValue(_current.Actor, out int pending) && pending > 0)
            {
                if (pending == 1)
                {
                    _extraTurns.Remove(_current.Actor);
                }
                else
                {
                    _extraTurns[_current.Actor] = pending - 1;
                }

                return _current.Actor;
            }

            _current = ChargeUntilNextReady();
            return _current.Actor;
        }

        // Moves the current actor to the end of the remaining order (they
        // act last this round) and hands the turn to whoever is now next.
        // Gives up this turn: the actor surrenders a full turn's worth of
        // charge and the schedule hands off to whoever is ready next.
        //
        // Rewritten for charge scheduling. The old version moved the actor
        // to the end of the entry LIST, which under a rotation was the same
        // thing as going last — but list position no longer has anything to
        // do with turn order, so that version silently did nothing.
        public TActor DelayCurrent()
        {
            if (_current == null)
            {
                throw new InvalidOperationException("No current actor to delay.");
            }

            if (_entries.Count == 1)
            {
                return _current.Actor;
            }

            // Drop to just behind the least-charged other combatant, so the
            // delayer goes LAST among those already waiting — which is what
            // delaying means. Deducting a whole threshold instead would cost
            // a full turn, sending them behind the next lap rather than to
            // the back of this one.
            float lowest = float.MaxValue;
            foreach (var e in _entries)
            {
                if (!ReferenceEquals(e, _current) && e.Charge < lowest)
                {
                    lowest = e.Charge;
                }
            }

            _current.Charge = lowest - 1f;
            _current = ChargeUntilNextReady();
            return _current.Actor;
        }

        // Reports who acts next without consuming the current turn.
        // Grants an extra turn: the actor acts again immediately after its
        // current turn instead of the queue moving on. Built now rather than
        // when it is first needed, because the queue is the ONLY correct
        // place for it — anything layered on top (a flag the driver checks,
        // a re-entrant call) would desynchronise the initiative tracker,
        // which reads this queue to say who is up next.
        //
        // Stacks: granting twice means two extra turns. Returns false if the
        // actor is not in the order at all, so a caller cannot silently
        // grant a turn to something that has already been removed.
        public bool GrantExtraTurn(TActor actor)
        {
            int index = _entries.FindIndex(e => EqualityComparer<TActor>.Default.Equals(e.Actor, actor));
            if (index < 0)
            {
                return false;
            }

            _extraTurns.TryGetValue(actor, out int pending);
            _extraTurns[actor] = pending + 1;
            return true;
        }

        public int PendingExtraTurns(TActor actor)
        {
            return _extraTurns.TryGetValue(actor, out int pending) ? pending : 0;
        }

        // Knocks an actor `slots` places later in the queue — the Black Ram's
        // Headbutt, and its transform's party-wide shove.
        //
        // Expressed in SLOTS rather than in charge or in speed, which settles
        // an open question the design handoff could not (§6.3: "whether
        // that's a flat speed penalty or a queue-index shift depends on how
        // intents are ordered"). "One slot later" is the stated intended
        // feel, and it is the only phrasing that needs no calibration against
        // this scheduler's arbitrary units: a flat charge penalty means
        // something different to a fast combatant than to a slow one, and a
        // speed penalty changes how often they act forever rather than
        // shuffling this one turn.
        //
        // Each slot drops the actor to just under whoever is charged
        // immediately below them, which is the exact inverse of what
        // DelayCurrent already does for a voluntary delay — so the two read
        // as one mechanic seen from both ends. When nobody is below them, a
        // full turn's charge comes off instead, which is the honest meaning
        // of "later" for the combatant already going last.
        //
        // Returns false if the actor is not in the order, so a caller cannot
        // silently push something that has already been removed.
        //
        // ONE ARITY OF PushBackAll, not a second implementation of the same
        // rule: a single push IS a batch of one, and for a batch of one the
        // "every destination is fixed before the first one moves" rule below
        // is a no-op. Two arities, one path -- the same shape CastSkill uses
        // for its own one-target and two-target doors.
        public bool PushBack(TActor actor, int slots)
        {
            return PushBackAll(new[] { actor }, slots) > 0;
        }

        // A DELAY APPLIED TO SEVERAL ACTORS AT ONCE, with every destination
        // computed against the board as it stood BEFORE any of them moved
        // (docs/PLAN_SPELL_EXPANSION.md 1.9 rule 1) -- Gale Scythe's sweep,
        // which delays every enemy it actually hit.
        //
        // WHY NOT JUST LOOP PushBack. Applied one at a time, the second
        // target measures itself against the first target's NEW charge and
        // lands somewhere the player was never shown: two enemies a level
        // apart would concertina into each other instead of both dropping
        // one level. 1.9's own worked example (d) is exactly that board, and
        // its stated answer is the pre-pass one.
        //
        // Returns HOW MANY were actually displaced. An actor not in the order
        // is skipped rather than throwing -- the batch runs after deaths have
        // settled, and "this one is gone" is an ordinary outcome there, not
        // an error (1.9 rule 6).
        public int PushBackAll(IReadOnlyList<TActor> actors, int slots)
        {
            return DisplaceAll(actors, slots, ApplyPushBack);
        }

        // THE EXACT MIRROR OF PushBack, and Borrowed Moment's whole
        // mechanic. Each slot raises the actor to just above the least-
        // charged entry ABOVE them -- one charge LEVEL per slot, the same
        // unit a push drops by -- and a slot with nothing above it to pass
        // changes nothing, which is the clip.
        //
        // NOT PullToFront. That one is the Fragile Lamb's "act next" special
        // case: it jumps every level at once and has its own talent and its
        // own tests. An advance buys the number of places it says it buys
        // and can be refused for having none to buy (1.9 rule 5), which is a
        // different promise to the player.
        //
        // IT CANNOT BUY A TURN. The destination is always one point above an
        // existing charge, and every existing charge is below the threshold
        // (whoever crossed it is already acting), so an advance can never
        // itself put an entry over the line -- see PullToFront's own header
        // on why crossing it would be worth "a turn and a half" rather than
        // a position.
        public bool PullForward(TActor actor, int slots)
        {
            return DisplaceAll(new[] { actor }, slots, ApplyPullForward) > 0;
        }

        // The shared half of the two batch operations: resolve the actors,
        // compute each destination against ONE pre-pass copy of the board,
        // and only then write.
        //
        // A FRESH COPY PER TARGET, not one copy shared across the loop. The
        // rule reads the whole list on every slot, so a shared copy would
        // leak the first target's destination into the second's measurement
        // -- the very thing the pre-pass exists to prevent.
        //
        // THE CURRENT ACTOR IS NEVER A TARGET (1.9 rule 2). Index 0 of the
        // forecast is the action happening right now; moving its entry would
        // either replay it or drop it, and no caller in this game wants
        // either. Refused the same way a missing actor is, with a false
        // rather than a throw.
        private int DisplaceAll(IReadOnlyList<TActor> actors, int slots, Action<List<Entry>, Entry, int> rule)
        {
            if (actors == null || actors.Count == 0)
            {
                return 0;
            }

            var baseline = Snapshot();
            var destinations = new List<KeyValuePair<Entry, float>>(actors.Count);

            foreach (var actor in actors)
            {
                var entry = _entries.FirstOrDefault(e => EqualityComparer<TActor>.Default.Equals(e.Actor, actor));
                if (entry == null || (_current != null && ReferenceEquals(entry, _current)))
                {
                    continue;
                }

                var sim = baseline.Select(e => new Entry
                {
                    Actor = e.Actor,
                    Initiative = e.Initiative,
                    Charge = e.Charge,
                    Rate = e.Rate,
                }).ToList();

                var simEntry = sim.FirstOrDefault(e => EqualityComparer<TActor>.Default.Equals(e.Actor, actor));
                if (simEntry == null)
                {
                    continue;
                }

                rule(sim, simEntry, slots);
                destinations.Add(new KeyValuePair<Entry, float>(entry, simEntry.Charge));
            }

            foreach (var destination in destinations)
            {
                destination.Key.Charge = destination.Value;
            }

            return destinations.Count;
        }

        // The displacement PushBack applies to the real queue, factored out
        // so ProjectPushed can run the identical rule against a SIMULATED
        // copy -- see its own header. `entries` is whichever list `entry`
        // actually belongs to; a throwaway copy for the batch above and for
        // a preview.
        private static void ApplyPushBack(List<Entry> entries, Entry entry, int slots)
        {
            for (int i = 0; i < Math.Max(1, slots); i++)
            {
                float below = float.MinValue;
                foreach (var other in entries)
                {
                    if (ReferenceEquals(other, entry) || other.Charge >= entry.Charge)
                    {
                        continue;
                    }

                    if (other.Charge > below)
                    {
                        below = other.Charge;
                    }
                }

                entry.Charge = below == float.MinValue ? entry.Charge - TurnThreshold : below - 1f;
            }
        }

        // ApplyPushBack read upside down, and deliberately shaped to be
        // diffable against it: "greatest strictly below, minus one" becomes
        // "least strictly above, plus one".
        //
        // THE ONE PLACE THE TWO ARE NOT MIRRORS is the nobody-there branch.
        // A push with nobody below still means something -- the combatant
        // already going last can be made to go a whole turn later, which is
        // what taking a full threshold off says. An advance with nobody
        // above has nothing left to say: it is already the next action, and
        // any further movement would have to cross the threshold and buy a
        // turn. So it clips instead (1.9 rule 4).
        private static void ApplyPullForward(List<Entry> entries, Entry entry, int slots)
        {
            for (int i = 0; i < Math.Max(1, slots); i++)
            {
                float above = float.MaxValue;
                foreach (var other in entries)
                {
                    if (ReferenceEquals(other, entry) || other.Charge <= entry.Charge)
                    {
                        continue;
                    }

                    if (other.Charge < above)
                    {
                        above = other.Charge;
                    }
                }

                if (above == float.MaxValue)
                {
                    return;
                }

                entry.Charge = above + 1f;
            }
        }

        // WHAT AN ENTRY'S CHARGE IS RIGHT NOW, read-only.
        //
        // The displacement contract is written in charges -- every one of
        // 1.9's four worked examples states its answer as a number on this
        // scale -- and a test that could only assert the resulting ORDER
        // would pass for a destination that is wrong but happens to sort the
        // same way. float.NaN for an actor that is not in the order, which is
        // the same "there is nothing there" the -1 sentinels elsewhere mean
        // and cannot be confused with a real charge.
        public float ChargeOf(TActor actor)
        {
            var entry = _entries.FirstOrDefault(e => EqualityComparer<TActor>.Default.Equals(e.Actor, actor));
            return entry == null ? float.NaN : entry.Charge;
        }

        // WHERE AN ACTOR SITS IN THE FORECAST, or -1 if it does not appear
        // inside `count` turns at all.
        //
        // ITS FIRST APPEARANCE. A fast combatant legitimately appears more
        // than once in one window (Project's own header, and
        // UpcomingTurnsTests pins a five-turn window holding one actor three
        // times); the position a displacement is measured against is the
        // soonest it acts, because that is the turn a player is trying to
        // move. 1.9's worked example (b) is this case and nothing else.
        //
        // INDEX 0 IS THE CURRENT ACTOR, which is the fact the whole of 1.9
        // turns on and the one the baseline document never wrote down.
        public int ForecastPositionOf(TActor actor, int count, Func<TActor, bool> include = null)
        {
            var forecast = Project(count, include);
            for (int i = 0; i < forecast.Count; i++)
            {
                if (EqualityComparer<TActor>.Default.Equals(forecast[i], actor)) return i;
            }

            return -1;
        }

        private readonly Dictionary<TActor, int> _extraTurns = new Dictionary<TActor, int>();

        // Pulls an actor to the FRONT of the queue: they act next, before
        // anyone else currently waiting.
        //
        // The exact mirror of PushBack, and the Fragile Lamb's Gift: Haste.
        // He shoves enemies down the order; she drags an ally up it. Having
        // both be one method each, on the queue itself, is what keeps the
        // initiative tracker honest — it reads this queue, so anything
        // layered on top would show a future the scheduler is not going to
        // produce.
        //
        // Lands them just ABOVE the most-charged other combatant rather than
        // over the threshold outright. Crossing the line here would hand them
        // the turn AND leave the overflow carried into the next contest, so a
        // single gift would quietly buy a turn and a half. This buys exactly
        // the position it says it does.
        //
        // Returns false if the actor is not in the order, so a caller cannot
        // silently hurry something that has already been removed.
        public bool PullToFront(TActor actor)
        {
            var entry = _entries.FirstOrDefault(e => EqualityComparer<TActor>.Default.Equals(e.Actor, actor));
            if (entry == null)
            {
                return false;
            }

            float highest = float.MinValue;
            foreach (var other in _entries)
            {
                if (!ReferenceEquals(other, entry) && other.Charge > highest)
                {
                    highest = other.Charge;
                }
            }

            if (highest > float.MinValue && entry.Charge <= highest)
            {
                entry.Charge = highest + 1f;
            }

            return true;
        }

        // Ticks every combatant's charge forward until someone crosses the
        // threshold, and hands them the turn. THIS is where speed stops
        // being merely an ordering and starts changing how OFTEN you act: a
        // combatant charging at 2x crosses roughly twice as often, so it
        // genuinely takes extra turns rather than just going earlier in a
        // fixed rotation.
        private Entry ChargeUntilNextReady()
        {
            // Bounded so a content error (every rate zero) fails loudly
            // instead of hanging the game inside a while(true).
            const int MaxTicks = 1_000_000;
            for (int tick = 0; tick < MaxTicks; tick++)
            {
                Entry ready = null;
                foreach (var e in _entries)
                {
                    if (e.Charge < TurnThreshold)
                    {
                        continue;
                    }

                    // Most overcharged goes first; initiative breaks exact
                    // ties so the opening order stays deterministic.
                    if (ready == null || e.Charge > ready.Charge ||
                        (e.Charge == ready.Charge && e.Initiative > ready.Initiative))
                    {
                        ready = e;
                    }
                }

                if (ready != null)
                {
                    // Subtract rather than reset: the overflow carries into
                    // the next turn, so a fast combatant's fractional
                    // advantage accumulates honestly instead of being thrown
                    // away every turn.
                    ready.Charge -= TurnThreshold;
                    return ready;
                }

                foreach (var e in _entries)
                {
                    e.Charge += e.Rate;
                }

                _ticksThisRound++;
                if (_ticksThisRound * SpeedScale.TickRate((int)SpeedScale.BaselineSpeed) >= TurnThreshold)
                {
                    // A "round" is now just how long a baseline combatant
                    // takes to earn one turn. It no longer means everyone has
                    // acted exactly once, because that is precisely the
                    // property this system gives up.
                    _round++;
                    _ticksThisRound = 0;
                }
            }

            throw new InvalidOperationException("No combatant ever became ready — every charge rate is zero or negative.");
        }

        private int _ticksThisRound;

        // Simulates the schedule forward WITHOUT mutating it, for the
        // initiative tracker. Has to simulate rather than read the list in
        // order, because with per-combatant rates the upcoming sequence is
        // not a rotation of the roster — the same actor can legitimately
        // appear twice before someone slower appears once.
        // `include` filters what is REPORTED without changing what is
        // SIMULATED, and the distinction is load-bearing. A defeated
        // combatant still charges and still consumes its turn in the real
        // schedule — ChargeUntilNextReady hands it the turn and only then
        // does the caller skip past it — so removing it from the simulation
        // would make everyone behind it appear to act sooner than they will.
        // Null includes everyone.
        public IReadOnlyList<TActor> Project(int count, Func<TActor, bool> include = null)
        {
            if (count <= 0 || _entries.Count == 0)
            {
                return new List<TActor>();
            }

            // Work on copies so the real schedule is untouched.
            var sim = Snapshot();
            var pendingExtras = new Dictionary<TActor, int>(_extraTurns);
            var simCurrent = _current != null ? sim.FirstOrDefault(e => Equals(e.Actor, _current.Actor)) : null;

            return SimulateForward(sim, simCurrent, pendingExtras, count, include);
        }

        // The same projection, but as if `pushedActor` had already taken
        // `slots` of PushBack -- the hover preview for a skill that carries
        // QueuePushSlots, so the initiative tracker can show where the cast
        // would actually land before it is committed. NOTHING here touches
        // the real schedule: the push lands on the same throwaway snapshot
        // Project itself simulates forward from, one line earlier.
        //
        // `pushedActor` not found in the snapshot (already dead, already
        // removed) just projects the ordinary queue -- the same "returns
        // false rather than throwing" spirit PushBack itself follows for a
        // missing actor, adapted to a method that has no bool to return.
        public IReadOnlyList<TActor> ProjectPushed(TActor pushedActor, int slots, int count,
            Func<TActor, bool> include = null)
        {
            if (count <= 0 || _entries.Count == 0)
            {
                return new List<TActor>();
            }

            var sim = Snapshot();
            var pushed = sim.FirstOrDefault(e => Equals(e.Actor, pushedActor));
            if (pushed != null)
            {
                ApplyPushBack(sim, pushed, slots);
            }

            var pendingExtras = new Dictionary<TActor, int>(_extraTurns);
            var simCurrent = _current != null ? sim.FirstOrDefault(e => Equals(e.Actor, _current.Actor)) : null;

            return SimulateForward(sim, simCurrent, pendingExtras, count, include);
        }

        // ProjectPushed's mirror, and Borrowed Moment's hover preview: where
        // the tracker would read if this ally were advanced `slots` places,
        // shown before the cast is committed.
        //
        // IT CANNOT TOUCH THE REAL SCHEDULE, by construction rather than by
        // care (plan 1.13): ApplyPullForward runs on the Snapshot() copy
        // Project itself simulates forward from, one line below. The preview
        // and the resolution therefore run the identical rule, which is the
        // whole reason ApplyPullForward is a static over a list rather than
        // a method over `_entries`.
        //
        // THE CURRENT ACTOR IS NOT PREVIEWABLE either, matching the real
        // operation's own refusal -- a preview that showed a movement the
        // cast would then refuse is worse than no preview.
        public IReadOnlyList<TActor> ProjectPulled(TActor pulledActor, int slots, int count,
            Func<TActor, bool> include = null)
        {
            if (count <= 0 || _entries.Count == 0)
            {
                return new List<TActor>();
            }

            var sim = Snapshot();
            var pulled = sim.FirstOrDefault(e => Equals(e.Actor, pulledActor));
            bool isCurrent = _current != null && Equals(_current.Actor, pulledActor);
            if (pulled != null && !isCurrent)
            {
                ApplyPullForward(sim, pulled, slots);
            }

            var pendingExtras = new Dictionary<TActor, int>(_extraTurns);
            var simCurrent = _current != null ? sim.FirstOrDefault(e => Equals(e.Actor, _current.Actor)) : null;

            return SimulateForward(sim, simCurrent, pendingExtras, count, include);
        }

        // A fresh, independent copy of every entry -- the starting point
        // both Project and ProjectPushed simulate forward from, so neither
        // can ever mutate the real schedule no matter what runs on the copy
        // afterwards.
        private List<Entry> Snapshot()
        {
            return _entries.Select(e => new Entry
            {
                Actor = e.Actor,
                Initiative = e.Initiative,
                Charge = e.Charge,
                Rate = e.Rate,
            }).ToList();
        }

        // Runs ChargeUntilNextReady's exact rule against a SIMULATED list
        // instead of the real `_entries` -- factored out of Project so
        // ProjectPushed can share it rather than re-deriving the same
        // charge/extra-turn logic a second time with its own chance to drift
        // from the real scheduler's behaviour.
        private static List<TActor> SimulateForward(List<Entry> sim, Entry simCurrent,
            Dictionary<TActor, int> pendingExtras, int count, Func<TActor, bool> include)
        {
            var result = new List<TActor>(Math.Max(0, count));
            bool Wanted(TActor actor) => include == null || include(actor);

            if (simCurrent != null && Wanted(simCurrent.Actor))
            {
                result.Add(simCurrent.Actor);
            }

            const int MaxTicks = 1_000_000;
            int ticks = 0;
            while (result.Count < count && ticks < MaxTicks)
            {
                if (simCurrent != null && pendingExtras.TryGetValue(simCurrent.Actor, out int extra) && extra > 0)
                {
                    pendingExtras[simCurrent.Actor] = extra - 1;
                    if (Wanted(simCurrent.Actor))
                    {
                        result.Add(simCurrent.Actor);
                    }

                    continue;
                }

                Entry ready = null;
                foreach (var e in sim)
                {
                    if (e.Charge < TurnThreshold)
                    {
                        continue;
                    }

                    if (ready == null || e.Charge > ready.Charge ||
                        (e.Charge == ready.Charge && e.Initiative > ready.Initiative))
                    {
                        ready = e;
                    }
                }

                if (ready != null)
                {
                    ready.Charge -= TurnThreshold;
                    simCurrent = ready;
                    if (Wanted(ready.Actor))
                    {
                        result.Add(ready.Actor);
                    }

                    continue;
                }

                foreach (var e in sim)
                {
                    e.Charge += e.Rate;
                }

                ticks++;
            }

            return result;
        }

        // A starting head start, never a free turn. Kept strictly below the
        // threshold so the seed can only ever influence WHO opens and the
        // early ordering — crossing the line is something you have to charge
        // for. See Start() for what happens without the cap.
        private static float SeedCharge(int initiative)
        {
            return Math.Min(TurnThreshold - 1f, Math.Max(0f, initiative));
        }

        private void StableSortDescendingByInitiative()
        {
            // OrderByDescending is a stable sort in .NET, so combatants with
            // equal initiative keep their relative insertion order rather
            // than shuffling unpredictably.
            var sorted = _entries.OrderByDescending(e => e.Initiative).ToList();
            _entries.Clear();
            _entries.AddRange(sorted);
        }
    }
}
