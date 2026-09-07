namespace PrincesPalace.Domain.Party
{
    // Seat vocabulary, 0-based. Named constants rather than raw 0/1/2 so a
    // seat index reads as a position: Front is where the melee lands
    // (CombatEncounter.LivingRankOf's rank 0, FightSession's "enemy melee
    // now concentrates on rank 0" comment) rather than an arbitrary array
    // slot the reader has to remember the meaning of.
    public static class PartySeat
    {
        public const int Front = 0;
        public const int Middle = 1;
        public const int Rear = 2;

        // How many seats the model ever has, open or closed. MaxSeats (1-3)
        // says how many of these are currently OPEN; this is the array
        // length, not the current capacity.
        public const int Count = 3;
    }
}
