using System;

namespace PrincesPalace.Domain.Stage
{
    // How long each frame of a stance is held.
    //
    // NOT FrameTiming (UnityEngine has one) and not StanceTiming (StanceManifest
    // has one, a struct, four lines away in this same namespace). Both were
    // tried; the compiler caught both, which is the only reason this is not
    // still called one of them.
    //
    // Every frame used to be held for exactly secondsPerFrame, and THAT is what
    // read as choppy rather than the frame count. Six frames is not a low
    // number -- hand-drawn animation runs on twos all the time -- but six
    // frames at a metronome-flat 80ms is, because nothing in the motion is
    // accelerating. A drawing held for the same time as every other drawing
    // reads as a slideshow however good it is.
    //
    // DERIVED FROM impactFrame, WHICH EVERY STANCE ALREADY AUTHORS. The
    // manifest has said where the blow lands since it was written; it was only
    // ever used to fire damage and sound. The same number describes the shape
    // of the motion for free: everything before it is a wind-up and everything
    // after it is a follow-through, which is the whole of what timing needs to
    // know. No new field, and every stance in the game changes on the same
    // edit.
    //
    // TOTAL DURATION IS PRESERVED EXACTLY. The weights are normalised to sum to
    // the frame count, so a stance still takes secondsPerFrame x FrameCount.
    // That is not tidiness -- FightBeatPlayer subtracts exactly that product
    // from its beat budget, and a stance that quietly ran long would eat the
    // pause after it and desynchronise every beat that followed.
    public static class FrameHoldCurve
    {
        // The wind-up: a long hold on the opening pose, accelerating into the
        // blow. The last frame before impact is the fastest thing on screen.
        private const float WindUpStart = 1.70f;
        private const float WindUpEnd = 0.55f;

        // The follow-through: snaps out of the impact, then settles long. The
        // final frame holds nearly three times the shortest one, which is what
        // gives a blow somewhere to land.
        private const float FollowStart = 0.60f;
        private const float FollowEnd = 1.80f;

        // What frame `index` is worth, before normalisation. Exposed for tests
        // and for anyone asking "is this curve doing anything" without a
        // stance to hand.
        public static float RawWeight(int index, int frameCount, int impactFrame)
        {
            if (frameCount <= 1) return 1f;

            int impact = Clamp(impactFrame, 1, frameCount);

            if (index < impact)
            {
                // Eased so the hold is generous and the acceleration late,
                // rather than a straight ramp that starts leaving immediately.
                float p = impact <= 1 ? 1f : index / (float)(impact - 1);
                return Lerp(WindUpStart, WindUpEnd, EaseIn(p));
            }

            int after = frameCount - impact;
            float q = after <= 1 ? 1f : (index - impact) / (float)(after - 1);
            return Lerp(FollowStart, FollowEnd, EaseOut(q));
        }

        // The shortest a drawing is ever shown for.
        //
        // ADDED AFTER PLAYING IT. The first pass had no floor and put the two
        // frames either side of the blow at 35 and 38ms, which is barely two
        // display frames at 60Hz -- short enough that a pose does not register
        // as a pose at all. It reads as a flicker between the drawings either
        // side of it, which is worse than not having drawn it: the emphasis
        // was there, and what it emphasised was invisible.
        //
        // 55ms is a bit over three frames at 60Hz. Fast enough to still be the
        // snap, long enough to be seen.
        public const float MinimumHoldSeconds = 0.055f;

        // How long to hold frame `index`, in seconds.
        public static float HoldFor(int index, int frameCount, int impactFrame, float secondsPerFrame)
        {
            if (frameCount <= 1 || secondsPerFrame <= 0f) return Math.Max(0f, secondsPerFrame);

            var holds = Holds(frameCount, impactFrame, secondsPerFrame);
            return holds[index < 0 ? 0 : index >= frameCount ? frameCount - 1 : index];
        }

        // Every frame's hold, which is the only honest way to apply the floor:
        // raising one frame has to take the time from the others, so no frame
        // can be computed alone.
        public static float[] Holds(int frameCount, int impactFrame, float secondsPerFrame)
        {
            var holds = new float[Math.Max(0, frameCount)];
            if (frameCount <= 0) return holds;

            float total = secondsPerFrame * frameCount;
            if (frameCount == 1 || secondsPerFrame <= 0f)
            {
                for (int i = 0; i < frameCount; i++) holds[i] = Math.Max(0f, secondsPerFrame);
                return holds;
            }

            float weightSum = 0f;
            for (int i = 0; i < frameCount; i++) weightSum += RawWeight(i, frameCount, impactFrame);
            if (weightSum <= 0f)
            {
                for (int i = 0; i < frameCount; i++) holds[i] = secondsPerFrame;
                return holds;
            }

            // Normalised against the frame count, so the holds sum to
            // secondsPerFrame x frameCount -- which FightBeatPlayer's beat
            // budget depends on.
            for (int i = 0; i < frameCount; i++)
            {
                holds[i] = secondsPerFrame * RawWeight(i, frameCount, impactFrame) * frameCount / weightSum;
            }

            // The floor cannot exceed the average, or holding every frame to it
            // would change the total. A sheet authored faster than the floor
            // keeps its own pace and simply gets no emphasis, which is the
            // right answer: it asked to be fast.
            float floor = Math.Min(MinimumHoldSeconds, total / frameCount);

            float deficit = 0f;
            float surplus = 0f;
            for (int i = 0; i < frameCount; i++)
            {
                if (holds[i] < floor) deficit += floor - holds[i];
                else surplus += holds[i] - floor;
            }

            if (deficit <= 0f || surplus <= 0f) return holds;

            // What the short frames gain, the long frames pay, in proportion to
            // how far above the floor they were. The shape survives; only its
            // contrast is reduced.
            float keep = 1f - deficit / surplus;
            for (int i = 0; i < frameCount; i++)
            {
                holds[i] = holds[i] < floor ? floor : floor + (holds[i] - floor) * keep;
            }

            return holds;
        }

        private static float EaseIn(float t) => t * t;

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
