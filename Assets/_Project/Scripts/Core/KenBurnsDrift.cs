using UnityEngine;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // A very slow push-in and pan across the whole painted layer of the main
    // menu. On its own it is the cheapest thing on the screen that makes a
    // still image stop reading as a still image, and it costs no art.
    //
    // This belongs on the PARENT of the background and its ambience, never on
    // the background alone. The lantern glows, window bloom and motes are
    // authored in the painting's own coordinates; zooming the painting out
    // from under them would slide every glow off the lantern it belongs to
    // within a few seconds. Moving the layer moves the whole world together
    // and leaves the buttons -- which are not part of that world -- fixed.
    public class KenBurnsDrift : MonoBehaviour
    {
        public float MaxScale = 1.055f;
        public float ScalePeriodSeconds = 54f;
        public Vector2 PanAmplitude = new Vector2(16f, 9f);

        // Deliberately not a multiple of ScalePeriodSeconds. Two cycles that
        // share a period return to the same pose together and the whole
        // motion reads as a loop; 54 against 79 does not repeat inside any
        // realistic visit to the menu.
        public float PanPeriodSeconds = 79f;

        private RectTransform _rect;
        private Vector2 _basePosition;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _basePosition = _rect.anchoredPosition;
        }

        private void Update()
        {
            // Never below 1. The layer is the full-bleed background, so any scale
            // under 1 pulls its edges inside the canvas and shows bare camera
            // colour down the sides -- the cosine in AmbienceCurves is what
            // guarantees the curve only ever reaches 1 at its trough rather than
            // crossing it.
            float scale = AmbienceCurves.KenBurnsScaleAt(Time.time, MaxScale, ScalePeriodSeconds);
            _rect.localScale = new Vector3(scale, scale, 1f);
            _rect.anchoredPosition = _basePosition
                + SlowDrift.OffsetAt(Time.time, PanAmplitude, PanPeriodSeconds, 0f);
        }
    }
}
