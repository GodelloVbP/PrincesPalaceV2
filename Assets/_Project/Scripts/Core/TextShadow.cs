using TMPro;
using UnityEngine;

namespace PrincesPalace
{
    // A SOFT DROP SHADOW UNDER TEXT THAT SITS ON PAINTED CLOTH, with no plate
    // behind it (the Reckoning's board and card faces). It deepens the
    // underlay the role's material already carries, on the label's own
    // material instance, so the shared font material is untouched.
    //
    // Underlay rather than OverArtEdge's outline: these faces are 16-36pt on a
    // surface the kit chose, so a shadow separates them without the ring an
    // outline draws. Every role but OnBarCaption ships UNDERLAY_ON, so the
    // keyword variant exists in a player build; a label whose material lacks
    // it keeps its underlay as it is.
    //
    // The face goes white on the instance too, so the label's own colour is
    // the colour drawn: a role's face colour multiplies the vertex colour,
    // and TacticalData's violet turned parchment into lilac and gold into
    // copper on the cloth.
    public static class TextShadow
    {
        internal static readonly Color ShadowColour = new Color(0.02f, 0.01f, 0.03f, 0.9f);
        internal const float OffsetX = 0.35f;
        internal const float OffsetY = -0.55f;
        internal const float Softness = 0.35f;
        internal const float Dilate = 0.25f;

        public static void Apply(TMP_Text label)
        {
            if (label == null) return;

            var material = label.fontMaterial;
            if (material == null) return;

            if (material.HasProperty(ShaderUtilities.ID_FaceColor))
                material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            if (!material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay))
            {
                label.SetVerticesDirty();
                return;
            }

            material.SetColor(ShaderUtilities.ID_UnderlayColor, ShadowColour);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, OffsetX);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, OffsetY);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Softness);
            if (material.HasProperty(ShaderUtilities.ID_UnderlayDilate))
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, Dilate);

            label.UpdateMeshPadding();
            label.SetVerticesDirty();
        }

        public static void Apply(TMP_Text[] labels)
        {
            if (labels == null) return;
            foreach (var label in labels) Apply(label);
        }
    }
}
