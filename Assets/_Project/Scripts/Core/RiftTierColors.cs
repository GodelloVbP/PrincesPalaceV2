using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace
{
    // The Unity-side face of RiftTierBands -- the RiftTier twin of
    // RarityColors, and deliberately its OWN cache rather than RarityColors
    // resized.
    //
    // RarityColors.Cache is `Color[6]`, indexed by `(int)Rarity` -- a table
    // built and sized for exactly one enum. RiftTier is a second, independent
    // axis (see RiftTier.cs's own header): an item's NAME is coloured by its
    // Rarity, as it always has been, and a rolled item's GLOW is coloured by
    // its RiftTier on top of that -- two colours on one card, from two tables,
    // never one slot doing both jobs. Sharing a cache sized for one enum's
    // ordinal range between two different enums is exactly the class of bug
    // that would let a Rare item's rarity colour (index 2) silently answer for
    // RiftForged (also index 2) the day their ordinals happened to line up --
    // a same-sized sibling table makes that coincidence impossible rather than
    // merely unlikely.
    public static class RiftTierColors
    {
        private static readonly Color[] Cache = new Color[4];
        private static bool _parsed;

        public static Color For(RiftTier tier)
        {
            EnsureParsed();
            int index = (int)tier;
            return index >= 0 && index < Cache.Length ? Cache[index] : Color.white;
        }

        // Ordinary is the overwhelming majority of items -- everything before
        // this system existed, and everything that rolls no modifiers after it
        // -- and it must draw EXACTLY as it did before this phase: no ring, no
        // badge, nothing. Every card-painting call site gates its glow node on
        // this rather than inlining `tier > RiftTier.Ordinary`, so the one rule
        // "Ordinary never glows" lives in one place.
        public static bool ShouldGlow(RiftTier tier) => tier > RiftTier.Ordinary;

        public static string DisplayName(RiftTier tier) => RiftTierBands.DisplayName(tier);

        private static void EnsureParsed()
        {
            if (_parsed)
            {
                return;
            }

            for (int i = 0; i < Cache.Length; i++)
            {
                // Same defensive fallback as RarityColors.EnsureParsed: a
                // malformed hex reads as white (invisible-ish, safe) rather
                // than black (which would look like real, wrong information).
                Cache[i] = ColorUtility.TryParseHtmlString(RiftTierBands.HexColor((RiftTier)i), out var parsed)
                    ? parsed
                    : Color.white;
            }

            _parsed = true;
        }
    }
}
