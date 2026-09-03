using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // GameSettings' own model/persistence -- balance-bot item 7 (2026-09-03)
    // asked for a "Fullscreen" row (Windowed / Borderless Fullscreen /
    // Exclusive), persisted with the other options and applied immediately.
    // That row already exists: OptionRows' "window" stepper, GameSettings.
    // WindowModeIndex/WindowModeLabels ("Windowed", "Borderless Windowed",
    // "Fullscreen"), applied through Screen.SetResolution with the matching
    // FullScreenMode in Apply() (skipped only in batch mode, where there is
    // no real window to resize -- see Apply's own comment).
    //
    // IN PLAYMODE, NOT EDITMODE, despite the handoff's own wording -- this
    // project's EditMode test assembly (PrincesPalace.Domain.Tests.asmdef)
    // references only PrincesPalace.Domain, never Core, so GameSettings (a
    // Core type, for the same reason SaveData is -- see SaveDataSquadOfThree
    // Tests' own header) is not reachable from an EditMode test at all. This
    // is the model/persistence test the handoff asked for, in the assembly
    // that can actually compile it; OptionsPaneTests already covered the
    // pane wiring on top of it.
    public class GameSettingsTests
    {
        private int _savedIndex;

        [SetUp]
        public void SaveRealValue()
        {
            // GameSettings is a static, process-wide model -- capture,
            // mutate, restore, so this test cannot leave the runner's own
            // settings different from how it found them.
            _savedIndex = GameSettings.WindowModeIndex;
        }

        [TearDown]
        public void RestoreRealValue()
        {
            GameSettings.SetWindowModeIndex(_savedIndex);
        }

        [Test]
        public void ThreeWindowModesAreLabelledWindowedBorderlessAndFullscreen()
        {
            CollectionAssert.AreEqual(
                new[] { "Windowed", "Borderless Windowed", "Fullscreen" },
                GameSettings.WindowModeLabels);
        }

        [Test]
        public void SetWindowModeIndexPersistsAcrossAReload()
        {
            int target = (GameSettings.DefaultWindowModeIndex + 1) % GameSettings.WindowModeLabels.Length;
            GameSettings.SetWindowModeIndex(target);
            Assert.AreEqual(target, GameSettings.WindowModeIndex);

            // Save() already ran inside SetWindowModeIndex; Load() re-reads
            // PlayerPrefs the way a fresh process boot would.
            GameSettings.Load();
            Assert.AreEqual(target, GameSettings.WindowModeIndex,
                "the window mode should survive a Load() the way a real relaunch would trigger one");
        }

        [Test]
        public void SetWindowModeIndexClampsOutOfRangeValues()
        {
            GameSettings.SetWindowModeIndex(99);
            Assert.AreEqual(GameSettings.WindowModeLabels.Length - 1, GameSettings.WindowModeIndex);

            GameSettings.SetWindowModeIndex(-5);
            Assert.AreEqual(0, GameSettings.WindowModeIndex);
        }

        [Test]
        public void RestoringDefaultsGoesBackToBorderlessWindowed()
        {
            GameSettings.SetWindowModeIndex(2);
            GameSettings.SetWindowModeIndex(GameSettings.DefaultWindowModeIndex);
            Assert.AreEqual(1, GameSettings.WindowModeIndex);
            Assert.AreEqual("Borderless Windowed", GameSettings.WindowModeLabels[GameSettings.WindowModeIndex]);
        }
    }
}
