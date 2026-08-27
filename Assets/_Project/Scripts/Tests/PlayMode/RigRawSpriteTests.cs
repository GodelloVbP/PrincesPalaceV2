using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // DISCRIMINATOR: renders the rig atlas's 7 sprites as PLAIN SpriteRenderers
    // with NO SpriteSkin anywhere. Splits the remaining bug in half --
    //   renders correctly here => authored mesh + UVs are fine, the fault is
    //                             in SpriteSkin deformation;
    //   still fragments here    => the fault is in the authored sprite mesh.
    public class RigRawSpriteTests
    {
        [UnityTest]
        public IEnumerator RawSprites_RenderWithoutSkinning()
        {
            var atlas = Resources.Load<Texture2D>("Rigs/Enemies/rat/atlas");
            Sprite[] sprites;
#if UNITY_EDITOR
            sprites = UnityEditor.AssetDatabase
                .LoadAllAssetsAtPath("Assets/_Project/Resources/Rigs/Enemies/rat/atlas.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
#else
            sprites = Resources.LoadAll<Sprite>("Rigs/Enemies/rat/atlas");
#endif
            Assert.AreEqual(7, sprites.Length, "expected 7 sprites in the rig atlas");

            var root = new GameObject("RawSprites");
            // lay them out in a row, each on its own patch of ground, so a
            // squashed or fragmented one is obvious against its neighbours.
            float x = 0f;
            foreach (var s in sprites)
            {
                var go = new GameObject("raw_" + s.name);
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                float wUnits = s.rect.width / s.pixelsPerUnit;
                go.transform.localPosition = new Vector3(x + wUnits * 0.5f, 0f, 0f);
                Debug.Log($"RAW: {s.name} rect={s.rect} pivotPx={s.pivot} verts={s.vertices.Length} " +
                          $"tris={s.triangles.Length / 3} boundsAfterPlace={sr.bounds}");
                x += wUnits + 0.3f;
            }

            var camGO = new GameObject("RawCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.84f, 0.86f, 1f);

            var rends = root.GetComponentsInChildren<SpriteRenderer>();
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            int w = 1600, h = 500;
            cam.aspect = (float)w / h;
            cam.transform.position = new Vector3(b.center.x, b.center.y, -10f);
            cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x * ((float)h / w)) * 1.12f;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            yield return null;
            yield return null;

            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string outDir = @"C:\Users\Godel\AppData\Local\Temp\claude\C--Games-Prince-s-Palace-v2\b969cb95-02e9-45fc-a0e4-b6567a692cc2\scratchpad";
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "rig_raw_sprites.png"), tex.EncodeToPNG());

            var px = tex.GetPixels32();
            int distinct = px.Count(p => p.a > 200 && !(p.r > 200 && p.g > 205 && p.b > 210));
            Debug.Log($"RAW: distinct-from-bg pixels = {distinct}");

            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(camGO);
            Object.Destroy(tex);
            Object.Destroy(root);

            Assert.Greater(distinct, 1000, "no raw sprite content rendered at all");
        }
    }
}
