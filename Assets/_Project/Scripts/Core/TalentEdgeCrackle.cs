using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Jittery alpha flicker for a lit talent edge's bright core -- the line
    // crackling with energy, as distinct from BeaconPulse's smooth breathing.
    //
    // TWO SINE WAVES AT NON-HARMONIC FREQUENCIES, summed. One wave has a
    // visible period and reads as a pulse; two that never line up read as
    // irregular flicker, which is the difference between "charged" and
    // "blinking". 17 and 29 share no factor, so the pair does not repeat on
    // any timescale a player will sit through.
    //
    // A RANDOM PER-INSTANCE PHASE, because the alternative is worse than no
    // effect: twenty edges flickering in lockstep reads as the whole screen
    // strobing rather than as current running through separate lines.
    //
    // Migrated from v1, with one change -- unscaled time. Nothing pauses the
    // talent screen today, but decoration that stops when the clock does is a
    // bug waiting for the first thing that pauses it, and this has no reason
    // to care.
    [RequireComponent(typeof(Image))]
    public class TalentEdgeCrackle : MonoBehaviour
    {
        // Never fully dark. The core is what says the connection is LIVE, so
        // the flicker rides on top of it rather than switching it off -- a
        // crack that reaches zero reads as a broken connection, which is the
        // opposite of what an invested edge means.
        private const float MinAlphaScale = 0.6f;

        private const float FrequencyA = 17f;
        private const float FrequencyB = 29f;

        private Image _image;
        private Color _baseColor;
        private float _phase;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _baseColor = _image.color;

            // Captured once. Re-reading the live colour each frame would
            // compound the flicker into a fade to nothing within a second.
            _phase = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float wave = Mathf.Sin(t * FrequencyA + _phase) * 0.5f
                       + Mathf.Sin(t * FrequencyB + _phase * 1.7f) * 0.5f;

            float k = Mathf.Clamp01((wave + 1f) * 0.5f);

            var colour = _baseColor;
            colour.a = _baseColor.a * Mathf.Lerp(MinAlphaScale, 1f, k);
            _image.color = colour;
        }
    }
}
