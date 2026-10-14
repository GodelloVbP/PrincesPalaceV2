using System;

namespace PrincesPalace.Domain.Rewards
{
    // How far above the expected a drop climbs.
    //
    // A LADDER, NOT A TABLE, and that choice is the whole design. Every
    // shape tried before this one shared a fault: any bonus -- a floor
    // multiplier, an encounter multiplier, a luck bonus added to the roll --
    // eventually pushes the score past the top cut, and everything past the
    // top cut lands in the RAREST band. Measured on the multiplicative version
    // this replaces, a floor-1 boss produced the jackpot tier 34% of the time
    // and a floor-3 normal produced it 67% of the time. Bonuses were making
    // the rare outcome the DEFAULT outcome, which is exactly backwards.
    //
    // Here each rung has to be earned separately: climbing s rungs means s
    // consecutive successes, so P(climb >= s) = p^s. The tail therefore decays
    // geometrically and CANNOT fill up, however generous p becomes -- at the
    // hard cap of 0.55 the top rung is still only 5%. Luck and encounter class
    // raise p, which fattens the whole tail without ever collapsing it into a
    // spike at the ceiling.
    //
    // Pure and engine-free like everything else in Rewards: randomness arrives
    // as the same upper-bound-exclusive Func<int,int> the rest of this folder
    // already takes.
    public static class LootLadder
    {
        // Rungs are drawn against this, so a probability is expressed in
        // thousandths and stays integer arithmetic at the draw site.
        public const int Resolution = 1000;

        // The highest climb, for both the tier spike and the plus. Five is the
        // number the design asks for: +5 is the "this changes the run" find.
        public const int MaxRungs = 5;

        // What one point of Prince's Favor buys, and the ceiling it buys
        // toward.
        //
        // The CAP is the load-bearing half. Without it a deep enough Favor
        // stack would walk p toward 1 and re-create the saturation this whole
        // type exists to avoid; with it, the very best a run can ever do is a
        // 5% top rung. Favor makes the tail fatter, never certain.
        public const float FavorPerPoint = 0.006f;
        public const float MaxStep = 0.55f;

        // One base rate plus Favor at a per-point rate, capped: the dial a
        // rewards.json row feeds (its tierStep or ladderStep).
        public static float StepChance(float baseStep, int favor, float favorPerPoint, float maxStep)
        {
            if (favor < 0) favor = 0;

            float step = baseStep + favor * favorPerPoint;
            return step > maxStep ? maxStep : step;
        }

        // How many rungs this roll climbs, 0..maxRungs.
        //
        // Draws a FIXED maxRungs times and counts the leading successes rather
        // than stopping at the first failure. Distributionally identical, and
        // it costs the same number of draws whatever the outcome -- which is
        // what keeps a seeded run reproducible when these odds are retuned.
        // RarityTable.RollTier routes through this same Climb rather than
        // drawing once itself; the fixed-count discipline is the point, not a
        // comparison to some other single-draw method.
        //
        // maxRungs is a PARAMETER rather than always MaxRungs so a shorter
        // ladder can reuse this exact draw-and-count rule instead of
        // duplicating it.
        public static int Climb(float stepChance, int maxRungs, Func<int, int> nextIndex)
        {
            if (nextIndex == null || stepChance <= 0f || maxRungs <= 0) return 0;

            // ROUNDED, not truncated: 0.22f is 0.2199999988f, and whether
            // the product is kept in float (.NET) or widened to double
            // (Unity's Mono) decides between 220 and 219 under truncation, so
            // the same seed rolled different pages on the two hosts.
            int threshold = (int)Math.Round((double)stepChance * Resolution);
            if (threshold <= 0) return 0;
            if (threshold > Resolution) threshold = Resolution;

            int rungs = 0;
            bool climbing = true;
            for (int i = 0; i < maxRungs; i++)
            {
                int roll = nextIndex(Resolution);
                if (roll < 0) roll = 0;
                else if (roll >= Resolution) roll = Resolution - 1;

                if (climbing && roll < threshold) rungs++;
                else climbing = false;
            }

            return rungs;
        }

        public static int Climb(float stepChance, Func<int, int> nextIndex) =>
            Climb(stepChance, MaxRungs, nextIndex);
    }
}
