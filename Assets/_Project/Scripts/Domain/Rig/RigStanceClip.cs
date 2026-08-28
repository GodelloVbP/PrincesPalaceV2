using System;

namespace PrincesPalace.Domain.Rig
{
    // One bone's pose at one moment in a clip -- rotation in degrees plus an
    // optional translation, both relative to the bind pose the rig was
    // authored at (see RigPrefabBuilder: every bone Transform starts at
    // Quaternion.identity and its own bind localPosition, so (0deg, 0, 0) IS
    // the bind pose).
    //
    // Every part on the rat rig binds rigidly to exactly one bone (rig.json's
    // weights are all 1.0 to a single boneIndex), so most motion is still a
    // chain of rotations around each bone's own pivot. Translation exists
    // for the one case rotation-about-a-pivot cannot express: a body whose
    // pivot sits at the feet (root-at-feet by rig design) can only SWING from
    // a rotation, never rise and fall the way a chest actually does when it
    // breathes -- see the idle clip's body track for the case this was added
    // for. DxPixels/DyPixels are in source pixels, +y up.
    public readonly struct RigKeyframe
    {
        public readonly float TimeSeconds;
        public readonly float RotationDegrees;
        public readonly float DxPixels;
        public readonly float DyPixels;

        public RigKeyframe(float timeSeconds, float rotationDegrees, float dxPixels = 0f, float dyPixels = 0f)
        {
            TimeSeconds = Math.Max(0f, timeSeconds);
            RotationDegrees = rotationDegrees;
            DxPixels = dxPixels;
            DyPixels = dyPixels;
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

        // The rotation this bone holds at `t`. CLAMPED rather than
        // extrapolated past either end -- a clock before the first keyframe
        // or after the last holds that keyframe's pose, the same clamp
        // StanceAnimation.FrameAt takes for a stale frame index.
        public float RotationAt(float t) => ValueAt(t, static k => k.RotationDegrees);

        // The translation this bone holds at `t`, same clamp and easing as
        // RotationAt -- see RigKeyframe's own header for why this exists.
        public float DxAt(float t) => ValueAt(t, static k => k.DxPixels);
        public float DyAt(float t) => ValueAt(t, static k => k.DyPixels);

        // SMOOTHSTEP between the keyframes either side of `t`
        // (p*p*(3-2p)), not linear: velocity is zero AT every keyframe, so
        // a looping clip wraps with no seam and a mid-clip keyframe has no
        // mechanical corner -- see RigAnimationContentTests
        // .TheIdleClipHasNoMechanicalCorner, the regression guard this
        // curve exists to satisfy. Mid-segment velocity peaks at 1.5x the
        // linear rate, so a sharp impact keyframe (attack/hurt) still
        // reads as a snap, not just idle motion made mushy.
        private float ValueAt(float t, Func<RigKeyframe, float> select)
        {
            if (Keyframes.Length == 0) return 0f;
            if (Keyframes.Length == 1 || t <= Keyframes[0].TimeSeconds) return select(Keyframes[0]);

            for (int i = 1; i < Keyframes.Length; i++)
            {
                if (t > Keyframes[i].TimeSeconds) continue;

                var a = Keyframes[i - 1];
                var b = Keyframes[i];
                float span = b.TimeSeconds - a.TimeSeconds;
                float p = span <= 0f ? 1f : (t - a.TimeSeconds) / span;
                float eased = p * p * (3f - 2f * p);
                return select(a) + (select(b) - select(a)) * eased;
            }

            return select(Keyframes[Keyframes.Length - 1]);
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
