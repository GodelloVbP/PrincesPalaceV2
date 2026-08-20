using UnityEngine;

namespace PrincesPalace
{
    // The moment a talent is kindled: the LIT orb is revealed through a mask
    // that grows from the node's centre to its full size.
    //
    // WHAT ANIMATES IS THE MASK, NOT THE ORB, and v1 records why: scale-and-
    // fade was the first attempt and it was wrong. Growing the lit orb from
    // zero while its alpha climbed reads as the orb dimly appearing and
    // brightening -- "turning on" -- rather than as something arriving. The
    // content this reveals is held at full size and full opacity the entire
    // time; only the mask's own sizeDelta moves. So at every instant, whatever
    // sliver of the orb is visible is already at full strength.
    //
    // Same Update-driven lerp as every other small animator here
    // (ColumnOpenAnimator, ButtonPressAnimator); this project has no tweening
    // library and none of its animations have needed one.
    //
    // THREE EXPLICIT STATES rather than one continuous target. Play is a
    // one-shot fired exactly once, by the single call site that newly invests
    // a node. EnsureShown and HideInstantly are the idempotent "match the
    // current state" calls the repaint makes on every redraw -- a path change,
    // a different character, selecting another orb -- and those must never
    // restart or interrupt a Play already in flight.
    public class TalentNodeInvestReveal : MonoBehaviour
    {
        // v1's own figure, arrived at by playtest: "the animation of it
        // activating can take 1.5x as long" against an original 0.9s.
        private const float DurationSeconds = 1.35f;

        // The mask. Its child is the always-opaque lit orb, wired by the build
        // step and never touched here.
        //
        // Internal rather than private so the wiring step can assign it --
        // Contract B, the same reason every controller field on this screen is.
        [SerializeField] internal RectTransform mask;

        // The as-built size, captured before Play ever zeroes it. Read off the
        // mask rather than restated as a constant here: the orb's diameter
        // depends on its ROLE, so a second copy of that number would be wrong
        // for two of the twenty-one slots.
        private Vector2 _fullSize;

        // Below zero means "not playing".
        private float _elapsed = -1f;

        public bool IsPlaying => _elapsed >= 0f;

        private void Awake()
        {
            if (mask != null) _fullSize = mask.sizeDelta;
        }

        public void Play()
        {
            if (mask == null) return;

            _elapsed = 0f;
            mask.gameObject.SetActive(true);
            mask.sizeDelta = Vector2.zero;
        }

        // Does nothing while a Play is running or once already fully shown.
        // Only bites right after a path or character switch, where an orb
        // invested earlier has to appear at full strength immediately rather
        // than replaying its reveal.
        public void EnsureShown()
        {
            if (mask == null || _elapsed >= 0f) return;

            if (mask.gameObject.activeSelf && Mathf.Approximately(mask.sizeDelta.x, _fullSize.x)) return;

            mask.gameObject.SetActive(true);
            mask.sizeDelta = _fullSize;
        }

        public void HideInstantly()
        {
            if (mask == null) return;

            _elapsed = -1f;
            mask.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_elapsed < 0f || mask == null) return;

            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / DurationSeconds);

            // Quadratic ease-out, the same curve ColumnOpenAnimator uses: fast
            // off the mark, settling in rather than arriving at a constant rate.
            float eased = 1f - (1f - t) * (1f - t);

            mask.sizeDelta = _fullSize * eased;

            if (t >= 1f) _elapsed = -1f;
        }
    }
}
