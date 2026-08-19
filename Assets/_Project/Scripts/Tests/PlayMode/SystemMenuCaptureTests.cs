using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Captures the overarching menu open, which no static capture can reach --
    // it is an inactive modal inside another scene's tree, the same situation
    // AUDIT #43 records for the Reckoning.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // SystemMenuCaptureTests.
    public class SystemMenuCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [UnityTest]
        public IEnumerator CaptureTheSkeletonOnEachTab()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController - the menu was never embedded");

            menu.Open();
            menu.Select(2);   // Options, so the shot shows a tab that is not the default
            yield return new WaitForSecondsRealtime(0.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "SystemMenu_skeleton.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[SystemMenuCapture] wrote {path}");
        }
    }
}
