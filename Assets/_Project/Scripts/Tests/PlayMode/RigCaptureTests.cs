using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Core.Rig;
using PrincesPalace.Domain.Rig;

namespace PrincesPalace.PlayModeTests
{
    // Samples a rig's own clips to real rendered PNGs -- the visual QA tool
    // the plan called for and Phase 6/7 shipped without: nothing else lets
    // anyone (human or Claude) SEE what a rig clip's motion actually looks
    // like without opening the Editor, starting a fight and hoping to catch
    // the right beat on camera.
    //
    // MUST be PlayMode, not an Editor -executeMethod batch script -- the
    // first version of this tool was exactly that, and every frame it
    // produced was garbage: SpriteSkin deformation is driven by
    // DeformationManagerUpdater.LateUpdate, an [ExecuteInEditMode]
    // component that still needs an actual per-frame tick to run. A
    // synchronous Edit-mode method that sets a bone rotation and calls
    // camera.Render() on the same line never gives Unity's own update loop
    // a chance to run that LateUpdate in between, so every part rendered at
    // its raw un-skinned mesh position instead of its posed one -- a
    // disassembled, overlapping mess, not a wrong pose. Play Mode's normal
    // per-frame ticking (a `yield return null` between ApplyPose and
    // Render) is what makes deformation actually happen, the same way the
    // real fight already proves it does (RigStageTests).
    //
    // "not an assertion so much as a DELIVERABLE" -- see
    // FightPlayableTests.CaptureTheLiveStage for the same shape: gated on
    // CanvasCapture.IsSupported, self-skips under -nographics, runs for
    // real via tools/graphics_tests.ps1 (or tools/rig_qa.ps1, a thin
    // wrapper naming this fixture).
    //
    // Output is `f0.png, f1.png, ...` per stance under
    // tools/screenshots/rigs/<root>/<id>/<stance>/ -- the exact convention
    // tools/actor_stance_qa.py's discover_creature already reads for
    // frame-sheet actors, so pointing --report at this tool's output
    // directory gets the onion-skin/redraw-ratio analysis on a RIG'S
    // motion for free, no changes to that tool needed.
    [Ignore("Rat rig temporarily disabled via RigLibrary._temporarilyDisabled (2026-08-29) -- " +
            "RigLibrary.Resolve now misses 'Enemies/rat' on purpose; re-enable once a rig is back.")]
    public class RigCaptureTests
    {
        private const float SamplesPerSecond = 12f;
        private const int CaptureSize = 512;

        private GameObject _instance;
        private GameObject _cameraGo;
        private RenderTexture _rt;

        [TearDown]
        public void Cleanup()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
        }

        [UnityTest]
        public IEnumerator CaptureEveryRatClip()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage. " +
                              "Run via tools/rig_qa.ps1 or tools/graphics_tests.ps1.");
                yield break;
            }

            const string root = "Enemies";
            const string id = "rat";
            string folder = $"{root}/{id}";

            var prefab = RigLibrary.Resolve(folder);
            Assert.IsNotNull(prefab, $"RigLibrary.Resolve('{folder}') returned null -- no rig prefab to capture");

            string outRoot = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "rigs", root, id));

            foreach (string stance in new[] { "idle", "attack", "hurt", "defeated" })
            {
                var clip = RigManifestLoader.ClipFor(folder, stance);
                if (clip.IsEmpty)
                {
                    Debug.LogWarning($"RigCaptureTests: '{folder}' has no '{stance}' clip -- skipping");
                    continue;
                }

                yield return CaptureStance(prefab, clip, Path.Combine(outRoot, stance));
            }
        }

        private IEnumerator CaptureStance(GameObject prefab, RigStanceClip clip, string outDir)
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
            Directory.CreateDirectory(outDir);

            _instance = Object.Instantiate(prefab);
            var rigActor = _instance.AddComponent<RigActor>();
            rigActor.Initialize(_instance.transform.Find("bones"));

            var meta = _instance.GetComponent<RigMeta>();
            float referenceHeightUnits = (meta != null && meta.ReferenceHeightPx > 0f
                ? meta.ReferenceHeightPx
                : 800f) / RigLibrary.PixelsPerUnit;

            _cameraGo = new GameObject("RigCaptureCamera");
            var camera = _cameraGo.AddComponent<Camera>();
            _rt = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32);

            camera.orthographic = true;
            // The rat is much WIDER than it is tall (body + tail run the
            // full length of the bind-pose canvas), so framing off the
            // reference HEIGHT alone crops the head -- confirmed against a
            // real capture, not guessed. Generous margin on top of that so
            // an attack lunge past the bind silhouette does not clip
            // either.
            camera.orthographicSize = referenceHeightUnits * 1.3f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.targetTexture = _rt;
            _cameraGo.transform.position = new Vector3(referenceHeightUnits * 0.9f, referenceHeightUnits * 0.4f, -10f);

            int frameCount = Mathf.Max(2, Mathf.RoundToInt(clip.DurationSeconds * SamplesPerSecond));

            for (int i = 0; i < frameCount; i++)
            {
                float t = clip.DurationSeconds * i / (frameCount - 1);
                rigActor.ApplyPose(RigSampler.Sample(clip, t));

                // Two real frames between posing the bones and rendering,
                // so DeformationManagerUpdater.LateUpdate (an actual
                // per-frame tick, not a synchronous call) gets to run and
                // the SpriteSkins actually deform before the camera sees
                // them. NOT WaitForEndOfFrame: it hangs indefinitely in
                // -batchmode, which has no display to present a frame to.
                yield return null;
                yield return null;

                camera.Render();

                var previousActive = RenderTexture.active;
                RenderTexture.active = _rt;
                var texture = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
                texture.Apply();
                RenderTexture.active = previousActive;

                File.WriteAllBytes(Path.Combine(outDir, $"f{i}.png"), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(_cameraGo);
            _cameraGo = null;
            _rt.Release();
            Object.DestroyImmediate(_rt);
            _rt = null;
            Object.DestroyImmediate(_instance);
            _instance = null;
        }
    }
}
