using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Party
{
    // The Party screen's state and rules, engine-free. Three SEATS
    // (PartySeat.Front/Middle/Rear), a click-to-select-then-click-to-place
    // interaction (never drag-only -- Drop below is an alternate ENTRY point
    // into the same commands, not a second set of rules), and one MODE that
    // gates what a click is even allowed to do.
    //
    // POSITIONS ARE MECHANICAL, not cosmetic: seat 0 is the front rank enemy
    // melee concentrates on (CombatEncounter.LivingRankOf, FightSession's
    // "enemy melee now concentrates on rank 0" comment), so which seat a
    // character sits in is a combat decision the player is making here, not
    // a portrait arrangement.
    //
    // LOCKED IS A PREDICATE, not a stored set of indices, because nothing
    // that can lock a seat exists yet -- there is no run-modifier system to
    // ask. Production passes `_ => false` today. The seam exists so this
    // type is fully testable against a hypothetical lock without inventing
    // the feature that would produce one, and so the eventual feature only
    // has to supply a function, not a second copy of this model's plumbing.
    public sealed class PartyFormation
    {
        private readonly Dictionary<string, PartyRosterEntry> _roster;
        private readonly string[] _seats = new string[PartySeat.Count];
        private readonly Func<int, bool> _isLocked;

        private string _selectedId;
        private PartySelectionSource _selectedFrom;

        public PartyMode Mode { get; }
        public int MaxSeats { get; }

        public PartyFormation(
            IReadOnlyList<PartyRosterEntry> roster,
            IReadOnlyList<string> seatIds,
            PartyMode mode,
            int maxSeats,
            Func<int, bool> isLocked)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (isLocked == null) throw new ArgumentNullException(nameof(isLocked));

            // "Clamp nothing silently": a caller handing this 4 seat ids
            // has a bug upstream (SaveData's own EffectiveMaxSquadSize is
            // capped at FightHudSpec.StageSlotsPerSide == 3), and silently
            // dropping the fourth would hide that bug instead of failing
            // where it was made.
            if (seatIds != null && seatIds.Count > PartySeat.Count)
                throw new ArgumentException($"seatIds cannot carry more than {PartySeat.Count} entries.", nameof(seatIds));

            _roster = new Dictionary<string, PartyRosterEntry>();
            foreach (var entry in roster)
            {
                _roster[entry.Id] = entry;
            }

            Mode = mode;
            MaxSeats = maxSeats;
            _isLocked = isLocked;

            var seen = new HashSet<string>();
            for (int i = 0; i < PartySeat.Count; i++)
            {
                string id = seatIds != null && i < seatIds.Count ? seatIds[i] : null;
                if (id == null) continue;

                if (!_roster.ContainsKey(id))
                    throw new ArgumentException($"Seat {i} names '{id}', which is not in the roster.", nameof(seatIds));

                if (!seen.Add(id))
                    throw new ArgumentException($"'{id}' occupies two seats.", nameof(seatIds));

                _seats[i] = id;
            }
        }

        // ------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------

        public int FilledCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < PartySeat.Count; i++)
                {
                    if (_seats[i] != null) count++;
                }

                return count;
            }
        }

        // A copy, not the live array -- callers get IReadOnlyList<string> in
        // the signature but a cast back to string[] must not be able to
        // mutate seating out from under the commands that are supposed to
        // be the only way to change it.
        public IReadOnlyList<string> SeatIds => (string[])_seats.Clone();

        public int SeatOf(string id)
        {
            for (int i = 0; i < PartySeat.Count; i++)
            {
                if (_seats[i] == id) return i;
            }

            return -1;
        }

        public bool IsSeatClosed(int i)
        {
            ValidateSeatIndex(i);
            return i >= MaxSeats;
        }

        // Closed seats are never "locked" -- they are closed, a different
        // reason for the same refusal, and SeatBadge/CardState both need to
        // tell the two apart to pick the right toast.
        public bool IsSeatLocked(int i)
        {
            ValidateSeatIndex(i);
            return !IsSeatClosed(i) && _isLocked(i);
        }

        public string SelectedId => _selectedId;

        // Meaningless when SelectedId is null -- see PartySelectionSource's
        // own header for why this can't be made to answer "nothing" on its
        // own (there is no sentinel that isn't also a valid source).
        public PartySelectionSource SelectedFrom => _selectedFrom;

        public PartySeatBadge SeatBadge(int i)
        {
            ValidateSeatIndex(i);
            if (_selectedId == null) return PartySeatBadge.None;

            // Clicking the seat that is itself the selection cancels rather
            // than placing -- ClickSeat step 4. The badge has to agree, or
            // the seat would show a pill promising a swap-with-self.
            if (_selectedFrom.IsSeat && _selectedFrom.SeatIndex == i) return PartySeatBadge.None;

            // A locked ORIGIN has no legal destination at all, so no seat may
            // advertise one -- ClickSeat refuses the whole move (see its own
            // origin-lock step). Without this the badge promises a swap the
            // model then declines, which is the same mismatch the
            // selected-seat case above exists to avoid.
            if (_selectedFrom.IsSeat && IsSeatLocked(_selectedFrom.SeatIndex)) return PartySeatBadge.None;

            if (IsSeatClosed(i) || IsSeatLocked(i)) return PartySeatBadge.None;

            if (_seats[i] == null) return PartySeatBadge.PlaceHere;

            return _selectedFrom.Kind == PartySelectionSourceKind.Roster
                ? PartySeatBadge.Replace
                : PartySeatBadge.SwapWith;
        }

        public bool IsValidDestination(int i) => SeatBadge(i) != PartySeatBadge.None;

        public PartyCardState CardState(string id)
        {
            if (!_roster.TryGetValue(id, out var entry))
                throw new ArgumentException($"'{id}' is not in the roster.", nameof(id));

            int seat = SeatOf(id);
            bool isActive = seat >= 0;
            bool isSelected = _selectedId == id;
            bool isBenched = Mode == PartyMode.Run && !isActive;

            // Selectable mirrors what ClickCard would actually do with this
            // id: ViewOnly blocks everything before it looks at the card;
            // a seated-and-locked card would only earn a SeatLocked toast;
            // an unseated card during a Run would only earn BenchedDuringRun.
            // Anything else is a real selection, so the screen can dim a
            // card exactly where a click on it would go nowhere.
            bool isSelectable = Mode != PartyMode.ViewOnly &&
                (isActive ? !IsSeatLocked(seat) : Mode != PartyMode.Run);

            return new PartyCardState(isActive, seat, isSelected, isBenched, isSelectable, entry.HasArt);
        }

        public bool CanSendToBench =>
            _selectedId != null &&
            _selectedFrom.Kind == PartySelectionSourceKind.Seat &&
            Mode == PartyMode.Camp &&
            FilledCount > 1 &&
            !IsSeatLocked(_selectedFrom.SeatIndex);

        // ------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------

        public PartyOutcome ClickSeat(int i)
        {
            ValidateSeatIndex(i);

            if (Mode == PartyMode.ViewOnly)
                return PartyOutcome.Blocked(PartyToastKind.FormationFixedInFight);

            if (IsSeatClosed(i))
                return PartyOutcome.Blocked(PartyToastKind.SeatNotOpen, seat: i);

            if (_selectedId == null)
            {
                return TrySelectSeat(i, out var blockedBySeat) ? PartyOutcome.Blocked(PartyToastKind.None) : blockedBySeat;
            }

            // Clicking the source seat again cancels -- this has to come
            // before the general lock check below, because a locked seat
            // that got selected before it locked (or that the caller is
            // re-testing against) must still be cancellable.
            if (_selectedFrom.IsSeat && _selectedFrom.SeatIndex == i)
            {
                ClearSelection();
                return PartyOutcome.Blocked(PartyToastKind.None);
            }

            if (IsSeatLocked(i))
            {
                string occupantName = _seats[i] != null ? DisplayNameOf(_seats[i]) : null;
                return PartyOutcome.Blocked(PartyToastKind.SeatLocked, actor: occupantName, seat: i);
            }

            // THE ORIGIN'S LOCK, not just the destination's. A seat-sourced
            // move or swap VACATES the origin, which is the thing a lock
            // forbids -- "locked rejects any placement, and its occupant
            // can't be moved out either". SendToBench already refuses on exactly
            // this ground; without the same step here the identical
            // sequence commits through the other gesture.
            //
            // Only reachable when the predicate's answer changes after the
            // selection -- selecting a locked seat is refused by ClickCard
            // and TrySelectSeat -- which is the run-modifier case the
            // predicate seam exists for, and the same one
            // SendToBenchOnASeatThatLockedAfterSelectionRefuses covers.
            if (_selectedFrom.IsSeat && IsSeatLocked(_selectedFrom.SeatIndex))
            {
                return PartyOutcome.Blocked(PartyToastKind.SeatLocked,
                    actor: DisplayNameOf(_selectedId), seat: _selectedFrom.SeatIndex);
            }

            string selectedId = _selectedId;
            string selectedName = DisplayNameOf(selectedId);

            if (_selectedFrom.Kind == PartySelectionSourceKind.Roster)
            {
                // REPLACE: whoever was in seat i leaves the formation
                // entirely (they occupied no other seat), rather than being
                // parked anywhere -- that is what "leaves" means for a
                // single-seat occupant.
                string previousOccupant = _seats[i];
                _seats[i] = selectedId;
                ClearSelection();

                return previousOccupant == null
                    ? PartyOutcome.Committed(PartyToastKind.Placed, actor: selectedName, seat: i)
                    : PartyOutcome.Committed(PartyToastKind.Replaced, actor: selectedName, other: DisplayNameOf(previousOccupant), seat: i);
            }
            else
            {
                int origin = _selectedFrom.SeatIndex;
                string previousOccupant = _seats[i];
                _seats[i] = selectedId;

                // null when seat i was empty -- the mover's old seat simply
                // empties out (a MOVE); otherwise seat i's occupant takes
                // the mover's old seat (a SWAP), never left homeless.
                _seats[origin] = previousOccupant;
                ClearSelection();

                return previousOccupant == null
                    ? PartyOutcome.Committed(PartyToastKind.Moved, actor: selectedName, seat: i)
                    : PartyOutcome.Committed(PartyToastKind.Swapped, actor: selectedName, other: DisplayNameOf(previousOccupant), seat: i);
            }
        }

        public PartyOutcome ClickCard(string id)
        {
            // ViewOnly is checked BEFORE the unknown-id guard below, on
            // purpose: inside a fight every click on the party UI is
            // refused before the model even asks whether the id makes
            // sense, matching ClickSeat's own step order (Mode gates
            // everything else).
            if (Mode == PartyMode.ViewOnly)
                return PartyOutcome.Blocked(PartyToastKind.FormationFixedInFight);

            if (!_roster.ContainsKey(id))
                throw new ArgumentException($"'{id}' is not in the roster.", nameof(id));

            if (_selectedId == id)
            {
                ClearSelection();
                return PartyOutcome.Blocked(PartyToastKind.None);
            }

            int seat = SeatOf(id);
            if (seat >= 0)
            {
                if (IsSeatLocked(seat))
                    return PartyOutcome.Blocked(PartyToastKind.SeatLocked, actor: DisplayNameOf(id), seat: seat);

                // Selecting a SEATED card replaces whatever else was
                // selected -- a card is never a destination, so there is
                // nothing for a prior selection to commit against here.
                Select(id, PartySelectionSource.Seat(seat));
                return PartyOutcome.Blocked(PartyToastKind.None);
            }

            if (Mode == PartyMode.Run)
                return PartyOutcome.Blocked(PartyToastKind.BenchedDuringRun);

            Select(id, PartySelectionSource.Roster);
            return PartyOutcome.Blocked(PartyToastKind.None);
        }

        public PartyOutcome SendToBench()
        {
            if (Mode == PartyMode.ViewOnly)
                return PartyOutcome.Blocked(PartyToastKind.FormationFixedInFight);

            if (_selectedId == null || _selectedFrom.Kind == PartySelectionSourceKind.Roster)
                return PartyOutcome.Blocked(PartyToastKind.None);

            if (Mode == PartyMode.Run)
                return PartyOutcome.Blocked(PartyToastKind.RepositionOnlyDuringRun);

            if (FilledCount == 1)
                return PartyOutcome.Blocked(PartyToastKind.PartyNeverEmpty);

            int seat = _selectedFrom.SeatIndex;
            if (IsSeatLocked(seat))
                return PartyOutcome.Blocked(PartyToastKind.SeatLocked, actor: DisplayNameOf(_seats[seat]), seat: seat);

            string actor = DisplayNameOf(_seats[seat]);
            _seats[seat] = null;
            ClearSelection();
            return PartyOutcome.Committed(PartyToastKind.Benched, actor: actor, seat: seat);
        }

        // The drag-back-onto-the-roster path (P4). Same operation, same
        // precedence as the banner's Bench link -- unseating a member is
        // unseating a member whichever gesture triggered it, so this is not
        // a second copy of SendToBench's rules, it IS SendToBench.
        public PartyOutcome DropOnRoster() => SendToBench();

        public PartyOutcome Cancel()
        {
            ClearSelection();
            return PartyOutcome.Blocked(PartyToastKind.None);
        }

        // The drag path P4 will use: one call standing in for "click the
        // source, then click the destination" so a seat-to-seat drag does
        // not need two round trips through the precedence ClickSeat already
        // owns.
        //
        // ROSTER SOURCES ARE THE ONE GAP, and it is a deliberate one rather
        // than an oversight: PartySelectionSource carries no id (see its own
        // header), so a Roster `from` can only mean "the selection already
        // active IS a roster selection" -- i.e. the drag's pickup already
        // went through ClickCard(id), same as the two-click path always
        // required for a roster card (it is never a destination, so seating
        // one has always been "select the card, then click a seat"). A
        // Roster `from` with no such selection active is a caller bug -- P4
        // asking to drop a card the model was never told the id of -- not a
        // state a player can reach through the UI, so it throws rather than
        // returning a blocked toast (Domain throws on programmer error;
        // docs/CODE_STANDARDS.md "Functions").
        public PartyOutcome Drop(PartySelectionSource from, int targetSeat)
        {
            if (Mode == PartyMode.ViewOnly)
                return PartyOutcome.Blocked(PartyToastKind.FormationFixedInFight);

            if (from.Kind == PartySelectionSourceKind.Seat)
            {
                ValidateSeatIndex(from.SeatIndex);
                if (IsSeatClosed(from.SeatIndex))
                    return PartyOutcome.Blocked(PartyToastKind.SeatNotOpen, seat: from.SeatIndex);

                if (!TrySelectSeat(from.SeatIndex, out var blockedBySeat))
                    return blockedBySeat;
            }
            else if (_selectedId == null || _selectedFrom.Kind != PartySelectionSourceKind.Roster)
            {
                throw new InvalidOperationException(
                    "Drop(PartySelectionSource.Roster, ...) requires a roster selection " +
                    "already active (call ClickCard(id) to start the drag) -- the source " +
                    "alone carries no id to reselect from.");
            }

            return ClickSeat(targetSeat);
        }

        // ------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------

        // The "nothing selected, click seat i" rule -- ClickSeat's own step
        // 3, pulled out because Drop needs to fabricate the exact same
        // selection from a cold drag start. Duplicating the empty/locked
        // checks in two places would risk the two entry points disagreeing
        // about what a locked seat's click looks like.
        private bool TrySelectSeat(int i, out PartyOutcome blocked)
        {
            string occupant = _seats[i];
            if (occupant == null)
            {
                blocked = PartyOutcome.Blocked(PartyToastKind.None);
                return false;
            }

            if (IsSeatLocked(i))
            {
                blocked = PartyOutcome.Blocked(PartyToastKind.SeatLocked, actor: DisplayNameOf(occupant), seat: i);
                return false;
            }

            Select(occupant, PartySelectionSource.Seat(i));
            blocked = default;
            return true;
        }

        private void Select(string id, PartySelectionSource from)
        {
            _selectedId = id;
            _selectedFrom = from;
        }

        private void ClearSelection()
        {
            _selectedId = null;
            _selectedFrom = default;
        }

        private string DisplayNameOf(string id) => _roster[id].DisplayName;

        private static void ValidateSeatIndex(int i)
        {
            if (i < 0 || i >= PartySeat.Count)
                throw new ArgumentOutOfRangeException(nameof(i), $"Seat index must be 0-{PartySeat.Count - 1}.");
        }
    }
}
