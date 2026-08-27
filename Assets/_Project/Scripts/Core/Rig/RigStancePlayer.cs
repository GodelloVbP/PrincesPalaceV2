using System;
using System.Collections;
using UnityEngine;
using PrincesPalace.Domain.Rig;

namespace PrincesPalace.Core.Rig
{
    // Plays a slice of one RigStanceClip against one RigActor over real
    // time -- the rig twin of StanceStepper, sharing its two conventions:
    // the beat clock (FightBeatPlayer.Scaled, so a PlayMode test can run a
    // rigged fight at 60x too) and an `abandon` polled every frame rather
    // than once, for the same reason StanceStepper's own header gives --
    // a reaction can outlive the beat that started it.
    public static class RigStancePlayer
    {
        // Samples and applies the clip across [fromSeconds, toSeconds).
        public static IEnumerator PlaySegment(RigActor actor, RigStanceClip clip, float fromSeconds, float toSeconds,
                                              Func<bool> abandon = null)
        {
            if (actor == null || clip == null || clip.IsEmpty) yield break;
            if (toSeconds <= fromSeconds) yield break;

            float t = fromSeconds;
            actor.ApplyPose(RigSampler.Sample(clip, t));

            while (t < toSeconds)
            {
                if (abandon != null && abandon()) yield break;

                yield return null;

                float dt = FightBeatPlayer.BeatSpeedMultiplier <= 0f
                    ? 0f
                    : Time.deltaTime * FightBeatPlayer.BeatSpeedMultiplier;
                t = Mathf.Min(t + dt, toSeconds);
                actor.ApplyPose(RigSampler.Sample(clip, t));
            }
        }
    }
}
