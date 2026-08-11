namespace PrincesPalace.Domain.Content
{
    // What a rarity band is CALLED.
    //
    // Spelled out rather than ToString(), because UltraRare has to read as
    // "ULTRA-RARE" and an enum name is not a display name. Written out twice
    // within an hour -- once in the draft controller, once in the glossary
    // adapter -- which is two places for the same six strings to drift.
    public static class RelicRarityNames
    {
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
