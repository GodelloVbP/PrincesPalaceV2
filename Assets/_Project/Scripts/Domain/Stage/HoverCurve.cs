using System;

namespace PrincesPalace.Domain.Stage
{
    // HOW HIGH OFF ITS GROUND LINE AN AIRBORNE ACTOR RIDES, and how it bobs
    // there. Authored per actor in StanceManifest.json; default is grounded.
    //
    // Both numbers are STAGE pixels -- the slot's own coordinate space, the
    // one FightStageAnchors places marks in -- not canvas pixels of the
    // drawing. A far-slot figure is drawn at roughly half size, so a hover
    // authored in its canvas would halve with it and a flyer would ride
    // lower the further back it stood. In stage pixels it rides where it was
    // told to, at every depth.
    public readonly struct HoverSpec
    {
        public readonly float Height;
        public readonly float Bob;
        public readonly float PeriodSeconds;

        public HoverSpec(float height, float bob, float periodSeconds)
        {
            Height = height < 0f ? 0f : height;
            Bob = bob < 0f ? 0f : bob;
            PeriodSeconds = periodSeconds <= 0f ? HoverCurve.DefaultPeriodSeconds : periodSeconds;
        }

        public static readonly HoverSpec Grounded = default;

        // A grounded actor never hears about any of this: the idle driver
        // reads this once per frame and does nothing for a false.
        public bool IsAirborne => Height > 0f || Bob > 0f;
    }

    public static class HoverCurve
    {
        // Slower than the 2.8s breath's half-cycle and quicker than its whole
        // one, so a flyer beside a breathing figure is visibly on its own
        // rhythm rather than in lockstep with the swell next to it.
        public const float DefaultPeriodSeconds = 2.4f;

        // Where a flyer is, in stage pixels above its ground line, at `seconds`.
        //
        // A plain sine, and unlike BreathCurve it is meant to be one. A
        // breath is skewed because an inhale is quicker than an exhale; a
        // hover has no such asymmetry -- the thing being sold is a body held
        // up by wingbeats too small to draw, and the smooth dwell a sine has
        // at both ends is exactly that. Height is the resting altitude and
        // the bob is the excursion either side of it, so the curve never dips
        // below Height - Bob: a flyer authored to clear the front row still
        // clears it at the bottom of the bob.
        public static float At(float seconds, HoverSpec spec)
        {
            if (!spec.IsAirborne) return 0f;

            double turns = seconds / spec.PeriodSeconds;
            float bob = (float)Math.Sin(turns * 2.0 * Math.PI);
            return spec.Height + spec.Bob * bob;
        }
    }
}
