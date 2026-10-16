using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;
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

        // The back's drawn shape, width over height (492x768).
        public const float CardBackAspect = 492f / 768f;

        // The cloth under the whole Reckoning, 3:2 (ReckoningScreen.PanelWidth/Height).
        public const string SpreadKey = Dir + "reward_spread.png";

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

        // The delta line's sign: the up-chevron, the down-chevron, the level bar.
        public const string Up = "glyph_up";
        public const string Down = "glyph_down";
        public const string Level = "glyph_level";

        // The plinth under a card's item, by quality step (C5): one 600x499
        // canvas per step, bottom-aligned, so the art carries the size.
        public const string PlinthCommon = "reward_plinth_common";
        public const string PlinthUncommon = "reward_plinth_uncommon";
        public const string PlinthRare = "reward_plinth_rare";
        public const string PlinthTopTier = "reward_plinth_toptier";
        public const float PlinthAspect = 600f / 499f;

        // Every id the Reckoning's table holds.
        public static readonly IReadOnlyList<string> TableIds = new[]
        {
            CacheSmall, CacheMedium, CacheLarge, RelicSmall, RelicMedium, RelicLarge,
            Whetstone, Coins, Door, Sword, Shield, Mana, Heart, Feather,
            HeartCracked, ShieldCracked, Leaf, Skull, Potion,
            Up, Down, Level, PlinthCommon, PlinthUncommon, PlinthRare, PlinthTopTier,
        };

        // THE MAP'S OWN ART, which the Reckoning shows as the map showed it
        // (C1 medallions, C2 chips): its own table, wired in WireReckoning,
        // since the Map scene bakes these by key and the Fight scene has no
        // copy. Ids are the file stems.
        public const string MapDir = "Assets/_Project/Art/Backgrounds/Processed/";
        public const string MedallionFight = "map_node_fight";
        public const string MedallionElite = "map_node_elite";
        public const string MedallionBoss = "map_node_boss";
        public const string ChipGold = "map_chip_gold";
        public const string ChipPotion = "map_chip_potion";
        public const string ChipGear = "map_chip_gear";
        public const string ChipRelic = "map_chip_relic";
        public const string ChipBook = "map_chip_book";

        public static readonly IReadOnlyList<string> MapTableIds = new[]
        {
            MedallionFight, MedallionElite, MedallionBoss, ChipGold, ChipPotion, ChipGear, ChipRelic, ChipBook,
        };

        public static string MapPathOf(string id) => MapDir + id + ".png";

        // The crest's medallion: the room's class, and the plain fight
        // medallion when no room was fought (an event fight, the outcome form).
        public static string MedallionId(EncounterClass? encounter)
        {
            switch (encounter)
            {
                case EncounterClass.Boss: return MedallionBoss;
                case EncounterClass.Elite: return MedallionElite;
                default: return MedallionFight;
            }
        }

        // The chip the map showed for a prize, or null for the fight's own gold.
        public static string ChipId(PrizeKind shown)
        {
            switch (shown)
            {
                case PrizeKind.Gold: return ChipGold;
                case PrizeKind.Consumable: return ChipPotion;
                case PrizeKind.Item: return ChipGear;
                case PrizeKind.Relic: return ChipRelic;
                case PrizeKind.Book: return ChipBook;
                default: return null;
            }
        }

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

        // The plinth for a quality step, 1 (Common) to 4 (top tier).
        public static string PlinthId(int step)
        {
            switch (step)
            {
                case 2: return PlinthUncommon;
                case 3: return PlinthRare;
                case 4: return PlinthTopTier;
                default: return PlinthCommon;
            }
        }

        // The delta line's sign glyph: up, down, or level for no change.
        public static string SignGlyphId(int sign) => sign > 0 ? Up : sign < 0 ? Down : Level;

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
