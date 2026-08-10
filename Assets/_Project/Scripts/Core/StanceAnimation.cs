using UnityEngine;

namespace PrincesPalace
{
    // One stance's frame sequence and its timing, resolved once per
    // (folder, stance) pair by StanceAnimationLibrary and cached there.
    //
    // A flat single PNG (today's only shape) resolves to a ONE-frame
    // animation rather than a special case — every consumer (frame
    // stepping, impact timing, sound timing) has exactly one code path,
    // and a 1-frame animation's own math degenerates to today's behavior
    // by construction (see FightController.Beats.cs's StepActorFrames).
    //
    // Deliberately the single seam a future config source would plug into
    // (per-actor authored timings, a Loop flag actually wired up) without
    // touching resolution or playback at all — see StanceAnimationLibrary's
    // own header comment.
    public readonly struct StanceAnimation
    {
        public static readonly StanceAnimation Empty = default;

        public readonly Sprite[] Frames;
        public readonly float SecondsPerFrame;

        // 1-based, matching the vfxImpactFrame convention skills.json/
        // enemies.json already use ("frame 3 of 6").
        public readonly int ImpactFrame;

        // 1-based. Defaults to ImpactFrame at construction, so "the sound
        // lands with the hit" costs nothing to author and only needs
        // overriding when a wind-up sound should play before the swing
        // connects.
        public readonly int SoundFrame;

        // Reserved for a future breathing/idle loop. Always false today —
        // nothing reads it yet, and it exists so that feature is additive
        // rather than a signature change when it arrives.
        public readonly bool Loop;

        public StanceAnimation(Sprite[] frames, float secondsPerFrame, int impactFrame, int soundFrame, bool loop)
        {
            Frames = frames;
            SecondsPerFrame = Mathf.Max(0f, secondsPerFrame);
            ImpactFrame = Mathf.Max(1, impactFrame);
            SoundFrame = Mathf.Max(1, soundFrame);
            Loop = loop;
        }

        public bool IsEmpty => Frames == null || Frames.Length == 0;

        public int FrameCount => Frames?.Length ?? 0;

        // Clamped so a stale or out-of-range index (an animation re-cut
        // shorter since the index was set) never throws — a held frame
        // beats a missing sprite.
        public Sprite FrameAt(int index)
        {
            if (IsEmpty)
            {
                return null;
            }

            return Frames[Mathf.Clamp(index, 0, Frames.Length - 1)];
        }
    }
}
