using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // The flame inside a practical light -- the hanging lanterns down the main
    // menu's right-hand colonnade, the balustrade posts, the gazebo.
    //
    // THE CURVE ITSELF LIVES IN DOMAIN (FlickerCurve). This is only the part
    // that needs Unity: reading the clock, tinting an Image, scaling a rect.
    // While the curve was a static on this MonoBehaviour it was pure and
    // seam-shaped and completely untestable -- the EditMode suite is Domain-only
    // and could not see it. Moving the arithmetic across the boundary is what
    // makes it both pinnable and tunable.
    //
    // Time.time-driven, carrying none of the project's determinism requirements
    // -- the same reasoning StarTwinkle and BeaconPulse both state.
    [RequireComponent(typeof(Image))]
    public class LanternFlicker : MonoBehaviour
    {
        // The tint the flicker swings around, and the tint SceneBuilder also
        // BAKES the Image at. That matters because nothing guarantees a frame
        // ever ticks: ScreenshotTool renders straight from the authored scene
        // without entering Play Mode, and a component whose Update never runs
        // leaves whatever colour was baked.
        public Color BaseColor = Color.white;

        public float MinAlpha = 0.42f;
        public float MaxAlpha = 1f;
        public float MinScale = 0.93f;
        public float MaxScale = 1.07f;

        // Which flame this is. Swappable so a different light can read
        // differently without a second component -- see FlickerCurve.Ember.
        public FlickerCurve Curve = FlickerCurve.Lantern;

        private Image _image;
        private RectTransform _rect;
        private Vector3 _baseScale;
        private float _phase;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;

            // Without a per-instance offset every lantern in the frame flares on
            // the same frame. A single lantern's curve can be as organic as it
            // likes; eight in lockstep is unmistakably one machine driving all
            // of them.
            _phase = Random.value * 100f;
        }

        private void Update()
        {
            if (Curve == null) return;

            float intensity = Curve.At(Time.time + _phase);
            _image.color = new Color(
                BaseColor.r,
                BaseColor.g,
                BaseColor.b,
                BaseColor.a * Mathf.Lerp(MinAlpha, MaxAlpha, intensity));
            _rect.localScale = _baseScale * Mathf.Lerp(MinScale, MaxScale, intensity);
        }
    }
}
