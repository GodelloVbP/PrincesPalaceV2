using System;

namespace PrincesPalace.Domain.Party
{
    public enum PartySelectionSourceKind
    {
        Roster,
        Seat
    }

    // Where the pending selection came from -- a roster card, or a seat
    // that was already occupied. This is what tells ClickSeat whether
    // landing on another occupied seat is a REPLACE (the roster member
    // being fielded bumps whoever was there) or a SWAP (two seated members
    // trade places) -- see PartyFormation.ClickSeat steps 6-7. It travels
    // with the selection instead of being re-derived from SelectedId at the
    // point of use, so a card that happens to also be seated can't make the
    // two cases ambiguous.
    //
    // Deliberately carries no id for the Seat case -- SeatIndex plus the
    // formation's own seats array already names the occupant unambiguously,
    // and a second copy of the id here could disagree with it after a
    // command mutates the seats array out from under a stale selection.
    // Roster carries no id either, which is the one gap PartyFormation.Drop
    // has to work around -- see that method's header.
    public readonly struct PartySelectionSource : IEquatable<PartySelectionSource>
    {
        public readonly PartySelectionSourceKind Kind;

        // Meaningless when Kind == Roster.
        public readonly int SeatIndex;

        private PartySelectionSource(PartySelectionSourceKind kind, int seatIndex)
        {
            Kind = kind;
            SeatIndex = seatIndex;
        }

        public static readonly PartySelectionSource Roster =
            new PartySelectionSource(PartySelectionSourceKind.Roster, -1);

        public static PartySelectionSource Seat(int index) =>
            new PartySelectionSource(PartySelectionSourceKind.Seat, index);

        public bool IsSeat => Kind == PartySelectionSourceKind.Seat;

        public bool Equals(PartySelectionSource other) =>
            Kind == other.Kind && SeatIndex == other.SeatIndex;
        public override bool Equals(object obj) => obj is PartySelectionSource other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ SeatIndex);
        public static bool operator ==(PartySelectionSource a, PartySelectionSource b) => a.Equals(b);
        public static bool operator !=(PartySelectionSource a, PartySelectionSource b) => !a.Equals(b);
        public override string ToString() => Kind == PartySelectionSourceKind.Roster ? "Roster" : $"Seat({SeatIndex})";
    }
}
