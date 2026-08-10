using UnityEngine;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // A long, slow elliptical wander around wherever the rect was authored:
    // the mist banks over the water, the haze in the nebula, and the pan half
    // of KenBurnsDrift.
    //
    // An ellipse rather than a line traced back and forth. A rect shuttling
    // along one axis reads as a mechanism; driving x and y a quarter cycle
    // apart reads as drift, for one extra trig call. Periods are in the tens
    // of seconds on purpose -- visible over a minute spent on the menu, never
    // caught moving by a single glance.
    public class SlowDrift : MonoBehaviour
    {
        public Vector2 Amplitude = new Vector2(24f, 8f);
        public float PeriodSeconds = 40f;
        public float PhaseSeconds;

        // On by default because the common case is a handful of these that
        // must not travel in formation. SceneBuilder turns it off where the
        // spread matters more than the randomness -- an evenly phased pool
        // covers its cycle better than a random one, which clumps.
        public bool RandomisePhaseOnAwake = true;

        private RectTransform _rect;
        private Vector2 _basePosition;

        // Forwarder onto AmbienceCurves.DriftOffsetAt. Vector2 is a Unity type
        // and Domain cannot see it, so the boundary converts -- which is the
        // whole cost of having the arithmetic somewhere testable.
        public static Vector2 OffsetAt(float seconds, Vector2 amplitude, float periodSeconds, float phaseSeconds)
        {
            var offset = AmbienceCurves.DriftOffsetAt(
                seconds, new Drift2(amplitude.x, amplitude.y), periodSeconds, phaseSeconds);
            return new Vector2(offset.X, offset.Y);
        }

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            // Captured once. Every frame writes base + offset rather than
            // accumulating onto the previous frame, so float error cannot
            // walk the rect away from where it was authored.
            _basePosition = _rect.anchoredPosition;

            if (RandomisePhaseOnAwake)
            {
                PhaseSeconds = Random.value * PeriodSeconds;
            }
        }

        private void Update()
        {
            _rect.anchoredPosition = _basePosition + OffsetAt(Time.time, Amplitude, PeriodSeconds, PhaseSeconds);
        }
    }
}
