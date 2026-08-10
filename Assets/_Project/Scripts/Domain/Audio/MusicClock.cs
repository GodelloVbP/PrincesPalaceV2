using System;

namespace PrincesPalace.Domain.Audio
{
    // When a layer change is allowed to start.
    //
    // An instant volume jump to or from zero clicks; worse, a layer arriving
    // mid-phrase sounds like a bug rather than a swell. Both are fixed by
    // fading, and by landing the fade on a downbeat.
    //
    // This is exact arithmetic rather than a guess, and only because every
    // stem in a set was scheduled from ONE shared dspTime (see
    // MusicController's set start). That is the real reason that detail
    // matters: without a single known t0 there is no boundary to compute, and
    // the whole thing degrades to "fade whenever the frame happened to land",
    // which is what makes reactive music sound reactive.
    //
    // Pure and engine-free so the arithmetic can be tested without an audio
    // device — the one part of this system a headless runner can prove
    // outright.
    public static class MusicClock
    {
        // The next boundary at or after `now`, for a set that started at
        // `startedAt` and quantises every `windowSeconds`.
        //
        // AT or after, not strictly after: a change requested exactly on a
        // downbeat should take that downbeat rather than wait a whole bar for
        // the next one. The window is real time, so "exactly" is a measure-
        // zero case in practice, but the boundary condition is the one a test
        // can actually pin and the one an off-by-one would live in.
        //
        // Returns `now` when the set has no tempo to quantise against
        // (windowSeconds <= 0) — an unauthored bpm means "change immediately"
        // rather than "never change", because a silent refusal to ever
        // transition is far harder to notice than an unquantised fade.
        public static double NextBoundary(double startedAt, double now, double windowSeconds)
        {
            if (windowSeconds <= 0d || double.IsNaN(windowSeconds) || double.IsInfinity(windowSeconds))
            {
                return now;
            }

            double elapsed = now - startedAt;

            // Before the set has audibly begun — the ScheduleLead window
            // between PlayScheduled and the first sample — the first boundary
            // IS the start.
            if (elapsed <= 0d)
            {
                return startedAt;
            }

            double windows = Math.Ceiling(elapsed / windowSeconds);
            return startedAt + windows * windowSeconds;
        }

        // How long until that boundary. Never negative, so a caller can wait
        // on it without a guard of its own.
        public static double SecondsUntilNextBoundary(double startedAt, double now, double windowSeconds)
        {
            return Math.Max(0d, NextBoundary(startedAt, now, windowSeconds) - now);
        }
    }
}
