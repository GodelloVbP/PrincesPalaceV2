using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // A DARK EDGE ON TEXT THAT SITS STRAIGHT ON THE ART, with no kit plate of
    // its own behind it. Added by UiEmitter to every label a screen tree
    // declares .OverArt() on; DamagePopup calls Apply itself, because its glow
    // rewrites the same material on every play.
    //
    // Why an edge and not a colour: the fight log and the ENEMIES heading float
    // over whatever the backdrop is at that spot, and a spell can put white,
    // violet or black under them a moment later. No face colour separates
    // from all of those;
    // a near-black ring around a light face does. The fight HUD took the black
    // slab away from the log on purpose ("read as a slab dropped over the
    // battlefield"), so the edge is carried by the glyphs, not by a plate.
    //
    // ON THE LABEL'S OWN MATERIAL INSTANCE (fontMaterial), so the shared font
    // material every other label of this font draws with is untouched.
    //
    // OUTLINE_ON EXPLICITLY. The fonts are on TMP_SDF-Mobile, whose outline
    // branch is `#ifdef OUTLINE_ON` (shader_feature): the width alone draws
    // nothing, which is how DamagePopup's first cut passed a property test and
    // drew no edge. The keyword variant ships because OnBarCaption.mat enables
    // it on that shader. No underlay: OUTLINE_ON + UNDERLAY_ON together is a
    // variant no material asset uses, so a player build would strip it.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class OverArtEdge : MonoBehaviour
    {
        // Near-black with a violet cast, the same ink DamagePopup already uses,
        // so every edged glyph in the fight is one family.
        internal static readonly Color32 EdgeColour = new Color32(10, 7, 16, 235);

        // For the 12-20pt log and heading faces. An SDF outline is centred on
        // the glyph's edge, so half of it eats into the face; the dilate gives
        // that half back, or ChakraPetch Regular's thin strokes go spidery.
        internal const float EdgeWidth = 0.4f;
        internal const float EdgeDilate = 0.3f;

        // SMALL FACES NEED MORE FACE. At 12pt a ChakraPetch stroke is barely
        // wider than the SDF's own antialiasing ramp, so with the 20pt dilate
        // no pixel of the face ever reaches its full colour: every pixel of
        // "1 STANDING" blends into the dark ring, measuring a muddy midtone
        // whatever face colour the tree asked for. The heavier dilate
        // thickens the stroke until its core is solid face again; the ring
        // is unchanged. At 0.8 the 14pt ENEMIES heading still had stems
        // alternating full and ~90% face columns; 1.0 closes that, and the
        // backing below carries the rest of the legibility over pale art.
        internal const float SmallFaceBelowPt = 16f;
        internal const float SmallEdgeDilate = 1.0f;

        // THE BACKING. A feathered, low-alpha dark plate hugging the label's
        // rendered text, so the label reads over any backdrop at any size;
        // the edge alone cannot at 14pt, where the ring and the face are each
        // about one pixel column. A SIBLING drawn just before the label (a
        // child would draw over its own text), re-fitted every frame because log lines, counts and
        // intent numbers change under it. Fades with the label's own alpha.
        internal const float BackingAlpha = 0.62f;
        internal const float BackingPadX = 12f;
        internal const float BackingPadY = 4f;

        // Emitted by UiEmitter as the label's previous sibling (a GameObject
        // may only be created there), without a sprite: a sprite built in the
        // Editor is not an asset and would not survive the scene save, so the
        // feathered tile is made here at Awake instead.
        public Image Backing;

        private TMP_Text _label;
        private RectTransform _backing;
        private Image _backingImage;

        // The last values written to the backing. Writing a RectTransform or
        // an Image colour dirties the HUD canvas even when the value is
        // unchanged, so a static log line would force a rebuild 60 times a
        // second. The fit is computed every LateUpdate (it depends on the
        // text, the alpha, the parent rect and the label's transform relative
        // to the parent) and written only when it moved. OnEnable forces one
        // write because OnDisable hides the plate.
        private const float WriteEpsilon = 0.01f;
        private const float AlphaEpsilon = 0.001f;
        private Vector2 _writtenSize;
        private Vector2 _writtenPos;
        private float _writtenAlpha;
        private bool _forceWrite = true;

        // Counts the frames that wrote anything; read by tests.
        public int WriteCountForTest { get; private set; }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            if (_label == null) return;
            Apply(_label, EdgeWidth, _label.fontSize < SmallFaceBelowPt ? SmallEdgeDilate : EdgeDilate);
            if (Backing == null) return;
            _backingImage = Backing;
            _backing = Backing.rectTransform;
            _backingImage.sprite = BackingSprite();
            _backingImage.type = Image.Type.Sliced;
            _backingImage.raycastTarget = false;
            _backing.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _forceWrite = true;
        }

        private void OnDisable()
        {
            if (_backing != null) _backing.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_backing != null) FitBacking();
        }

        private void FitBacking()
        {
            var parent = _backing.parent as RectTransform;
            bool has = parent != null && _label != null && !string.IsNullOrEmpty(_label.text)
                && _label.color.a > 0.01f;
            if (!has) { SetBackingActive(false); return; }

            var b = _label.textBounds;
            if (b.size.x <= 0.01f || b.size.y <= 0.01f) { SetBackingActive(false); return; }

            var rt = _label.rectTransform;
            Vector2 lo = parent.InverseTransformPoint(rt.TransformPoint(b.min));
            Vector2 hi = parent.InverseTransformPoint(rt.TransformPoint(b.max));
            Vector2 size = hi - lo;
            // Padding is in the label's own units, so the plate scales with a
            // badge that rides a depth scale.
            float scale = size.x / b.size.x;
            Vector2 newSize = size + new Vector2(BackingPadX, BackingPadY) * 2f * scale;
            Vector2 newPos = (lo + hi) * 0.5f - parent.rect.center;
            float newAlpha = BackingAlpha * _label.color.a;

            bool wrote = false;
            if (_forceWrite || (newSize - _writtenSize).sqrMagnitude > WriteEpsilon * WriteEpsilon)
            {
                _backing.sizeDelta = newSize;
                _writtenSize = newSize;
                wrote = true;
            }
            if (_forceWrite || (newPos - _writtenPos).sqrMagnitude > WriteEpsilon * WriteEpsilon)
            {
                _backing.anchoredPosition = newPos;
                _writtenPos = newPos;
                wrote = true;
            }
            if (_forceWrite || Mathf.Abs(newAlpha - _writtenAlpha) > AlphaEpsilon)
            {
                var c = _backingImage.color;
                c.a = newAlpha;
                _backingImage.color = c;
                _writtenAlpha = newAlpha;
                wrote = true;
            }
            _forceWrite = false;
            if (!_backing.gameObject.activeSelf)
            {
                _backing.gameObject.SetActive(true);
                wrote = true;
            }
            if (wrote) WriteCountForTest++;
        }

        private void SetBackingActive(bool active)
        {
            if (_backing.gameObject.activeSelf == active) return;
            _backing.gameObject.SetActive(active);
            WriteCountForTest++;
        }

        private static Sprite _backingSprite;

        // 48px tile, 14px rounded corner fading out over 12px; the border
        // keeps the feather at a fixed size while the middle stretches.
        private static Sprite BackingSprite()
        {
            if (_backingSprite != null) return _backingSprite;
            const int n = 48;
            const float radius = 14f, feather = 12f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "OverArtBacking", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Signed distance to a rounded box whose edge sits half a
                    // feather inside the tile; alpha ramps across the edge.
                    float hb = half - feather * 0.5f;
                    float qx = Mathf.Abs(x + 0.5f - half) - (hb - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (hb - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = 1f - Mathf.SmoothStep(-feather * 0.5f, feather * 0.5f, d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _backingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
            _backingSprite.name = "OverArtBacking";
            return _backingSprite;
        }

        public static void Apply(TMP_Text label, float width, float dilate)
        {
            if (label == null) return;

            var material = label.fontMaterial;
            if (material == null) return;

            if (!material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }

            if (material.HasProperty(ShaderUtilities.ID_FaceDilate))
            {
                material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            }

            label.outlineColor = EdgeColour;
            label.outlineWidth = width;
            label.UpdateMeshPadding();
            label.SetVerticesDirty();
        }
    }
}
