using System;

namespace PrincesPalace.Domain.Stage
{
    // How a stance is played when it does not end -- which today means idles,
    // and tomorrow means anything else that breathes.
    public enum StanceLoop
    {
        // 0..N-1 then back to 0. Right for a sheet the artist drew as a
        // complete cycle, where the last drawing already leads into the first.
        Forward = 0,

        // 0..N-1..0, with the turn-arounds held longest. Right for a sheet
        // drawn as HALF a cycle -- rest through to full inhale -- which is how
        // most idle sheets are actually authored, and the only way to play one
        // without a hard cut at the wrap.
        PingPong = 1,
    }

    // WHICH DRAWING A LOOPING STANCE IS SHOWING, given a clock.
    //
    // In Domain, and not beside the coroutine that calls it, for the reason
    // BreathCurve and HitStop are here: this is arithmetic about a number,
    // it decides what the stage looks like at rest, and a rule an EditMode
    // test cannot reach is a rule nothing checks.
    //
    // THE PROBLEM IT SOLVES, in the words it was reported in: "I clearly see
    // that it is a loop of 6 sprites". Six drawings stepped at a flat pace and
    // wrapped hard at the end is a flick-book, and no amount of drawing fixes
    // it -- the tell is the RHYTHM, not the count. Two things give it away and
    // this file removes both:
    //
    //   THE WRAP. A forward loop cuts from the last drawing to the first, and
    //   on a sheet drawn as half a breath that cut is the single biggest change
    //   in the whole cycle. Measured on the Treant's idle: the five in-sequence
    //   steps average 28 units of pixel difference and the wrap is 34, the
    //   largest of the six. The eye finds the largest change and locks onto it,
    //   which is precisely how a loop announces its own length.
    //
    //   THE METRONOME. Every drawing held for exactly the same time reads as a
    //   machine. A breath is slowest at the top and bottom and quickest in
    //   between, and that is a timing fact rather than a drawing one.
    //
    // A RAISED COSINE ANSWERS BOTH AT ONCE, which is why the ping-pong branch
    // is one line of trigonometry rather than a triangle wave plus an easing
    // table. Sweeping the frame index along (1-cos)/2 turns at the ends by
    // construction -- so there is no wrap to see -- and spends most of its time
    // near those ends, which is the easing. One formula, and the two symptoms
    // are the same symptom.
    public static class LoopCycle
    {
        // How long one full pass takes, at the sheet's authored pace.
        //
        // A PING-PONG CYCLE IS TWICE THE SHEET, less the two turn-around
        // drawings that would otherwise be shown twice in a row. Holding the
        // authored secondsPerFrame as the per-DRAWING pace rather than
        // stretching it to fit is what keeps "0.14" meaning the same thing on
        // a looping stance as on a swing.
        //
        // THE PEAK HOLD IS ADDED, NOT STOLEN. `endHoldSeconds` is a pause at
        // the top of a ping-pong's arc -- the full-inhale drawing, held a beat
        // longer before the exhale -- and it lengthens the cycle rather than
        // slowing the sweep, so a sheet that asked for 0.14s a drawing still
        // gets it and the hold is extra time on top. Meaningless on a Forward
        // loop, which wraps rather than reverses and so has no peak to dwell
        // on; ignored there.
        public static float SecondsForCycle(int frameCount, float secondsPerFrame, StanceLoop loop,
                                            float endHoldSeconds = 0f)
        {
            if (frameCount <= 1 || secondsPerFrame <= 0f) return 0f;

            bool pingPong = loop == StanceLoop.PingPong && frameCount > 2;
            int steps = pingPong ? frameCount * 2 - 2 : frameCount;

            float sweep = secondsPerFrame * steps;
            if (pingPong && endHoldSeconds > 0f) sweep += endHoldSeconds;

            return sweep;
        }

        // Which drawing to show at `elapsed` seconds into the loop.
        //
        // Never throws and never returns an out-of-range index: a clock that
        // has run for an hour is the same as one that has run for a second,
        // which is what makes the caller a one-liner with no bookkeeping.
        public static int FrameAt(float elapsed, int frameCount, float secondsPerFrame, StanceLoop loop,
                                  float endHoldSeconds = 0f)
        {
            if (frameCount <= 1) return 0;

            float position = FramePositionAt(elapsed, frameCount, secondsPerFrame, loop, endHoldSeconds);

            if (loop == StanceLoop.Forward || frameCount <= 2)
            {
                int forward = (int)position;
                return forward >= frameCount ? frameCount - 1 : forward < 0 ? 0 : forward;
            }

            // Rounded rather than floored: the curve is symmetric and flooring
            // would bias every drawing half a step early on the way up and half
            // a step late on the way down, which puts a limp in a motion whose
            // whole point is evenness.
            int index = (int)Math.Round(position);
            return index < 0 ? 0 : index >= frameCount ? frameCount - 1 : index;
        }

        // WHERE THE LOOP IS BETWEEN TWO DRAWINGS, not which drawing is nearest.
        //
        // FrameAt is this value rounded (ping-pong) or floored (forward), and
        // is deliberately expressed in terms of it so the two can never
        // disagree about where the animation has got to. The fractional part is
        // what a cross-dissolve needs: at 12 frames on a 0.16s pace a sheet
        // shows about six drawings a second, which is visibly a slideshow when
        // each one is simply swapped in, and the missing information is not
        // more frames but the position BETWEEN them, which was already being
        // computed here and thrown away by the rounding.
        //
        // Range is [0, frameCount-1] for a ping-pong and [0, frameCount) for a
        // forward loop, matching what each mode's own index means.
        public static float FramePositionAt(float elapsed, int frameCount, float secondsPerFrame,
                                            StanceLoop loop, float endHoldSeconds = 0f)
        {
            if (frameCount <= 1) return 0f;

            float cycle = SecondsForCycle(frameCount, secondsPerFrame, loop, endHoldSeconds);
            if (cycle <= 0f) return 0f;

            // Phase in [0,1). Negative elapsed is not a real case but must not
            // produce a negative index if it ever happens.
            float phase = (float)(elapsed / cycle - Math.Floor(elapsed / cycle));

            if (loop == StanceLoop.Forward || frameCount <= 2)
            {
                return phase * frameCount;
            }

            // A PING-PONG IS A RISE, AN OPTIONAL HELD PEAK, THEN A FALL, and
            // with no hold it is exactly the single raised cosine this used to
            // be. Rise() below is (1-cos)/2 over a 0..1 sweep, smooth at both
            // ends; running it up over the first half and back down over the
            // last half is algebraically identical to (1-cos(2*pi*phase))/2 --
            // so endHoldSeconds == 0 leaves every existing frame unchanged, and
            // a hold simply flattens the very top for a while.
            float hold = endHoldSeconds > 0f ? endHoldSeconds : 0f;
            float t = phase * cycle;          // seconds into the cycle
            float half = (cycle - hold) * 0.5f;   // the rise and the fall are equal

            double swept;
            if (t < half)
            {
                swept = Rise(t / half);                    // 0 -> peak
            }
            else if (t < half + hold)
            {
                swept = 1.0;                               // held at the peak
            }
            else
            {
                swept = Rise(1f - (t - half - hold) / half);   // peak -> 0
            }

            return (float)(swept * (frameCount - 1));
        }

        // 0 -> 1 with both ends flat, the raised cosine LoopCycle has always
        // swept its frame index along. Shared by the rise and the fall so a
        // held peak inserted between them turns smoothly into and out of the
        // dwell rather than cornering.
        private static double Rise(float t)
        {
            float clamped = t < 0f ? 0f : t > 1f ? 1f : t;
            return (1.0 - Math.Cos(clamped * Math.PI)) * 0.5;
        }
    }
}
