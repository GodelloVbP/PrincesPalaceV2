namespace PrincesPalace.Domain.Content
{
    // How good a gear copy is, separate from its tier (docs/PLAN_ITEMS.md
    // 1.4). Derived from what rolled on the copy, never stored: a copy's
    // ModifierIds are the only input, so a stored quality could only ever
    // disagree with them.
    public enum Quality
    {
        Common,
        Uncommon,
        Rare,
        Legendary,
    }

    public static class ItemQuality
    {
        // Plan 1.2 step 8: Uncommon one affix, Rare two (or three, when no
        // trait was left to draw), Legendary two plus a trait.
        public static Quality Of(int affixes, int traits)
        {
            if (traits > 0) return Quality.Legendary;
            if (affixes >= 2) return Quality.Rare;
            return affixes == 1 ? Quality.Uncommon : Quality.Common;
        }

        // Hex strings because Domain is engine-free; RarityColors parses them.
        public static string HexColor(Quality quality)
        {
            switch (quality)
            {
                case Quality.Uncommon: return "#6FBF73";
                case Quality.Rare: return "#5AA6E8";
                case Quality.Legendary: return "#F0913D";
                default: return "#9A93A8";
            }
        }

        public static string DisplayName(Quality quality) => quality.ToString();
    }
}
