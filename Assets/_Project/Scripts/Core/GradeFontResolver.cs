using PrincesPalace.Domain.Stats;
using TMPro;
using UnityEngine;

namespace PrincesPalace
{
    // Resolves the <font="..."> tag ScalingGrades.Display wraps every
    // player-facing grade letter in, so "INT-D" draws its D in a face where
    // it cannot be read as a 0 (see ScalingGrades.Display for the defect).
    //
    // WHY A RUNTIME FONT. TMP resolves a <font> tag from its own cache of
    // fonts already on screen, then from this request hook, then from
    // Resources/"Fonts & Materials"/. The generated SourceSans3-SemiBold SDF
    // lives in _Project/Fonts, outside Resources, and is only in the cache on
    // a screen that happens to show a FunctionalHeading label -- so leaning on
    // the cache would make the grade letter render or not depending on what
    // else is visible, and TMP prints an unresolved tag as literal text. The
    // TTF already ships under Resources/Fonts, so this builds one dynamic
    // asset from it on first request and hands that back every time after.
    // Dynamic and never saved: its atlas is filled at runtime and nothing is
    // written to disk, so there is no glyph-table churn in git (AUDIT #47).
    //
    // Named after the tag it answers, so TMP's cache keys it under the same
    // hash the tag carries and later lookups never reach this hook.
    public static class GradeFontResolver
    {
        private const string SourceTtf = "Fonts/SourceSans3/SourceSans3-SemiBold";

        private static TMP_FontAsset _font;
        private static bool _loadFailed;

#if UNITY_EDITOR
        // Edit-time rendering (scene previews, EditMode captures) resolves
        // the tag too, not only a play session.
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            // -= first: with domain reload off, a second play session would
            // otherwise subscribe twice. Harmless, but not a thing to leave.
            TMP_Text.OnFontAssetRequest -= Resolve;
            TMP_Text.OnFontAssetRequest += Resolve;
        }

        public static TMP_FontAsset Resolve(int hashCode, string name)
        {
            if (name != ScalingGrades.DisplayFontName)
            {
                return null;
            }

            if (_font != null || _loadFailed)
            {
                return _font;
            }

            var source = Resources.Load<Font>(SourceTtf);
            if (source == null)
            {
                // Graceful degradation: TMP then prints the tag as text, which
                // is loud enough to be caught in a capture, and this warns
                // once rather than every frame.
                _loadFailed = true;
                Debug.LogWarning($"[GradeFontResolver] No font at Resources/{SourceTtf}; grade letters cannot switch face.");
                return null;
            }

            _font = TMP_FontAsset.CreateFontAsset(source);
            if (_font == null)
            {
                _loadFailed = true;
                Debug.LogWarning($"[GradeFontResolver] Could not build a font asset from Resources/{SourceTtf}.");
                return null;
            }

            _font.name = ScalingGrades.DisplayFontName;
            _font.hideFlags = HideFlags.DontSave;
            return _font;
        }
    }
}
