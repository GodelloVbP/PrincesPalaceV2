using System;

namespace PrincesPalace.Domain.Rig
{
    // What every bone in a clip is worth at a moment in its playback. The
    // seam RigStancePlayer drives at runtime and RigSamplerTests drives with
    // a bare clip and no rig, no prefab and no scene at all.
    public static class RigSampler
    {
        // One bone's answer -- a name rather than an index, matched against
        // RigActor's own bone map by name (the same key rig.json's bones and
        // parts already agree on).
        public readonly struct BonePose
        {
            public readonly string BoneName;
            public readonly float RotationDegrees;

            public BonePose(string boneName, float rotationDegrees)
            {
                BoneName = boneName;
                RotationDegrees = rotationDegrees;
            }
        }

        // `t` is wrapped into [0, DurationSeconds) first when the clip loops,
        // so a caller never has to know which stance breathes and which
        // plays once -- the same split LoopCycle draws for the frame-sheet
        // idle, moved here because a rig clip has no frame count to wrap by.
        public static BonePose[] Sample(RigStanceClip clip, float t)
        {
            if (clip == null || clip.IsEmpty) return Array.Empty<BonePose>();

            float at = clip.Loops ? Wrap(t, clip.DurationSeconds) : Clamp(t, 0f, clip.DurationSeconds);

            var poses = new BonePose[clip.Tracks.Length];
            for (int i = 0; i < clip.Tracks.Length; i++)
            {
                var track = clip.Tracks[i];
                poses[i] = new BonePose(track.BoneName, track.RotationAt(at));
            }

            return poses;
        }

        private static float Wrap(float t, float length)
        {
            if (length <= 0f) return 0f;
            float wrapped = t % length;
            return wrapped < 0f ? wrapped + length : wrapped;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
