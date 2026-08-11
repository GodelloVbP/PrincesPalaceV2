using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // Every achievement the game knows how to award.
    //
    // A validated string list rather than an enum, for one reason: relics.json
    // names achievements, and an enum in Domain that content has to match by
    // name gives exactly the same typo surface as a string with a whitelist --
    // but a string survives a save file, an enum ordinal does not. Achievement
    // ids end up in save data the moment anything is earned, and an enum whose
    // members get reordered would silently re-map what a player has done.
    //
    // The whitelist is what keeps it honest: RelicEntryResolver rejects an
    // unknown `unlockedBy` at CONTENT BUILD time, so a typo is a build failure
    // rather than a relic that can never unlock and never says why.
    public static class AchievementIds
    {
        // The first real gate. Named for the stage rather than for a boss id
        // so re-skinning floor 1's boss does not orphan every save that had
        // already earned it.
        public const string FirstForestBoss = "first_forest_boss";

        // Registered here, not implemented yet -- listed so content can be
        // authored against them and so the glossary has something to show as
        // locked. Awarding them is Core's job and lands with the achievement
        // system proper.
        public const string CharacterLevel30 = "character_level_30";
        public const string ClearARunWithoutLosingACharacter = "flawless_run";

        public static readonly IReadOnlyList<string> All = new[]
        {
            FirstForestBoss,
            CharacterLevel30,
            ClearARunWithoutLosingACharacter,
        };

        public static bool IsKnown(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == id) return true;
            }

            return false;
        }
    }
}
