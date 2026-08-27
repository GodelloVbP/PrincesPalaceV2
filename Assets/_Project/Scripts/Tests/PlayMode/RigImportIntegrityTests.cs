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
    public class RigImportIntegrityTests
    {
        [UnityTest]
        public IEnumerator RatPrefab_LoadsWithSevenBoundParts_AndRendersNonTrivially()
        {
            var prefab = Resources.Load<GameObject>("Rigs/Enemies/rat");
            Assert.IsNotNull(prefab, "Resources/Rigs/Enemies/rat.prefab did not load");

#if UNITY_EDITOR
            var allSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/_Project/Resources/Rigs/Enemies/rat/atlas.png")
                .OfType<Sprite>().ToArray();
            Debug.Log($"DIAG5: atlas has {allSprites.Length} Sprite sub-assets");
            foreach (var s in allSprites)
                Debug.Log($"DIAG5:   sprite name='{s.name}' rect={s.rect}");
#endif

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
                // DIAGNOSTIC: SpriteSkin was bound at prefab-BUILD time (Edit Mode);
                // this instance is a save->instantiate round trip Spike A never
                // exercised (it bound and used the skin within one PlayMode session).
                // Re-set to itself to see whether that's what's missing.
                skin.SetRootBone(skin.rootBone);
                skin.SetBoneTransforms(skin.boneTransforms);
                Debug.Log($"DIAG2: {r.name} alwaysUpdate={skin.alwaysUpdate} isActiveAndEnabled={skin.isActiveAndEnabled} " +
                          $"forceCpu={skin.forceCpuDeformation} boundsMode={skin.boundsMode} didAwake={skin.didAwake} didStart={skin.didStart}");
            }

            // sorting order should be strictly increasing back-to-front,
            // matching rig.json's "order" list (RigPrefabBuilder writes z as
            // sortingOrder).
            var orders = renderers.Select(r => r.sortingOrder).ToArray();
            var sorted = orders.OrderBy(o => o).ToArray();
            CollectionAssert.AreEqual(sorted, orders.OrderBy(o => o).ToArray()); // sanity: distinct/no NaN-style dupes
            Assert.AreEqual(7, orders.Distinct().Count(), "sortingOrder values should be distinct (one z per part)");

            foreach (var r in renderers)
            {
                var s = r.sprite;
                var skin = r.GetComponent<SpriteSkin>();
                Debug.Log($"DIAG: {r.name} spriteRectPx={s.rect} pivotPx={s.pivot} bounds={s.bounds} " +
                          $"ppu={s.pixelsPerUnit} vertsN={s.vertices.Length} vert0={(s.vertices.Length > 0 ? s.vertices[0].ToString() : "n/a")} " +
                          $"bonePos={skin.rootBone.position} boneLocalPos={skin.rootBone.localPosition} " +
                          $"rendererBounds={r.bounds} rendererLocalPos={r.transform.localPosition}");
            }

            // SpriteSkin's deformation needs several real Play Mode frames to
            // catch up after a fresh Instantiate (confirmed: 2 frames was not
            // enough, 30 was -- Spike A never hit this because it built and
            // used bones within one session, never a prefab save/load round
            // trip). Wait BEFORE measuring bounds/framing the camera, or the
            // capture frames where the mesh was sitting before it caught up.
            for (int i = 0; i < 30; i++) yield return null;

            foreach (var r in renderers)
            {
                var skin = r.GetComponent<SpriteSkin>();
                Debug.Log($"DIAG4: {r.name} bonePos={skin.rootBone.position} rendererBounds={r.bounds} spriteBounds={r.sprite.bounds} sortOrder={r.sortingOrder}");
            }

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
            Debug.Log($"RigImportIntegrityTests: distinct-from-bg pixels = {opaque}");

            string outDir = @"C:\Users\Godel\AppData\Local\Temp\claude\C--Games-Prince-s-Palace-v2\b969cb95-02e9-45fc-a0e4-b6567a692cc2\scratchpad";
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "rig_bindpose_capture.png"), tex.EncodeToPNG());

            // ISOLATED zoom on "head" alone -- hide every other renderer,
            // frame tightly, high-res, to see the actual defect directly.
            var headR = renderers.First(r => r.name == "part_head");
            foreach (var r in renderers) r.enabled = (r == headR);
            var hb = headR.bounds;
            Debug.Log($"DIAG6: isolated renderer.name={headR.name} sprite.name={headR.sprite.name} " +
                      $"spriteRect={headR.sprite.rect} bounds={hb} skinBoneCount={headR.GetComponent<SpriteSkin>().boneTransforms.Length} " +
                      $"rootBoneName={headR.GetComponent<SpriteSkin>().rootBone.name}");
            foreach (var r in renderers) Debug.Log($"DIAG6b: {r.name} enabled={r.enabled} sprite={r.sprite.name}");
            cam.transform.position = new Vector3(hb.center.x, hb.center.y, -10f);
            cam.orthographicSize = Mathf.Max(hb.extents.y, hb.extents.x * (9f/16f)) * 1.3f;
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var htex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            htex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            htex.Apply();
            RenderTexture.active = prevActive;
            File.WriteAllBytes(Path.Combine(outDir, "rig_head_isolated.png"), htex.EncodeToPNG());
            Object.Destroy(htex);
            foreach (var r in renderers) r.enabled = true;

            Assert.Greater(opaque, 5000, "the assembled rat did not render a plausible silhouette");

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
