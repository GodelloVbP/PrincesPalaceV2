using System;

namespace PrincesPalace.Domain.Progression
{
    // What a character level costs.
    //
    // IN DOMAIN, NOT ON Character, for the reason TalentSkeleton's header
    // gives for the same move: the EditMode assembly references Domain and
    // nothing else, so arithmetic that lives in Core can only be pinned from
    // PlayMode or duplicated into the test. `Character.ExpToNextLevel` still
    // exists and is still what every caller asks -- it delegates here, so the
    // seam callers depend on has not moved.
    //
    // GEOMETRIC, replacing `level * 100`, and the reason is that the thing it
    // was racing is geometric too.
    //
    // `DifficultyCurve.ScaleReward` is `ScaleHealth`: XP income compounds at 77
    // permille per depth step, so a step-80 fight pays roughly 370x a step-0
    // one. Against that, a linear-increment cost is not a slow curve, it is a
    // curve running the wrong way -- levels ACCELERATE. Cumulative cost to
    // level 100 under `level * 100` is 495,000, and a run reaching leg 10 pays
    // about 134,000, so the whole hundred-level track was worth under four
    // deep runs.
    //
    // At 90 permille the cost outruns the income (1.09 a level against 1.077 a
    // step) and level 100 lands near 6.7M -- about fifty deep runs, which is a
    // track rather than a formality. The full working, including what happens
    // if the real depth ceiling turns out to be leg 6-8, is in
    // docs/PLAN_PROGRESSION_TRACK.md.
    //
    // Worth knowing what this does to a save written before it: per-level cost
    // is LOWER than the old curve between roughly levels 2 and 44 and higher
    // after, so an existing character can be sitting on more exp than their
    // current level now requires. Nothing is corrupted by that -- AddExperience
    // loops, so they collect every level they are owed on their next gain, and
    // both XP bars clamp to 0..1 in the meantime (CombatReward.BarFill01,
    // CharacterDossierController's xpFill). No migration, which is why
    // SaveData.CurrentVersion does not move.
    public static class LevelCurve
    {
        // What level 0 would cost, and the number every step compounds from.
        public const int BaseCost = 100;

        // The dial, in the same integer permille per step DifficultyCurve
        // holds its two rates in -- deliberately the same idiom, because these
        // two curves are in a race and comparing them should not mean
        // converting between two notations first.
        //
        // 90 against the reward curve's 77. The margin is small on purpose:
        // much wider and the deep game stops paying for itself, much narrower
        // and the track never slows.
        public const int PermillePerLevel = 90;

        // Ceilings, the same defensive shape DifficultyCurve uses and for the
        // same reason: `level` comes off a save and nothing bounds it. 9%
        // compounding passes int at about level 190, and a wrapped cost reads
        // as a NEGATIVE requirement -- which AddExperience's `while (exp >=
        // ExpToNextLevel(level))` would treat as "always met" and spin.
        public const int MaxCurvedLevel = 200;
        private const int MaxCost = 1_000_000_000;

        // What it costs to get from `level` to `level + 1`.
        //
        // Floored, like every other curve in the project -- Scale() in
        // DifficultyCurve makes the same choice, and the alternative is a cost
        // that rounds up to a suspiciously round number at exactly the levels
        // worth checking.
        public static int ExpToNextLevel(int level)
        {
            if (level <= 0)
            {
                return BaseCost;
            }

            int clamped = level > MaxCurvedLevel ? MaxCurvedLevel : level;
            double cost = BaseCost * Math.Pow(1d + PermillePerLevel / 1000d, clamped);

            return cost >= MaxCost ? MaxCost : (int)Math.Floor(cost);
        }
    }
}
