using System;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Rewards
{
    // THE RUN-UP: the time from a won fight's killing blow to the moment the
    // Reckoning's wipe starts (docs/PLAN_REWARD_SCREENS.md C0). One pure
    // model; FightController plays it back and never re-derives a time.
    //
    // CLOCK. t = 0 is the contact of the final blow. The stop and the last
    // enemy's death fade run on the fight's own pace (FightBeatPlayer.Pace), so
    // they arrive here as wall seconds already; what the run-up ADDS (the lift,
    // the wash, the voices' delay) is authored at the default battle speed and
    // goes through TimeScale, so it speeds up at 1.5x and 2x and never slows
    // below its authored time. Only the stop and the fade can hold the wipe
    // back, and CapSeconds ends the wait whatever they do, at every preset.
    //
    // THE FADE STARTS WHEN THE STOP ENDS (FightBeatPlayer skips the final
    // beat's settle), so the sting, which marks the kill registering, lands
    // when the fade's hold ends.
    public readonly struct RunUpSchedule
    {
        // Final contact to wipe start, at any preset. A fade still running
        // here finishes under the Reckoning's gloom.
        public const float CapSeconds = 1.3f;

        // The survivors answer the blow: lift, then settle, one after another.
        public const float LiftPixels = 8f;
        public const float LiftSeconds = 0.25f;
        public const float LiftStaggerSeconds = 0.08f;

        // The share of the lift spent going up; the rest is the settle.
        public const float LiftRiseFraction = 0.35f;

        // The light turns: a warm wash behind the party rises while the
        // enemy half of the stage dims.
        public const float WashSeconds = 0.4f;
        public const float WashAlpha = 0.15f;
        public const float EnemyDim = 0.10f;

        // The voices follow the sting by this much.
        public const float VoiceDelaySeconds = 0.2f;

        // min(1, Default / preset): the Reckoning's rule for every duration it
        // adds (C10). A non-positive preset reads as the default.
        public static float TimeScale(float presetDisplay) =>
            presetDisplay <= 0f ? 1f : Math.Min(1f, BattleSpeed.DefaultDisplay / presetDisplay);

        public readonly float StopEnd;
        public readonly float StingAt;
        public readonly float VoiceAt;
        public readonly float WashStart;
        public readonly float WashSecondsWall;
        public readonly float LiftSecondsWall;
        public readonly float LiftStaggerWall;

        // Wipe start when nothing is pressed: the fade's end, or the cap.
        public readonly float WipeAt;

        // Whether a fade was running when the stop ended. False: nothing to
        // wait on, the wipe follows the stop.
        public readonly bool HasFade;

        // Set by SkippedAt: the lift and the wash are at their end state and
        // the fade is complete.
        public readonly bool Skipped;

        private RunUpSchedule(float stopEnd, float stingAt, float voiceAt, float washStart, float washSeconds,
                              float liftSeconds, float liftStagger, float wipeAt, bool hasFade, bool skipped)
        {
            StopEnd = stopEnd;
            StingAt = stingAt;
            VoiceAt = voiceAt;
            WashStart = washStart;
            WashSecondsWall = washSeconds;
            LiftSecondsWall = liftSeconds;
            LiftStaggerWall = liftStagger;
            WipeAt = wipeAt;
            HasFade = hasFade;
            Skipped = skipped;
        }

        // pace: FightBeatPlayer.Pace (the stop and the fade divide by it).
        // fadeHoldAuthored / fadeAuthored: the last enemy's StageDeathFade
        // hold and fade at 1x pace; both zero when no fade is running.
        public static RunUpSchedule For(EncounterClass fightClass, float pace, float presetDisplay,
                                        float fadeHoldAuthored, float fadeAuthored)
        {
            float stop = pace <= 0f ? 0f : HitStop.FinalBlowSeconds(fightClass) / pace;
            return Build(stop, pace, presetDisplay, fadeHoldAuthored, fadeAuthored);
        }

        // A fight that ended with no final-blow beat to stop on (a fixture, a
        // kill outside the beat pipeline): the same run-up from t = 0.
        public static RunUpSchedule WithoutStop(float pace, float presetDisplay,
                                                float fadeHoldAuthored, float fadeAuthored) =>
            Build(0f, pace, presetDisplay, fadeHoldAuthored, fadeAuthored);

        // The leader-fall and darkness endings have already waited out their
        // bodies: only the sting, the voices and the wipe remain, all at once.
        public static RunUpSchedule Settled(float presetDisplay) =>
            Build(0f, 1f, presetDisplay, 0f, 0f);

        private static RunUpSchedule Build(float stop, float pace, float presetDisplay,
                                           float fadeHoldAuthored, float fadeAuthored)
        {
            float scale = TimeScale(presetDisplay);
            bool hasFade = fadeAuthored > 0f;
            float hold = pace <= 0f ? 0f : fadeHoldAuthored / pace;
            float fade = pace <= 0f ? 0f : fadeAuthored / pace;

            float fadeEnd = hasFade ? stop + hold + fade : stop;
            float wipe = Math.Min(CapSeconds, fadeEnd);
            float sting = Math.Min(hasFade ? stop + hold : stop, wipe);
            float washStart = Math.Min(hasFade ? stop + hold : stop, wipe);

            return new RunUpSchedule(stop, sting, sting + VoiceDelaySeconds * scale, washStart,
                WashSeconds * scale, LiftSeconds * scale, LiftStaggerSeconds * scale, wipe, hasFade, false);
        }

        // When survivor `rank` (squad order, 0 first) starts to lift.
        public float LiftAt(int rank) => StopEnd + rank * LiftStaggerWall;

        // The state when a press arrives at `t`: the wipe starts now, the
        // sting has played by now (it is never cut, and never skipped), and
        // the voices stay on their delay behind it.
        public RunUpSchedule SkippedAt(float t)
        {
            float now = Math.Min(t, WipeAt);
            float sting = Math.Min(StingAt, now);
            return new RunUpSchedule(StopEnd, sting, Math.Min(VoiceAt, sting + VoiceDelaySeconds),
                WashStart, WashSecondsWall, LiftSecondsWall, LiftStaggerWall, now, HasFade, true);
        }

        // Whether the wipe starts at `t`, given whether the last fade is still
        // running. The wipe waits for the fade or the cap, whichever is first.
        public bool WipeDue(float t, bool fadeRunning) =>
            Skipped || t >= WipeAt || (t >= StopEnd && !fadeRunning);
    }
}
