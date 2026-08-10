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

        public ResolvedRelic(string id, string displayName, string description, RelicEffect effect, int sortOrder, string iconPath = "")
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Effect = effect;
            SortOrder = sortOrder;
            IconPath = iconPath;
        }
    }
}
