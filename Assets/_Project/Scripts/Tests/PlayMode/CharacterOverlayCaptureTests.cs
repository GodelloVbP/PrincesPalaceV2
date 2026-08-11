using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Captures the overlay OPEN.
    //
    // The Edit-Mode screenshot tool renders a scene at rest, and at rest this
    // overlay is correctly inactive -- so it can only be seen by opening it in
    // a running game. Graphics-gated like every other pixel test here.
    public class CharacterOverlayCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureTheOverlay()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            var root = Path.Combine(Path.GetTempPath(), "pp-overlay-shot");
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var hub = Object.FindAnyObjectByType<HubController>();
            hub.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "CharacterSheetBuilding")
                .GetComponent<Button>().onClick.Invoke();

            yield return null;
            yield return null;

            var dir = Path.Combine(Application.dataPath, "..", "tools", "screenshots", "runtime");
            Directory.CreateDirectory(dir);
            CanvasCapture.RenderToFile(Object.FindAnyObjectByType<Canvas>(),
                Path.Combine(dir, "CharacterOverlay.png"), 1920, 1080);

            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            Assert.Pass();
        }
    }
}
