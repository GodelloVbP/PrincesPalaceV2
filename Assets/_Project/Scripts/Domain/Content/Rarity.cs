namespace PrincesPalace.Domain.Content
{
    // What a tier looks like to the player.
    //
    // Tier is a number from 0 to 10 and numbers do not read at a glance;
    // rarity is the band that number falls in, and it is what carries the
    // colour. Every item name in the game is currently the same white, which
    // means a Mythic drop and a starting coif are typographically identical
    // — this is the cheapest readability win available.
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic,
    }

    // The tier -> rarity -> colour mapping, pure and engine-free.
    //
    // Colours are HEX STRINGS rather than UnityEngine.Color because Domain is
    // noEngineReferences: true. The Unity side parses them once with
    // ColorUtility.TryParseHtmlString — see RarityColors.
    public static class RarityBands
    {
        // Two tiers per band, except Mythic which is tier 10 alone.
        //
        // Mythic being a single tier is the point: the bands are otherwise
        // even, so the top of the ladder is the one place where going up one
        // tier changes what the item is called. A band two wide there would
        // make tier 9 and tier 10 the same word, and tier 10 is the whole
        // reason a player is still descending.
        public static Rarity For(int tier)
        {
            if (tier <= 1)
            {
                return Rarity.Common;
            }

            if (tier <= 3)
            {
                return Rarity.Uncommon;
            }

            if (tier <= 5)
            {
                return Rarity.Rare;
            }

            if (tier <= 7)
            {
                return Rarity.Epic;
            }

            return tier <= 9 ? Rarity.Legendary : Rarity.Mythic;
        }

        // The lowest tier that falls in a band. The inverse of For, used by
        // the reward tables to turn "a boss never drops below Uncommon" into
        // a number without restating the boundaries.
        public static int FloorTierOf(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return 0;
                case Rarity.Uncommon: return 2;
                case Rarity.Rare: return 4;
                case Rarity.Epic: return 6;
                case Rarity.Legendary: return 8;
                default: return 10;
            }
        }

        public static string HexColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return "#9A93A8";
                case Rarity.Uncommon: return "#6FBF73";
                case Rarity.Rare: return "#5AA6E8";
                case Rarity.Epic: return "#B48CFF";
                case Rarity.Legendary: return "#F0913D";
                default: return "#FFD76B";
            }
        }

        public static string HexColorForTier(int tier)
        {
            return HexColor(For(tier));
        }

        // "Legendary". Its own method rather than ToString() at every call
        // site so a band that later wants a name unlike its enum member
        // ("Mythic" -> "Sovereign", say) changes in one place.
        public static string DisplayName(Rarity rarity)
        {
            return rarity.ToString();
        }
    }
}
