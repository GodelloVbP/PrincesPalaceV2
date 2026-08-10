using UnityEngine;
using PrincesPalace.Domain.Ambience;
using UnityEngine.UI;

namespace PrincesPalace
{
    // The soft glow behind an OPEN room. CSS keyframes `beacon`: opacity
    // .35<->.85, scale 1<->1.14, over a 2.4s ease-in-out loop. DescentMapView
    // sets BaseColor once per Render() and toggles this GameObject active;
    // this owns only the continuous animation, same split as everything
    // else here that separates "what state is this" from "how does it move".
    //
    // Time.time-driven rather than anything seeded -- this is decoration,
    // not gameplay, so it carries none of the project's determinism
    // requirements (see SeededRandom's own convention).
    [RequireComponent(typeof(Image))]
    public class BeaconPulse : MonoBehaviour
    {
        private const float DefaultPeriodSeconds = 2.4f;
        private const float DefaultMinAlpha = 0.35f;
        private const float DefaultMaxAlpha = 0.85f;
        private const float MinScale = 1f;
        private const float MaxScale = 1.14f;

        public Color BaseColor = Color.white;

        // Overridable per instance, not just per-color: the talent orb glow
        // wants a more prominent pulse than the map's room beacons without
        // moving the shared default every other BeaconPulse still uses.
        public float MinAlpha = DefaultMinAlpha;
        public float MaxAlpha = DefaultMaxAlpha;

        // Period and phase are per instance for the same reason the alphas
        // are, plus one more: a FIELD of these. The map only ever shows a
        // couple of beacons at once, so a shared period and a shared zero
        // phase were invisible. The main menu's lit palace windows are
        // sixteen of them in one frame, and sixteen glows breathing in
        // perfect unison reads as a single flashing sign rather than a
        // building with people in it.
        public float PeriodSeconds = DefaultPeriodSeconds;
        public float PhaseSeconds;

        private Image _image;
        private RectTransform _rect;
        private Vector3 _baseScale;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;
        }

        private void Update()
        {
            // 0 at the top and bottom of the cycle, 1 at the midpoint --
            // matches the CSS keyframe's 0%/50%/100% shape directly.
            // Guarded because PeriodSeconds is public now: a zero left in it
            // would divide to NaN and blank the Image entirely.
            float period = PeriodSeconds > 0f ? PeriodSeconds : DefaultPeriodSeconds;
            float t = AmbienceCurves.BeaconProgressAt(Time.time, period, PhaseSeconds);
            _image.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, BaseColor.a * Mathf.Lerp(MinAlpha, MaxAlpha, t));
            _rect.localScale = _baseScale * Mathf.Lerp(MinScale, MaxScale, t);
        }
    }
}
