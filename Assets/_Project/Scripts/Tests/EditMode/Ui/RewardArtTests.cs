using System.IO;
using NUnit.Framework;
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

        [Test]
        public void ThePathsAreTheProcessedFiles()
        {
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/glyph_whetstone.png", RewardArt.PathOf("glyph_whetstone"));
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_card_back.png", RewardArt.CardBackKey);
            Assert.AreEqual("Assets/_Project/Art/UI/Reckoning/Processed/reward_chain.png", RewardArt.ChainKey);
        }

        [Test]
        public void TheTableHoldsTheSixCachesAndThirteenGlyphs_EachWithItsFile()
        {
            Assert.AreEqual(19, RewardArt.TableIds.Count);

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
