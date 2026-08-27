using System;

namespace PrincesPalace.Domain.Rig
{
    // One bone's rotation at one moment in a clip -- degrees, relative to the
    // bind pose the rig was authored at (see RigPrefabBuilder: every bone
    // Transform starts at Quaternion.identity, so 0 degrees IS the bind pose).
    //
    // ROTATION ONLY, no position track. Every part on the rat rig binds
    // rigidly to exactly one bone (rig.json's weights are all 1.0 to a
    // single boneIndex), so a swing is a chain of rotations around each
    // bone's own pivot -- the same vocabulary a real skeleton animates in
    // everywhere else. Position keyframing is not ruled out for a future
    // rig, just not needed by this one.
    public readonly struct RigKeyframe
    {
        public readonly float TimeSeconds;
        public readonly float RotationDegrees;

        public RigKeyframe(float timeSeconds, float rotationDegrees)
        {
            TimeSeconds = Math.Max(0f, timeSeconds);
            RotationDegrees = rotationDegrees;
        }
    }

    // One bone's whole track across a clip. Keyframes are sorted by time at
    // construction (RigAnimationResolver's job); RotationAt does not sort
    // them itself, the same "resolve once, read many" split
    // StanceAnimationLibrary draws between loading a sheet and playing it.
    public sealed class RigBoneTrack
    {
        public readonly string BoneName;
        public readonly RigKeyframe[] Keyframes;

        public RigBoneTrack(string boneName, RigKeyframe[] keyframes)
        {
            BoneName = boneName ?? "";
            Keyframes = keyframes ?? Array.Empty<RigKeyframe>();
        }

        // The rotation this bone holds at `t`, linearly interpolated between
        // the keyframes either side of it. CLAMPED rather than extrapolated
        // past either end -- a clock before the first keyframe or after the
        // last holds that keyframe's pose, the same clamp
        // StanceAnimation.FrameAt takes for a stale frame index.
        public float RotationAt(float t)
        {
            if (Keyframes.Length == 0) return 0f;
            if (Keyframes.Length == 1 || t <= Keyframes[0].TimeSeconds) return Keyframes[0].RotationDegrees;

            for (int i = 1; i < Keyframes.Length; i++)
            {
                if (t > Keyframes[i].TimeSeconds) continue;

                var a = Keyframes[i - 1];
                var b = Keyframes[i];
                float span = b.TimeSeconds - a.TimeSeconds;
                float p = span <= 0f ? 1f : (t - a.TimeSeconds) / span;
                return a.RotationDegrees + (b.RotationDegrees - a.RotationDegrees) * p;
            }

            return Keyframes[Keyframes.Length - 1].RotationDegrees;
        }
    }

    // One stance's whole rig performance -- the rig twin of StanceAnimation.
    // Engine-free like everything else in Domain, so RigSamplerTests can pin
    // its arithmetic with no scene, no texture and no coroutine.
    public sealed class RigStanceClip
    {
        public static readonly RigStanceClip Empty = new RigStanceClip(0f, 0f, 0f, false, Array.Empty<RigBoneTrack>());

        public readonly float DurationSeconds;

        // Seconds into the clip the blow lands -- the rig twin of
        // StanceAnimation.ImpactFrame, in seconds because a rig has no
        // frames to count. FightBeatPlayer's windup/follow-through split
        // reads this exactly as it reads FrameStancePlayback.WindupSeconds.
        public readonly float ImpactAt;
        public readonly float SoundAt;

        // Whether RigStancePlayer wraps its clock rather than clamping it at
        // DurationSeconds -- the rig twin of StanceLoop, for the one stance
        // that never ends. See RigActor callers for who actually loops one.
        public readonly bool Loops;

        public readonly RigBoneTrack[] Tracks;

        public bool IsEmpty => Tracks.Length == 0 || DurationSeconds <= 0f;

        public RigStanceClip(float durationSeconds, float impactAt, float soundAt, bool loops, RigBoneTrack[] tracks)
        {
            DurationSeconds = Math.Max(0f, durationSeconds);
            ImpactAt = Clamp(impactAt, 0f, DurationSeconds);
            SoundAt = Clamp(soundAt, 0f, DurationSeconds);
            Loops = loops;
            Tracks = tracks ?? Array.Empty<RigBoneTrack>();
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
