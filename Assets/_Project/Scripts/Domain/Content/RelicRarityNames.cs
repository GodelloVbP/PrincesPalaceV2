namespace PrincesPalace.Domain.Content
{
    // What a rarity band is CALLED.
    //
    // Spelled out rather than ToString(), because UltraRare has to read as
    // "ULTRA-RARE" and an enum name is not a display name. One home, so the
    // draft controller and the glossary adapter cannot drift apart.
    public static class RelicRarityNames
    {
        // The hex a relic's rarity is drawn in. Engine-free: the Unity side
        // parses it once (RarityColors).
        public static string HexColor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return "#9A93A8";
                case RelicRarity.Uncommon: return "#6FBF73";
                case RelicRarity.Rare: return "#5AA6E8";
                case RelicRarity.UltraRare: return "#B48CFF";
                case RelicRarity.Mythic: return "#F0913D";
                default: return "#FFD76B";
            }
        }

        public static string Of(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return "COMMON";
                case RelicRarity.Uncommon: return "UNCOMMON";
                case RelicRarity.Rare: return "RARE";
                case RelicRarity.UltraRare: return "ULTRA-RARE";
                case RelicRarity.Mythic: return "MYTHIC";
                default: return "GODLIKE";
            }
        }
    }
}
