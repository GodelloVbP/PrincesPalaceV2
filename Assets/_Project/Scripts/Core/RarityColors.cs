using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace
{
    // The Unity-side face of RarityBands.
    //
    // Domain is engine-free, so it hands out hex strings; this is the one
    // place they become a UnityEngine.Color, and the one place an item's
    // name becomes a coloured, plus-suffixed string. Every screen that shows
    // an item name goes through Label/Wrap, so a Legendary drop cannot read
    // as white on one screen and orange on another.
    //
    // Parsed values are cached because ColorUtility.TryParseHtmlString is not
    // free and the inventory redraws every one of its rows on each Refresh.
    public static class RarityColors
    {
        private static readonly Color[] Cache = new Color[6];
        private static bool _parsed;

        private static Color ForTier(int tier)
        {
            return For(RarityBands.For(tier));
        }

        public static Color For(Rarity rarity)
        {
            EnsureParsed();
            int index = (int)rarity;
            return index >= 0 && index < Cache.Length ? Cache[index] : Color.white;
        }

        public static Color For(ItemDefinition item)
        {
            // A null item is drawn in the neutral colour rather than skipped
            // — same graceful-degradation posture the rest of the content
            // layer takes on missing ids.
            return item == null ? Color.white : ForTier(item.tier);
        }

        // The name a player should see for one particular copy: the tier's
        // adjective (already baked into displayName) with this instance's
        // plus appended.
        public static string NameOf(ItemDefinition item, int plus = 0)
        {
            if (item == null)
            {
                return "";
            }

            return ItemNaming.WithPlus(item.displayName, plus);
        }

        // The same name, wrapped in a uGUI rich-text colour tag — for names
        // embedded in a longer string, where setting Text.color would tint
        // the whole line.
        public static string Wrap(ItemDefinition item, int plus = 0)
        {
            if (item == null)
            {
                return "";
            }

            return $"<color={RarityBands.HexColorForTier(item.tier)}>{NameOf(item, plus)}</color>";
        }

        private static void EnsureParsed()
        {
            if (_parsed)
            {
                return;
            }

            for (int i = 0; i < Cache.Length; i++)
            {
                // A malformed hex leaves the colour black rather than
                // throwing, which would be an invisible label. White is the
                // safer failure, and RarityBandsTests pins the strings so
                // this branch should be unreachable.
                Cache[i] = ColorUtility.TryParseHtmlString(RarityBands.HexColor((Rarity)i), out var parsed)
                    ? parsed
                    : Color.white;
            }

            _parsed = true;
        }
    }
}
