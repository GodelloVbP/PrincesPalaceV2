using UnityEditor;

// Forces every texture under Resources/Enemies/ to import as a Sprite.
//
// Without this the stage silently shows nothing: a PNG imported as a plain
// Texture still exists, still previews fine in the Project window, and
// Resources.Load<Sprite> on it just returns null — so FightController would
// fall through to its no-art nameplate and every monster would look like it
// simply had no sprites authored. Nothing throws, nothing logs.
//
// It has to be an importer rule rather than a one-off fix because these
// assets are dropped in by hand (a generated sheet sliced by
// tools/slice_enemy_sheet.py) and are NOT referenced by any scene, so
// SceneBuilder's LoadSprite — which is what coerces textureType for every
// other sprite in the project — never touches them. A future enemy sheet
// would otherwise hit exactly the same invisible failure.
public class EnemySpriteImportPostprocessor : AssetPostprocessor
{
    private const string EnemySpriteFolder = "Assets/_Project/Resources/Enemies/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(EnemySpriteFolder))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Bottom-centre matches the pivot the stage slots use, so a sprite's
        // own pivot can never fight the slot's ground-line alignment.
        importer.spritePivot = new UnityEngine.Vector2(0.5f, 0f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
    }
}
