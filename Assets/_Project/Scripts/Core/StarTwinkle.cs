using UnityEngine;
using PrincesPalace.Domain.Ambience;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Gentle alpha flicker for the talent tree background's near star
    // layer -- only the sparse near layer gets this (far/mid stay static),
    // since a few hundred far-layer stars each running their own Update
    // would be needless cost for a barely-visible effect at that size.
    // Time.time-driven, same non-deterministic-is-fine reasoning as
    // BeaconPulse -- this is decoration, not gameplay. A random per-
    // instance phase and period keep the whole layer from twinkling in
    // lockstep.
    [RequireComponent(typeof(Image))]
    public class StarTwinkle : MonoBehaviour
    {
        private const float MinAlphaScale = 0.4f;
        private const float MinPeriod = 1.6f;
        private const float MaxPeriod = 3.4f;

        private Image _image;
        private Color _baseColor;
        private float _period;
        private float _phase;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _baseColor = _image.color;
            _period = Random.Range(MinPeriod, MaxPeriod);
            _phase = Random.value * _period;
        }

        private void Update()
        {
            var color = _baseColor;
            color.a = _baseColor.a * AmbienceCurves.StarAlphaScaleAt(Time.time, _period, _phase);
            _image.color = color;
        }
    }
}
