using System;
using System.Collections;
using UnityEngine;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace
{
    // Walking a figure through the frames of one stance, at the sheet's pace.
    //
    // ONE COPY, because there were two and they disagreed. FightBeatPlayer had
    // the original -- the beat's actor, paced by FrameHoldCurve -- and when the
    // death and flinch animations turned out never to run at all, the fix added
    // a second stepper in FightController for the bodies playback does not
    // drive. That copy paced its frames FLAT, which is precisely the thing
    // FrameHoldCurve exists to stop: "six frames at a metronome-flat 80ms reads
    // as a slideshow however good the drawings are". So the corpse animated and
    // still looked wrong, and the reason was a duplicated loop rather than
    // anything anyone would think to look at.
    //
    // A DELEGATE FOR THE SINK, not a CombatantState, because the two callers
    // write the frame through different doors: playback owns an Action it was
    // handed, the controller owns the dictionary directly. Neither of those is
    // this file's business -- what is shared is the PACING, and that is all
    // that lives here.
    internal static class StanceStepper
    {
        // Frames [from, to), holding each for whatever FrameHoldCurve says it
        // is worth, on the beat clock.
        //
        // `abandon` is polled before every frame rather than once at the top: a
        // body can outlive the beat that killed it (see
        // FightController.PlayDefeatedFrames), and the fight can end underneath
        // it while it is still falling.
        public static IEnumerator Play(StanceAnimation animation, int from, int to,
                                       Action<int> show, Func<bool> abandon = null)
        {
            // A single-frame pose -- every combatant with flat-file art -- has
            // nothing to step, which is why all of this vanishes for them
            // rather than misbehaving.
            if (show == null || animation.FrameCount <= 1) yield break;

            for (int frame = from; frame < to && frame < animation.FrameCount; frame++)
            {
                if (abandon != null && abandon()) yield break;

                show(frame);

                // NOT a flat SecondsPerFrame. The sheet's pace is still the
                // budget -- FrameHoldCurve normalises to exactly
                // SecondsPerFrame x FrameCount, which FightBeatPlayer's own
                // `remaining` arithmetic depends on -- but it is SPENT
                // unevenly: a long hold on the wind-up, a snap through the
                // blow, a long settle after it.
                yield return new WaitForSeconds(FightBeatPlayer.Scaled(FrameHoldCurve.HoldFor(
                    frame, animation.FrameCount, animation.ImpactFrame, animation.SecondsPerFrame)));
            }
        }

        // The whole stance, which is what every caller outside a beat wants.
        public static IEnumerator Play(StanceAnimation animation, Action<int> show,
                                       Func<bool> abandon = null) =>
            Play(animation, 0, animation.FrameCount, show, abandon);
    }
}
