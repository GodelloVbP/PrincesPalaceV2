using System;

namespace PrincesPalace.Domain.Ambience
{
    // A 2D offset, engine-free. Domain cannot see Vector2, and these curves are
    // pure arithmetic that has no business needing it.
    public readonly struct Drift2
    {
        public readonly float X;
        public readonly float Y;

        public Drift2(float x, float y) { X = x; Y = y; }

        public static readonly Drift2 Zero = new Drift2(0f, 0f);

        public override string ToString() => $"({X}, {Y})";
    }

    // Every ambient curve in the game, in one testable place.
    //
    // These were six pure static methods sitting on six MonoBehaviours in Core,
    // where the EditMode suite -- Domain-only by asmdef -- could not reach a
    // single one of them. They were written AS seams, with comments explaining
    // that a test could pin them, and then no test ever could. Moving the
    // arithmetic across the boundary is the whole change; the components keep
    // the clock, the rect and the tint.
    public static class AmbienceCurves
    {
        private const float Tau = (float)(2.0 * Math.PI);

        // ---- motes ---------------------------------------------------------

        // An ember fades in FAST and out SLOW as it rises, which is how airborne
        // light actually reads. A symmetric fade looks like a dot being
        // cross-faded.
        public const float MoteFadeInEnds = 0.18f;
        public const float MoteFadeOutBegins = 0.68f;

        public static float MoteAlphaAt(float progress)
        {
            progress = Clamp01(progress);

            if (progress < MoteFadeInEnds) return progress / MoteFadeInEnds;
            if (progress > MoteFadeOutBegins) return (1f - progress) / (1f - MoteFadeOutBegins);
            return 1f;
        }

        // ---- slow drift ----------------------------------------------------

        // Sine on x against cosine on y, so the path is an ELLIPSE rather than a
        // diagonal line -- a layer sliding back and forth along one axis reads
        // as a slipping texture, not as air.
        public static Drift2 DriftOffsetAt(float seconds, Drift2 amplitude, float periodSeconds, float phaseSeconds)
        {
            // A zero or negative period would divide into NaN and strand the
            // rect off-screen permanently, so it degrades to "no drift" rather
            // than destroying the layout.
            if (periodSeconds <= 0f) return Drift2.Zero;

            float angle = (seconds + phaseSeconds) * (Tau / periodSeconds);
            return new Drift2(
                amplitude.X * (float)Math.Sin(angle),
                amplitude.Y * (float)Math.Cos(angle));
        }

        // ---- Ken Burns -----------------------------------------------------

        // NEVER BELOW 1. The layer is the full-bleed background, so any scale
        // under 1 pulls its edges inside the canvas and shows bare camera colour
        // down the sides. The cosine is what guarantees the curve only touches 1
        // at its trough rather than crossing it.
        public static float KenBurnsScaleAt(float seconds, float maxScale, float periodSeconds)
        {
            if (periodSeconds <= 0f) return 1f;

            float t = (1f - (float)Math.Cos(seconds * (Tau / periodSeconds))) * 0.5f;
            return Lerp(1f, maxScale < 1f ? 1f : maxScale, t);
        }

        // ---- star twinkle --------------------------------------------------

        // Each star gets its own period inside a band, so a field of them never
        // pulses in lockstep. The period is derived from the star's own index
        // rather than randomised, so the same scene twinkles the same way twice
        // -- which is what lets a screenshot be compared at all.
        public const float StarMinPeriod = 1.6f;
        public const float StarMaxPeriod = 3.4f;
        public const float StarMinAlphaScale = 0.4f;

        public static float StarPeriodFor(int index, int count)
        {
            if (count <= 1) return StarMinPeriod;

            float t = (float)(index % count) / (count - 1);
            return Lerp(StarMinPeriod, StarMaxPeriod, t);
        }

        // Returns the multiplier on a star's authored alpha, in
        // [StarMinAlphaScale, 1].
        public static float StarAlphaScaleAt(float seconds, float periodSeconds, float phaseSeconds)
        {
            if (periodSeconds <= 0f) return 1f;

            float t = (1f - (float)Math.Cos((seconds + phaseSeconds) * (Tau / periodSeconds))) * 0.5f;
            return Lerp(StarMinAlphaScale, 1f, t);
        }

        // ---- beacon --------------------------------------------------------

        // One clean cosine, deliberately. This is a magic light under a map
        // room and is SUPPOSED to read as a steady pulse -- it is the thing
        // FlickerCurve exists not to be.
        public static float BeaconProgressAt(float seconds, float periodSeconds, float phaseSeconds)
        {
            if (periodSeconds <= 0f) return 0f;

            return (1f - (float)Math.Cos((seconds + phaseSeconds) * (Tau / periodSeconds))) * 0.5f;
        }

        // ---- shared --------------------------------------------------------

        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
