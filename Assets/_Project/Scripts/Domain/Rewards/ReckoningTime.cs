using System;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Rewards
{
    // THE ONE HOME OF THE RECKONING'S SPEED RULE (docs/PLAN_REWARD_SCREENS.md
    // C10): authored x min(1, Default / preset) x the test seam. Authored times
    // are at the default battle speed; the Reckoning speeds up at 1.5x and 2x
    // and never slows below them, so a cap written in authored seconds holds at
    // every preset. It does not use FightBeatPlayer.Scaled (Pace = preset / 1.5),
    // which belongs to the fight's own beats and would stretch every wait at 1x.
    public static class ReckoningTime
    {
        // A non-positive preset reads as the default.
        public static float Scale(float presetDisplay) =>
            presetDisplay <= 0f ? 1f : Math.Min(1f, BattleSpeed.DefaultDisplay / presetDisplay);

        // seam: ReckoningController.SpeedMultiplier, 1 in play.
        public static float Wall(float authoredSeconds, float presetDisplay, float seam = 1f) =>
            authoredSeconds * Scale(presetDisplay) * Math.Max(0f, seam);
    }
}
