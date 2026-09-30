namespace PrincesPalace.Domain.Content
{
    // Which of the five routes put a skill on a character's strip. The order is
    // the order the CHARACTER tab groups them in, and the order the routes are
    // tried in when a skill is reachable by more than one
    // (ContentDatabase.SkillSourcesFor).
    public enum SkillSource
    {
        // Its unlock level, from the character's own row.
        Innate,

        // A kindled talent named it in grantsSkillId.
        Talent,

        // A spell book learned this descent.
        SpellBook,

        // The character's reward track handed it over and it was collected.
        RewardTrack,

        // Taught outright (Character.unlockedSkillIds), the seam an event
        // teacher writes through.
        Taught,
    }
}
