using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Makes TextMeshPro usable in a freshly cloned project without anyone clicking
// anything.
//
// TMP needs two things that are normally created by hand: its Essential
// Resources (a .unitypackage shipped inside com.unity.ugui) and a TMP_FontAsset
// per typeface. "Someone imported it once" is not a reproducible project state
// -- a fresh clone, or a wiped Library, and the whole UI renders as nothing --
// so both are generated, from code, like scenes and content.
public static class TmpBootstrap
{
    private const string FontOutputDir = "Assets/_Project/Fonts";

    // Deliberately NOT under Resources/Content: ContentBuilder deletes that
    // whole tree with no prompt on every run, and a font asset vanishing
    // mid-build would be a genuinely baffling failure.
    private const string SourceFontDir = "Assets/_Project/Resources/Fonts";

    // A SEPARATE build step, run in its own Unity invocation before
    // SceneBuilder.
    //
    // It has to be separate: ImportPackage lands the settings asset on disk,
    // but TMP_Settings.instance is a cached Resources.Load that stays null for
    // the rest of THAT process. Building a scene in the same run therefore dies
    // inside TMP_FontAsset.CreateFontAsset with a bare NullReferenceException
    // that names nothing useful. Second process, and it is simply there.
    [MenuItem("Prince's Palace/Bootstrap TextMeshPro")]
    public static void Bootstrap()
    {
        EnsureEssentialResources();
        Debug.Log("BUILD-COMPLETE: TmpBootstrap");
    }

    // TMP's own settings asset, GENERATED rather than imported.
    //
    // The documented route is AssetDatabase.ImportPackage on the
    // "TMP Essential Resources.unitypackage" that ships inside com.unity.ugui.
    // That route does not work here and fails in the worst way: in batch mode
    // ImportPackage is ASYNCHRONOUS, so it logs success, -quit exits before the
    // import runs, and nothing lands on disk. The build then dies later inside
    // TMP_FontAsset.CreateFontAsset with a bare NullReferenceException, because
    // TMP_Settings.instance is a cached Resources.Load of an asset that was
    // never written.
    //
    // AssetDatabase.CreateAsset is synchronous, and a generated settings asset
    // is more in keeping with this project's first rule than a package someone
    // has to remember to import. Only the fields TMP actually reads during font
    // asset creation matter; everything else keeps its default, and every
    // TextMeshProUGUI this project emits is given an explicit font, so
    // defaultFontAsset is never consulted.
    private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string ShaderPath = "Assets/TextMesh Pro/Shaders/TMP_SDF.shader";

    public static void EnsureEssentialResources()
    {
        if (TMP_Settings.instance != null) return;

        if (File.Exists(ShaderPath) && File.Exists(SettingsPath))
        {
            // On disk but not visible to THIS process - TMP_Settings.instance is
            // a cached Resources.Load and the miss is cached with it. The next
            // Unity invocation picks it up, which is why the harness runs this
            // bootstrap as its own pass.
            Debug.Log("[TmpBootstrap] TMP essentials are on disk; they become visible next run.");
            return;
        }

        throw new System.Exception(
            "[TmpBootstrap] TextMeshPro's essential resources are missing, so no font asset can be built " +
            "(TMP_FontAsset.CreateFontAsset needs the TMP_SDF shader and dies with " +
            "'ArgumentNullException: Parameter name: shader' without it).\n" +
            "  Fix by running: python tools/extract_tmp_essentials.py\n" +
            "  That unpacks TMP Essential Resources synchronously. AssetDatabase.ImportPackage is deliberately " +
            "NOT used here - it is asynchronous in batch mode, so it reports success and writes nothing before " +
            "-quit exits.");
    }

    // Builds (once) a TMP_FontAsset next to the project's .ttf, and returns it.
    // Falls back to TMP's default rather than throwing, matching the project's
    // graceful-degradation-on-missing-art posture: a wrong-looking font is a
    // visible, fixable problem; a build that refuses to run is not.
    public static TMP_FontAsset EnsureFontAsset(string ttfFileName)
    {
        string assetPath = $"{FontOutputDir}/{Path.GetFileNameWithoutExtension(ttfFileName)} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        string ttfPath = $"{SourceFontDir}/{ttfFileName}";
        var source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (source == null)
        {
            Debug.LogWarning($"[TmpBootstrap] No font at {ttfPath} - falling back to the TMP default.");
            return TMP_Settings.defaultFontAsset;
        }

        Directory.CreateDirectory(FontOutputDir);
        var font = TMP_FontAsset.CreateFontAsset(source);
        if (font == null)
        {
            Debug.LogWarning($"[TmpBootstrap] CreateFontAsset failed for {ttfPath} - falling back to the TMP default.");
            return TMP_Settings.defaultFontAsset;
        }

        font.name = Path.GetFileNameWithoutExtension(ttfFileName) + " SDF";
        AssetDatabase.CreateAsset(font, assetPath);

        // The atlas texture and material are sub-assets; without this they are
        // silently dropped and the font renders as blank boxes.
        if (font.atlasTextures != null)
        {
            foreach (var tex in font.atlasTextures)
            {
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, font);
            }
        }
        if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);

        AssetDatabase.SaveAssets();
        Debug.Log($"[TmpBootstrap] Generated {assetPath}");
        return font;
    }
}
