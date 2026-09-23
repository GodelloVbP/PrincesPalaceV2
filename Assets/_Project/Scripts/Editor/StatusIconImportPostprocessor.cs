using UnityEditor;

// Forces every texture under Resources/Status/ to import as a Sprite.
//
// The Status/ counterpart of IntentIconImportPostprocessor, and a separate
// class for the same reason that one is separate from the enemy sprite rule:
// a PNG imported as a plain Texture previews fine and Resources.Load<Sprite>
// on it returns null, so a status badge would silently fall back to its
// three-letter glyph forever with nothing in the log to say why. These icons
// are commissioned from docs/STATUS_ICON_PROMPTS.md and keyed by
// tools/key_green_screen.py --kit status (make_status_icons.py is retired,
// see PLAN_STATUS_EFFECT_UI.md phase 2), swapped in at RUNTIME by
// StatusBadgeIcons.ResourceFor, so nothing else ever coerces their import
// settings the way SceneBuilder.LoadSprite does for a scene-referenced asset.
public class StatusIconImportPostprocessor : AssetPostprocessor
{
    private const string StatusIconFolder = "Assets/_Project/Resources/Status/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(StatusIconFolder))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePivot = new UnityEngine.Vector2(0.5f, 0.5f);
        importer.alphaIsTransparency = true;

        // Mips ON, Trilinear filtering -- same call as UiKitImportPostprocessor
        // and PortraitImportPostprocessor. These are 256px masters drawn at
        // 20px on party plates and 36px on enemy rows; without mips, GPU
        // minification samples the full-res texture bilinearly and aliases
        // into visible grain at that scale-down. mipmapEnabled=false had no
        // stated reason in this file's history -- it was set alongside
        // textureType/pivot/alphaIsTransparency as import boilerplate, never
        // called out on its own.
        importer.mipmapEnabled = true;
        importer.filterMode = UnityEngine.FilterMode.Trilinear;
    }
}
