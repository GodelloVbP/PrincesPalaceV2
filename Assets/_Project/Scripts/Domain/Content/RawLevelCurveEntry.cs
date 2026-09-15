using System;

namespace PrincesPalace.Domain.Content
{
    // One row of the character level cost table, exactly as typed into
    // level_curve.json.
    //
    // TWO FIELDS AND BOTH REQUIRED, the same stance RawSpellTierEntry takes
    // and for the same reason: a progression table is exactly the kind of
    // content where every step deserves a deliberate, hand-picked number
    // rather than a formula guessing on the author's behalf. That is the
    // whole point of this file existing -- the formula it replaced
    // (geometric, c=100 g=1.09) could not price the early levels and the
    // late ones at the same time.
    [Serializable]
    public class RawLevelCurveEntry
    {
        // The level this row is the price OF. `level: 2` is what a fresh
        // level-1 character pays for their first level-up, so a table for a
        // cap of N has N-1 rows, starting at 2.
        [ContentDoc("The character level this row is the cost to ENTER; the table runs from 2 to RewardTrack.MaxLevel with no gaps.")]
        public int level;

        // What entering it costs. Bounded by the plan's contract 2, which
        // LevelCurveEntryResolver enforces rather than restating here.
        [ContentDoc("Experience needed to go from the previous level to this one; must be 58-10153 and never lower than the row before it.")]
        public int cost;
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // level_curve.json is one object with a "levels" array inside it.
    [Serializable]
    public class RawLevelCurveFile
    {
        [ContentDoc("This file's rows; see RawLevelCurveEntry.")]
        public RawLevelCurveEntry[] levels = Array.Empty<RawLevelCurveEntry>();
    }
}
