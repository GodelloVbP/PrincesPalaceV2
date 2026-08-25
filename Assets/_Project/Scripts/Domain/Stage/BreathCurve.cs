using System;

namespace PrincesPalace.Domain.Stage
{
    // HOW BIG A FIGURE IS AT REST, given a clock.
    //
    // Beside LoopCycle, which answers the neighbouring question (which drawing
    // is showing) for the same reason: it is arithmetic about a number, it
    // decides what the stage looks like while nothing is happening, and a rule
    // an EditMode test cannot reach is a rule nothing checks.
    //
    // THE PROBLEM IT SOLVES. LoopCycle made the three actors with a six-frame
    // idle step their frames, and that is as far as sprite swapping can go.
    // Measured on the Forest Warden's delivered idle, aligned exactly as
    // FightController.StageVisuals aligns it:
    //
    //   the head travels 7px on a 470px figure -- 1.5%, under 4px on screen
    //   once the stage's depth scale is applied -- while 13% of the silhouette
    //   is redrawn between adjacent frames, and 25% of it across the head and
    //   its mushrooms alone.
    //
    // So the creature barely moves and is heavily redrawn, which is what reads
    // as boiling rather than as breathing. The Treant and the Beetle are worse
    // on churn (22.6% and 25.5%) and better on travel. No easing fixes that,
    // because there is nothing between the drawings to ease.
    //
    // AND FOUR OF THE SIX FIGURES ON A TYPICAL STAGE HAVE NO IDLE SHEET AT
    // ALL. The rat, the golem, the bog witch and Shawn ship a single idle.png,
    // so LoopCycle's driver takes one look at FrameCount <= 1 and returns.
    // They stood perfectly still next to a wobbling troll, which is what made
    // the troll look broken rather than merely rough.
    //
    // A TRANSFORM ANSWERS BOTH AT ONCE, and that is why this is a curve rather
    // than more art. It runs at the display's rate instead of the sheet's, so
    // the eye tracks a smooth motion and stops hunting the swaps; and it needs
    // no drawings, so the four still figures get it for nothing.
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
        // on screen continuously and unbroken, where the sheet-driven loops it
        // sits beside run 1.2s and read as hurried for creatures this heavy.
        public const float PeriodSeconds = 2.8f;

        // How much taller a figure gets at the top of a full breath.
        //
        // Against the measured sheets rather than picked: the Warden's own
        // drawn breath moves its head 1.5% of its height and the Treant's
        // moves 5%. 2% sits between them, and it is the SMOOTHNESS rather than
        // the size that does the work -- 2% swept continuously is far more
        // legible than 5% delivered in six steps.
        public const float FullAmplitude = 0.02f;

        // What an actor gets when its own sheet already breathes.
        //
        // Not zero, and not one. A six-frame idle is already moving the figure
        // -- driving a full transform breath on top of the Treant's 22px of
        // drawn head travel would read as two animations disagreeing -- but
        // those sheets are exactly the ones whose motion is buried under
        // churn, so removing the transform entirely gives back the problem
        // this file exists for. A third of the amplitude is enough to carry
        // the eye between drawings without competing with them.
        //
        // The DEFAULT, overridable per actor in StanceManifest.json, the same
        // shape StanceTiming.Steady takes: a rule supplies the usual answer
        // and a sheet that disagrees says so.
        public const float SheetScale = 0.35f;

        // How far apart two figures' breaths are pushed, in seconds.
        //
        // The same argument as FightController's IdlePhaseFrames, which
        // separates their sheet loops: two of anything breathing in lockstep
        // read as one animation drawn twice. Independent of that offset rather
        // than derived from it, because a sheet's phase is measured in its own
        // frames and this is measured against a fixed period -- tying them
        // together would make a slow sheet separate its breaths less.
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
        // `scale` multiplies the amplitude and is what the manifest authors:
        // 1 for a still drawing, SheetScale for one whose frames already move.
        // Zero or less returns zero, so "this actor does not breathe" needs no
        // branch at the call site.
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

        // 0 -> 1 with both ends flat. The same (1-cos)/2 LoopCycle sweeps its
        // frame index along, and for the same reason: it turns at the ends
        // rather than reversing at them.
        private static float Rise(float t)
        {
            float clamped = t < 0f ? 0f : t > 1f ? 1f : t;
            return (float)((1.0 - Math.Cos(clamped * Math.PI)) * 0.5);
        }
    }
}
