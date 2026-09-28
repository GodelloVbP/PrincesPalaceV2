using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M4 CAPTURE (docs/PLAN_BELLWETHER_KIT.md M4): the Chains badge
    // ("Dark Chains! Death Knell next"), the lethal Knell badge at the front,
    // and the same badge after a free Passage to the middle. A picture, not
    // an assertion: KnellBadgeTests pins the behaviour on the same fixture.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.KnellBadgeCaptureTests
    // PNGs land in tools/screenshots/runtime/kit_m4/ in the runner copy; the
    // set worth keeping is copied to docs/captures/kit-m4/.
    public class KnellBadgeCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "kit_m4"));

        private static readonly (string Name, UiVec Frame)[] Aspects =
        {
            ("16x9", UiFrames.Reference),
            ("4x3", UiFrames.FourThree),
        };

        private Canvas _canvas;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        [UnityTest]
        public IEnumerator CaptureTheChainsAndTheKnell()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.KnellBadgeCaptureTests");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);

            var (session, shawn, bell) = KnellBadgeFixture.Bind(fight);
            yield return KnellBadgeFixture.Settle(fight);
            _canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            yield return WaitReal(0.5f);
            yield return Shoot("a_chains");

            KnellBadgeFixture.ToTheKnell(fight, session, bell);
            yield return KnellBadgeFixture.Settle(fight);
            yield return WaitReal(0.5f);
            yield return Shoot("b_knell_front_lethal");

            yield return KnellBadgeFixture.PassageTo(fight, session, shawn, 1);
            yield return WaitReal(0.5f);
            yield return Shoot("c_knell_middle");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shoot(string state)
        {
            foreach (var (name, frame) in Aspects)
            {
                int width = (int)frame.X;
                int height = (int)frame.Y;

                var rig = StageCaptureRig.FullFrame(_canvas, width, height);
                try
                {
                    yield return null;
                    yield return null;
                    yield return null;
                }
                finally
                {
                    rig.Restore();
                }

                Directory.CreateDirectory(OutputDir);
                string path = Path.Combine(OutputDir, $"knell_{state}_{name}.png");
                CanvasCapture.RenderToFile(_canvas, path, width, height);
                FileAssert.Exists(path);
                Debug.Log($"[KnellBadgeCapture] wrote {path}");
            }
        }
    }
}
