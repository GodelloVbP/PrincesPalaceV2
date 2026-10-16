using System.IO;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Which processed sprite a reward-screen state shows
    // (docs/PLAN_REWARD_SCREENS.md section 5). Literal ids and paths: a rename
    // that drops a sprite from the table must fail here, not blank a slot.
    public class RewardArtTests
    {
        [TestCase(false, EncounterClass.Normal, "reward_cache_small")]
        [TestCase(false, EncounterClass.Elite, "reward_cache_medium")]
        [TestCase(false, EncounterClass.Boss, "reward_cache_large")]
        [TestCase(true, EncounterClass.Normal, "reward_relic_small")]
        [TestCase(true, EncounterClass.Elite, "reward_relic_medium")]
        [TestCase(true, EncounterClass.Boss, "reward_relic_large")]
        public void TheCacheSizeFollowsThePagesClass(bool relic, EncounterClass cls, string expected)
        {
            Assert.AreEqual(expected, RewardArt.CacheId(relic, cls));
        }

        [TestCase(0, "glyph_sword")]
        [TestCase(1, "glyph_heart")]
        [TestCase(2, "glyph_shield")]
        [TestCase(3, "glyph_feather")]
        [TestCase(4, "glyph_mana")]
        public void TheHeadlineStatsGlyphFollowsItsAxis(int axis, string expected)
        {
            Assert.AreEqual(expected, RewardArt.DeltaGlyphId(axis));
        }

        [Test]
        public void NoAxisHasNoGlyph()
        {
            Assert.IsNull(RewardArt.DeltaGlyphId(-1));
        }

        [TestCase(null, "map_node_fight")]
        [TestCase(EncounterClass.Normal, "map_node_fight")]
        [TestCase(EncounterClass.Elite, "map_node_elite")]
        [TestCase(EncounterClass.Boss, "map_node_boss")]
        public void TheCrestsMedallionIsTheRoomsClass_AndAnEventFightIsAFight(EncounterClass? cls, string expected)
        {
            Assert.AreEqual(expected, RewardArt.MedallionId(cls));
        }

        [TestCase(PrizeKind.Item, "map_chip_gear")]
        [TestCase(PrizeKind.Relic, "map_chip_relic")]
        [TestCase(PrizeKind.Book, "map_chip_book")]
        [TestCase(PrizeKind.Consumable, "map_chip_potion")]
        [TestCase(PrizeKind.Gold, "map_chip_gold")]
        public void AChipIsTheOneTheMapDrew(PrizeKind shown, string expected)
        {
            Assert.AreEqual(expected, RewardArt.ChipId(shown));
        }

        [Test]
        public void TheFightsOwnGoldHasNoChip()
        {
            Assert.IsNull(RewardArt.ChipId(PrizeKind.None));
        }

        [Test]
        public void TheMapTableHoldsEightFiles_EachOnDisk()
        {
            Assert.AreEqual(8, RewardArt.MapTableIds.Count);
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/Processed/map_node_boss.png",
                RewardArt.MapPathOf("map_node_boss"));
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate the repo root from the working directory.");
            foreach (var id in RewardArt.MapTableIds)
                Assert.IsTrue(File.Exists(Path.Combine(dir.FullName, RewardArt.MapPathOf(id))), id);
        }

        [Test]
        public void ThePathsAreTheProcessedFiles()
        {
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/glyph_whetstone.png", RewardArt.PathOf("glyph_whetstone"));
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_card_back.png", RewardArt.CardBackKey);
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_chain.png", RewardArt.ChainKey);
        }

        [TestCase(1, "reward_plinth_common")]
        [TestCase(2, "reward_plinth_uncommon")]
        [TestCase(3, "reward_plinth_rare")]
        [TestCase(4, "reward_plinth_toptier")]
        public void ThePlinthIsTheQualityStep(int step, string expected)
        {
            Assert.AreEqual(expected, RewardArt.PlinthId(step));
        }

        [TestCase(1, "glyph_up")]
        [TestCase(-3, "glyph_down")]
        [TestCase(0, "glyph_level")]
        public void TheSignGlyph_FollowsTheDeltasSign(int sign, string expected)
        {
            Assert.AreEqual(expected, RewardArt.SignGlyphId(sign));
        }

        [Test]
        public void TheTableHoldsTheCachesGlyphsAndPlinths_EachWithItsFile()
        {
            Assert.AreEqual(26, RewardArt.TableIds.Count);

            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate the repo root from the working directory.");

            foreach (string id in RewardArt.TableIds)
            {
                Assert.IsTrue(File.Exists(Path.Combine(dir.FullName, RewardArt.PathOf(id))), $"{id}: no art at {RewardArt.PathOf(id)}");
            }

            Assert.IsTrue(File.Exists(Path.Combine(dir.FullName, RewardArt.CardBackKey)));
            Assert.IsTrue(File.Exists(Path.Combine(dir.FullName, RewardArt.ChainKey)));
        }
    }
}
