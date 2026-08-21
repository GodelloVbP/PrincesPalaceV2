using UnityEditor;

// Item art imports with a mip chain, because it is drawn at six different
// sizes and none of them is its own.
//
// THE NUMBERS, measured rather than assumed. The sliced icons are about
// 250-295 x 384. They are drawn at:
//
//     pack cell           64px      DossierLayout.PackIconSize
//     equipment slot      74px      DossierLayout.SlotSize
//     glossary detail    140px      GlossaryScreen
//     relic draft card   160px      RelicDraftScreen
//     reckoning offer    200px      ReckoningScreen
//
// So one asset serves a 6x reduction at the smallest and 1.9x at the largest.
// Without a mip chain, bilinear takes roughly one texel in six for the pack
// cell -- the sparse-sampling aliasing that reads as grain on painterly art,
// and that crawls the moment anything scales or moves.
//
// WHY NOT SIMPLY SLICE SMALLER, which is the cheaper-looking fix: the 3x spread
// between 64 and 200 is what rules it out. A 128px source would be soft on the
// Reckoning offer and still aliased in the pack. Serving a range off one asset
// is the case mipmaps exist for.
//
// KAISER, not the default box filter. Box averaging on this art loses the
// brush edges that make it read as painted; Kaiser is sharper through the
// chain, which is the whole point of turning them on.
//
// THE OLD SETTING WAS NOT A DECISION. EnemySpriteImportPostprocessor and
// IntentIconImportPostprocessor both write `mipmapEnabled = false`, and neither
// says why -- unlike the pivot line beside it in the first, which explains
// itself. It is Unity's sprite default written out by hand. Those two are left
// alone deliberately: a stage actor is drawn at one size, and an intent icon is
// 28px and never scales, so neither has the spread that earns the memory.
public sealed class ItemSpriteImportPostprocessor : AssetPostprocessor
{
    private const string ItemArtFolder = "Assets/_Project/Art/Items/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ItemArtFolder))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;

        importer.mipmapEnabled = true;
        importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;

        // The pivot is deliberately NOT set here, unlike the stage actors'
        // postprocessor. An item icon is centred in its cell by the layout; a
        // bottom-centre pivot would be a stage convention leaking into the UI.
    }

    // Forces the settings above onto art that was imported before this existed.
    //
    // A postprocessor only runs on import, so adding one changes nothing about
    // the 580 icons already on disk -- their .meta files keep whatever they were
    // given. This is the one-shot that rewrites them, and the .meta changes have
    // to be committed with the art (CLAUDE.md gotcha #2).
    [MenuItem("Prince's Palace/Reimport Item Art")]
    public static void ReimportItemArt()
    {
        AssetDatabase.ImportAsset(
            ItemArtFolder.TrimEnd('/'),
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("REIMPORT-COMPLETE: ItemArt");
    }
}
