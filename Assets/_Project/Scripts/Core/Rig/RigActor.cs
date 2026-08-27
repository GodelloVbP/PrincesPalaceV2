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

        public void Initialize(Transform bonesHolder)
        {
            _bones.Clear();
            if (bonesHolder == null) return;

            CollectBones(bonesHolder);
        }

        private void CollectBones(Transform t)
        {
            foreach (Transform child in t)
            {
                _bones[child.name] = child;
                CollectBones(child);
            }
        }

        // Bind pose: every known bone back to zero rotation. What a beat
        // returns an actor to between blows -- the rig twin of
        // SetFrame(actor, 0) on the frame-sheet path (see
        // RigStancePlayback.ResetToRest).
        public void ResetToRest()
        {
            foreach (var bone in _bones.Values)
            {
                if (bone != null) bone.localRotation = Quaternion.identity;
            }
        }

        // Poses every bone the clip actually authored a track for. A bone
        // with no track (most of them, on most clips) is left exactly where
        // the last pose or ResetToRest put it, which is always one or the
        // other and never a stale mid-swing angle -- see RigStancePlayback's
        // own ResetToRest call between beats.
        public void ApplyPose(RigSampler.BonePose[] poses)
        {
            if (poses == null) return;

            for (int i = 0; i < poses.Length; i++)
            {
                if (_bones.TryGetValue(poses[i].BoneName, out var bone) && bone != null)
                {
                    bone.localRotation = Quaternion.Euler(0f, 0f, poses[i].RotationDegrees);
                }
            }
        }
    }
}
