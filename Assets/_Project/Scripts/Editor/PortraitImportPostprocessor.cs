using UnityEditor;
using UnityEngine;

// Character portraits import as Sprites with a mip chain, because nothing else
// sets them any more.
//
// THIS FOLDER USED TO BE COVERED BY ACCIDENT. Portraits lived under Art/ and
// were baked into the Character pane's scene by SceneBuilder.LoadSpriteByKey,
// which force-imports whatever it loads: textureType Sprite, alphaIsTransparency,
// mipmaps on, Trilinear. Moving them to Resources so the dossier can load them
// at runtime takes that seam away -- Resources.Load reads the .meta and forces
// nothing -- so the settings have to be stated somewhere, and this is that
// somewhere. The four values below are LoadSpriteByKey's, deliberately
// unchanged: the point of the move was runtime loading, not a different look.
//
// The silent failure this prevents is the one StanceSpriteImporter's header
// describes at length: Resources.Load<Sprite> returns NULL, not an error, for a
// texture imported with Unity's default plain-Texture2D type. The dossier then
// falls back to the armour stand and nothing anywhere says why.
//
// SEPARATE FROM StanceSpriteImporter rather than another root on its list,
// because the settings genuinely differ in both directions. A stance sprite is
// isReadable (FightController samples its opaque bounds at runtime to place a
// foot shadow) and has no mip chain (drawn at one size); a portrait is drawn at
// ~219px from a 1122x1402 source, which is a 5x reduction and exactly the case
// mipmaps exist for, and nothing ever reads its pixels back. Folding them
// together would mean one of the two silently carrying the other's settings.
public sealed class PortraitImportPostprocessor : AssetPostprocessor
{
    private const string PortraitRoot = "/Resources/Portraits/";

    private void OnPreprocessTexture()
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.Contains(PortraitRoot))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;

        // Cut-out figures on transparency; without this the edges pick up a
        // halo where Unity premultiplies against black.
        importer.alphaIsTransparency = true;

        // Trilinear rather than Bilinear for the reason LoadSpriteByKey gives:
        // this UI scales while it is being looked at, and Bilinear switches mip
        // level in a single frame, which is visible as a pop.
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
    }

    // Forces the settings above onto portraits imported before this existed.
    // A postprocessor only runs on import, so adding one changes nothing about
    // what is already on disk; the .meta changes have to be committed with the
    // art (CLAUDE.md gotcha #2).
    [MenuItem("Prince's Palace/Reimport Portraits")]
    public static void ReimportPortraits()
    {
        AssetDatabase.ImportAsset(
            "Assets/_Project/Resources/Portraits",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("REIMPORT-COMPLETE: Portraits");
    }
}
