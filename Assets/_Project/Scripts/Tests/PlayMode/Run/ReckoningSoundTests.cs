using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's cues (docs/PLAN_REWARD_SCREENS.md C11): checked by CLIP,
    // not by enum, so a cue pointing at the button click or the refusal buzz
    // fails here even though its name is its own.
    public class ReckoningSoundTests
    {
        private static readonly Sound[] Reckoning =
        {
            Sound.CardDeal, Sound.CacheOpen, Sound.CardFlip, Sound.LegendaryBell, Sound.CoinTick,
            Sound.PotionLand, Sound.BookLand, Sound.Take, Sound.HoneScrape, Sound.EmberSnuff, Sound.XpHum,
            Sound.LevelBell, Sound.Fell, Sound.Endured, Sound.EmberLight, Sound.ChevronNote, Sound.BossPeak,
        };

        [Test]
        public void NoReckoningCue_BorrowsTheButtonClickOrTheRefusalBuzz()
        {
            Assert.AreEqual(17, Reckoning.Length, "vacuity guard: the cue list");
            foreach (var cue in Reckoning)
            {
                string path = SoundLibrary.PathOf(cue);
                Assert.IsNotNull(path, cue + " has no clip");
                Assert.AreNotEqual("Audio/Sfx/button click", path, cue + " is the button click");
                Assert.AreNotEqual("Audio/Sfx/click_fail", path, cue + " is the refusal buzz");
            }
        }

        [Test]
        public void EveryReckoningCue_IsItsOwnAudibleClip()
        {
            var paths = Reckoning.Select(SoundLibrary.PathOf).ToList();
            CollectionAssert.AllItemsAreUnique(paths, "two moments share one clip");
            foreach (var cue in Reckoning)
            {
                var clip = Resources.Load<AudioClip>(SoundLibrary.PathOf(cue));
                Assert.IsNotNull(clip, cue + " has no clip on disk");
                Assert.Greater(clip.length, 0.1f, cue + " is silent or empty");
            }
        }

        // Cut by tools/make_reckoning_cues.py; only CacheOpen reuses a shipped clip.
        [Test]
        public void ReckoningCues_LiveInTheirOwnFolder_ExceptCacheOpen()
        {
            foreach (var cue in Reckoning.Where(c => c != Sound.CacheOpen))
                StringAssert.StartsWith("Audio/Sfx/Reckoning/", SoundLibrary.PathOf(cue), cue.ToString());
            Assert.AreEqual("Audio/Sfx/open_the_pack", SoundLibrary.PathOf(Sound.CacheOpen));
        }
    }
}
