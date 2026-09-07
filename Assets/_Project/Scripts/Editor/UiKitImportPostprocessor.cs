using UnityEditor;
using UnityEngine;

// The six-theme UI kit (button plates, row plates, containers, banner flags,
// the tab plate, the continue arrow) was importing as plain default-Compressed
// Sprite: textureCompression Compressed (DXT5/BC1 on desktop) at
// compressionQuality 50, no postprocessor behind any of it. Nobody had ever
// stated a setting for this folder; Unity's own defaults were doing the
// talking.
//
// DXT5 quantises to a 4x4 block with one 16-step alpha ramp and 16-step colour
// ramps per block. On a 1536x512 plate carrying a thin gold hairline against a
// dark ground, or a soft alpha falloff at a banner's edge, that block grid is
// exactly big enough to average the hairline into the block average and band
// the falloff into 16 visible steps. Drawn full-size the loss is subtle;
// drawn at button size (300x50-ish, the common case) the block edges line up
// with feature edges often enough to read as mushiness rather than compression
// artifacting.
//
// The owner regenerated all 68 files at higher source quality and reported
// they still look soft in-game -- the PNGs on disk changed, but the encoded
// GPU texture Unity already produced for the old .meta did not get any
// better, because nothing re-imports without either a content change trigger
// or a forced reimport, and the settings that shaped that texture were never
// stated anywhere to change in the first place.
//
// CompressedHQ (BC7 on desktop), not Uncompressed. BC7 encodes per-block with
// multiple partition/endpoint modes instead of DXT5's one fixed layout, which
// is what actually fixes the hairline/alpha-ramp banding -- it is a
// meaningfully different codec, not just a slower encode of the same one.
// Uncompressed would also fix it, but at roughly 3MB per 1536x512 RGBA plate
// times 68 files that is real memory for a texture set that is mostly solid
// fill; BC7 gets the same visual fix at compressed size.
public sealed class UiKitImportPostprocessor : AssetPostprocessor
{
    private const string ButtonsRoot = "/Art/UI/Buttons/Processed/";
    private const string FightButtonsRoot = "/Art/UI/FightButtons/Processed/";

    private void OnPreprocessTexture()
    {
        if (string.IsNullOrEmpty(assetPath)
            || (!assetPath.Contains(ButtonsRoot) && !assetPath.Contains(FightButtonsRoot)))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;

        // This kit scales across a wide range of on-screen sizes (a button
        // plate at ~300x50 next to a full banner flag), the same spread that
        // earns a mip chain on item art -- see ItemSpriteImportPostprocessor.
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;

        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }

    // Forces the settings above onto the kit that was imported before this
    // existed. A postprocessor only runs on import, so adding one changes
    // nothing about the 68+ files already on disk -- their .meta files keep
    // whatever Unity's defaults gave them. This is the one-shot that rewrites
    // them, and the .meta changes have to be committed with the art
    // (CLAUDE.md gotcha #2).
    [MenuItem("Prince's Palace/Reimport UI Kit")]
    public static void ReimportUiKit()
    {
        AssetDatabase.ImportAsset(
            "Assets/_Project/Art/UI/Buttons/Processed",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(
            "Assets/_Project/Art/UI/FightButtons/Processed",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("REIMPORT-COMPLETE: UiKit");
    }
}
