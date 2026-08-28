using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace PrincesPalace.Editor.Rigging
{
    // Reads tools/rig_actor.py's rig.json and authors SpriteRects + a
    // per-part single-bone skeleton + a triangulated, weighted mesh onto the
    // atlas PNG, via the programmatic Sprite Editor Data Provider API --
    // proven headless-persistent and deformable by Spike A (see
    // docs/ART_PIPELINE.md's rigging section once written).
    //
    // COORDINATE DESIGN, worth reading before touching this file: rig.json's
    // "outline"/"pivot" are in image convention (Y-down, origin at the
    // part's own tight-bbox top-left). Unity's Sprite/mesh space is Y-up.
    // Rather than chase which of several plausible Unity-internal
    // conventions (whole-atlas vs per-sprite-rect origin) the data providers
    // actually use, every part's mesh vertices are authored RELATIVE TO
    // THAT PART'S OWN PIVOT, and that part's one SpriteBone sits at LOCAL
    // (0,0,0) -- the bone IS the pivot, by construction. This makes the
    // authored bind pose self-consistent regardless of Unity's absolute
    // convention, and rigid (this pilot never blends across parts, so no
    // cross-part shared origin is ever needed).
    public static class RigImporter
    {
        public static void ImportRat() => Import("Enemies", "rat");

        public static void Import(string root, string id)
        {
            string folder = $"Assets/_Project/Resources/Rigs/{root}/{id}";
            string jsonPath = $"{folder}/rig.json";
            string atlasPath = $"{folder}/atlas.png";

            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"no rig.json at {jsonPath} -- run tools/rig_actor.py {id} first");
            }

            var rig = MiniJson.Parse(File.ReadAllText(jsonPath));

            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            if (importer == null)
            {
                throw new InvalidOperationException($"no TextureImporter at {atlasPath}");
            }

            // ATLAS Y ORIGIN: tools/rig_actor.py packs in IMAGE space (Y-down,
            // origin top-left, the convention PIL/numpy write the PNG in).
            // Unity's SpriteRect.rect is TEXTURE space (Y-up, origin
            // bottom-left). Passing the packed rect through unchanged makes
            // every part sample the wrong horizontal strip of the atlas --
            // which is what made `head` render the tail's art, and left `body`
            // looking almost-right only because its 73px error still overlapped
            // itself by 86%. Flip once, here, at the boundary between the two
            // conventions.
            var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (atlasTex == null)
            {
                throw new InvalidOperationException($"could not load atlas texture at {atlasPath}");
            }
            int atlasHeight = atlasTex.height;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dp = factories.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-running this importer against an atlas it has already
            // authored (every rig.json edit, every backing_px/epsilon
            // tuning pass) does NOT cleanly replace the previous sprite
            // sheet -- SetSpriteRects/Apply further down is documented as a
            // full replace, but in practice stale mesh/bone/rect entries
            // from earlier imports of the SAME deterministic spriteIDs kept
            // surviving underneath the new ones. Caught the hard way: the
            // rat's committed atlas.png.meta had grown to carry 9500+ dead
            // lines of old sprite data, and one part ("tail") ended up with
            // a rect so stale it fell outside the CURRENT atlas bounds --
            // Unity silently dropped the sprite entirely, passing every
            // test that happened to run in the SAME live session that had
            // just re-authored it (correct in memory, wrong on disk) while
            // being broken for any fresh process loading the committed file
            // cold, which is exactly what a real player's first import
            // does. Explicitly replacing with an EMPTY rect set first, via
            // this same data-provider API, forces that stale per-sprite
            // state out before the real rects go back in -- tried toggling
            // SpriteImportMode.Single/Multiple instead first, but
            // RigAtlasImportPostprocessor forces Multiple back on every
            // reimport before the toggle can take effect, so it never
            // actually cleared anything.
            if (dp.GetSpriteRects().Length > 0)
            {
                dp.SetSpriteRects(Array.Empty<SpriteRect>());
                dp.Apply();
                importer.SaveAndReimport();
                dp.InitSpriteEditorDataProvider();
            }

            var partsJson = rig["parts"].AsArray();
            var spriteIds = new Dictionary<string, GUID>();
            var rects = new List<SpriteRect>();

            foreach (var partJson in partsJson)
            {
                string name = partJson["name"].AsString();
                var rectArr = partJson["rect"].AsArray();
                float x = (float)rectArr[0].AsNumber();
                float y = (float)rectArr[1].AsNumber();
                float w = (float)rectArr[2].AsNumber();
                float h = (float)rectArr[3].AsNumber();

                var guid = DeterministicGuid($"{root}/{id}/{name}");
                spriteIds[name] = guid;

                // pivot -> normalized, Y-flipped within the part's own rect.
                var pivotArr = partJson["pivot"].AsArray();
                float pivotLocalX = (float)pivotArr[0].AsNumber();
                float pivotLocalYImage = (float)pivotArr[1].AsNumber();
                float pivotUnityY = h - pivotLocalYImage;
                var pivotNorm = new Vector2(w > 0 ? pivotLocalX / w : 0.5f, h > 0 ? pivotUnityY / h : 0.5f);

                float unityY = atlasHeight - (y + h);   // image-space -> texture-space

                rects.Add(new SpriteRect
                {
                    spriteID = guid,
                    name = name,
                    rect = new Rect(x, unityY, w, h),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivotNorm,
                    border = Vector4.zero,
                });
            }

            dp.SetSpriteRects(rects.ToArray());
            dp.Apply();
            importer.SaveAndReimport();

            var boneDp = dp.GetDataProvider<ISpriteBoneDataProvider>();
            var meshDp = dp.GetDataProvider<ISpriteMeshDataProvider>();

            foreach (var partJson in partsJson)
            {
                string name = partJson["name"].AsString();
                var guid = spriteIds[name];
                var rectArr = partJson["rect"].AsArray();
                float h = (float)rectArr[3].AsNumber();
                var pivotArr = partJson["pivot"].AsArray();
                float pivotLocalX = (float)pivotArr[0].AsNumber();
                float pivotLocalYImage = (float)pivotArr[1].AsNumber();
                float pivotUnityY = h - pivotLocalYImage;

                // One bone per part, sitting AT the pivot -- but in TILE-LOCAL
                // space (matching the rect's own [0,w]x[0,h] frame), not
                // relative to itself. Earlier draft put vertices relative to
                // the pivot and the bone at local zero: mesh shape/bounds came
                // out correct (both are translation-invariant) but the UV
                // Unity auto-derives from vertex position assumes vertices
                // range [0,w]x[0,h] from the RECT's own corner -- shifting
                // every vertex by the pivot shifted every UV out of that
                // range too, so most of each mesh sampled the atlas's
                // transparent inter-part padding instead of its own texture.
                // Confirmed against the rat rig: mesh silhouette/winding/bounds
                // were all independently verified correct; only the rendered
                // FILL was wrong, which is exactly a UV-space symptom.
                var boneList = new List<SpriteBone>
                {
                    new SpriteBone
                    {
                        name = name,
                        position = new Vector3(pivotLocalX, pivotUnityY, 0f),
                        rotation = Quaternion.identity,
                        length = Mathf.Max(4f, h * 0.25f),
                        parentId = -1,
                    },
                };
                boneDp.SetBones(guid, boneList);

                var outlineArr = partJson["outline"].AsArray();
                var trianglesArr = partJson["triangles"].AsArray();
                int vertCount = outlineArr.Count;

                var vertices = new Vertex2DMetaData[vertCount];
                for (int i = 0; i < vertCount; i++)
                {
                    var pt = outlineArr[i].AsArray();
                    float lx = (float)pt[0].AsNumber();
                    float lyImage = (float)pt[1].AsNumber();
                    float lyUnity = h - lyImage;
                    // tile-local, matching the rect's own frame -- NOT offset
                    // by the pivot. The bone (above) carries the pivot offset
                    // instead, so bind-pose math still ends up self-consistent.
                    vertices[i] = new Vertex2DMetaData
                    {
                        position = new Vector2(lx, lyUnity),
                        boneWeight = new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    };
                }
                meshDp.SetVertices(guid, vertices);

                // WINDING: rig_actor.py emits triangles CCW in IMAGE space
                // (Y-down). The Y-flip above is a reflection, which reverses
                // orientation -- so every triangle would land CW in Unity's
                // Y-up space. Swap the last two indices of each triangle to
                // put them back to CCW.
                var indices = new int[trianglesArr.Count];
                for (int t = 0; t + 2 < trianglesArr.Count; t += 3)
                {
                    indices[t] = trianglesArr[t].AsInt();
                    indices[t + 1] = trianglesArr[t + 2].AsInt();
                    indices[t + 2] = trianglesArr[t + 1].AsInt();
                }
                meshDp.SetIndices(guid, indices);

                // perimeter edges, in outline order (closed loop).
                var edges = new Vector2Int[vertCount];
                for (int i = 0; i < vertCount; i++) edges[i] = new Vector2Int(i, (i + 1) % vertCount);
                meshDp.SetEdges(guid, edges);
            }

            dp.Apply();
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();

            // Second explicit synchronous reimport, forced. In a cold batch-mode
            // process the asset-import WORKER (a separate process Unity spawns
            // lazily) was still starting up while the calls above ran -- its own
            // log line ("Worker ready: AssetImportWorkerHW0") showed up AFTER
            // SaveAndReimport() had already returned, on the run that first hit
            // this. The in-memory read-back below (AssetDatabase.LoadAllAssetsAtPath)
            // passed anyway, because it reads whatever the CURRENT process already
            // has cached -- but the .meta file on disk kept whatever sprite sheet
            // was there before this run, silently. Any FRESH process loading that
            // file cold (a real test run, a real player) sees the stale one. Forcing
            // a second ForceUpdate import here gives the worker a synchronization
            // point to actually catch up before this method returns.
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            // Compare what we SENT against what Unity actually STORED. If these
            // disagree, Unity re-tessellated or rejected the authored mesh and
            // the bug is upstream of anything the prefab/stage does.
            var stored = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);
            foreach (var partJson in partsJson)
            {
                string name = partJson["name"].AsString();
                int sentVerts = partJson["outline"].AsArray().Count;
                int sentTris = partJson["triangles"].AsArray().Count / 3;
                if (!stored.TryGetValue(name, out var sp))
                {
                    Debug.Log($"MESHDUMP: {name} MISSING from atlas after import");
                    continue;
                }
                var vs = sp.vertices;
                var ts = sp.triangles;
                string firstTris = string.Join(",", ts.Take(9));
                Debug.Log($"MESHDUMP: {name} sentVerts={sentVerts} storedVerts={vs.Length} " +
                          $"sentTris={sentTris} storedTris={ts.Length / 3} firstIdx=[{firstTris}] " +
                          $"v0={(vs.Length > 0 ? vs[0].ToString("F3") : "n/a")}");
            }

            Debug.Log($"RigImporter: imported {partsJson.Count} parts for {root}/{id} into {atlasPath}");
        }

        private static GUID DeterministicGuid(string seed)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
            string hex = string.Concat(hash.Select(b => b.ToString("x2")));
            GUID.TryParse(hex, out var guid);
            return guid;
        }
    }
}
