using UnityEditor;
using UnityEngine;

// Forces every part atlas under Resources/Rigs/ to import as a Multiple-mode
// Sprite, readable, uncompressed -- the same silent-null trap
// StanceSpriteImporter.cs documents for stance art applies here identically:
// Resources.Load<GameObject> on the generated prefab would still return a
// real object, but SpriteSkin's bind poses / RigImporter's bounds reads
// would be working against unreadable or wrongly-typed texture data with no
// error anywhere.
public class RigAtlasImportPostprocessor : AssetPostprocessor
{
    private const string RigsRoot = "/Resources/Rigs/";

    private void OnPreprocessTexture()
    {
        if (!IsRigAtlas(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        var platformSettings = importer.GetDefaultPlatformTextureSettings();
        platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platformSettings);
    }

    private static bool IsRigAtlas(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Contains(RigsRoot);
    }

    // OnPreprocessTexture only fires on an actual (re)import. Called once via
    // -executeMethod (or after tools/rig_actor.py writes a new atlas) to force
    // every existing rig atlas through this importer if it was ever imported
    // under Unity's default settings first.
    public static void ForceReimportAll()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Resources/Rigs" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsRigAtlas(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
