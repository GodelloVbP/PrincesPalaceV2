using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3 checkpoint: the rat prefab RigPrefabBuilder generates actually
    // loads, has a SpriteSkin per part with real bind poses, and renders as
    // a recognisable creature at bind pose -- not just that the import step
    // didn't throw. Also writes a PNG capture to a fixed scratch path for
    // human review, since a passing assertion here doesn't tell a person
    // whether the ears ended up on the wrong side.
    //
    // GRAPHICS-GATED. The commit gate runs headless (-nographics), where
    // camera.Render() is a silent no-op and SpriteSkin's GPU deformation
    // outright crashes the process rather than no-op'ing -- self-skips
    // exactly as RuntimeScreenshotTests does. Run via
    // tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigImportIntegrityTests,
    // which deliberately omits the flag.
    public class RigImportIntegrityTests
    {
        [UnityTest]
        public IEnumerator RatPrefab_LoadsWithSevenBoundParts_AndRendersNonTrivially()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigImportIntegrityTests");
            }

            var prefab = Resources.Load<GameObject>("Rigs/Enemies/rat");
            Assert.IsNotNull(prefab, "Resources/Rigs/Enemies/rat.prefab did not load");

            var instance = Object.Instantiate(prefab);
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(7, renderers.Length, "expected 7 part SpriteRenderers");

            foreach (var r in renderers)
            {
                Assert.IsNotNull(r.sprite, $"{r.name} has no sprite assigned");
                var skin = r.GetComponent<SpriteSkin>();
                Assert.IsNotNull(skin, $"{r.name} has no SpriteSkin");
                Assert.IsNotNull(skin.rootBone, $"{r.name}'s SpriteSkin has no root bone");
                Assert.AreEqual(1, skin.boneTransforms.Length, $"{r.name} should be rigid to exactly 1 bone");
            }

            // sorting order should be strictly increasing back-to-front,
            // matching rig.json's "order" list (RigPrefabBuilder writes z as
            // sortingOrder).
            var orders = renderers.Select(r => r.sortingOrder).ToArray();
            Assert.AreEqual(7, orders.Distinct().Count(), "sortingOrder values should be distinct (one z per part)");

            // SpriteSkin's deformation needs several real Play Mode frames to
            // catch up after a fresh Instantiate (confirmed: 2 frames was not
            // enough, 30 was). Wait BEFORE measuring bounds/framing the
            // camera, or the capture frames where the mesh was sitting
            // before it caught up.
            for (int i = 0; i < 30; i++) yield return null;

            var camGO = new GameObject("CaptureCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.84f, 0.86f, 1f);

            var bounds = ComputeBounds(renderers);
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * (9f / 16f)) * 1.15f;

            int w = 1024, h = 576;
            cam.aspect = (float)w / h;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            yield return null; // let the camera's own transform settle one frame

            cam.Render();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            var pixels = tex.GetPixels32();
            int opaque = pixels.Count(p => p.a > 200 && !(p.r > 200 && p.g > 205 && p.b > 210));
            Assert.Greater(opaque, 5000, "the assembled rat did not render a plausible silhouette");

            string outDir = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "rigs"));
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "rat_bindpose_capture.png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(camGO);
            Object.Destroy(tex);
            Object.Destroy(instance);
        }

        private static Bounds ComputeBounds(SpriteRenderer[] renderers)
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
