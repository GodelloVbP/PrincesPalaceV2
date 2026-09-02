using TMPro;
using UnityEditor;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

// Resolves a TypographyRole (Domain/UiKit/Typography.cs, engine-free) to the
// generated font/material assets TmpBootstrap.Typography.cs writes. Not
// wired into any emit call yet -- ScreenRegistry/UiEmitter still drive today
// with SceneBuilder.UiFont, and adopting these per-role is a separate change
// with its own screen-by-screen sweep.
public static partial class SceneBuilder
{
    // Null on a miss (asset not yet generated) rather than throwing --
    // matches this project's graceful-degradation-on-missing-art posture
    // (see SceneBuilder.LoadSpriteByKey and TmpBootstrap.EnsureFontAsset).
    public static TMP_FontAsset FontFor(TypographyRole role)
    {
        if (_typographyFonts.TryGetValue(role, out var cached) && cached != null) return cached;

        string assetPath = $"{TypographyFontDir}/{Typography.Specs[role].FontAssetName}.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (font == null)
        {
            Debug.LogWarning($"[SceneBuilder] No typography font at {assetPath} for role {role} -- run TmpBootstrap.GenerateTypographyAssets.");
            return null;
        }

        _typographyFonts[role] = font;
        return font;
    }

    public static Material MaterialFor(TypographyRole role)
    {
        if (_typographyMaterials.TryGetValue(role, out var cached) && cached != null) return cached;

        string matPath = $"{TypographyMaterialDir}/{Typography.Specs[role].MaterialName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogWarning($"[SceneBuilder] No typography material at {matPath} for role {role} -- run TmpBootstrap.GenerateTypographyAssets.");
            return null;
        }

        _typographyMaterials[role] = mat;
        return mat;
    }
}
