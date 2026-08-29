using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace PrincesPalace.Editor.Rigging
{
    // Grafts the rat's hand-skinned tail onto the auto-built rig, AFTER
    // RigPrefabBuilder's own head/body/legs loop -- see the comment on the
    // removed "tail" part entry in tools/rig_actor.py's RIGS manifest for
    // why the tail is not cut from the shared atlas at all. This is the
    // only place that boundary is bridged.
    //
    // SOURCE OF TRUTH for the bone/mesh/weight data: the sprite's own
    // Editor Data Provider records, authored once by a person in Unity's
    // Sprite Editor (Skinning module) and never touched by this script --
    // only READ. Scripting the smooth-weight solver itself
    // (BoundedBiharmonicWeightsGenerator, what the Sprite Editor's own
    // "Auto Weights" button calls) was tried and failed 3 times with zero
    // diagnostic output; this is the sanctioned, documented Editor API
    // (UnityEditor.U2D.Sprites), not that dead end.
    //
    // COORDINATE MATH -- read this before touching CropOriginX/Y or
    // ReferenceHeightPx below. ISpriteBoneDataProvider.GetBones() returns
    // positions in TEXTURE-PIXEL space (Y-up, origin at the texture's own
    // bottom-left) -- confirmed empirically, not assumed: the root bone's
    // authored position landed within ~0.2 Unity units of where the OLD
    // rigid tail's own pivot used to sit, once run through the conversion
    // below. A CHILD bone's position/rotation are already expressed
    // relative to its PARENT's own local (rotated) frame -- exactly a
    // normal Unity Transform.localPosition/localRotation, no further
    // conversion needed, confirmed by a child bone's position.x matching
    // its parent's own reported `length` when the chain continues in a
    // near-straight line.
    //
    // The ROOT bone alone needs reconciling into the rig's own coordinate
    // frame:
    //   1. texture pixel (px, py), Y-up, origin bottom-left of the PNG.
    //   2. -> rig.json crop-space pixel (Y-down, origin top-left of the
    //      crop every OTHER bone's x/y already lives in): flip py against
    //      the texture's own height, then add the crop's own origin
    //      (CropOriginX/Y -- printed by
    //      `python tools/rig_actor.py --export-tail-for-skinning rat`,
    //      hardcoded here because re-deriving it from the PNG at runtime
    //      would need the SAME crop math tools/rig_actor.py's Python side
    //      already owns, and re-running that export would overwrite the
    //      hand-skinned file this script depends on).
    //   3. -> Unity absolute units, via the exact formula
    //      RigPrefabBuilder uses for every other bone
    //      (x/PPU, (referenceHeightPx-y)/PPU).
    //   4. -> local-to-body, by subtracting body's own already-computed
    //      absolute position (same technique RigPrefabBuilder uses for
    //      every parent/child bone pair).
    public static class RigTailGrafter
    {
        private const string TailSpritePath = "Assets/_Project/Art/Rigs/rat/tail_standalone_for_skinning.png";

        // From `python tools/rig_actor.py --export-tail-for-skinning rat`.
        // Re-run that (never by hand) if bind_pose.png ever changes, and
        // update these three constants from its printed output -- this
        // script does not, and must not, regenerate the tail crop itself,
        // or a person's hand-skinning work is silently discarded on the
        // next rebuild (see tools/rig_actor.py's own TAIL_REGION comment).
        private const float CropOriginX = 0f;
        private const float CropOriginY = 184f;
        private const float ReferenceHeightPx = 756f;

        // Sits between "far_foreleg"(z=1) and "body"(z=2) in the auto-built
        // order -- exactly where the old "tail" part used to render,
        // before this file existed.
        private const int SortingOrder = 2;

        public static void GraftOntoRat(GameObject rigGo, Dictionary<string, Transform> boneTransforms,
            Dictionary<string, Vector3> boneAbsPos, GameObject partsHolder)
        {
            if (!boneAbsPos.TryGetValue("body", out var bodyAbsPos) ||
                !boneTransforms.TryGetValue("body", out var bodyBone))
            {
                throw new System.InvalidOperationException(
                    "RigTailGrafter: no 'body' bone built yet -- must run after the main auto-build loop");
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(TailSpritePath);
            if (importer == null)
            {
                throw new System.InvalidOperationException($"RigTailGrafter: no TextureImporter at {TailSpritePath}");
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TailSpritePath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TailSpritePath);
            if (tex == null || sprite == null)
            {
                throw new System.InvalidOperationException($"RigTailGrafter: could not load texture/sprite at {TailSpritePath}");
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dp = factories.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var boneDp = dp.GetDataProvider<ISpriteBoneDataProvider>();
            var rects = dp.GetSpriteRects();

            if (rects.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"RigTailGrafter: {TailSpritePath} has no sprite rects -- was it ever set to Sprite Mode: Single and skinned?");
            }

            var bones = boneDp.GetBones(rects[0].spriteID);
            if (bones == null || bones.Count == 0)
            {
                throw new System.InvalidOperationException(
                    $"RigTailGrafter: {TailSpritePath} has no bones -- the hand-skinning step (Sprite Editor -> " +
                    "Skinning Editor -> create bones -> generate mesh -> auto weights -> Apply) was never done or never saved");
            }

            // Bottom-left corner of the WHOLE TEXTURE (Unity's own texture-
            // pixel origin), in the rig's absolute Unity-unit space -- every
            // bone's own (px,py) is added to this directly, no further flip,
            // because the texture's Y-up convention and Unity's world Y-up
            // convention already agree in direction; only the ORIGIN needed
            // reconciling (see the coordinate-math header comment above).
            float originUxBottomLeft = CropOriginX / RigPrefabBuilder.PixelsPerUnit;
            float originUyBottomLeft = (ReferenceHeightPx - (CropOriginY + tex.height)) / RigPrefabBuilder.PixelsPerUnit;

            var tailBones = new Transform[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones[i];
                var boneGo = new GameObject(b.name);
                tailBones[i] = boneGo.transform;

                if (b.parentId < 0)
                {
                    Vector3 rootAbs = new Vector3(
                        originUxBottomLeft + b.position.x / RigPrefabBuilder.PixelsPerUnit,
                        originUyBottomLeft + b.position.y / RigPrefabBuilder.PixelsPerUnit,
                        0f);
                    boneGo.transform.SetParent(bodyBone, false);
                    boneGo.transform.localPosition = rootAbs - bodyAbsPos;
                    boneGo.transform.localRotation = b.rotation;
                }
                else
                {
                    if (b.parentId >= i)
                    {
                        throw new System.InvalidOperationException(
                            $"RigTailGrafter: bone '{b.name}' parentId={b.parentId} is not earlier in the list -- " +
                            "expected parents authored before their children (GetBones()'s own natural order)");
                    }
                    boneGo.transform.SetParent(tailBones[b.parentId], false);
                    boneGo.transform.localPosition = new Vector3(b.position.x, b.position.y, b.position.z) / RigPrefabBuilder.PixelsPerUnit;
                    boneGo.transform.localRotation = b.rotation;
                }

                boneTransforms[b.name] = boneGo.transform;
            }

            var partGo = new GameObject("part_tail");
            partGo.transform.SetParent(partsHolder.transform, false);
            var sr = partGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = StageActorsSortingLayer.LayerName;
            sr.sortingOrder = SortingOrder;
            var skin = partGo.AddComponent<SpriteSkin>();
            skin.SetRootBone(tailBones[0]);
            skin.SetBoneTransforms(tailBones);
            skin.alwaysUpdate = true;

            Debug.Log($"RigTailGrafter: grafted {bones.Count} hand-skinned tail bones onto 'body', root='{bones[0].name}'");
        }
    }
}
