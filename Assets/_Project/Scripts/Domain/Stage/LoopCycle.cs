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
    // FrameHoldCurve and HitStop are here: this is arithmetic about a number,
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
        public static float SecondsForCycle(int frameCount, float secondsPerFrame, StanceLoop loop)
        {
            if (frameCount <= 1 || secondsPerFrame <= 0f) return 0f;

            int steps = loop == StanceLoop.PingPong && frameCount > 2
                ? frameCount * 2 - 2
                : frameCount;

            return secondsPerFrame * steps;
        }

        // Which drawing to show at `elapsed` seconds into the loop.
        //
        // Never throws and never returns an out-of-range index: a clock that
        // has run for an hour is the same as one that has run for a second,
        // which is what makes the caller a one-liner with no bookkeeping.
        public static int FrameAt(float elapsed, int frameCount, float secondsPerFrame, StanceLoop loop)
        {
            if (frameCount <= 1) return 0;

            float cycle = SecondsForCycle(frameCount, secondsPerFrame, loop);
            if (cycle <= 0f) return 0;

            // Phase in [0,1). Negative elapsed is not a real case but must not
            // produce a negative index if it ever happens.
            float phase = (float)(elapsed / cycle - Math.Floor(elapsed / cycle));

            if (loop == StanceLoop.Forward || frameCount <= 2)
            {
                int forward = (int)(phase * frameCount);
                return forward >= frameCount ? frameCount - 1 : forward;
            }

            // (1 - cos)/2 sweeps 0 -> 1 -> 0 across the phase, turning smoothly
            // at both ends and lingering there. Rounded rather than floored:
            // the curve is symmetric and flooring would bias every drawing
            // half a step early on the way up and half a step late on the way
            // down, which puts a limp in a motion whose whole point is evenness.
            double swept = (1.0 - Math.Cos(phase * 2.0 * Math.PI)) * 0.5;
            int index = (int)Math.Round(swept * (frameCount - 1));

            return index < 0 ? 0 : index >= frameCount ? frameCount - 1 : index;
        }
    }
}
