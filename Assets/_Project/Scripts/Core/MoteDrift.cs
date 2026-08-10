using UnityEngine;
using PrincesPalace.Domain.Ambience;
using UnityEngine.UI;

namespace PrincesPalace
{
    // One drifting ember. It rises from where it was authored, swaying,
    // fading in at the bottom of its travel and out at the top, then
    // restarts -- so a fixed pool of these reads as a continuous drift of
    // light with no spawning, no allocation, and no pool manager to own
    // them. Each mote is entirely self-contained, the same "every instance
    // owns its own randomised phase" shape StarTwinkle uses.
    //
    // StartProgress is what makes the pool work. SceneBuilder hands each
    // mote a different point in the same cycle, so the field is already
    // evenly spread on the very first frame instead of all launching
    // together and then slowly decohering.
    [RequireComponent(typeof(Image))]
    public class MoteDrift : MonoBehaviour
    {
        public Color BaseColor = Color.white;
        public float TravelHeight = 430f;
        public float SwayAmplitude = 26f;
        public float SwayCycles = 1.5f;
        public float LifeSeconds = 15f;

        [Range(0f, 1f)] public float StartProgress;

        // Asymmetric on purpose: a mote should be at full strength quickly
        // after it appears low in the frame, then spend a long time thinning
        // out as it rises, which is how airborne light actually reads. A
        // symmetric fade looks like a dot being cross-faded.
        private const float FadeInEnds = 0.18f;
        private const float FadeOutBegins = 0.68f;

        private Image _image;
        private RectTransform _rect;
        private Vector2 _basePosition;

        // Forwarder. The curve itself is AmbienceCurves.MoteAlphaAt -- in
        // Domain, where a test can finally reach it.
        public static float AlphaAt(float progress) => AmbienceCurves.MoteAlphaAt(progress);

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            _basePosition = _rect.anchoredPosition;
        }

        private void Update()
        {
            // Repeat rather than a modulo on a growing counter: this stays
            // exact for as long as the menu is open, and needs no state to
            // reset when the scene reloads.
            float progress = Mathf.Repeat(Time.time / Mathf.Max(0.01f, LifeSeconds) + StartProgress, 1f);

            float sway = Mathf.Sin(progress * SwayCycles * 2f * Mathf.PI) * SwayAmplitude;
            _rect.anchoredPosition = _basePosition + new Vector2(sway, progress * TravelHeight);

            _image.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, BaseColor.a * AlphaAt(progress));
        }
    }
}
