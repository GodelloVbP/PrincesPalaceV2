using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace
{
    // Plays back what the session already resolved.
    //
    // The whole round landed in one synchronous pass before this runs, so every
    // beat here describes a moment that has ALREADY happened. That is what makes
    // the snapshots load-bearing: painting live state during playback would show
    // the end of the round on the first blow.
    //
    // PAINT FIRST, THEN MOVE. Each beat paints its own snapshot before it
    // animates, so the numbers on screen are the numbers as they stood when that
    // blow landed. Reversing the two makes a beat show the NEXT beat's health
    // for a frame, which reads as a hit landing early.
    public class FightBeatPlayer : MonoBehaviour
    {
        [SerializeField] internal DamagePopup[] popups;

        // How long a beat holds, and the gap between beats.
        public const float BeatHoldSeconds = 0.45f;
        public const float BeatGapSeconds = 0.3f;

        // The test seam. A PlayMode test that had to wait real seconds per beat
        // would take longer than the whole EditMode suite; this lets one run a
        // twelve-beat round in well under a second without changing a single
        // rule about ordering.
        public static float BeatSpeedMultiplier = 1f;

        public static float Scaled(float seconds) =>
            BeatSpeedMultiplier <= 0f ? 0f : seconds / BeatSpeedMultiplier;

        public bool IsPlaying { get; private set; }

        // A read-only view of the pool, for the tests that assert it comes back
        // whole. Read-only rather than exposing the field: nothing outside this
        // class has any business swapping the array, and a leak test that could
        // replace the pool would not be testing the leak.
        public IReadOnlyList<DamagePopup> Popups => popups ?? System.Array.Empty<DamagePopup>();

        private Coroutine _running;
        private Action _onFinished;

        // Set by the controller so playback can find where a combatant is
        // standing without knowing anything about stages or slots.
        internal Func<CombatantState, RectTransform> SlotFor;
        // Paints a set of vitals. Called TWICE per beat -- once with what stood
        // before the blow, once with what stood after.
        internal Action<IReadOnlyDictionary<CombatantState, Vitals>> PaintVitals;
        internal Action<string> PushLine;

        // Starts a beat's spell effect, and says how long after the beat opens
        // its blow actually lands. Both live on the controller because both need
        // the loaded frames.
        internal Action<CombatBeat> PlayVfx;
        internal Action<CombatBeat> FadeTheFallen;
        internal Func<CombatBeat, float> ImpactDelayFor;
        internal Action StopVfx;

        // Poses an actor and repaints the stage. The controller owns the sprite
        // lookup; playback only says WHO holds WHAT and WHEN.
        internal Action<CombatantState, string> SetStance;

        // Advances a combatant to frame N of its current pose. Separate from
        // SetStance because a pose is chosen once and then STEPPED -- collapsing
        // the two would re-resolve the animation on every frame.
        internal Action<CombatantState, int> SetFrame;

        // How a combatant's pose is timed: how many frames it has, how long each
        // holds, and which one is the impact. Resolved by the controller, which
        // is the only thing that can load the sheet.
        internal Func<CombatantState, string, StanceAnimation> AnimationFor;
        internal Action<CombatBeat> FlashTarget;

        public void Play(IReadOnlyList<CombatBeat> beats, Action onFinished)
        {
            Flush();

            _onFinished = onFinished;
            IsPlaying = true;
            _running = StartCoroutine(PlayBeats(beats));
        }

        // Stops playback dead and puts every borrowed thing back.
        //
        // Reclaiming the popups here is the point. v1's Flush left them running,
        // and its DamagePopup.Clear had no call sites at all, so an abandoned
        // fight leaked one popup per in-flight number out of a pool of six that
        // is never refilled.
        public void Flush()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    if (popup != null) popup.Reclaim();
                }
            }

            // An abandoned fight must not leave a spell frozen mid-frame over an
            // empty stage. Same rule as the popups, and the same reason it is
            // here rather than at the call sites.
            StopVfx?.Invoke();

            IsPlaying = false;
        }

        private void OnDisable()
        {
            // A scene change mid-round is exactly the abandoned-fight case, and
            // it must not be the caller's job to remember.
            Flush();
        }

        private IEnumerator PlayBeats(IReadOnlyList<CombatBeat> beats)
        {
            foreach (var beat in beats)
            {
                // THE PRE-SNAPSHOT, which is the whole reason the session
                // records two.
                //
                // A spell's bolt takes most of a second to arrive. Dropping the
                // target's HP the instant the beat opens shows the damage before
                // the spell has left the ceiling -- the number moves, then the
                // thing that caused it happens. Painting what stood BEFORE the
                // blow and only landing the after-state at the impact frame is
                // what puts cause back in front of effect.
                PaintVitals?.Invoke(beat.PreSnapshot);

                if (beat.Messages != null)
                {
                    foreach (var line in beat.Messages) PushLine?.Invoke(line);
                }

                // The poses the session recorded, applied to the stage. Every
                // combatant the beat mentions, not just the actor: a blow poses
                // the one taking it too, which is what makes a hit visible on
                // the stage and not only in the log.
                foreach (var pair in beat.Stances) SetStance?.Invoke(pair.Key, pair.Value);

                Lunge(beat);
                PlayVfx?.Invoke(beat);

                // A beat's own clip, if it authored one. Unconditional and
                // BEFORE the positioning work, because a spell with a sound but
                // no frames should still be audible -- an empty path is a silent
                // no-op, which is why this needs no guard of its own.
                SoundController.PlayClip(beat.SfxPath);

                var animation = AnimationOf(beat);

                // 1-BASED, matching the vfxImpactFrame convention enemies.json
                // already uses ("frame 3 of 6"). So the wind-up is frames
                // [0, impact) and the follow-through is [impact, count).
                int impactFrame = animation.IsEmpty
                    ? 1
                    : Mathf.Clamp(animation.ImpactFrame, 1, animation.FrameCount);

                // Wind-up: the actor's own frames up to and including its impact
                // frame. A spell instead waits out its VFX's impact fraction --
                // whichever of the two this beat has, only one of them is
                // non-zero, so they add rather than compete.
                yield return StepActorFrames(beat.Actor, 0, impactFrame, animation);

                float impact = ImpactDelayFor == null ? 0f : ImpactDelayFor(beat);
                if (impact > 0f) yield return new WaitForSeconds(Scaled(impact));

                // The blow lands: the numbers move, the target flashes and the
                // floating figure appears, all on the same frame.
                PaintVitals?.Invoke(beat.Snapshot);
                ShowAmount(beat);
                FlashTarget?.Invoke(beat);
                Recoil(beat);
                Speak(beat);

                // Follow-through, AT THE SAME PACE as the wind-up.
                //
                // The obvious alternative -- stretch the remaining frames to fill
                // the hold -- is what v1 did and it is visibly wrong: the Giant
                // Rat's six-frame swing ran its first three at 0.08s and its last
                // three at 0.15s, one animation changing speed halfway through.
                // That is the "feels a bit blocky" report. Matching the pace makes
                // it one continuous motion and leaves the beat's total length
                // untouched, which matters because the hold is what gives the
                // player time to read the damage number.
                yield return StepActorFrames(beat.Actor, impactFrame, animation.FrameCount, animation);

                float remaining = BeatHoldSeconds - animation.SecondsPerFrame * animation.FrameCount;
                if (remaining > 0f) yield return new WaitForSeconds(Scaled(remaining));

                // Back to idle before the next beat opens, so a pose belongs to
                // the blow that caused it rather than persisting until something
                // else happens to overwrite it. The defeated stay defeated --
                // the controller decides that from IsAlive, not from here.
                // The fallen fade AFTER the hold, so the defeated pose is seen
                // before it goes. Fading on the frame the blow lands would make
                // a kill read as the figure being deleted rather than dying.
                FadeTheFallen?.Invoke(beat);

                foreach (var pair in beat.Stances) SetStance?.Invoke(pair.Key, FightSession.Stances.Idle);

                // Idle is a single frame today, so this is a no-op -- but a
                // future idle animation would otherwise start from wherever the
                // last attack happened to end.
                if (beat.Actor != null) SetFrame?.Invoke(beat.Actor, 0);

                yield return new WaitForSeconds(Scaled(BeatGapSeconds));
            }

            _running = null;
            IsPlaying = false;

            var finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        private void ShowAmount(CombatBeat beat)
        {
            if (beat.Amount <= 0 || beat.Target == null) return;

            var popup = FreePopup();
            if (popup == null) return;   // every one still in flight; the number is dropped, not queued

            var slot = SlotFor?.Invoke(beat.Target);
            var at = slot != null ? slot.anchoredPosition : Vector2.zero;
            popup.Play(at, beat.Amount, beat.IsHealing);
        }

        private DamagePopup FreePopup()
        {
            if (popups == null) return null;

            foreach (var popup in popups)
            {
                if (popup != null && popup.IsFree) return popup;
            }

            return null;
        }
    
        // The actor steps toward its target and back.
        //
        // Skipped entirely when the beat says the actor HOLDS POSITION -- a cast
        // is delivered from where it stands, and so is a monster whose plain
        // attack art is itself a stationary pose. Without that check the view
        // lunged a ground-slamming golem across the stage while its art showed
        // it rooted, which read as the creature flying.
        private void Lunge(CombatBeat beat)
        {
            if (beat.ActorHoldsPosition || beat.Actor == null || beat.Target == null) return;

            var from = SlotFor?.Invoke(beat.Actor);
            var to = SlotFor?.Invoke(beat.Target);
            if (from == null || to == null) return;

            var animator = from.GetComponent<StageActorAnimator>();
            if (animator == null) return;

            // A fraction of the way, not all of it: the figures are meant to
            // close the gap, not swap places.
            float dx = (to.anchoredPosition.x - from.anchoredPosition.x) * LungeFraction;
            animator.Play(new Vector2(dx, 0f), Scaled(BeatHoldSeconds) * 0.45f);
        }

        // The other half of a blow: the figure taking it flinches AWAY.
        //
        // Away is decided by which side it is on rather than by where the blow
        // came from -- the party faces right and the monsters face left, so a
        // recoil is always backwards into its own half. Deriving it from the
        // attacker's position instead would send a back-row monster stumbling
        // TOWARD the party when it was hit from behind by a status tick.
        private void Recoil(CombatBeat beat)
        {
            if (beat.Target == null || beat.Amount <= 0) return;

            // Nobody flinches away from themselves. A self-heal still gets its
            // number, just no recoil.
            if (ReferenceEquals(beat.Target, beat.Actor)) return;

            var slot = SlotFor?.Invoke(beat.Target);
            var animator = slot == null ? null : slot.GetComponent<StageActorAnimator>();
            if (animator == null) return;

            float dx = beat.Target.IsPlayerSide ? -RecoilDistance : RecoilDistance;
            animator.Play(new Vector2(dx, 0f), Scaled(BeatHoldSeconds) * 0.45f);
        }

        private const float LungeFraction = 0.35f;
        private const float RecoilDistance = 45f;

        // How this beat's actor animates. Falls back to a single instantaneous
        // frame when there is no kit, no art, or no timing -- which is every
        // combatant with flat-file art, and is why the stepping below vanishes
        // rather than misbehaving for them.
        private StanceAnimation AnimationOf(CombatBeat beat)
        {
            if (AnimationFor == null || beat?.Actor == null) return StanceAnimation.Empty;

            string stance = beat.Stances.TryGetValue(beat.Actor, out var pose)
                ? pose
                : FightSession.Stances.Idle;

            return AnimationFor(beat.Actor, stance);
        }

        // Walks an actor through frames [from, to), holding each for the sheet's
        // own authored pace.
        private IEnumerator StepActorFrames(CombatantState actor, int from, int to, StanceAnimation animation)
        {
            // A single-frame pose -- every combatant with flat-file art -- has
            // nothing to step, which is why all of this vanishes for them rather
            // than misbehaving.
            if (actor == null || SetFrame == null || animation.FrameCount <= 1) yield break;

            for (int frame = from; frame < to && frame < animation.FrameCount; frame++)
            {
                SetFrame(actor, frame);
                yield return new WaitForSeconds(Scaled(animation.SecondsPerFrame));
            }
        }

        // What the two of them say about the blow.
        //
        // The LINE was chosen when the beat was recorded, because it depends on
        // health at the moment the blow landed and live health has already moved
        // on by now. All that is left here is playing it.
        private static void Speak(CombatBeat beat)
        {
            if (beat.HasActorVoice)
            {
                CharacterVoice.Play(beat.ActorVoiceId, beat.ActorVoiceLine, oncePerFight: false);
            }

            if (beat.HasTargetVoice)
            {
                // "Nearly dead" marks a threshold rather than an event, so it is
                // said once per fight. A grunt is an event and repeats.
                CharacterVoice.Play(beat.TargetVoiceId, beat.TargetVoiceLine, beat.TargetVoiceOncePerFight);
            }
        }
}
}
