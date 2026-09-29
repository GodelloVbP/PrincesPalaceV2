using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // The main menu with and without a save, for owner review of the Continue
    // row, the column reflow and the left-edge wash (docs/PLAN_MAIN_MENU.md).
    // Pictures, not assertions: MainMenuGamepadNavigationTests pins the
    // behaviour.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.MainMenuCaptureTests
    // PNGs land in tools/screenshots/runtime/main_menu/ in the runner copy.
    public class MainMenuCaptureTests
    {
        private const int CaptureFps = 60;
        private const float SettleSeconds = 1.5f;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-mainmenu-capture-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Time.captureFramerate = 0;
            Navigation.Reset();
            SaveSlotManager.Forget();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "main_menu"));

        private static IEnumerator Capture(string fileName)
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run tools/graphics_tests.ps1");
            }

            SharedScene.MarkDirty("captures a freshly loaded menu");
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);

            Time.captureFramerate = CaptureFps;
            for (float elapsed = 0f; elapsed < SettleSeconds; elapsed += 1f / CaptureFps) yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .First(c => c.isRootCanvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, fileName);
            CanvasCapture.RenderToFile(canvas, path);
            FileAssert.Exists(path);
        }

        [UnityTest]
        public IEnumerator MainMenu_WithoutASave()
        {
            yield return Capture("main_menu_no_save.png");
        }

        [UnityTest]
        public IEnumerator MainMenu_WithASave()
        {
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Assert.IsNotNull(SaveSlotManager.CurrentSave);
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            yield return Capture("main_menu_with_save.png");
        }
    }
}
