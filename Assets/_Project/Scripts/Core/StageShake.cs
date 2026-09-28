using System.Collections;
using UnityEngine;

namespace PrincesPalace
{
    // A kick on the whole stage when something lands.
    //
    // THE STAGE, NOT THE CAMERA, and that is the one real decision here. Every
    // reference for this effect shakes the camera, which on a screen-space
    // canvas means the HUD goes with it -- and this fight's HUD is a painted
    // ornamental frame, so shaking it reads as the FRAME being loose rather
    // than as the blow being heavy. Moving the two stage containers instead
    // keeps the furniture nailed down and throws only the thing that was hit.
    //
    // ADDITIVE ON TOP OF THE MARK, never absolute. Three things write a
    // position on this stage and they compose only because they write
    // different transforms: AnchorStageSlots places the SLOTS (and re-places
    // them every time an enemy dies), StageActorAnimator moves each FIGURE
    // relative to its slot, and this moves the RACK both of those live in.
    //
    // NOISE, NOT A SINE. A sine wave reads as a wobble -- a pendulum, something
    // with a rhythm to predict. An impact has no rhythm: it is loudest at the
    // first frame and gone. Random offsets inside a decaying envelope are what
    // that sounds like, and are why this takes an amplitude rather than a
    // duration as its dial.
    public class StageShake : MonoBehaviour
    {
        // How long a full-strength kick takes to die away. Short: past about a
        // quarter of a second it stops reading as an impact and starts reading
        // as an earthquake, which is a different event. Nudged up with the
        // amplitude below, because a throw twice as far over the same 0.19s
        // decays too fast to be seen at all.
        private const float Seconds = 0.23f;

        // Pixels at full strength.
        //
        // The nearest slots sit at x +/-470 and a figure is ~400px wide, so
        // double figures registers -- but "registers" is a lower bar than
        // "hits" against a 1080-tall stage. The threshold where the eye
        // starts tracking the movement rather than the blow is nearer
        // 45-50px on this stage; 30 sits well under it.
        private const float MaxPixels = 30f;

        // Vertical is deliberately smaller. The stage recedes along a fake
        // ground plane (StageLayout), so a big vertical throw breaks the floor
        // the whole illusion rests on; sideways does not.
        private const float VerticalRatio = 0.55f;

        private RectTransform _rect;
        private Vector2 _home;
        private Coroutine _running;

        private void Awake() => Rehome();

        // Re-reads the mark. The racks themselves do NOT move at runtime --
        // AnchorStageSlots re-lays the slots inside them, which is a different
        // transform -- so unlike StageActorAnimator.Rehome this is a seam for a
        // future layout rather than a fix for a live bug. Kept because the
        // alternative is discovering that the day one of them does move.
        public void Rehome()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect != null) _home = _rect.anchoredPosition;
        }

        // `strength` is 0..1. Anything at or below zero is not a no-op by
        // accident -- a beat that landed nothing should not shake, and saying
        // so here means no caller has to.
        public void Kick(float strength)
        {
            if (_rect == null || !isActiveAndEnabled || strength <= 0f) return;

            if (_running != null)
            {
                StopCoroutine(_running);
                _rect.anchoredPosition = _home;
            }

            _running = StartCoroutine(Kicking(Mathf.Clamp01(strength)));
        }

        // Snaps back, for a fight ending or the panel closing mid-kick. Same
        // reason StageActorAnimator has one: nothing else writes this position,
        // so a stopped coroutine would leave the rack parked off-mark for the
        // next encounter.
        public void ResetToHome()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_rect != null) _rect.anchoredPosition = _home;
        }

        private IEnumerator Kicking(float strength)
        {
            // DELIBERATELY NOT re-reading _home here. It looks defensive and is
            // the opposite: this runs while the rack may be sitting off-mark
            // from a kick that something else interrupted, so re-reading would
            // adopt that offset as the new home and keep it forever. Awake and
            // Rehome are the only two places this is captured, and both of them
            // are looking at a rack that is definitely at rest.
            // LONGER FOR A HEAVIER KICK, rather than the same length at every
            // amplitude. A big throw over a short envelope decays before the
            // eye has found it, which is the shape the first pass had and part
            // of why it read as no shake at all. 0.7x at a scratch, 1.3x at the
            // top of the scale.
            float seconds = FightBeatPlayer.Scaled(Seconds * (0.7f + 0.6f * strength));
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                // ENGINE TIME, the one clock every stage move steps by
                // (StageActorAnimator's header says why), so a pause holds
                // the kick rather than letting it play out behind the menu.
                //
                // AND A FRAME THE CLOCK DID NOT ADVANCE DRAWS NO NEW NOISE.
                // The envelope alone would freeze, but the offset inside it
                // is re-rolled every frame -- so a paused kick would rattle
                // the rack at its frozen amplitude for as long as the menu
                // stayed open. Holding the last offset is what "paused" means
                // for noise.
                float step = Time.deltaTime;
                if (step <= 0f)
                {
                    yield return null;
                    continue;
                }

                elapsed += step;

                // Squared falloff, so the kick is nearly over by the time the
                // eye has found it. Linear decay reads as a slow settle.
                float left = 1f - Mathf.Clamp01(elapsed / seconds);
                float amplitude = MaxPixels * strength * left * left;

                _rect.anchoredPosition = _home + new Vector2(
                    Random.Range(-amplitude, amplitude),
                    Random.Range(-amplitude, amplitude) * VerticalRatio);

                yield return null;
            }

            _rect.anchoredPosition = _home;
            _running = null;
        }
    }
}
