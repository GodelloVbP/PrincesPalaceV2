using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Rig;

namespace PrincesPalace.Core.Rig
{
    // The runtime bone map for one rig instance -- built by walking the
    // already-instantiated "bones" hierarchy RigPrefabBuilder authored,
    // rather than baked into the prefab itself. Added at instantiation time
    // (see FightController.StageVisuals.cs's RefreshRigActor) so a change
    // here never requires the destructive rig-prefab regeneration; the only
    // contract this depends on is the NAMES RigPrefabBuilder gives its bone
    // GameObjects, and that contract already has to hold for each part's
    // SpriteSkin to find its own bone.
    public class RigActor : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();

        // Each bone's own bind localPosition/localRotation, captured once at
        // instantiation. Position: a translated pose is always this PLUS the
        // sampled offset, never the offset alone, so a bone whose rig art
        // sits off-origin (any non-root bone) still ends up in the right
        // place. Rotation: identity for every bone RigPrefabBuilder builds
        // (it never sets localRotation, so the default IS identity) -- but
        // NOT for a hand-skinned chain a grafter builds from its own
        // authored SpriteBone data (see RigTailGrafter.cs), where a child
        // bone's bind rotation is the tail's own natural curl and is very
        // much not zero. Assuming identity there would snap the tail
        // straight on every ResetToRest -- caching the REAL bind rotation
        // per bone, the same way position already was, is what keeps this
        // correct for a bone RigActor didn't build and knows nothing else
        // about.
        private readonly Dictionary<string, Vector3> _bindPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Quaternion> _bindRotations = new Dictionary<string, Quaternion>();

        public void Initialize(Transform bonesHolder)
        {
            _bones.Clear();
            _bindPositions.Clear();
            _bindRotations.Clear();
            if (bonesHolder == null) return;

            CollectBones(bonesHolder);
        }

        private void CollectBones(Transform t)
        {
            foreach (Transform child in t)
            {
                _bones[child.name] = child;
                _bindPositions[child.name] = child.localPosition;
                _bindRotations[child.name] = child.localRotation;
                CollectBones(child);
            }
        }

        // Bind pose: every known bone back to its OWN bind rotation and
        // position (see the field comments above for why rotation is not
        // simply identity). What a beat returns an actor to between blows --
        // the rig twin of SetFrame(actor, 0) on the frame-sheet path (see
        // RigStancePlayback.ResetToRest). Position must be restored too,
        // not just rotation -- a bone left at its last sampled translation
        // is exactly the stale mid-swing angle this method exists to rule
        // out, just on a different channel.
        public void ResetToRest()
        {
            foreach (var pair in _bones)
            {
                if (pair.Value == null) continue;
                pair.Value.localRotation = _bindRotations.TryGetValue(pair.Key, out var bindRot) ? bindRot : Quaternion.identity;
                if (_bindPositions.TryGetValue(pair.Key, out var bind)) pair.Value.localPosition = bind;
            }
        }

        // Poses every bone the clip actually authored a track for. A bone
        // with no track (most of them, on most clips) is left exactly where
        // the last pose or ResetToRest put it, which is always one or the
        // other and never a stale mid-swing angle -- see RigStancePlayback's
        // own ResetToRest call between beats.
        //
        // RotationDegrees is authored RELATIVE TO THE BIND POSE (see
        // RigStanceClip's own header: "0 degrees IS the bind pose"), so it
        // is applied ON TOP OF the bone's own bind rotation, not in place
        // of it -- identical to the old `Quaternion.Euler(...)` alone for
        // every bone RigPrefabBuilder builds (bind rotation there is always
        // identity, so bindRot * delta == delta), and correct for a
        // hand-skinned chain's own non-identity bind rotations too.
        public void ApplyPose(RigSampler.BonePose[] poses)
        {
            if (poses == null) return;

            for (int i = 0; i < poses.Length; i++)
            {
                if (_bones.TryGetValue(poses[i].BoneName, out var bone) && bone != null)
                {
                    var bindRot = _bindRotations.TryGetValue(poses[i].BoneName, out var br) ? br : Quaternion.identity;
                    bone.localRotation = bindRot * Quaternion.Euler(0f, 0f, poses[i].RotationDegrees);
                    if (_bindPositions.TryGetValue(poses[i].BoneName, out var bind))
                    {
                        bone.localPosition = bind + new Vector3(poses[i].DxPixels, poses[i].DyPixels, 0f) / RigLibrary.PixelsPerUnit;
                    }
                }
            }
        }
    }
}
