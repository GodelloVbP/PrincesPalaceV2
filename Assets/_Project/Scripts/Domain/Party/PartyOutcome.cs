namespace PrincesPalace.Domain.Party
{
    // What a command did, or refused to do. Every case a command can reach --
    // see PartyFormation's command headers for exactly when each fires.
    public enum PartyToastKind
    {
        None,
        Placed,
        Moved,
        Swapped,
        Replaced,
        Benched,
        FormationFixedInFight,
        SeatNotOpen,
        SeatLocked,
        BenchedDuringRun,
        RepositionOnlyDuringRun,
        PartyNeverEmpty
    }

    // What a command did, as DATA rather than a string. Display copy lives in
    // UiStrings (a UiKit/Ui-package concern; Domain never authors
    // user-facing text -- see .claude/rules/ui.md's UiString rule), so this
    // hands back a KIND plus the up-to-two names a toast template needs to
    // fill in ("{Actor} takes the Front seat", "{Actor} swaps places with
    // {Other}"). Seat is the seat the toast is about when the kind cares --
    // where Actor landed for Placed/Moved, where the swap/replace/bench
    // happened for the rest. Fields outside what a given Toast uses are left
    // at their default rather than guessed at.
    public readonly struct PartyOutcome
    {
        public readonly bool Changed;
        public readonly PartyToastKind Toast;
        public readonly string Actor;
        public readonly string Other;
        public readonly int Seat;

        private PartyOutcome(bool changed, PartyToastKind toast, string actor, string other, int seat)
        {
            Changed = changed;
            Toast = toast;
            Actor = actor;
            Other = other;
            Seat = seat;
        }

        // A refused command, or a pure selection change (Toast == None).
        public static PartyOutcome Blocked(PartyToastKind toast, string actor = null, string other = null, int seat = -1) =>
            new PartyOutcome(false, toast, actor, other, seat);

        // A committed move. Every caller of this also clears the pending
        // selection in the same breath -- see PartyFormation's ClearSelection
        // calls -- because "selection null after Changed == true" is an
        // invariant this type cannot enforce on its own (it doesn't hold a
        // reference to the formation that produced it).
        public static PartyOutcome Committed(PartyToastKind toast, string actor = null, string other = null, int seat = -1) =>
            new PartyOutcome(true, toast, actor, other, seat);
    }
}
