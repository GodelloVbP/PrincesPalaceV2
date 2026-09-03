namespace PrincesPalace.Domain.Content
{
    // Constants for the learned-spell system (docs/PLAN_SHOP.md §1b/§1d).
    // Pure and engine-free, beside the entry type it sizes -- see
    // RunSnapshot.LearnedSpellEntry (Data layer, which is allowed to
    // reference Domain but not the reverse) for the shape a slot holds.
    public static class SpellBooks
    {
        // Three slots per character, per run. Not authored per-character or
        // per-content: every character gets the same three, and nothing in
        // the plan asks for a character-specific count.
        public const int MaxSpellSlots = 3;
    }
}
