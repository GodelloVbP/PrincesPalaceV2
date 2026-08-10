using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Fades a defeated combatant's figure — and its ground-shadow ring,
    // a sibling under the same slot — out after a beat, rather than
    // leaving a "dead" pose sitting at full opacity for the rest of the
    // encounter. Lives on the SLOT, like StageActorAnimator, since it has
    // to reach two separate sibling Images rather than the one Image a
    // RequireComponent could demand.
    //
    // Deliberately NOT keyed off StanceDefeated — SetCombatantStance is
    // pure bookkeeping with no idea whether this is the first time it has
    // seen a death or the fiftieth repaint of an already-dead combatant.
    // RefreshCombatantSprite calls PlayIfNotAlready()/ResetToVisible()
    // itself, from the one place that actually knows both facts: the
    // combatant's live IsAlive and whether this exact slot already
    // started fading it.
    public class StageDeathFade : MonoBehaviour
    {
        // Held at full a moment after death — long enough for the hit that
        // killed it and the "X is defeated!" message to register — before
        // the fade itself, which is slow enough to read as a fade rather
        // than a flicker.
        private const float HoldSeconds = 0.35f;
        private const float FadeSeconds = 0.6f;

        [SerializeField] internal Image sprite;
        [SerializeField] internal Image shadow;

        // Each image's own resting alpha — the shadow ring is baked at
        // 0.85, not 1, so fading has to scale RELATIVE to that rather than
        // overwrite it, or "reset to visible" would leave the ring more
        // opaque than SceneBuilder ever authored it.
        private float _spriteBaseAlpha = 1f;
        private float _shadowBaseAlpha = 1f;

        private Coroutine _running;
        private bool _played;

        private void Awake()
        {
            if (sprite != null)
            {
                _spriteBaseAlpha = sprite.color.a;
            }

            if (shadow != null)
            {
                _shadowBaseAlpha = shadow.color.a;
            }
        }

        // No-ops on every call after the first, so RefreshCombatantSprite
        // can call this on EVERY repaint of a dead combatant without
        // restarting the fade each time.
        public void PlayIfNotAlready()
        {
            if (_played || !isActiveAndEnabled)
            {
                return;
            }

            _played = true;
            if (_running != null)
            {
                StopCoroutine(_running);
            }

            _running = StartCoroutine(FadeRoutine());
        }

        // Called whenever this slot repaints a LIVING combatant — a slot
        // reused for a fresh monster after its last occupant died here has
        // to come back at full opacity, and _played has to clear so THAT
        // combatant's own death fades again instead of silently no-opping
        // against the previous one's.
        public void ResetToVisible()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _played = false;
            SetFraction(1f);
        }

        // Snaps to fully faded, for a fight ending or the panel closing
        // mid-fade — otherwise a figure could be left part-visible going
        // into the next encounter's reused slot, the same reasoning
        // StageHitFlash.Clear() and StageActorAnimator.ResetToHome() use.
        public void Clear()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _played = true;
            SetFraction(0f);
        }

        private IEnumerator FadeRoutine()
        {
            yield return new WaitForSeconds(FightBeatPlayer.Scaled(HoldSeconds));

            // Scaled like the hold above and like every other fight animator:
            // anything driven by a beat runs on the beat's clock, or it
            // desynchronises from the thing it illustrates. The lunge learned
            // this the hard way.
            float fadeSeconds = FightBeatPlayer.Scaled(FadeSeconds);
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                SetFraction(1f - (t / fadeSeconds));
                yield return null;
            }

            SetFraction(0f);
            _running = null;
        }

        private void SetFraction(float fraction)
        {
            SetImageAlpha(sprite, _spriteBaseAlpha * fraction);
            SetImageAlpha(shadow, _shadowBaseAlpha * fraction);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
