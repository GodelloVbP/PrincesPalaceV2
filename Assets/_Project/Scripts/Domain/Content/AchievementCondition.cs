namespace PrincesPalace.Domain.Content
{
    // How an achievement is earned.
    //
    // UNLIKE RelicEffect, this genuinely generalises, and that is why it is a
    // data-driven table rather than a switch full of special cases. Every
    // achievement worth having is some variant of "count a thing, compare it to
    // a threshold" -- bosses put down, levels reached, rooms cleared, damage
    // dealt. The relic effects resisted a general system because "hit twice"
    // and "take another turn" have no shared shape; these have exactly one.
    //
    // Adding an achievement is therefore a line of JSON whenever it fits a
    // condition already here, and a new value plus one case in
    // AchievementProgress when it does not.
    public enum AchievementCondition
    {
        // Never satisfiable. The default, so an achievement authored without a
        // condition is inert rather than accidentally earned on the first
        // frame -- the same reasoning as RelicEffect.None sitting at zero.
        Never = 0,

        // A specific boss has been put down at least once, ever. `parameter`
        // names the boss id; `threshold` is ignored.
        DefeatSpecificBoss,

        // Any `threshold` distinct bosses have been put down, ever.
        DefeatDistinctBosses,

        // Some character on the roster has reached `threshold`.
        ReachCharacterLevel,

        // `threshold` rooms cleared across all runs.
        ClearRooms,

        // Reached step `threshold` or deeper in a single run.
        ReachDepth,

        // `threshold` total damage dealt by the party across all runs.
        DealTotalDamage,
    }
}
