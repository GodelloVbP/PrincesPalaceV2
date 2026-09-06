using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // Shared "screenshot the whole canvas of a bound FightController" plumbing.
    //
    // OutputDir/Named/RootCanvas/Shoot were byte-for-byte identical between
    // FightMenuCaptureTests and StatusRowCaptureTests (S12's review) except
    // for each fixture's own filename prefix and log tag -- both are
    // constructor arguments here instead of a second hand-copy of the method.
    // Extracted the way StageCaptureRig.cs (beside this file) already was for
    // the stage-crop capture path: this file carries no [Test] of its own,
    // only the machinery a capture fixture needs, which is what lets
    // Tests/PlayMode/Shared/ hold it without tripping the area-coverage
    // refusal in run_tests_parallel.ps1 (no testable class may sit in Shared/).
    internal sealed class HudCaptureRig
    {
        private readonly FightController _fight;
        private readonly string _filePrefix;
        private readonly string _logTag;

        public HudCaptureRig(FightController fight, string filePrefix, string logTag)
        {
            _fight = fight;
            _filePrefix = filePrefix;
            _logTag = logTag;
        }

        public static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        public GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        public static Canvas RootCanvas() =>
            Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).FirstOrDefault(c => c.isRootCanvas);

        public IEnumerator Shoot(string label)
        {
            // Two frames: one for a click's state change to apply, one for an
            // open animation to have started drawing -- without the second, a
            // just-opened column captures at alpha 0 and the shot looks like
            // it never opened.
            yield return null;
            yield return null;

            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the Fight scene has no root Canvas");
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, $"{_filePrefix}{label}.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"capture '{label}' was not written");
            Debug.Log($"[{_logTag}] wrote {path}");
        }
    }
}
