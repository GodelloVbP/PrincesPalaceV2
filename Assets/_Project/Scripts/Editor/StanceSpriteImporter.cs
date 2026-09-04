using UnityEditor;

// Forces every battle-stance and spell-VFX PNG under Resources to import
// as a Sprite.
//
// This exists because the failure it prevents is completely silent.
// FightController loads stance art with Resources.Load<Sprite>, which
// returns NULL — not an error, not a warning — for a texture imported with
// Unity's default textureType (plain Texture2D rather than Sprite). The
// fight then runs perfectly: RefreshCombatantSprite falls back to the plain
// plate, every test passes, and the only symptom is that a character's art
// simply isn't there.
//
// That is exactly what happened when Shawn's sliced stances first landed:
// they imported as textureType 0 while the earlier enemy sheets happened to
// be sitting at textureType 8, so the party stage showed a fallback plate
// and nothing anywhere reported a problem. Caught only because
// PartyStageTests asserts on the loaded sprite's NAME rather than merely
// that some sprite exists.
//
// A postprocessor rather than a one-off fix to those six files: the next
// character or monster sheet dropped into Resources would hit the identical
// trap, and "remember to set the texture type" is not a rule anyone should
// have to keep.
//
// It then happened AGAIN, to the folder this did not cover. Spell VFX load
// through the same Resources.Load<Sprite> (SpellVfxPlayer.Frames), so the
// trap is identical, but Resources/Spells/ was not in the list: the golem's
// Boulder Slam shipped in c869823 with all six frames at textureType 0, its
// frame probe broke on the very first null, and the enemy's signature attack
// played no effect at all from then until 2026-08-03. Nothing reported it.
// Spells/ is covered here now for the same reason the other two are.
public class StanceSpriteImporter : AssetPostprocessor
{
    private const string EnemyStanceRoot = "/Resources/Enemies/";
    private const string CharacterStanceRoot = "/Resources/Characters/";
    private const string SpellVfxRoot = "/Resources/Spells/";

    // The house's own contact effects (slash arc, impact burst), which are not
    // a spell and so were not covered by the root above. Same
    // Resources.Load<Sprite> door via FrameSequenceLoader, therefore the same
    // silent trap: a texture imported as a plain Texture2D loads as null and
    // the swing simply plays no effect, with nothing anywhere reporting it.
    private const string ContactVfxRoot = "/Resources/Vfx/";

    private void OnPreprocessTexture()
    {
        if (!IsStanceSprite(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // These sheets are cut-out figures on transparency; without this the
        // edges pick up a halo where Unity premultiplies against black.
        importer.alphaIsTransparency = true;
        // Read/write enabled so FightController.StageVisuals can sample the
        // sprite's actual opaque bounds at runtime (ContentCenterOffsetFraction)
        // — delivered stance art doesn't always keep its figure centered in
        // its own canvas (Elite Bog Witch's idle frame in particular), and
        // the foot shadow needs the real content center, not the canvas
        // center, to sit under the figure rather than to one side of it.
        importer.isReadable = true;
    }

    private static bool IsStanceSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Contains(EnemyStanceRoot)
            || path.Contains(CharacterStanceRoot)
            || path.Contains(SpellVfxRoot)
            || path.Contains(ContactVfxRoot);
    }

    // OnPreprocessTexture only fires on an actual (re)import — editing this
    // postprocessor does not retroactively touch assets Unity already
    // imported under the old settings. Called once via -executeMethod to
    // force every existing stance sprite through readable-texture import;
    // any FUTURE drop into these folders gets it automatically from the
    // postprocessor above with no manual step needed.
    public static void ForceReimportAll()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[]
        {
            "Assets/_Project/Resources/Enemies",
            "Assets/_Project/Resources/Characters",
            "Assets/_Project/Resources/Spells",
            "Assets/_Project/Resources/Vfx",
        });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsStanceSprite(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
