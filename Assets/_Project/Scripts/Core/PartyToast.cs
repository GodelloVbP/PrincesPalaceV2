using System.Collections;
using UnityEngine;

namespace PrincesPalace
{
    // The Party pane's toast, fading rather than P3's hard show/hide -- the
    // handoff's own copy: "confirms every committed action ... then fades
    // after ~2.4s." Split as HOLD (2.0s at full alpha) then FADE (0.4s to 0),
    // which sums to the handoff's 2.4s while giving the fade itself a
    // distinct, readable duration rather than dividing 2.4s in half.
    //
    // NOT StageDeathFade, checked first per .claude/rules/ui.md's reuse-first
    // registry: that component fades THREE sibling Images together on the
    // fight beat's scaled clock (FightBeatPlayer.Scaled) and exists on a
    // combat slot, not a menu pane -- adopting it here would mean carrying a
    // beat dependency this pane has no reason to have, for a component that
    // only ever drives one CanvasGroup. Not BeaconPulse or the Ambient-motion
    // curves either (.claude/rules/ui.md): those are unbounded loops (a
    // pulse, a flicker), and this is a one-shot with a start and an end. A
    // straight linear fade needs no eased curve of its own.
    //
    // A NEW Show() RESTARTS the timer rather than stacking with whatever
    // fade is already running -- a second commit inside the first toast's
    // hold window (two swaps in quick succession) snaps back to full alpha
    // and reads the new text, instead of finishing a fade whose text no
    // longer matches what just happened.
    [RequireComponent(typeof(CanvasGroup))]
    public class PartyToast : MonoBehaviour
    {
        public const float HoldSeconds = 2.0f;
        public const float FadeSeconds = 0.4f;

        private CanvasGroup _group;
        private Coroutine _running;

        public void Show()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();

            gameObject.SetActive(true);
            _group.alpha = 1f;

            if (_running != null) StopCoroutine(_running);
            if (isActiveAndEnabled) _running = StartCoroutine(HoldThenFade());
        }

        // UNSCALED, like every other clock the system menu hosts (see
        // ExitsController.ArmSeconds's own comment) -- this pane only ever
        // runs while the menu has paused the game at timeScale 0.
        private IEnumerator HoldThenFade()
        {
            yield return new WaitForSecondsRealtime(HoldSeconds);

            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }

            _group.alpha = 0f;
            gameObject.SetActive(false);
            _running = null;
        }
    }
}
