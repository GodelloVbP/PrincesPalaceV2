namespace PrincesPalace.Domain.Content
{
    // One validated relic — the shape ContentBuilder needs to create a
    // RelicDefinition asset from.
    public readonly struct ResolvedRelic
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly RelicEffect Effect;
        public readonly int SortOrder;
        public readonly string IconPath;
        public readonly RelicRarity Rarity;
        public readonly string UnlockedBy;

        // Empty UnlockedBy means available from the first run. Expressed as a
        // property rather than left for each caller to test the string, so
        // "is this locked" has one definition.
        public bool IsUnlockedFromTheStart => string.IsNullOrEmpty(UnlockedBy);

        public ResolvedRelic(string id, string displayName, string description, RelicEffect effect, int sortOrder,
                             string iconPath = "", RelicRarity rarity = RelicRarity.Common, string unlockedBy = "")
        {
            Rarity = rarity;
            UnlockedBy = unlockedBy ?? "";
            Id = id;
            DisplayName = displayName;
            Description = description;
            Effect = effect;
            SortOrder = sortOrder;
            IconPath = iconPath;
        }
    }
}
