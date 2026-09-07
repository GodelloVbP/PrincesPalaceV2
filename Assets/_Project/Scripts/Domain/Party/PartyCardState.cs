namespace PrincesPalace.Domain.Party
{
    // Which pill, if any, a seat draws over itself for the pending
    // selection. None covers three different reasons at once (nothing is
    // selected; this IS the selected seat, so clicking it again cancels;
    // the seat is closed or locked) -- a seat only ever needs to know
    // whether it's a legal destination, not why it isn't one, so
    // PartyFormation.SeatBadge does not distinguish them.
    public enum PartySeatBadge
    {
        None,
        PlaceHere,
        Replace,
        SwapWith
    }

    // A roster card's full presentation state, computed once per query
    // rather than the screen re-deriving IsSelectable from four separate
    // formation queries (Mode, SeatOf, IsSeatLocked, SelectedId) and risking
    // the four disagreeing about a card that just got benched mid-frame.
    public readonly struct PartyCardState
    {
        public readonly bool IsActive;

        // -1 when not seated.
        public readonly int Seat;

        public readonly bool IsSelected;
        public readonly bool IsBenched;
        public readonly bool IsSelectable;
        public readonly bool HasArt;

        public PartyCardState(bool isActive, int seat, bool isSelected, bool isBenched, bool isSelectable, bool hasArt)
        {
            IsActive = isActive;
            Seat = seat;
            IsSelected = isSelected;
            IsBenched = isBenched;
            IsSelectable = isSelectable;
            HasArt = hasArt;
        }
    }
}
