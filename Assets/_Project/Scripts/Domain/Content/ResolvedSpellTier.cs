using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated row of the Skill power/cost curve — the shape
    // ContentBuilder needs to create a SpellTierDefinition asset from.
    public readonly struct ResolvedSpellTier
    {
        public readonly int Level;
        public readonly string DisplayName;
        public readonly int ManaCost;
        public readonly float PowerMultiplier;
        public readonly int SortOrder;

        // Which ability scores this tier's spell rides. None for a tier that
        // does not say, which is exactly how every tier behaved before.
        public readonly ScalingProfile Scaling;

        // What a character needs before THIS tier is the one their Skill
        // uses — see RawSpellTierEntry.requires's own comment.
        public readonly AbilityScoreBlock Requirements;

        public ResolvedSpellTier(int level, string displayName, int manaCost, float powerMultiplier, int sortOrder,
            ScalingProfile scaling = default, AbilityScoreBlock requirements = default)
        {
            Level = level;
            DisplayName = displayName;
            ManaCost = manaCost;
            PowerMultiplier = powerMultiplier;
            SortOrder = sortOrder;
            Scaling = scaling;
            Requirements = requirements;
        }
    }
}
