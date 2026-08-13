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

        // How long to hold frame `index`, in seconds.
        public static float HoldFor(int index, int frameCount, int impactFrame, float secondsPerFrame)
        {
            if (frameCount <= 1 || secondsPerFrame <= 0f) return Math.Max(0f, secondsPerFrame);

            float total = 0f;
            for (int i = 0; i < frameCount; i++)
            {
                total += RawWeight(i, frameCount, impactFrame);
            }

            if (total <= 0f) return secondsPerFrame;

            // Normalised against the frame count, so the sum of every hold is
            // secondsPerFrame x frameCount to the last float.
            float weight = RawWeight(index, frameCount, impactFrame) * frameCount / total;
            return secondsPerFrame * weight;
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
