using UnityEngine;
using PrincesPalace.Domain.Stage;

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

        // THE FUTURE ARRIVED. This was a bool "reserved for a future
        // breathing/idle loop", nothing read it, and it was always false --
        // which is a fair description of the idle animations themselves, since
        // nothing drove those either. It is now the STYLE of loop, because a
        // caller that has decided to loop something still has to know how (see
        // Domain/Stage/LoopCycle), and whether-to-loop is the caller's question
        // rather than the sheet's: FightController loops the idle stance and
        // plays every other one once.
        public readonly StanceLoop Loop;

        // Whether the figure's own centre is held still across these frames.
        // See StanceTiming.Steady for the drift this cancels and why a swing
        // must not have it cancelled.
        public readonly bool Steady;

        // Seconds the peak of a ping-pong loop is held on top of the sweep.
        // Zero is the default symmetric linger. See StanceTiming.EndHoldSeconds
        // and LoopCycle.FrameAt -- consulted only where FightController loops
        // this stance, which today is the idle.
        public readonly float EndHoldSeconds;

        // Whether this one-shot plays back down to its first frame after its
        // last -- the shell uncurling. See StanceTiming.ReturnsToStart; the
        // beat player plays the reverse tail.
        public readonly bool ReturnsToStart;

        public StanceAnimation(Sprite[] frames, float secondsPerFrame, int impactFrame, int soundFrame,
                               StanceLoop loop = StanceLoop.PingPong, bool steady = false,
                               float endHoldSeconds = 0f, bool returnsToStart = false)
        {
            Frames = frames;
            SecondsPerFrame = Mathf.Max(0f, secondsPerFrame);
            ImpactFrame = Mathf.Max(1, impactFrame);
            SoundFrame = Mathf.Max(1, soundFrame);
            Loop = loop;
            Steady = steady;
            EndHoldSeconds = Mathf.Max(0f, endHoldSeconds);
            ReturnsToStart = returnsToStart;
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
