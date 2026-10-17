using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace
{
    // The Unity-side face of the two rarity ladders: a gear copy's Quality
    // and a relic's RelicRarity.
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
        private static readonly Color[] QualityCache = new Color[System.Enum.GetValues(typeof(Quality)).Length];
        private static readonly Color[] RelicCache = new Color[System.Enum.GetValues(typeof(RelicRarity)).Length];
        private static bool _parsed;

        public static Color For(Quality quality)
        {
            EnsureParsed();
            int index = (int)quality;
            return index >= 0 && index < QualityCache.Length ? QualityCache[index] : Color.white;
        }

        // A copy content cannot resolve is drawn in the neutral colour rather
        // than skipped — same graceful-degradation posture the rest of the
        // content layer takes on missing ids.
        public static Color For(ItemInstance copy)
        {
            return ContentDatabase.DefinitionOf(copy) == null ? Color.white : For(ContentDatabase.QualityOf(copy));
        }

        public static Color For(RelicRarity rarity)
        {
            EnsureParsed();
            int index = (int)rarity;
            return index >= 0 && index < RelicCache.Length ? RelicCache[index] : Color.white;
        }

        // The ring behind a copy that rolled anything: Common draws none, so
        // an ordinary item looks exactly as it did before affixes existed.
        public static bool ShouldGlow(Quality quality) => quality > Quality.Common;

        // The name a player should see for one particular copy: the tier's
        // adjective with the slot's honing appended.
        public static string NameOf(ItemInstance copy, int plus = 0)
        {
            return copy == null ? "" : ItemNaming.WithPlus(ContentDatabase.NameOf(copy), plus);
        }

        // The same name, wrapped in a uGUI rich-text colour tag — for names
        // embedded in a longer string, where setting Text.color would tint
        // the whole line.
        public static string Wrap(ItemInstance copy, int plus = 0)
        {
            if (ContentDatabase.DefinitionOf(copy) == null)
            {
                return "";
            }

            string hex = ItemQuality.HexColor(ContentDatabase.QualityOf(copy));
            return $"<color={hex}>{NameOf(copy, plus)}</color>";
        }

        private static void EnsureParsed()
        {
            if (_parsed)
            {
                return;
            }

            // A malformed hex leaves the colour white rather than black,
            // which would read as real, wrong information.
            for (int i = 0; i < QualityCache.Length; i++)
            {
                QualityCache[i] = ColorUtility.TryParseHtmlString(ItemQuality.HexColor((Quality)i), out var parsed)
                    ? parsed
                    : Color.white;
            }

            for (int i = 0; i < RelicCache.Length; i++)
            {
                RelicCache[i] = ColorUtility.TryParseHtmlString(RelicRarityNames.HexColor((RelicRarity)i), out var parsed)
                    ? parsed
                    : Color.white;
            }

            _parsed = true;
        }
    }
}
