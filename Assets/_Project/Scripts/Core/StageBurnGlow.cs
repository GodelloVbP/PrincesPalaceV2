using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // The coal glow on a body that is burning: its own silhouette, redrawn in
    // ember orange over the figure, breathing on FlickerCurve.Ember, for as long
    // as the status lasts.
    //
    // A STATUS VISUAL, NOT A SPELL LAYER. FightController paints it from the
    // same per-beat snapshot as the badge row (PaintBodyGlow), so it comes on
    // the beat Burn lands, whoever applied it, and goes on the beat Burn ends
    // or the body dies. Nothing here knows about a spell.
    //
    // Shares StageHitFlash's silhouette mechanism and material (UIHitFlash keeps
    // the sprite's alpha and replaces its RGB), and sits UNDER the hit flash in
    // the slot so a blow still lights the figure white over the embers.
    //
    // Follows the figure's pose itself, in LateUpdate, from the sprite Image it
    // shadows: the flash is re-synced by the stage repaint, but a status glow
    // that waited for a repaint would lag a stance behind every beat.
    public class StageBurnGlow : MonoBehaviour
    {
        // Low on purpose: it has to read as heat on the body without hiding the
        // art underneath, the damage numbers or the badges.
        public const float PeakAlpha = 0.6f;

        // The valley of the breath as a share of the peak, so it never goes
        // out while Burn lasts.
        public const float TroughShare = 0.45f;

        public const float RiseSeconds = 0.30f;
        public const float FallSeconds = 0.45f;

        public static readonly Color Ember = new Color(1f, 0.42f, 0.10f, 1f);

        [SerializeField] internal Image image;

        // The figure this glow shadows. Set by the scene build.
        [SerializeField] internal Image source;

        // Assignable from a harness that builds its own canvas, the seam
        // StageHitFlash.Attach is for the same purpose.
        public void Attach(Image overlay, Image figure)
        {
            image = overlay;
            source = figure;
        }

        private float _level;
        private float _phase;

        // Whether Burn is on the body right now, as last painted.
        public bool Burning { get; private set; }

        // How far up the rise or fall it is, 0 (dark) to 1 (full breath).
        public float Level => _level;

        // True while the overlay is drawing anything at all.
        public bool Visible => image != null && image.enabled && image.color.a > 0f
                               && image.gameObject.activeInHierarchy;

        // NOT Clear(): the object is built inactive, so Awake runs inside the
        // first SetBurning(true), after Burning was set, and clearing here
        // would put the glow straight back out.
        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            _phase = Random.value * 100f;
        }

        // `instant` for a body that is gone: a corpse on its death fade carries
        // no embers, and a fall-out would hang on it.
        public void SetBurning(bool burning, bool instant = false)
        {
            Burning = burning;
            if (burning)
            {
                // Built inactive; an inactive object never runs Update.
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                    _level = 0f;
                    if (image != null)
                    {
                        var dark = Ember;
                        dark.a = 0f;
                        image.color = dark;
                    }
                }

                if (image != null) image.enabled = true;
            }
            else if (instant)
            {
                Clear();
            }
        }

        public void Clear()
        {
            Burning = false;
            _level = 0f;
            if (image == null) return;

            var colour = Ember;
            colour.a = 0f;
            image.color = colour;
            image.enabled = false;
        }

        // The alpha for a rise level and a clock reading, pure so it can be
        // pinned without a frame.
        public static float AlphaAt(float level, float seconds)
        {
            float breath = Mathf.Lerp(TroughShare, 1f, Mathf.Clamp01(FlickerCurve.Ember.At(seconds)));
            return PeakAlpha * Mathf.Clamp01(level) * breath;
        }

        // One tick, with the clock handed in so a test can step it.
        internal void Advance(float deltaSeconds, float clock)
        {
            if (image == null) return;

            float target = Burning ? 1f : 0f;
            float step = deltaSeconds / (Burning ? RiseSeconds : FallSeconds);
            _level = Mathf.MoveTowards(_level, target, step);

            if (_level <= 0f && !Burning)
            {
                Clear();
                return;
            }

            var colour = Ember;
            colour.a = AlphaAt(_level, clock + _phase);
            image.color = colour;
        }

        // Copies the figure's current drawing, flip and ground offset: the same
        // four things SyncHitFlash carries onto the flash, and for the same
        // reason (a silhouette in another pose, or standing elsewhere, is worse
        // than none).
        internal void Sync()
        {
            if (image == null || source == null) return;

            image.sprite = source.sprite;
            image.preserveAspect = source.preserveAspect;

            var rect = image.rectTransform;
            var from = source.rectTransform;
            rect.localScale = from.localScale;
            rect.offsetMin = from.offsetMin;
            rect.offsetMax = from.offsetMax;
        }

        private void LateUpdate()
        {
            if (!Burning && _level <= 0f) return;

            Sync();

            // No drawing, no overlay: an Image with no sprite fills its whole
            // rect with the tint.
            image.enabled = image.sprite != null && source != null && source.enabled;
            Advance(Time.deltaTime, Time.time);
        }
    }
}
