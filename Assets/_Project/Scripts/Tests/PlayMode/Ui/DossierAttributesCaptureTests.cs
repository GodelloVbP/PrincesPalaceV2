using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // Captures the attributes panel WITH AN UNSPENT POINT SHOWING, which no
    // static capture can reach -- same reason DossierPackCaptureTests exists
    // for the pack's own hover comparison: the panel is built Inactive and
    // only PaintAttributesPanel (a runtime call) fills its six rows with the
    // live effect text, so ScreenshotTool's Edit Mode render would show an
    // empty, closed modal at best.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // DossierAttributesCaptureTests.
    public class DossierAttributesCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        // Its own save root, for the reason DossierPackCaptureTests/
        // DossierEquipTests record: without it the panel shows whatever
        // unspent-point count the runner happened to be carrying.
        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-attrs-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator CaptureThePanelWithAnUnspentPoint()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DossierAttributesCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            var save = SaveSlotManager.CurrentSave;
            var character = save?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(character, "no squad member to grant a point to");

            // A REAL, VISIBLE POINT -- three, not one, so the screenshot shows
            // the plus buttons live rather than a coin-flip on whether the
            // one point a fresh character happens to carry survived to this
            // frame.
            character.unspentStatPoints = 3;
            SaveSlotManager.SaveCurrent();

            dossier.Refresh();
            dossier.ShowAttributes(true);
            yield return null;

            var closeButton = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.name == "DossierAttributesClose");
            Assert.IsNotNull(closeButton, "the attributes panel did not open (no DossierAttributesClose found active)");

            // Real seconds, same reason DossierPackCaptureTests waits one full
            // second rather than a frame count: SetShown/panel activation can
            // still be mid-transition on the very next Update.
            yield return new WaitForSecondsRealtime(0.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Dossier_attributes.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[AttributesCapture] wrote {path}");
        }
    }
}
