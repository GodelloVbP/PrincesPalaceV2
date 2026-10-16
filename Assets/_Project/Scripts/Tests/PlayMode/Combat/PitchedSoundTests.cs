using NUnit.Framework;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // Pitch is a property of an AudioSource, so a pitched shot has to live on
    // its own source or it retunes whatever is still ringing. These pin that
    // the pool never touches the main source, never cuts a playing shot, and
    // gives each shot the pitch it asked for.
    public class PitchedSoundTests
    {
        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        [Test]
        public void ASecondPitchedShot_DoesNotCutOffTheFirst()
        {
            SoundController.Play(Sound.CardFlip, 1.5f);
            SoundController.Play(Sound.CardFlip, 2f);

            Assert.GreaterOrEqual(SoundController.PitchSourceCount, 2);
            Assert.AreEqual(2, SoundController.PitchSourcesPlaying);
        }

        [Test]
        public void EachShotKeepsItsOwnPitch_AndTheMainSourceIsUntouched()
        {
            SoundController.Play(Sound.CardFlip, 1.5f);
            SoundController.Play(Sound.CardFlip, 2f);
            SoundController.Play(Sound.ButtonClick);

            var pitches = SoundController.PitchSourcePitches();
            CollectionAssert.Contains(pitches, 1.5f);
            CollectionAssert.Contains(pitches, 2f);
            Assert.AreEqual(1f, SoundController.MainSourcePitch);
        }

        [Test]
        public void APitchedSource_IsRestoredToOne_OnceItsShotEnds()
        {
            SoundController.Play(Sound.CardFlip, 1.5f);
            SoundController.StopPitched();

            // Update runs on the controller each frame; let one frame pass.
            Assert.AreEqual(0, SoundController.PitchSourcesPlaying);
            SoundController.RestoreIdlePitches();
            CollectionAssert.AreEqual(new[] { 1f }, SoundController.PitchSourcePitches());
        }

        [Test]
        public void PitchedPlay_ReportsToTheObserver_LikeAnyOtherPlay()
        {
            var seen = new System.Collections.Generic.List<Sound>();
            SoundController.Observer = s => seen.Add(s);

            SoundController.Play(Sound.Take, 1.2f);

            CollectionAssert.AreEqual(new[] { Sound.Take }, seen);
        }
    }
}
