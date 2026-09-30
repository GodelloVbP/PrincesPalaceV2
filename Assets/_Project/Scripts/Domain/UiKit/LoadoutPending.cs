namespace PrincesPalace.Domain.UiKit
{
    // What a LOADOUT slot is waiting for (plan C5). SlotArmed: an empty gear
    // or book slot chose to be filled, and focus went into the pack filtered
    // to it. BookPicked: a pack book was chosen while every book slot is
    // full, and focus went to the book slots to say which to replace. Either
    // is dropped with no change to the game when anything else happens first
    // (plan C6); it never commits on its own.
    public enum LoadoutPendingKind
    {
        None,
        SlotArmed,
        BookPicked,
    }

    public readonly struct LoadoutPending
    {
        public readonly LoadoutPendingKind Kind;

        // SlotArmed on a gear slot: its index in EquipmentSlots.All; -1 when
        // a book slot is armed.
        public readonly int GearSlot;

        // SlotArmed on a book slot: its index; -1 otherwise.
        public readonly int BookSlot;

        // BookPicked: the book waiting for a slot.
        public readonly string BookId;

        private LoadoutPending(LoadoutPendingKind kind, int gearSlot, int bookSlot, string bookId)
        {
            Kind = kind;
            GearSlot = gearSlot;
            BookSlot = bookSlot;
            BookId = bookId;
        }

        public static LoadoutPending None => new LoadoutPending(LoadoutPendingKind.None, -1, -1, null);

        public static LoadoutPending ArmGear(int gearSlot) =>
            new LoadoutPending(LoadoutPendingKind.SlotArmed, gearSlot, -1, null);

        public static LoadoutPending ArmBook(int bookSlot) =>
            new LoadoutPending(LoadoutPendingKind.SlotArmed, -1, bookSlot, null);

        public static LoadoutPending PickBook(string bookId) =>
            new LoadoutPending(LoadoutPendingKind.BookPicked, -1, -1, bookId);

        public bool IsNone => Kind == LoadoutPendingKind.None;

        public bool ArmsGear => Kind == LoadoutPendingKind.SlotArmed && GearSlot >= 0;

        public bool ArmsBook => Kind == LoadoutPendingKind.SlotArmed && BookSlot >= 0;
    }
}
