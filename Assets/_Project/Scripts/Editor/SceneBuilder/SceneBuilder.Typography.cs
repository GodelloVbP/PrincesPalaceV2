using TMPro;
using UnityEditor;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

// Resolves a TypographyRole (Domain/UiKit/Typography.cs, engine-free) to the
// generated font/material assets TmpBootstrap.Typography.cs writes. Wired
// into UiEmitter.ApplyTypography: every Role-bearing node reads FontFor and
// MaterialFor here, falling back to SceneBuilder.UiFont only when a node
// declares no Role, or when the generated asset is missing.
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
