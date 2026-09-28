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

        // ---- gear requirements are OFF ------------------------------------
        //
        // HIDDEN, NOT DELETED, and deliberately so: the authored requirements
        // are still on every item, RequirementResolver still settles a loadout
        // with them, and flipping this back on restores the whole system
        // untouched. Nothing has been removed from content.
        //
        // Scoped to GEAR rather than done with Percent = 0, and that is the
        // whole reason this flag exists instead. Percent is the knob for EVERY
        // requirement in the game at once -- skills and spell tiers gate on it
        // too (FightHudModel and ContentDatabase's tier pick) -- so zeroing it
        // would silently unlock those as well, which was not the ask.
        //
        // The display costs nothing extra: ItemStatLines.RequirementLine
        // already returns an empty string when nothing is required, so a
        // zeroed block removes the "Requires STR 15" line and the CAN'T WEAR
        // state on its own rather than needing a second switch that could
        // disagree with this one.
        public static bool GearRequirementsEnabled = false;

        // What a piece of GEAR demands. The one seam the two gear call sites
        // go through -- the equipment loadout in ContentDatabase.Effective and
        // the comparison line in ItemDescription -- so "are gear requirements
        // on" is answered in exactly one place.
        public static AbilityScoreBlock ApplyGear(AbilityScoreBlock requirement)
        {
            if (!GearRequirementsEnabled) return new AbilityScoreBlock(0, 0, 0, 0, 0, 0);
            return Apply(requirement);
        }

        // Entirely in integers, FLOORED in both directions — negative inputs
        // are not a real case for a requirement (0 means "none"), but staying
        // direction-symmetric costs nothing and avoids C#'s
        // truncate-toward-zero trap should that ever change.
        //
        // NOT the same rounding as ItemUpgrade.Apply: that one rounds AWAY
        // FROM ZERO (its `+ 99` against this one's plain `/ 100`), because a +1 that granted the
        // same numbers as a +0 was a lie on 58% of the catalogue. A
        // requirement has the opposite bias — rounding a gate UP would make
        // an item unwearable that its authored number says is wearable — so
        // the two deliberately differ, and only the negative branch is
        // shared.
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
