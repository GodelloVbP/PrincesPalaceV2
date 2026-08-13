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
    // Captures the RUNNING game to PNG from inside a PlayMode test.
    //
    // The distinction this rests on is worth restating, because getting it
    // wrong cost v1 months of blind visual work: the EDITOR entering Play Mode
    // from a batch -executeMethod call hangs in this environment. The PlayMode
    // TEST RUNNER is a different mechanism and has always worked. ScreenshotTool
    // renders Edit Mode, which never ticks Update(), so it can show the layout
    // but not one frame of the ambient layer actually moving.
    public class RuntimeScreenshotTests
    {
        // With Time.captureFramerate set, time advances by exactly 1/fps per
        // frame regardless of how long the frame really took, so "advance 7
        // seconds" means the same thing on any machine.
        private const int CaptureFps = 60;

        private static readonly float[] CaptureSeconds = { 1.5f, 7f, 15f };

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void RestoreCaptureFramerate()
        {
            // Global engine state; leaking it would change the pace of every
            // test that runs after this one.
            Time.captureFramerate = 0;
        }

        [UnityTest]
        public IEnumerator MainMenu_IsActuallyAnimating_AndCapturesToPng()
        {
            // Not a disabled test: with -nographics there is no graphics device,
            // camera.Render() is a silent no-op and ReadPixels returns garbage.
            // The commit gate passes -nographics, so this can only run through
            // tools/screenshot.ps1 -Runtime, which deliberately omits it.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime");
            }

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            // The ROOT canvas -- same reasoning as ScreenshotTool.Capture. A
            // node can be given its own nested Canvas for sort-order control,
            // so "any" of them would eventually be a sub-panel, and the
            // failure would present as an art bug rather than a lookup one.
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "MainMenu should have a root Canvas");

            Directory.CreateDirectory(OutputDir);
            Time.captureFramerate = CaptureFps;

            var captured = new byte[CaptureSeconds.Length][];
            float elapsed = 0f;

            for (int i = 0; i < CaptureSeconds.Length; i++)
            {
                while (elapsed < CaptureSeconds[i])
                {
                    elapsed += 1f / CaptureFps;
                    yield return null;
                }

                string path = Path.Combine(OutputDir, $"MainMenu_t{CaptureSeconds[i]:0.0}s.png");
                CanvasCapture.RenderToFile(canvas, path);

                FileAssert.Exists(path);
                captured[i] = File.ReadAllBytes(path);
                Assert.Greater(captured[i].Length, 1024, $"capture at {CaptureSeconds[i]}s is too small to be a frame");
            }

            // The assertion that matters. Identical bytes across three widely
            // separated points means nothing is moving -- the failure an Edit
            // Mode screenshot cannot see, and that the pure-curve unit tests
            // cannot see either, because those prove the FORMULAS vary, not that
            // anything calls them.
            for (int i = 1; i < captured.Length; i++)
            {
                Assert.AreNotEqual(captured[0], captured[i],
                    $"the menu at {CaptureSeconds[0]}s and {CaptureSeconds[i]}s rendered identically - the ambient layer is not animating.");
            }

            Debug.Log($"Wrote {CaptureSeconds.Length} runtime captures to {OutputDir}");
        }
    }
}
