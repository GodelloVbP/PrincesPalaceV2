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

        private RectTransform _rect;
        private Vector2 _home;
        private Coroutine _running;

        public Vector2 Home => _home;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            // Captured once, before anything can move us. Every animation
            // returns here rather than accumulating offsets — otherwise a
            // fight's worth of lunges would walk the figure off its mark.
            _home = _rect.anchoredPosition;
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
                yield return null;
            }

            _rect.anchoredPosition = to;
        }

        // The recovery keeps the old symmetrical ease: nothing is being
        // emphasised on the way back, and a snap home would undo the weight
        // the strike just established.
        private IEnumerator TweenBack(Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                _rect.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }

            _rect.anchoredPosition = to;
        }
    }
}
