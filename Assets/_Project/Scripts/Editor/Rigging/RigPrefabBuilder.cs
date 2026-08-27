using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace PrincesPalace.Editor.Rigging
{
    // Builds the generated actor prefab from rig.json + the imported atlas
    // sprites (RigImporter must have run first). Destructively regenerated,
    // ContentBuilder-style -- never hand-edited. A preserved .meta beside the
    // output path keeps the prefab's own GUID stable across regenerations.
    //
    // Bone Transform positions come from rig.json's "bones" (source-image
    // pixels, Y-down, root at the feet) converted to Unity units at a fixed
    // PIXELS_PER_UNIT, Y-flipped against the rig's own referenceHeightPx.
    // Part renderers sit at identity, as SIBLINGS of the bones (not
    // children) -- this mirrors Spike A's proven structure exactly, where
    // SpriteSkin deformation is driven entirely by the referenced bone
    // Transforms' world positions, not by the SpriteRenderer GameObject's
    // own transform.
    public static class RigPrefabBuilder
    {
        public const float PixelsPerUnit = 100f;

        public static void BuildRat() => Build("Enemies", "rat");

        public static void Build(string root, string id)
        {
            string folder = $"Assets/_Project/Resources/Rigs/{root}/{id}";
            string jsonPath = $"{folder}/rig.json";
            string atlasPath = $"{folder}/atlas.png";
            string prefabPath = $"Assets/_Project/Resources/Rigs/{root}/{id}.prefab";

            // The fight stage sandwiches a rig's SpriteRenderers between the
            // backdrop and the HUD by sortingOrder alone (Spike B), which
            // only works if they sit on their own sorting layer rather than
            // Default -- ensure it exists before any part below is assigned
            // to it. Idempotent, so rebuilding a rig that isn't the first
            // one costs nothing extra.
            StageActorsSortingLayer.Ensure();

            var rig = MiniJson.Parse(File.ReadAllText(jsonPath));
            float referenceHeightPx = (float)rig["referenceHeightPx"].AsNumber();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            var boneJson = rig["bones"].AsArray();
            var boneAbsPos = new Dictionary<string, Vector3>();
            var boneParent = new Dictionary<string, string>();
            string rootBoneName = null;

            foreach (var b in boneJson)
            {
                string name = b["name"].AsString();
                bool hasParent = b.AsObject().TryGetValue("parent", out var parentVal) && parentVal != null;
                string parent = hasParent ? parentVal.AsString() : null;
                float x = (float)b["x"].AsNumber();
                float y = (float)b["y"].AsNumber();
                float ux = x / PixelsPerUnit;
                float uy = (referenceHeightPx - y) / PixelsPerUnit;
                boneAbsPos[name] = new Vector3(ux, uy, 0f);
                boneParent[name] = parent;
                if (parent == null) rootBoneName = name;
            }

            if (rootBoneName == null)
            {
                throw new System.InvalidOperationException($"{root}/{id}: no root bone (every bone has a parent)");
            }

            var go = new GameObject(id);
            var bonesHolder = new GameObject("bones");
            bonesHolder.transform.SetParent(go.transform, false);
            var partsHolder = new GameObject("parts");
            partsHolder.transform.SetParent(go.transform, false);

            var boneTransforms = new Dictionary<string, Transform>();
            // build root(s) first, then children, however many passes it takes --
            // bone count is small (single digits) so this is not worth a topo sort.
            var remaining = new List<string>(boneAbsPos.Keys);
            while (remaining.Count > 0)
            {
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    string name = remaining[i];
                    string parent = boneParent[name];
                    if (parent != null && !boneTransforms.ContainsKey(parent)) continue; // parent not built yet

                    var boneGo = new GameObject(name);
                    Transform parentT = parent == null ? bonesHolder.transform : boneTransforms[parent];
                    boneGo.transform.SetParent(parentT, false);
                    Vector3 parentAbs = parent == null ? Vector3.zero : boneAbsPos[parent];
                    boneGo.transform.localPosition = boneAbsPos[name] - parentAbs;
                    boneTransforms[name] = boneGo.transform;
                    remaining.RemoveAt(i);
                }
            }

            var partsJson = rig["parts"].AsArray();
            foreach (var partJson in partsJson)
            {
                string name = partJson["name"].AsString();
                int z = partJson["z"].AsInt();

                if (!sprites.TryGetValue(name, out var sprite))
                {
                    throw new System.InvalidOperationException($"{root}/{id}: no imported sprite named '{name}' in {atlasPath} -- run RigImporter.Import first");
                }
                if (!boneTransforms.TryGetValue(name, out var ownBone))
                {
                    throw new System.InvalidOperationException($"{root}/{id}: part '{name}' has no matching bone");
                }

                var partGo = new GameObject($"part_{name}");
                partGo.transform.SetParent(partsHolder.transform, false);
                var sr = partGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = StageActorsSortingLayer.LayerName;
                sr.sortingOrder = z;
                var skin = partGo.AddComponent<SpriteSkin>();
                skin.SetRootBone(ownBone);
                skin.SetBoneTransforms(new[] { ownBone });
                skin.alwaysUpdate = true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out bool success);
            Object.DestroyImmediate(go);

            if (!success || saved == null)
            {
                throw new System.InvalidOperationException($"{root}/{id}: SaveAsPrefabAsset failed for {prefabPath}");
            }

            Debug.Log($"RigPrefabBuilder: built {prefabPath} -- {partsJson.Count} parts, {boneTransforms.Count} bones, root bone '{rootBoneName}'");
        }
    }
}
