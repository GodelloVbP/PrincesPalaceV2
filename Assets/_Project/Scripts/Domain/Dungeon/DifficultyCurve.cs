namespace PrincesPalace.Domain.Dungeon
{
    // How much harder the dungeon gets as a run goes deeper.
    //
    // THIS DID NOT EXIST. Before the descent became continuous, a floor-9
    // fight was statistically identical to a floor-1 fight: the same enemy
    // pool, the same baseStats, the same 1-2 count. The only multiplier in
    // the whole game was FightController's EliteStatMultiplier. That was
    // survivable while a run was seven columns and a boss; an infinite map
    // without a curve is an infinite corridor of trivial fights, so the leg
    // generator and this file ship together on purpose.
    //
    // Pure and engine-free so the curve can be pinned by literal tests
    // rather than sampled by playing.
    public static class DifficultyCurve
    {
        // 5.5% per step, compounding NOT applied — this is a straight line,
        // deliberately.
        //
        // A geometric curve is the obvious first instinct and is wrong here:
        // at 5.5% compounding, step 40 is 8.5x and step 80 is 72x, which
        // outruns anything the item ladder can answer (a fully honed top-tier
        // set is worth roughly 4x a starting one). Linear means the player's
        // power and the dungeon's climb at comparable rates, so depth stays a
        // question of attrition rather than a wall that arrives all at once.
        //
        // Held as an integer permille for the same precision reason
        // ItemUpgrade.PercentPerPlus is: a float rate applied to health would
        // land a step short of round numbers at exactly the depths worth
        // checking.
        private const int PermillePerStep = 55;

        // Step 8 (the first elite) is ~1.44x, step 16 (the first boss)
        // ~1.88x, step 40 ~3.2x, step 80 ~5.4x.
        public static float EnemyMultiplier(int step)
        {
            return 1f + PermillePerStep * Clamp(step) / 1000f;
        }

        // Applies the curve to one number, floored.
        //
        // Used for health, attack and break shields alike, so a deep enemy is
        // uniformly tougher rather than tougher in one dimension and not
        // another — a fight that gains health without gaining damage just
        // takes longer, which is the least interesting way to be hard.
        public static int Scale(int amount, int step)
        {
            if (amount == 0 || step <= 0)
            {
                return amount;
            }

            int scaled = amount * (1000 + PermillePerStep * Clamp(step));
            return scaled >= 0 ? scaled / 1000 : (scaled - 999) / 1000;
        }

        // Rewards ride the SAME curve as the threat.
        //
        // Not a separate rate, on purpose: if pay lagged difficulty the deep
        // game would quietly become worse value per fight and a player's best
        // move would be to farm shallow rooms forever, which is the opposite
        // of what an endless descent is for. Kept as its own named method
        // anyway so that decoupling them later is a deliberate edit rather
        // than a silent one.
        public static int ScaleReward(int amount, int step)
        {
            return Scale(amount, step);
        }

        // A run cannot descend forever in practice, but `step` comes off a
        // save and nothing else bounds it. Clamped so a corrupt or
        // hand-edited value cannot overflow the multiplication above into a
        // negative enemy.
        public const int MaxScaledStep = 400;

        private static int Clamp(int step)
        {
            if (step <= 0)
            {
                return 0;
            }

            return step > MaxScaledStep ? MaxScaledStep : step;
        }
    }
}
