using NUnit.Framework;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's placeholder cues (docs/PLAN_REWARD_SCREENS.md C11):
    // checked by CLIP, not by enum, so a placeholder pointing at the button
    // click or the refusal buzz fails here even though its name is its own.
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
    }
}
