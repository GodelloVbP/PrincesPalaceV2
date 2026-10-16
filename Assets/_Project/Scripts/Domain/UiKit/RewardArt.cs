using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit
{
    // The reward screens' processed art (docs/PLAN_REWARD_SCREENS.md section 5)
    // and the pure rules that pick which sprite a state shows.
    //
    // Static art (the card back, the chain) is keyed straight into the tree.
    // Art a controller swaps at runtime (the caches by size, every glyph)
    // lives in one IconEntry table the Reckoning wires at build time; its ids
    // are the file stems below, and ItemIcons.Apply reads it.
    public static class RewardArt
    {
        public const string Dir = "Assets/_Project/Art/UI/Reckoning/Processed/";
        public const string CardBackKey = Dir + "reward_card_back.png";
        public const string ChainKey = Dir + "reward_chain.png";

        public const string CacheSmall = "reward_cache_small";
        public const string CacheMedium = "reward_cache_medium";
        public const string CacheLarge = "reward_cache_large";
        public const string RelicSmall = "reward_relic_small";
        public const string RelicMedium = "reward_relic_medium";
        public const string RelicLarge = "reward_relic_large";

        public const string Whetstone = "glyph_whetstone";
        public const string Coins = "glyph_coins";
        public const string Door = "glyph_door";
        public const string Sword = "glyph_sword";
        public const string Shield = "glyph_shield";
        public const string Mana = "glyph_mana";
        public const string Heart = "glyph_heart";
        public const string Feather = "glyph_feather";

        // Registered for the board (docs/PLAN_REWARD_SCREENS.md C8, phase R5):
        // deeds, the fell mark, potions.
        public const string HeartCracked = "glyph_heart_cracked";
        public const string ShieldCracked = "glyph_shield_cracked";
        public const string Leaf = "glyph_leaf";
        public const string Skull = "glyph_skull";
        public const string Potion = "glyph_potion";

        // Every id the Reckoning's table holds.
        public static readonly IReadOnlyList<string> TableIds = new[]
        {
            CacheSmall, CacheMedium, CacheLarge, RelicSmall, RelicMedium, RelicLarge,
            Whetstone, Coins, Door, Sword, Shield, Mana, Heart, Feather,
            HeartCracked, ShieldCracked, Leaf, Skull, Potion,
        };

        // Glyphs are drawn square at this size on every face.
        public const float GlyphSize = 24f;

        public static string PathOf(string id) => Dir + id + ".png";

        // The cache's size is the page's class: small for a normal fight,
        // medium for an elite, large for a boss. A relic page uses the tins.
        public static string CacheId(bool relicPage, EncounterClass sourceClass)
        {
            switch (sourceClass)
            {
                case EncounterClass.Boss: return relicPage ? RelicLarge : CacheLarge;
                case EncounterClass.Elite: return relicPage ? RelicMedium : CacheMedium;
                default: return relicPage ? RelicSmall : CacheSmall;
            }
        }

        // The headline stat's glyph, by GearEvaluator.LargestDelta's axis:
        // 0 damage, 1 health, 2 defence, 3 speed, anything else mana. Null
        // for no axis (-1).
        public static string DeltaGlyphId(int axis)
        {
            switch (axis)
            {
                case 0: return Sword;
                case 1: return Heart;
                case 2: return Shield;
                case 3: return Feather;
                case -1: return null;
                default: return Mana;
            }
        }
    }
}
