using System.IO;
using PrincesPalace.Domain.Content;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// The TMP sprite asset behind description icons ({fire}, {mana}, ...),
// generated from DescriptionMarkup.Icons so the registry is the only list.
// Committed once and regenerated only when an icon's art or the registry
// changes (docs/ART_PIPELINE.md "Description icons").
//
// The source PNGs are read as raw bytes, not through the importer: the
// Resources/Icons/Ability sprites keep their own import settings, and this
// never calls LoadSprite (CLAUDE.md gotcha 3).
//
// GLYPH METRICS. The asset's face pointSize is left 0, which makes TMP scale
// a sprite to the ascent line of whatever font the label uses; the icon then
// follows the label's face and size with no per-label tuning. GlyphScale
// shrinks it under the ascent line so it reads at cap height, and its
// baseline is the text baseline. GlyphAdvancePad is the gap after the icon.
public static partial class TmpBootstrap
{
    private const string DescriptionIconsDir = "Assets/_Project/Resources/Icons/DescriptionIcons";
    private const string DescriptionIconSourceDir = "Assets/_Project/Resources/Icons/Ability";
    private const int DescriptionIconCell = 64;
    private const int DescriptionIconGutter = 4;
    private const int DescriptionIconColumns = 4;
    private const float DescriptionGlyphScale = 0.75f;
    private const float DescriptionGlyphAdvancePad = 8f;

    [MenuItem("Prince's Palace/Generate Description Icons")]
    public static void GenerateDescriptionIcons()
    {
        EnsureEssentialResources();
        Directory.CreateDirectory(DescriptionIconsDir);

        var icons = DescriptionMarkup.Icons;
        int stride = DescriptionIconCell + 2 * DescriptionIconGutter;
        int rows = (icons.Count + DescriptionIconColumns - 1) / DescriptionIconColumns;
        int width = stride * DescriptionIconColumns;
        int height = stride * rows;

        var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false);
        atlas.SetPixels32(new Color32[width * height]);

        var rects = new GlyphRect[icons.Count];
        for (int i = 0; i < icons.Count; i++)
        {
            string source = $"{DescriptionIconSourceDir}/{icons[i].SpriteName}.png";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!File.Exists(source) || !tex.LoadImage(File.ReadAllBytes(source))
                || tex.width != DescriptionIconCell || tex.height != DescriptionIconCell)
            {
                throw new System.Exception($"[TmpBootstrap.DescriptionIcons] {source} must exist and be {DescriptionIconCell}x{DescriptionIconCell}.");
            }

            // Row 0 at the top of the atlas; texture y runs upward.
            int x = (i % DescriptionIconColumns) * stride + DescriptionIconGutter;
            int y = height - ((i / DescriptionIconColumns) + 1) * stride + DescriptionIconGutter;
            atlas.SetPixels32(x, y, DescriptionIconCell, DescriptionIconCell, tex.GetPixels32());
            rects[i] = new GlyphRect(x, y, DescriptionIconCell, DescriptionIconCell);
            Object.DestroyImmediate(tex);
        }

        string pngPath = $"{DescriptionIconsDir}/DescriptionIcons_Atlas.png";
        File.WriteAllBytes(pngPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

        // Reuse the asset when it exists so its GUID, and every reference to
        // it, survives a regeneration.
        string assetPath = $"{DescriptionIconsDir}/DescriptionIcons.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        // The version string marks the asset as already in the glyph-table
        // format; without it TMP treats it as a legacy asset and upgrades it.
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
        asset.spriteSheet = sheet;
        asset.spriteGlyphTable.Clear();
        asset.spriteCharacterTable.Clear();

        for (int i = 0; i < icons.Count; i++)
        {
            var metrics = new GlyphMetrics(
                DescriptionIconCell, DescriptionIconCell,
                0f, DescriptionIconCell,
                DescriptionIconCell + DescriptionGlyphAdvancePad);
            var glyph = new TMP_SpriteGlyph((uint)i, metrics, rects[i], DescriptionGlyphScale, 0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph)
            {
                name = icons[i].SpriteName,
                scale = 1f,
            });
        }

        if (asset.material == null)
        {
            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = asset.name + " Material" };
            asset.material = material;
            AssetDatabase.AddObjectToAsset(material, asset);
        }

        asset.material.SetTexture(ShaderUtilities.ID_MainTex, sheet);
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(asset.material);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        Debug.Log("BUILD-COMPLETE: TmpBootstrap.DescriptionIcons");
    }
}
