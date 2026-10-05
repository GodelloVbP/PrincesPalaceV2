using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace
{
    // The coal glow on a body that is burning: ember light gathered on the figure's
    // own outline and at its feet, a faint tint over the rest, breathing on
    // FlickerCurve.Ember, for as long as the status lasts. The creature keeps its
    // own colours; where the light sits is BodyLight.BurnWeight, in UIBurnGlow.
    //
    // A STATUS VISUAL, NOT A SPELL LAYER. FightController paints it from the
    // same per-beat snapshot as the badge row (PaintBodyGlow), so it comes on
    // the beat Burn lands, whoever applied it, and goes on the beat Burn ends
    // or the body dies. Nothing here knows about a spell.
    //
    // Shares StageHitFlash's silhouette mechanism (SilhouetteLayout) but draws
    // with its own additive, bottom-weighted material (UIBurnGlow, built by
    // PipelineBuilder), and sits UNDER the hit flash in the slot so a blow
    // still lights the figure white over the embers.
    //
    // Follows the figure's pose itself, in LateUpdate, from the sprite Image it
    // shadows: the flash is re-synced by the stage repaint, but a status glow
    // that waited for a repaint would lag a stance behind every beat.
    public class StageBurnGlow : MonoBehaviour
    {
        // The strength of the rim at the feet; the shader scales it down to the
        // interior's faint share (BodyLight.BurnInteriorShare) and the crown's
        // half, so the body itself is never washed over.
        public const float PeakAlpha = 0.85f;

        // The valley of the breath as a share of the peak, so it never goes
        // out while Burn lasts.
        public const float TroughShare = 0.45f;

        // THE MOST A LIVE FIRE LAYER (SpellLayer.stoke) ADDS to the rim's alpha
        // while it burns on this body, so the glow flickers with the fire at its
        // feet. It may go past PeakAlpha (up to 1): the breath already sits near
        // the peak for stretches of its cycle, and a lift that clamped there
        // would show nothing at exactly those moments.
        public const float StokeBoost = 0.3f;

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
        private float _stoke;

        // 0..1, set each frame from the spell layers stoking this body.
        public float Stoke
        {
            get => _stoke;
            set => _stoke = Mathf.Clamp01(value);
        }

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
                    if (image != null) image.color = EmberAt(0f);
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

            image.color = EmberAt(0f);
            image.enabled = false;
        }

        public static Color EmberAt(float alpha)
        {
            var colour = Ember;
            colour.a = alpha;
            return colour;
        }

        // The alpha for a rise level and a clock reading, pure so it can be
        // pinned without a frame.
        public static float AlphaAt(float level, float seconds, float stoke = 0f)
        {
            float breath = Mathf.Lerp(TroughShare, 1f, Mathf.Clamp01(FlickerCurve.Ember.At(seconds)));
            float alpha = PeakAlpha * Mathf.Clamp01(level) * breath;
            return Mathf.Min(1f, alpha + StokeBoost * Mathf.Clamp01(stoke) * Mathf.Clamp01(level));
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

            image.color = EmberAt(AlphaAt(_level, clock + _phase, _stoke));
        }

        // Copies the figure's current drawing, flip and ground offset (see
        // SilhouetteLayout), and only when the figure moved on from the last
        // copy: most frames it has not.
        private Sprite _syncedSprite;
        private Vector3 _syncedScale;
        private Vector2 _syncedMin, _syncedMax;
        private bool _synced;

        internal void Sync()
        {
            if (image == null || source == null) return;

            var from = source.rectTransform;
            if (_synced && _syncedSprite == source.sprite && _syncedScale == from.localScale
                && _syncedMin == from.offsetMin && _syncedMax == from.offsetMax) return;

            image.sprite = source.sprite;
            SilhouetteLayout.Copy(source, image, from.localScale);
            _syncedSprite = source.sprite;
            _syncedScale = from.localScale;
            _syncedMin = from.offsetMin;
            _syncedMax = from.offsetMax;
            _synced = true;
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
