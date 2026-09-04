using System;

namespace PrincesPalace.Domain.Stage
{
    // HOW BIG A FIGURE IS AT REST, given a clock.
    //
    // Beside LoopCycle, which answers a neighbouring question in the same
    // shape, for the same reason: it is arithmetic about a number, it decides
    // what the stage looks like while nothing is happening, and a rule an
    // EditMode test cannot reach is a rule nothing checks.
    //
    // THE PROBLEM IT SOLVES. Every stance in this game is a single drawing
    // (docs/STANCE_SHEET_SPEC.md), so nothing on the stage moves at all
    // between blows -- six figures standing in a forest, perfectly still,
    // which reads as a paused game rather than as a fight waiting on the
    // player.
    //
    // Frame sheets were the other answer and they were tried first: three
    // actors shipped a six-frame idle and all three boiled rather than
    // breathed. Measured on the Forest Warden's, aligned exactly as
    // FightController.StageVisuals aligned it, the head travelled 7px on a
    // 470px figure -- under 4px on screen once the stage's depth scale was
    // applied -- while 13% of the silhouette was REDRAWN between adjacent
    // frames. The creature barely moved and was heavily redrawn. No easing
    // fixes that, because there is nothing between the drawings to ease.
    //
    // A TRANSFORM ANSWERS BOTH AT ONCE, and that is why this is a curve rather
    // than more art. It runs at the display's rate instead of a sheet's, so
    // the eye tracks a smooth motion; and it needs no drawings, so every
    // figure on the stage gets it for nothing.
    //
    // IT ONLY EVER GROWS. Zero is the authored size and the curve returns
    // 0..+amplitude, never negative. A figure's base scale is what content
    // authored for it (RawEnemyEntry.stageScale times the slot's depth), and a
    // breath centred on that would leave every actor spending half its life
    // smaller than the size somebody chose. Growing from rest costs one
    // asymmetry here and keeps "the authored size" meaning something.
    public static class BreathCurve
    {
        // One full breath, in seconds. Slower than instinct suggests: this is
        // on screen continuously and unbroken, and anything quicker reads as
        // hurried for creatures this heavy.
        public const float PeriodSeconds = 2.8f;

        // How much taller a figure gets at the top of a full breath.
        //
        // Measured rather than picked, off the hand-drawn breaths this
        // replaced: the Warden's own moved its head 1.5% of its height and the
        // Treant's moved 5%. 2% sits between them, and it is the SMOOTHNESS
        // rather than the size that does the work -- 2% swept continuously is
        // far more legible than 5% delivered in six steps.
        public const float FullAmplitude = 0.02f;

        // How far apart two figures' breaths are pushed, in seconds.
        //
        // Two of anything breathing in lockstep read as one animation drawn
        // twice -- the same failure AnchorStageSlots' own note describes for
        // two rats overlapping into "one monster with a spare tail".
        //
        // Deliberately not a whole fraction of the period: 0.63 against 2.8
        // means three figures land at 0, 22% and 45% of a breath rather than
        // meeting again on the fourth.
        public const float PhaseSecondsPerSlot = 0.63f;

        // Where in its breath a figure standing in slot `index` starts.
        // Deterministic, which a capture test depends on -- a random phase
        // would make a screenshot differ run to run.
        public static float PhaseFor(int index)
        {
            return (index < 0 ? 0 : index) * PhaseSecondsPerSlot;
        }

        // How much taller than its authored size a figure is at `seconds`.
        //
        // `scale` multiplies the amplitude and is what the manifest authors
        // per actor (StanceManifest.BreathFor), defaulting to 1. Zero or less
        // returns zero, so "this actor does not breathe" needs no branch at
        // the call site.
        public static float At(float seconds, float scale)
        {
            if (scale <= 0f || PeriodSeconds <= 0f) return 0f;

            // Phase in [0,1). Negative time is not a real case but must not
            // produce a negative phase if it ever happens.
            double turns = seconds / PeriodSeconds;
            float phase = (float)(turns - Math.Floor(turns));

            // A BREATH IS NOT A SINE. The inhale is quicker than the exhale
            // and the rest at the bottom is the longest part of it, which is
            // the difference between a creature breathing and a light pulsing.
            // Skewing the phase before the cosine costs three lines and is the
            // whole of that character.
            //
            // A raised cosine either side of the skew point, rather than one
            // cosine over a warped phase: both halves then turn flat at the
            // top and the bottom by construction, so there is no corner where
            // the two meet however far the skew is pushed.
            float swept = phase < InhaleFraction
                ? Rise(phase / InhaleFraction)
                : Rise(1f - (phase - InhaleFraction) / (1f - InhaleFraction));

            return swept * FullAmplitude * scale;
        }

        // How much of the cycle is spent breathing in. Under half, so the
        // exhale and the pause that follows it take the rest.
        private const float InhaleFraction = 0.4f;

        // 0 -> 1 with both ends flat -- it turns at the ends rather than
        // reversing at them, so there is no corner where the halves meet.
        private static float Rise(float t)
        {
            float clamped = t < 0f ? 0f : t > 1f ? 1f : t;
            return (float)((1.0 - Math.Cos(clamped * Math.PI)) * 0.5);
        }
    }
}
