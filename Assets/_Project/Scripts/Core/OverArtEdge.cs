using TMPro;
using UnityEngine;

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
        // is unchanged.
        internal const float SmallFaceBelowPt = 16f;
        internal const float SmallEdgeDilate = 0.8f;

        private void Awake()
        {
            var label = GetComponent<TMP_Text>();
            if (label == null) return;
            Apply(label, EdgeWidth, label.fontSize < SmallFaceBelowPt ? SmallEdgeDilate : EdgeDilate);
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
