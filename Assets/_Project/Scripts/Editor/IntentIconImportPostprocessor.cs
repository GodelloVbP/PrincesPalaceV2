using UnityEditor;

// Forces every texture under Resources/Intent/ to import as a Sprite.
//
// Exactly the failure EnemySpriteImportPostprocessor exists to prevent, one
// folder over: a PNG imported as a plain Texture previews fine and
// Resources.Load<Sprite> on it returns null, so the intent badge would fall
// back to nothing with no error logged anywhere. These icons are dropped in by
// hand (downloaded from game-icons.net, see CREDITS.md) and the runtime swaps
// them by Resources.Load, so SceneBuilder.LoadSprite -- which coerces
// textureType for everything a SCENE references -- only ever touches the one
// default icon baked into the scene, not the other six.
//
// A SEPARATE rule from the enemy one rather than a shared table, for one
// reason: the pivot differs and it is load-bearing on both sides. An enemy
// sprite pivots bottom-centre so the art meets the stage's ground line; a badge
// pivots CENTRE so it sits square on the point it is pinned to.
public class IntentIconImportPostprocessor : AssetPostprocessor
{
    private const string IntentIconFolder = "Assets/_Project/Resources/Intent/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(IntentIconFolder))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new UnityEngine.Vector2(0.5f, 0.5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
    }
}
