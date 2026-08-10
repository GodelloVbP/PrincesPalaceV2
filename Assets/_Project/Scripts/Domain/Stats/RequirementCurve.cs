namespace PrincesPalace.Domain.Stats
{
    // The one knob that scales every item/skill/spell-tier requirement in
    // the game at once, beside ItemUpgrade for the same reason: one file
    // owns one curve.
    //
    // Applied at READ time — by whoever turns an authored requirement into
    // a RequirementResolver candidate (ContentDatabase.ActiveLoadout) —
    // rather than baked into content, so retuning how gate-y the whole
    // system feels is one integer, not a content rebuild.
    //
    // Held as an INTEGER PERCENT for the same precision reason
    // ItemUpgrade.PercentPerPlus is: floating-point rounding at exactly the
    // round numbers a player is most likely to check is the trap, not a
    // hypothetical one.
    public static class RequirementCurve
    {
        // 100 = every requirement exactly as authored. Below 100 makes the
        // whole game's requirements easier by that percent; above 100,
        // harder. Static and player-facing-setting-shaped for the same
        // reason FightController.BeatSpeedMultiplier is — this is also the
        // natural home for a future difficulty setting, not a test-only
        // escape hatch.
        public const int DefaultPercent = 100;
        public static int Percent = DefaultPercent;

        // Entirely in integers, floored the same asymmetric-safe way
        // ItemUpgrade.Apply is — negative inputs are not a real case for a
        // requirement (0 means "none"), but staying direction-symmetric
        // costs nothing and avoids C#'s truncate-toward-zero trap should
        // that ever change.
        public static int Apply(int requirement)
        {
            if (requirement == 0)
            {
                return 0;
            }

            int scaled = requirement * Clamp(Percent);
            return scaled >= 0 ? scaled / 100 : (scaled - 99) / 100;
        }

        public static AbilityScoreBlock Apply(AbilityScoreBlock requirement)
        {
            return new AbilityScoreBlock(
                Apply(requirement.strength),
                Apply(requirement.dexterity),
                Apply(requirement.constitution),
                Apply(requirement.wisdom),
                Apply(requirement.intelligence),
                Apply(requirement.charisma));
        }

        // Percent arrives from a static field a future settings screen could
        // write to freely — never trust it not to have gone negative.
        private static int Clamp(int percent)
        {
            return percent < 0 ? 0 : percent;
        }
    }
}
