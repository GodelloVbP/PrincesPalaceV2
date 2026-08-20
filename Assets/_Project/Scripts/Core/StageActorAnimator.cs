using System.Collections;
using UnityEngine;

namespace PrincesPalace
{
    // Movement half of a stage figure's combat animation: the lunge forward
    // on an attack, the knock-back on a hit. Lives on the SLOT (the sprite's
    // parent) because that is what carries the depth scale and the bottom
    // pivot — moving the sprite itself would fight the scale, and moving the
    // slot keeps the figure's feet on the same ground line the whole way.
    //
    // Deliberately split from stance/pose: FightController owns which sprite
    // is shown (it is the only thing that knows how to resolve a combatant to
    // its art), this owns only where the figure stands. Keeping them separate
    // means a pose with no movement (defeated, victory) needs no animator
    // involvement at all, and this file never has to know what a CombatantState
    // is.
    //
    // Several of these run at once by design. An attacker lunging while its
    // target recoils reads as one exchange; sequencing them would need a
    // queue and a much larger change to how FightController resolves a turn
    // (the whole chain currently resolves synchronously in one frame).
    public class StageActorAnimator : MonoBehaviour
    {
        // The strike should read as fast and hard, so the two halves are
        // deliberately NOT symmetrical: the lunge is a snap, the return is
        // the slower recovery. Making both quick reads as a twitch; making
        // both slow reads as a stroll.
        // Internal, not private: FightController.Beats reads this to time a
        // melee beat's impact (voice grunt, hit flash, damage number)
        // against when the lunge actually finishes closing the distance,
        // rather than against an unrelated fixed hold duration.
        internal const float LungeSeconds = 0.055f;
        private const float ReturnSeconds = 0.16f;

        // How far the figure stretches along its travel, at the fastest point
        // of each leg. The strike gets nearly three times the recovery's,
        // because that is the half being emphasised — the same asymmetry the
        // two easing curves below already express.
        //
        // 0.11 is where it stopped reading as weight and started reading as a
        // wobble. Six frames of drawing cannot show acceleration; this can, and
        // it is the difference between a figure that lunges and one that slides.
        private const float OutStretch = 0.11f;
        private const float BackStretch = 0.04f;

        // How much of the horizontal stretch comes back out of the height.
        // Not 1.0 — true volume conservation on a 2D silhouette overshoots,
        // because the art is not a rubber ball and the eye reads the vertical
        // loss twice as strongly as the horizontal gain.
        private const float VolumeRatio = 0.55f;

        private RectTransform _rect;
        private Vector2 _home;
        private Vector3 _baseScale;
        private Coroutine _running;

        public Vector2 Home => _home;

        // The depth scale the slot carries, which the stretch multiplies onto.
        // Exposed for the same reason Home is: AnchorStageSlots has to be able
        // to ask whether the mark it is about to assign is the one already
        // held, and the live localScale is mid-stretch during a swing.
        public Vector3 BaseScale => _baseScale;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            // The mark this figure returns to, and the depth scale it returns
            // to. Every animation goes back to these rather than accumulating
            // offsets -- otherwise a fight's worth of lunges would walk the
            // figure off its mark, and the stretch would compound onto itself.
            //
            // CAPTURED HERE AND RE-CAPTURED BY Rehome(). Awake alone was only
            // correct while a slot's position was fixed at build time, and it
            // is not: see Rehome.
            Rehome();
        }

        // Re-reads the mark and the scale from where the slot ACTUALLY is now.
        //
        // THE BUG THIS EXISTS FOR. These were captured in Awake and never
        // again, which quietly assumed a slot never moves. It moves constantly:
        // FightController.AnchorStageSlots re-spreads and re-scales the live
        // slots from FightStageAnchors every time the live count changes, so
        // that two rats occupy the two ENDS of the formation rather than
        // crowding its first two positions -- and an enemy dying re-lays every
        // survivor.
        //
        // From that moment the animator was returning each figure to where its
        // slot used to be, and multiplying its stretch onto the scale the slot
        // used to have. Both are absolute writes at the end of a tween, so the
        // figure did not drift -- it SNAPPED to a stale mark the instant a
        // lunge finished, and did it again on every swing. That is the
        // "sprites constantly get misplaced".
        //
        // It bit on the first encounter too, not only after a death: the slots
        // are anchored during setup, which is after Awake.
        //
        // Called AFTER the slot has been given its new position and scale, so
        // this reads the new values rather than imposing the old ones.
        public void Rehome()
        {
            // Resolved here as well as in Awake, because a caller can reach
            // this before Unity has run Awake on a freshly activated slot --
            // and a Rehome that silently did nothing would leave the mark at
            // whatever the scene authored.
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            // Anything in flight belongs to the old mark. Stopped rather than
            // finished, and deliberately WITHOUT the snap-home ResetToHome
            // does -- that would write the stale _home back over the position
            // the caller just assigned.
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _home = _rect.anchoredPosition;
            _baseScale = _rect.localScale;
        }

        // Convenience overload for a pure sideways move (recoils, and any
        // caller that does not care about depth).
        public void Play(float dx, float holdSeconds)
        {
            Play(new Vector2(dx, 0f), holdSeconds);
        }

        // offset is signed in canvas units, relative to this figure's mark.
        // Vector2 rather than a bare dx because slots sit at different depths:
        // an attacker crossing to a target in the back row has to climb the
        // stage as well as cross it, or it lunges past the target's feet.
        public void Play(Vector2 offset, float holdSeconds)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_running != null)
            {
                // A second hit landing mid-animation restarts the move rather
                // than stacking on it — stacking would compound the offset and
                // drag the figure across the field.
                StopCoroutine(_running);
                _rect.anchoredPosition = _home;
                ApplyStretch(0f);
            }

            _running = StartCoroutine(PlayRoutine(offset, holdSeconds));
        }

        // Snaps home immediately, for a fight ending or the panel closing
        // mid-animation — otherwise a figure could be left parked off-mark
        // for the next encounter, since _home is only captured in Awake.
        public void ResetToHome()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_rect != null)
            {
                _rect.anchoredPosition = _home;

                // The stretch too. A coroutine stopped mid-arc leaves the
                // figure deformed, and unlike a position offset that is not
                // self-correcting -- nothing else writes localScale, so it
                // would simply stay squashed for the rest of the fight.
                ApplyStretch(0f);
            }
        }

        private IEnumerator PlayRoutine(Vector2 offset, float holdSeconds)
        {
            // SCALED, through the same seam SpellVfxPlayer and StageHitFlash
            // already use. This one did not, and the mismatch is visible rather
            // than academic: at 60x the beat player finished the whole round
            // while the figure was still sliding back, leaving it parked
            // mid-stage. Anything driven by a beat has to run on the beat's own
            // clock or it desynchronises from the thing it illustrates.
            var target = _home + offset;
            yield return TweenOut(_home, target, FightBeatPlayer.Scaled(LungeSeconds));
            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            yield return TweenBack(target, _home, FightBeatPlayer.Scaled(ReturnSeconds));
            _rect.anchoredPosition = _home;
            _running = null;
        }

        // Outbound uses ease-OUT (fast off the mark, decelerating into the
        // target) rather than the symmetrical smoothstep this used to use.
        // Smoothstep eases IN as well, so the strike began slowly — which is
        // exactly what made it read as gliding rather than striking.
        private IEnumerator TweenOut(Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                float k = 1f - (1f - n) * (1f - n);
                _rect.anchoredPosition = Vector2.Lerp(from, to, k);
                ApplyStretch(OutStretch * Arc(n));
                yield return null;
            }

            _rect.anchoredPosition = to;
            ApplyStretch(0f);
        }

        // The recovery keeps the old symmetrical ease: nothing is being
        // emphasised on the way back, and a snap home would undo the weight
        // the strike just established.
        private IEnumerator TweenBack(Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                _rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, n));
                ApplyStretch(BackStretch * Arc(n));
                yield return null;
            }

            _rect.anchoredPosition = to;
            ApplyStretch(0f);
        }

        // Zero at both ends, one in the middle. The stretch belongs to the
        // TRAVEL, so a figure standing still — at either end of the move — is
        // never caught mid-deformation, which is what would show up as a
        // permanently squashed actor if a coroutine were ever interrupted.
        private static float Arc(float n)
        {
            return Mathf.Sin(Mathf.Clamp01(n) * Mathf.PI);
        }

        // SQUASH AND STRETCH, and the only reason it can be this simple is that
        // a slot's pivot is (0.5, 0) — its own ground line. Scaling about that
        // point keeps the feet planted by construction, so none of this can
        // lift a figure off the floor. Scaling the SPRITE instead would have
        // needed the ground offset recomputing every frame, against the
        // offsetMin/offsetMax that GroundTheFigure owns.
        //
        // Volume is roughly conserved — wider is shorter — because a figure
        // that only gets bigger reads as zooming rather than as moving.
        //
        // Multiplied onto the CAPTURED base scale, never assigned outright: the
        // slot already carries its depth scale from FightStageAnchors, and
        // writing an absolute value here would flatten the back row to the size
        // of the front one on the first swing.
        private void ApplyStretch(float amount)
        {
            if (_rect == null) return;

            _rect.localScale = new Vector3(
                _baseScale.x * (1f + amount),
                _baseScale.y * (1f - amount * VolumeRatio),
                _baseScale.z);
        }
    }
}
