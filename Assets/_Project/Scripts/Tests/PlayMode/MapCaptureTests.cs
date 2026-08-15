using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Captures the map WITH A LEG ON IT.
    //
    // The Edit-Mode screenshot tool renders a scene at rest, and at rest this
    // screen is a pool of hidden rooms and an unscrolled wood -- correct, and
    // nothing like what the player sees. Every interesting property of this
    // screen (which clearing a room stands in, where the camera parked, whether
    // the fog covers the boss) is a runtime fact, so the only useful picture of
    // it is one taken mid-run.
    //
    // Same fixed seed the flow tests use, so the picture and the assertions are
    // of the same leg.
    public class MapCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureTheMap()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            var root = Path.Combine(Path.GetTempPath(), "pp-map-shot");
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var dir = Path.Combine(Application.dataPath, "..", "tools", "screenshots", "runtime");
            Directory.CreateDirectory(dir);
            CanvasCapture.RenderToFile(Object.FindAnyObjectByType<Canvas>(),
                Path.Combine(dir, "MapWithALeg.png"), 1920, 1080);

            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Assert.Pass();
        }
    }
}
