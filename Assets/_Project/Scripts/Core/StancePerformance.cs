using System;
using System.Collections;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace
{
    // One beat's actor animation, spoken in the vocabulary FightBeatPlayer
    // needs regardless of which art style is underneath: how long the
    // wind-up and the whole stance take, and three phases to play.
    //
    // FrameStancePlayback reproduces today's StanceStepper/FrameHoldCurve
    // math unchanged, and StillStancePlayback below covers the one-drawing
    // case. FightBeatPlayer calls only this interface, so it never needs to
    // know which art style it is driving.
    public interface IStancePlayback
    {
        // Real seconds the wind-up [start, impact) takes, UNSCALED -- what
        // Charge needs to time its travel tween before either phase plays,
        // the same contract WindupSeconds used to answer directly.
        float WindupSeconds { get; }

        // Real seconds the WHOLE stance takes, unscaled -- what
        // FightBeatPlayer.SettleAfter needs to compute the beat's hold.
        float TotalSeconds { get; }

        // Whether this stance has anything to actually animate. Flinch uses
        // this to skip starting a coroutine for a victim with nothing to
        // show -- the same FrameCount <= 1 guard Flinch took before this
        // seam existed.
        bool HasMotion { get; }

        // Whether Release() plays anything. FightBeatPlayer only calls
        // Release when this is true, exactly as it only called
        // StepActorFramesReverse when animation.ReturnsToStart was set.
        bool ReturnsToStart { get; }

        IEnumerator Windup();
        IEnumerator FollowThrough();
        IEnumerator Release();

        // Back to rest between beats -- SetFrame(actor, 0)'s twin.
        void ResetToRest();
    }

    // Does nothing, instantly. What PlaybackOf hands back for a beat with no
    // actor or a player with no PlaybackFor wired at all -- the fixture
    // FightBeatPacingTests' bare NewPlayer() exercises, which wires no
    // delegates whatsoever and still has to complete a beat.
    internal sealed class EmptyStancePlayback : IStancePlayback
    {
        public static readonly EmptyStancePlayback Instance = new EmptyStancePlayback();

        public float WindupSeconds => 0f;
        public float TotalSeconds => 0f;
        public bool HasMotion => false;
        public bool ReturnsToStart => false;

        public IEnumerator Windup() { yield break; }
        public IEnumerator FollowThrough() { yield break; }
        public IEnumerator Release() { yield break; }
        public void ResetToRest() { }
    }

    // Wraps today's StanceAnimation/StanceStepper/FrameHoldCurve math
    // exactly as FightBeatPlayer called it before this seam existed -- see
    // StancePerformanceTests for the "unchanged by the refactor" guarantee.
    public sealed class FrameStancePlayback : IStancePlayback
    {
        private readonly CombatantState _actor;
        private readonly StanceAnimation _animation;
        private readonly Action<CombatantState, int> _setFrame;
        private readonly Func<bool> _abandon;
        private readonly int _impactFrame;

        public FrameStancePlayback(CombatantState actor, StanceAnimation animation,
            Action<CombatantState, int> setFrame, Func<bool> abandon = null)
        {
            _actor = actor;
            _animation = animation;
            _setFrame = setFrame;
            _abandon = abandon;
            _impactFrame = animation.IsEmpty ? 1 : Mathf.Clamp(animation.ImpactFrame, 1, animation.FrameCount);
        }

        // The wind-up frames [0, impact) at the sheet's own uneven pace --
        // the arithmetic FightBeatPlayer.WindupSeconds used to run inline,
        // moved here unchanged so Charge can ask for it before either phase
        // plays a single frame.
        public float WindupSeconds
        {
            get
            {
                if (_animation.IsEmpty || _animation.FrameCount <= 1) return 0f;

                float total = 0f;
                for (int frame = 0; frame < _impactFrame; frame++)
                {
                    total += FrameHoldCurve.HoldFor(frame, _animation.FrameCount, _animation.ImpactFrame,
                        _animation.SecondsPerFrame);
                }

                return total;
            }
        }

        public float TotalSeconds => _animation.SecondsPerFrame * _animation.FrameCount;

        public bool HasMotion => _animation.FrameCount > 1;

        public bool ReturnsToStart => _animation.ReturnsToStart && _animation.FrameCount > 1;

        public IEnumerator Windup()
        {
            if (_actor == null || _setFrame == null) yield break;
            yield return StanceStepper.Play(_animation, 0, _impactFrame, frame => _setFrame(_actor, frame), _abandon);
        }

        public IEnumerator FollowThrough()
        {
            if (_actor == null || _setFrame == null) yield break;
            yield return StanceStepper.Play(_animation, _impactFrame, _animation.FrameCount,
                frame => _setFrame(_actor, frame), _abandon);
        }

        public IEnumerator Release()
        {
            if (_actor == null || _setFrame == null) yield break;
            yield return StanceStepper.PlayReverse(_animation, 0, _animation.FrameCount - 1,
                frame => _setFrame(_actor, frame), _abandon);
        }

        public void ResetToRest() => _setFrame?.Invoke(_actor, 0);
    }

    // A SINGLE-DRAWING ACTOR'S SWING, given the wind-up its art cannot draw.
    //
    // The defect this exists for is a timing one rather than a missing-art
    // one. FrameStancePlayback.Windup() over a one-frame stance yield-breaks
    // on its first line (StanceStepper.Play guards FrameCount <= 1), so for
    // every flat-art attacker in the game the impact instant -- the flash,
    // the recoil, the squash, the damage number -- fired about one frame
    // after Lunge() started its 0.055s outbound tween. The blow landed before
    // the attacker had crossed.
    //
    // ANTICIPATION AND THE IMPACT INSTANT MOVE TOGETHER, which is why this
    // wraps rather than sits beside. Adding a crouch in front of the lunge
    // (StageActorAnimator's leadSeconds) widens exactly that gap, so the only
    // safe way to add one is to make the wind-up REPORT the whole travel:
    // anticipation plus the outbound tween. FightBeatPlayer waits it out
    // before firing the impact, and the two cannot drift apart because there
    // is one number.
    //
    // Wraps the resolved playback rather than replacing it, so ResetToRest
    // still puts the real drawing back at rest between beats -- this class
    // knows nothing about sprites and must not start.
    public sealed class StaticStancePlayback : IStancePlayback
    {
        private readonly IStancePlayback _wrapped;

        public StaticStancePlayback(IStancePlayback wrapped)
        {
            _wrapped = wrapped ?? EmptyStancePlayback.Instance;
        }

        // The crouch plus the travel. Both numbers live on StageActorAnimator
        // beside the tween that actually spends them; this is the reader, not
        // a second home.
        public float WindupSeconds =>
            StageActorAnimator.AnticipationSeconds + StageActorAnimator.LungeSeconds;

        // The whole performance IS the wind-up: there is no follow-through to
        // play, because there is no second drawing to play it with. Equal to
        // WindupSeconds rather than to the wrapped stance's own total, so
        // FightBeatPlayer.SettleAfter budgets against the time this actually
        // spends -- charging the wrapped single-frame 0.08s instead would
        // under-report by the anticipation and quietly lengthen every beat.
        public float TotalSeconds => WindupSeconds;

        // FALSE, matching the drawing rather than the clock. Flinch is the
        // only reader (FightBeatPlayer.Flinch skips a victim with nothing to
        // show) and the question it asks is "are there frames to step", which
        // for a still is no however long the beat waits. A victim wrapped in
        // this would otherwise run a coroutine that shows nothing for an
        // eighth of a second, which is the exact waste that guard exists for.
        public bool HasMotion => false;

        public bool ReturnsToStart => false;

        public IEnumerator Windup()
        {
            // AT THE TOP OF THE CROUCH, not at contact. The swell has to be
            // audible before the blow or it reads as a second impact sound.
            SoundController.PlayClip(ContactCues.WhooshClipPath);

            yield return new WaitForSeconds(FightBeatPlayer.Scaled(WindupSeconds));
        }

        // Nothing left to spend. A still has no frames after its impact, and
        // the beat's settle is already sized to what TotalSeconds reports.
        public IEnumerator FollowThrough() { yield break; }

        public IEnumerator Release() { yield break; }

        public void ResetToRest() => _wrapped.ResetToRest();
    }
}
