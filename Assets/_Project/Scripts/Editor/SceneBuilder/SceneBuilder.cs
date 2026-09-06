using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

// Scene generation, v2.
//
// Rule 1 is unchanged from v1: scenes are GENERATED, never opened in the Editor
// and saved. What changed is everything underneath -- this file no longer knows
// how to lay anything out. It creates the scene, the camera and the canvas,
// then hands each screen's declared tree to UiEmitter and gets out of the way.
//
// v1's SceneBuilder was 7,608 lines across 14 partial files, and every one of
// its four documented failure modes came from layout arithmetic living at
// construction sites. There are no construction sites here.
public static partial class SceneBuilder
{
    public const string ScenesDir = "Assets/_Project/Scenes";

    // READ FROM UiFrames, not restated. UiAudit re-solves every screen at
    // UiFrames.Reference; this is what the canvas actually scales to. Two
    // literals meaning "the frame everything is authored against" is two
    // chances to be audited at a size the game does not render at.
    private static readonly Vector2 ReferenceResolution =
        new Vector2(UiFrames.Reference.X, UiFrames.Reference.Y);
    private const string ArtRoot = "Assets/_Project/Art";

    private static TMP_FontAsset _uiFont;
    private static Sprite _buttonSprite;
    private static bool _buttonSpriteLoaded;

    // SceneBuilder.Typography.cs: role lookups over TmpBootstrap.Typography.cs's
    // generated assets. Kept here per the partial-class convention (root file
    // owns fields/consts, parts own methods) -- see CODE_STANDARDS.md 4.
    private const string TypographyFontDir = "Assets/_Project/Fonts";
    private const string TypographyMaterialDir = "Assets/_Project/Fonts/Materials";
    private static readonly Dictionary<TypographyRole, TMP_FontAsset> _typographyFonts =
        new Dictionary<TypographyRole, TMP_FontAsset>();
    private static readonly Dictionary<TypographyRole, Material> _typographyMaterials =
        new Dictionary<TypographyRole, Material>();

    public static TMP_FontAsset UiFont
    {
        get
        {
            if (_uiFont == null) _uiFont = TmpBootstrap.EnsureFontAsset("ChakraPetch-Regular.ttf");
            return _uiFont;
        }
    }

    [MenuItem("Prince's Palace/Build All Scenes")]
    public static void BuildAllScenes()
    {
        // Checked, not attempted. Importing TMP's resources here would succeed
        // on disk and still leave TMP_Settings.instance null for the rest of
        // this process, so the first font asset built would die inside TMP with
        // a bare NullReferenceException. Failing loudly with the fix is worth
        // more than a stack trace pointing at someone else's code.
        if (TMP_Settings.instance == null)
        {
            throw new System.Exception(
                "[SceneBuilder] FAILED: TextMeshPro's essential resources are not imported, so no font asset can be " +
                "built. Run 'Prince's Palace/Bootstrap TextMeshPro' first (the test harness does this automatically " +
                "as a separate step before -BuildScenes).");
        }

        _uiFont = null;
        _buttonSpriteLoaded = false;
        _spriteCache.Clear();

        Directory.CreateDirectory(ScenesDir);

        foreach (var group in ScreenRegistry.All.GroupBy(s => s.ScenePath))
        {
            BuildScene(group.Key, group.ToList());
        }

        AssetDatabase.SaveAssets();

        // The sentinel run_tests_parallel.ps1's gate requires. Reachable only
        // past every throw above: a build that dies partway leaves the PREVIOUS
        // scenes on disk, where nothing looks wrong and the suite tests them
        // happily. See that gate's own comment.
        Debug.Log("BUILD-COMPLETE: SceneBuilder");
    }

    private static void BuildScene(string scenePath, List<ScreenDef> screens)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = CreateMainCamera();
        CreateGlobalVolume();
        var canvas = CreateCanvas(camera);
        CreateEventSystem();

        foreach (var screen in screens)
        {
            var tree = screen.BuildTree();
            var result = UiEmitter.Emit(tree, canvas.transform);

            screen.Wire?.Invoke(result);

            // E1, E4, E3 and F9: measured text, declared-vs-bound counts,
            // non-null wiring, and each reference pointing at the node it was
            // supposed to. All run per screen, so a failure names the screen
            // rather than the build.
            //
            // The binding audit runs LAST of the four, after the sweep, so a
            // field nobody filled is reported as null once by the sweep rather
            // than twice -- as null there and as "not from this screen" here.
            UiTextFitAudit.Run(screen.PanelName, tree, result);
            UiCountAudit.Run(screen.PanelName, screen.CountBindings?.Invoke());
            UiWiringSweep.Run(screen.PanelName, result);
            UiBindingAudit.Run(screen.PanelName, result);
        }

        EditorSceneManager.SaveScene(scene, scenePath);
        EnsureInBuildSettings(scenePath);
        Debug.Log($"[SceneBuilder] built {scenePath} with {screens.Count} screen(s)");
    }

    private static Camera CreateMainCamera()
    {
        var cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGO.tag = "MainCamera";
        var camera = cameraGO.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        camera.orthographic = true;

        // 1 world unit = 1 canvas unit. CanvasScaler already makes a
        // canvas-local unit resolution-independent (that is its whole job);
        // pinning orthographicSize to the same reference resolution -- a
        // fixed value, not a per-frame resize sync -- means anything ever
        // placed in world space agrees with the canvas about what a unit is
        // instead of inheriting Unity's arbitrary default 5.
        camera.orthographicSize = ReferenceResolution.y / 2f;

        // The whole point of the URP move. Without this the Volume exists and
        // does nothing.
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        return camera;
    }

    // A global Volume: no collider, no blending, applies everywhere. The
    // profile itself is generated by PipelineBuilder.
    private static void CreateGlobalVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PipelineBuilder.VolumeProfilePath);
        if (profile == null)
        {
            // Degrades rather than throws, matching the missing-art posture: a
            // scene without bloom is plainly worse but perfectly playable, and
            // the warning says exactly what to run.
            Debug.LogWarning($"[SceneBuilder] No volume profile at {PipelineBuilder.VolumeProfilePath} - " +
                             $"building without post-processing. Run 'Prince's Palace/Build Render Pipeline'.");
            return;
        }

        var volumeGO = new GameObject("GlobalVolume", typeof(Volume));
        var volume = volumeGO.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = profile;
    }

    private static Canvas CreateCanvas(Camera camera)
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();

        // ScreenSpaceCamera, NOT Overlay. An Overlay canvas is composited AFTER
        // post-processing, so bloom, vignette and grading cannot touch one
        // pixel of it -- which is the state this project was in with URP
        // installed and nothing to show for it. Camera space puts the UI inside
        // the post stack.
        //
        // The cost, stated: the UI now passes through tonemapping and grading
        // too. In a text-dense game that can hurt legibility, which is why the
        // bloom threshold sits at 0.90 and the grade is deliberately gentle. If
        // it ever is not enough, the fallback is a second Overlay canvas for
        // text-critical HUD, accepting that it sits outside the effects.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;

        // Expand (scale = min(w/1920, h/1080)) guarantees the whole authored
        // frame is always on screen and uniformly scaled. The default matches
        // WIDTH only, which is what let v1's canvas grow to 1200 units tall at
        // 16:10 and desynchronised stretched art from fixed-offset text.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void EnsureInBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---- resources the emitter needs ---------------------------------------

    public static Color ParseHex(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        // Magenta on a malformed token rather than silently black, so a typo
        // shows up in the first screenshot instead of reading as "very dark".
        return ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.magenta;
    }

    public static Sprite ButtonSprite()
    {
        if (!_buttonSpriteLoaded)
        {
            _buttonSprite = LoadSpriteByKey("UI/button_cropped.png");
            _buttonSpriteLoaded = true;
        }
        return _buttonSprite;
    }

    // SpriteKey is a path relative to Art/. Returns null with a warning when the
    // file is not there yet, matching the project's graceful-degradation posture
    // - a blank slot is a visible, fixable problem; a build that refuses to run
    // over one missing PNG is not.
    // Per-build, cleared by BuildAllScenes. Static because LoadSpriteByKey is,
    // and the build is a single synchronous pass.
    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    public static Sprite LoadSpriteByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        // "proc:" names a shape ProceduralSpriteBaker committed as a real PNG,
        // rather than one generated at runtime and accidentally serialised into
        // the scene. See that baker's header for what that cost.
        string assetPath =
            key.StartsWith("proc:") ? $"{ProceduralSpriteBaker.GeneratedDir}/{key.Substring(5)}.png"
            : key.StartsWith("Assets/") ? key
            : $"{ArtRoot}/{key}";
        // Distinct keys are a small fraction of the ~6100 calls -- the same
        // button, panel and frame art is referenced by nearly every screen --
        // so the second and later asks are answered without touching the asset
        // database at all. Nulls are cached too, so a missing file warns once
        // rather than once per reference.
        if (_spriteCache.TryGetValue(assetPath, out var cached)) return cached;

        if (!File.Exists(assetPath))
        {
            Debug.LogWarning($"[SceneBuilder] Art asset not found at '{assetPath}' - leaving this slot blank.");
            _spriteCache[assetPath] = null;
            return null;
        }

        // ONLY when Unity has never seen this file.
        //
        // ForceSynchronousImport was unconditional here, and it was the single
        // most expensive thing the whole build did. Every call opens and closes
        // an asset-import scope, and CLOSING one runs a full asset pipeline
        // refresh whether or not anything actually needed importing --
        // StopAssetImportingV2 in the editor log. This method is called about
        // 6100 times per build (every image node in every screen, plus every
        // icon array over the whole item, relic and talent tables), so
        // that was 6100 refresh cycles at roughly 50ms each: 320 of the build's
        // 343 seconds, spent re-importing multi-megabyte PNGs that had not
        // changed. Long enough that the author was killing the build rather
        // than waiting for it, which is how it was found.
        //
        // An asset already in the database needs none of it -- LoadAssetAtPath
        // reads it straight out. An unknown path means a file that was dropped
        // in without the editor noticing, and that one genuinely does need a
        // synchronous import before it can be loaded.
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        // A PNG Unity has never seen imports as a plain Texture, and
        // LoadAssetAtPath<Sprite> on a plain Texture silently returns null. In
        // v1 that shipped a spell playing nothing at all for weeks.
        //
        // Kept exactly as it was: this is the check that has to stay honest.
        // It costs a reimport only on the FIRST build after new art lands,
        // because the flipped setting is then written into the .meta and
        // synced back to main -- which is what gotcha #3 in CLAUDE.md is about.
        // MIPMAPS ARE PART OF THE SAME CHECK, and for the same class of reason.
        //
        // Every painted asset in this game is authored far larger than it is
        // drawn -- the talent icons are 240-350px square and land in 64px
        // slots, the reward track's card art in 86. Without a mip chain the
        // sampler takes four texels out of every twenty-five and drops the
        // rest, which is the graininess that shows on every downscaled icon in
        // the game. It is not an anti-aliasing setting on the canvas and no
        // amount of MSAA reaches it: the detail is discarded during sampling,
        // before anything could be smoothed.
        //
        // Trilinear because so much of this UI scales while it is looked at --
        // a hovered node, a growing ring, a card swapping in. Bilinear switches
        // mip level in one frame and the switch is visible as a pop.
        //
        // COMPRESSION IS LEFT ALONE HERE, unlike in the procedural baker, and
        // the difference is size. The baked shapes are 128px gradients where a
        // block codec bands visibly and uncompressed costs kilobytes; the
        // painted art is up to 2048 square, where uncompressed would cost 16MB
        // apiece and the artefacts are far less visible in painted texture than
        // in a flat ramp.
        // THE BAKED SPRITES ARE NOT THIS METHOD'S TO SET. ProceduralSpriteBaker
        // decides their import settings, including which of them must NOT have
        // mipmaps -- the ones stretched into bars, where a mip level chosen off
        // the squashed axis takes the whole gradient with it.
        //
        // Without this line the two seams fight and the later one wins: the
        // baker turned mipmaps off for rail_ramp and hairline_fade, and this
        // method turned them straight back on a few seconds later, in the same
        // build. Found by reading the .meta files afterwards rather than by
        // anything failing, which is the argument for looking.
        bool generated = assetPath.StartsWith(ProceduralSpriteBaker.GeneratedDir);

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null
            && (importer.textureType != TextureImporterType.Sprite
                || (!generated
                    && (!importer.mipmapEnabled
                        || importer.filterMode != FilterMode.Trilinear))))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;

            if (!generated)
            {
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
            }

            importer.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        _spriteCache[assetPath] = sprite;
        return sprite;
    }
}
