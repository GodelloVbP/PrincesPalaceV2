namespace PrincesPalace
{
    // Placeholder game-balance numbers that don't yet belong to any single
    // authored content type. Shared between FightController and the
    // Character Sheet so both screens agree on the same numbers rather than
    // each hard-coding their own copy.
    public static class GameplayConstants
    {
        // No per-role resource differentiation exists yet (e.g. "Sheep has
        // mana, someone else might not") — every player combatant gets this
        // same pool until that lands as real content.
        public const int DefaultMaxMana = 30;
    }
}
