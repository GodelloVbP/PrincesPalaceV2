using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        private static IEnumerator Capture(string fileName, System.Action afterLoad = null, System.Action afterStart = null)
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run tools/graphics_tests.ps1");
            }

            SharedScene.MarkDirty("captures a freshly loaded menu");
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);

            Time.captureFramerate = CaptureFps;
            for (float elapsed = 0f; elapsed < SettleSeconds; elapsed += 1f / CaptureFps) yield return null;

            if (afterLoad != null)
            {
                afterLoad();
                for (float elapsed = 0f; elapsed < SettleSeconds; elapsed += 1f / CaptureFps) yield return null;
            }

            if (afterStart != null)
            {
                // A modal's controller Start runs a frame after SetActive, so
                // its listeners exist only from the next frame on.
                yield return null;
                yield return null;
                afterStart();
                for (float elapsed = 0f; elapsed < SettleSeconds; elapsed += 1f / CaptureFps) yield return null;
            }

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

        // A non-first item lit, to see the lit colour against an idle column.
        [UnityTest]
        public IEnumerator MainMenu_WithASave_LoadSelected()
        {
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            yield return Capture("main_menu_load_selected.png", afterLoad: () =>
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Named("LoadButton").gameObject));
        }

        private static void SaveSlots(params int[] slots)
        {
            foreach (int slot in slots)
            {
                SaveSlotManager.EnterSlot(slot);
                SaveSlotManager.Forget();
            }
        }

        private static Button Named(string name) =>
            Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == name && b.gameObject.scene.IsValid());

        [UnityTest]
        public IEnumerator LoadList_WithTwoOfFiveSlotsFilled()
        {
            SaveSlots(0, 2);
            yield return Capture("load_list_two_saves.png", () => Named("LoadButton").onClick.Invoke());
        }

        [UnityTest]
        public IEnumerator SettingsModal_OverTheMenu()
        {
            SaveSlots(0);
            yield return Capture("settings_modal.png", () => Named("SettingsButton").onClick.Invoke());
        }

        [UnityTest]
        public IEnumerator DeleteConfirm_OverTheLoadList()
        {
            SaveSlots(0, 2);
            yield return Capture("delete_confirm.png",
                () => Named("LoadButton").onClick.Invoke(),
                () => Named("Slot2DeleteButton").onClick.Invoke());
        }
    }
}
