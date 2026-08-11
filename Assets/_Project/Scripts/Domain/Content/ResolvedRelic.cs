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
        public readonly System.Collections.Generic.IReadOnlyList<RelicModifier> Modifiers;

        // A relic has to DO something, one way or the other, to be worth
        // offering. Asked here so the draft and the glossary can both tell a
        // real relic from a placeholder without re-deriving the rule.
        public bool HasBehaviour =>
            Effect != RelicEffect.None || (Modifiers != null && Modifiers.Count > 0);

        // Empty UnlockedBy means available from the first run. Expressed as a
        // property rather than left for each caller to test the string, so
        // "is this locked" has one definition.
        public bool IsUnlockedFromTheStart => string.IsNullOrEmpty(UnlockedBy);

        public ResolvedRelic(string id, string displayName, string description, RelicEffect effect, int sortOrder,
                             string iconPath = "", RelicRarity rarity = RelicRarity.Common, string unlockedBy = "",
                             System.Collections.Generic.IReadOnlyList<RelicModifier> modifiers = null)
        {
            Rarity = rarity;
            UnlockedBy = unlockedBy ?? "";
            Modifiers = modifiers ?? System.Array.Empty<RelicModifier>();
            Id = id;
            DisplayName = displayName;
            Description = description;
            Effect = effect;
            SortOrder = sortOrder;
            IconPath = iconPath;
        }
    }
}
