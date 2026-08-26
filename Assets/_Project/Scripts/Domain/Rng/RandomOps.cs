namespace PrincesPalace.Domain.Rng
{
    // Small shared random-number formulas that more than one caller needs
    // bit-for-bit identical, kept here rather than on SeededRandom itself so
    // SeededRandom stays a plain generator and this stays a place for the
    // GAMEPLAY conventions built on top of it (e.g. what "a 40% chance"
    // means) to live once.
    public static class RandomOps
    {
        // THE ONE percent-chance convention every chance effect in this
        // codebase rolls through -- FightSession's on-hit modifier riders
        // (push-back, chill, root) and DamagePipeline.RollDodge both need
        // this exact formula, and a second hand-rolled copy is exactly how
        // the two would eventually drift.
        //
        // <= 0 never fires and >= 100 always does, WITHOUT consuming a draw
        // from `rng` in either case -- only a genuine 1-99 roll spends one.
        // Callers that must distinguish "never dodges" from "no rng stream
        // at all" (preview mode) guard that themselves before calling in;
        // this assumes `rng` is non-null.
        public static bool RollPercent(SeededRandom rng, int chance)
        {
            if (chance <= 0) return false;
            if (chance >= 100) return true;
            return rng.NextFloat(0f, 100f) < chance;
        }
    }
}
