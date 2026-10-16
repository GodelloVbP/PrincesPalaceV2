using System;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Rewards
{
    // THE RUN-UP: the time from a won fight's killing blow to the moment the
    // Reckoning wipe starts (docs/PLAN_REWARD_SCREENS.md C0). One pure model;
    // FightController plays it back and never re-derives a time.
    //
    // CLOCK. t = 0 is the contact of the final blow. The stop and the last
    // enemy's death fade run on the fight's own pace (FightBeatPlayer.Pace), so
    // they arrive here as wall seconds already; what the run-up ADDS (the hop,
    // the wash, the collapse, the voices' delay) is authored at the default
    // battle speed and goes through ReckoningTime.Scale, so it speeds up at 1.5x and 2x
    // and never slows below its authored time. Only the stop and the fade can
    // hold the wipe back, and CapSeconds ends the wait whatever they do.
    //
    // NO DEAD AIR. Everything that moves starts when the stop releases (the
    // kill registering): the hops, the wash, the collapse and the boss's
    // aftershock. The fade starts there too (FightBeatPlayer skips the final
    // beat's settle); only the sting waits for the fade's hold to end.
    public readonly struct RunUpSchedule
    {
        // Final contact to wipe start, at any preset. A fade still running
        // here finishes under the Reckoning's gloom.
        public const float CapSeconds = 1.3f;

        // The stop never holds longer than this in wall time, so a slow preset
        // cannot push the stop past the cap.
        public const float StopCeilingSeconds = 0.9f;

        // The survivors hop: up, back down, then a small squash on the landing.
        public const float LiftPixels = 24f;
        public const float LiftSeconds = 0.25f;
        public const float LiftStaggerSeconds = 0.08f;
        public const float LiftRiseFraction = 0.35f;
        public const float LandYScale = 0.94f;
        public const float LandSeconds = 0.06f;

        // The light turns: a warm wash rises behind the party while the
        // enemy half dims. The wash's size and brightness are the style's.
        public const float WashSeconds = 0.5f;
        public const float EnemyDim = 0.10f;

        // The last enemy sags before it fades (the style says how far).
        public const float CollapseSeconds = 0.18f;

        // The voices follow the sting by this much.
        public const float VoiceDelaySeconds = 0.2f;

        // How a class ends: normal is the baseline, a boss is clearly bigger,
        // an elite sits between.
        public readonly struct Style
        {
            public readonly float WashScale;
            public readonly float WashAlpha;
            public readonly float CollapseYScale;
            public readonly float CollapseTiltDegrees;
            public readonly float Aftershock;

            public Style(float washScale, float washAlpha, float collapseYScale, float collapseTilt, float aftershock)
            {
                WashScale = washScale;
                WashAlpha = washAlpha;
                CollapseYScale = collapseYScale;
                CollapseTiltDegrees = collapseTilt;
                Aftershock = aftershock;
            }

            public bool Collapses => CollapseYScale < 1f;
        }

        public static Style StyleOf(EncounterClass fightClass)
        {
            switch (fightClass)
            {
                case EncounterClass.Boss: return new Style(1.6f, 0.32f, 0.62f, 10f, 0.8f);
                case EncounterClass.Elite: return new Style(1.25f, 0.22f, 0.88f, 4f, 0f);
                default: return new Style(1f, 0.15f, 0.94f, 2f, 0f);
            }
        }

        // The final-blow stop in wall seconds at this pace, under the ceiling.
        public static float FinalStopWall(EncounterClass fightClass, float pace) =>
            pace <= 0f ? 0f : Math.Min(StopCeilingSeconds, HitStop.FinalBlowSeconds(fightClass) / pace);

        public readonly Style Look;
        public readonly float StopEnd;
        public readonly float StingAt;
        public readonly float VoiceAt;
        public readonly float WashStart;
        public readonly float WashSecondsWall;
        public readonly float LiftSecondsWall;
        public readonly float LiftStaggerWall;
        public readonly float LandSecondsWall;
        public readonly float CollapseSecondsWall;

        // Wipe start when nothing is pressed: the fade's end, or the cap.
        public readonly float WipeAt;

        // Whether a fade was running when the stop ended. False: nothing to
        // wait on, the wipe follows the stop.
        public readonly bool HasFade;

        // Set by SkippedAt: the hop and the wash are at their end state and
        // the fade is complete.
        public readonly bool Skipped;

        private RunUpSchedule(Style look, float stopEnd, float stingAt, float voiceAt, float scale,
                              float wipeAt, bool hasFade, bool skipped)
        {
            Look = look;
            StopEnd = stopEnd;
            StingAt = stingAt;
            VoiceAt = voiceAt;
            WashStart = stopEnd;
            WashSecondsWall = WashSeconds * scale;
            LiftSecondsWall = LiftSeconds * scale;
            LiftStaggerWall = LiftStaggerSeconds * scale;
            LandSecondsWall = LandSeconds * scale;
            CollapseSecondsWall = CollapseSeconds * scale;
            WipeAt = wipeAt;
            HasFade = hasFade;
            Skipped = skipped;
        }

        // pace: FightBeatPlayer.Pace (the stop and the fade divide by it).
        // fadeHoldAuthored / fadeAuthored: the last enemy's StageDeathFade
        // hold and fade at 1x pace; both zero when no fade is running.
        public static RunUpSchedule For(EncounterClass fightClass, float pace, float presetDisplay,
                                        float fadeHoldAuthored, float fadeAuthored) =>
            Build(StyleOf(fightClass), FinalStopWall(fightClass, pace), pace, presetDisplay, fadeHoldAuthored, fadeAuthored);

        // A fight that ended with no final-blow beat to stop on (a fixture, a
        // kill outside the beat pipeline): the same run-up from t = 0.
        public static RunUpSchedule WithoutStop(EncounterClass fightClass, float pace, float presetDisplay,
                                                float fadeHoldAuthored, float fadeAuthored) =>
            Build(StyleOf(fightClass), 0f, pace, presetDisplay, fadeHoldAuthored, fadeAuthored);

        // The leader-fall ending has already waited out every body: only the
        // sting, the voices and the wipe remain, all at once.
        public static RunUpSchedule Settled(float presetDisplay) =>
            Build(StyleOf(EncounterClass.Normal), 0f, 1f, presetDisplay, 0f, 0f);

        private static RunUpSchedule Build(Style look, float stop, float pace, float presetDisplay,
                                           float fadeHoldAuthored, float fadeAuthored)
        {
            float scale = ReckoningTime.Scale(presetDisplay);
            bool hasFade = fadeAuthored > 0f;
            float hold = pace <= 0f ? 0f : fadeHoldAuthored / pace;
            float fade = pace <= 0f ? 0f : fadeAuthored / pace;

            float fadeEnd = hasFade ? stop + hold + fade : stop;
            float wipe = Math.Max(stop, Math.Min(CapSeconds, fadeEnd));
            float sting = Math.Min(hasFade ? stop + hold : stop, wipe);

            return new RunUpSchedule(look, stop, sting, sting + VoiceDelaySeconds * scale, scale, wipe, hasFade, false);
        }

        // When survivor `rank` (squad order, 0 first) starts to hop, and when
        // it lands.
        public float LiftAt(int rank) => StopEnd + rank * LiftStaggerWall;
        public float LandAt(int rank) => LiftAt(rank) + LiftSecondsWall;

        // The state when a press arrives at `t`: the wipe starts now, the
        // sting has played by now (it is never cut, and never skipped), and
        // the voices stay on their delay behind it.
        public RunUpSchedule SkippedAt(float t)
        {
            float now = Math.Min(t, WipeAt);
            float sting = Math.Min(StingAt, now);
            return new RunUpSchedule(Look, StopEnd, sting, Math.Min(VoiceAt, sting + VoiceDelaySeconds),
                WashSecondsWall / WashSeconds, now, HasFade, true);
        }

        // Whether the wipe starts at `t`, given whether the last fade is still
        // running. The wipe waits for the fade or the cap, whichever is first.
        public bool WipeDue(float t, bool fadeRunning) =>
            Skipped || t >= WipeAt || (t >= StopEnd && !fadeRunning);
    }
}
