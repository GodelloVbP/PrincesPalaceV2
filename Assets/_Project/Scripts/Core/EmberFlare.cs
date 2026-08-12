using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // The heat under the Reckoning's Continue arrow, kept alive.
    //
    // A baked sprite sitting perfectly still reads as a decal on the panel
    // rather than as something burning, and no amount of work on the sprite
    // itself fixes that -- a flame is legible because it MOVES.
    //
    // THE CURVE COMES FROM DOMAIN (FlickerCurve.Ember), which is the same split
    // LanternFlicker makes and for the same reason: the arithmetic is testable
    // over there and untestable here, because the EditMode suite is Domain-only.
    // This file is the part that genuinely needs Unity -- reading a clock,
    // tinting an Image, moving a rect.
    //
    // Not LanternFlicker itself, despite the overlap. That component is worn by
    // eight lights across the main menu and scales uniformly; this one flares
    // WIDER than it does tall and drifts sideways, and bending a shared
    // component into doing both would have put two behaviours behind one set of
    // fields with nothing naming which lights wanted which.
    [RequireComponent(typeof(Image))]
    public class EmberFlare : MonoBehaviour
    {
        // ALL SUBTLE ON PURPOSE. The brief was "a little, so it doesn't feel
        // like a static asset" -- past roughly 1.10 the arrow starts to look
        // like it is being inflated rather than lit.
        //
        // X swings further than Y, which is what makes it read as flaring OUT
        // along the arrow rather than as a pulse. Y is held tighter for a
        // second reason: the box is 88 tall against 90 of headroom before the
        // frame's painted bottom ornament, so there is nowhere for it to go.
        public float MinScaleX = 0.96f;
        public float MaxScaleX = 1.07f;
        public float MinScaleY = 0.98f;
        public float MaxScaleY = 1.02f;

        public float MinAlpha = 0.80f;
        public float MaxAlpha = 1.00f;

        // How far it wanders along its own axis, in canvas pixels.
        public float DriftX = 7f;

        // Deliberately NOT a whole fraction of any rate in the curve. Two
        // cycles that share a period land back together every loop and the
        // whole thing starts to read as one machine ticking; incommensurate
        // rates never quite repeat, which is most of what "alive" means here.
        public float DriftRate = 0.83f;

        public FlickerCurve Curve = FlickerCurve.Ember;

        private Image _image;
        private RectTransform _rect;
        private Color _baseColour;
        private Vector3 _baseScale;
        private Vector2 _basePosition;
        private float _phase;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();

            // Captured, never assumed. Nothing guarantees a frame ever ticks --
            // ScreenshotTool renders straight from the authored scene without
            // entering Play Mode -- so the resting state has to be whatever
            // SceneBuilder baked, and this modulates around it.
            _baseColour = _image.color;
            _baseScale = _rect.localScale;
            _basePosition = _rect.anchoredPosition;

            _phase = Random.value * 100f;
        }

        // OnEnable rather than only Awake: the Reckoning's summary phase starts
        // inactive and is switched on mid-fight, and a flare that resumed from
        // wherever its last run left off would pop on the first visible frame.
        private void OnEnable()
        {
            if (_rect == null) return;

            _rect.localScale = _baseScale;
            _rect.anchoredPosition = _basePosition;
            _image.color = _baseColour;
        }

        private void Update()
        {
            if (Curve == null) return;

            // Unscaled, like everything else on this screen. The Reckoning is
            // an overlay over a fight that may well be paused, and a flame that
            // freezes the moment the game does is worse than one that never
            // moved.
            float t = Time.unscaledTime + _phase;
            float intensity = Curve.At(t);

            _rect.localScale = new Vector3(
                _baseScale.x * Mathf.Lerp(MinScaleX, MaxScaleX, intensity),
                _baseScale.y * Mathf.Lerp(MinScaleY, MaxScaleY, intensity),
                _baseScale.z);

            _rect.anchoredPosition = new Vector2(
                _basePosition.x + Mathf.Sin(t * DriftRate) * DriftX,
                _basePosition.y);

            _image.color = new Color(
                _baseColour.r,
                _baseColour.g,
                _baseColour.b,
                _baseColour.a * Mathf.Lerp(MinAlpha, MaxAlpha, intensity));
        }
    }
}
