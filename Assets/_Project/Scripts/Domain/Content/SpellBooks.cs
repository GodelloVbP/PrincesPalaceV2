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

        // WHETHER A BOOK CAN BE HELD AT ALL, and the ONE place that question
        // is answered (plan P6). A pool row says whether its holder reads
        // spell books; five gates read this predicate and nothing reads
        // ResolvedPool.AllowsSpellBooks directly:
        //
        //   1. RunOrchestrator.CanLearn        -- no slot, so no learn/replace
        //   2. RunOrchestrator's book offer    -- and the shop card's own line
        //   3. the dossier's slot block        -- hidden, with one line saying why
        //   4. ContentDatabase.LearnedThisRun  -- a stale save's book never casts
        //   5. SkillEntryResolver              -- refused at the content build
        //
        // ONE PREDICATE RATHER THAN FIVE READS because the five have to agree:
        // a shop that sells a book to somebody the dossier will not let place
        // it is a purchase with no effect, which is exactly the bug
        // AvailableSkillsFor's own header records happening once already.
        //
        // NULL MEANS YES, the house's graceful-degradation posture. A missing
        // pool is a save whose catalogue was swapped, not an authoring
        // decision, and the shipped answer for every character is "mana, and
        // mana reads books".
        public static bool CanHold(ResolvedPool primary) => primary == null || primary.AllowsSpellBooks;
    }
}
