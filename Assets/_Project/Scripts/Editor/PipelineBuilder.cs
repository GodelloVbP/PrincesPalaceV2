using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
// Bloom / Vignette / ColorAdjustments live in the Universal namespace above;
// VolumeProfile and Volume come from Rendering (the core RP package).

// Generates this project's URP assets, rather than having someone create them
// through Assets > Create and commit whatever the defaults happened to be.
//
// Same rule as scenes and content: the render pipeline is BUILT, not authored.
// A URP Asset is a ScriptableObject with about forty tunable fields, and the
// ones that matter here (HDR, MSAA, the shadow cascades a 2D game will never
// use) are exactly the sort of thing that silently drifts when it lives only
// as a binary asset nobody diffs.
//
// Renderer 2D specifically, not the default Universal Renderer: this game is
// entirely sprites and uGUI. The 2D renderer is what carries 2D lights and
// shadow casters if they are ever wanted, and it skips the deferred/forward
// 3D machinery that would otherwise sit in the frame doing nothing.
public static class PipelineBuilder
{
    private const string RenderingDir = "Assets/_Project/Rendering";
    private const string RendererPath = RenderingDir + "/Renderer2D.asset";
    private const string PipelinePath = RenderingDir + "/UrpPipeline.asset";
    public const string VolumeProfilePath = RenderingDir + "/MenuVolumeProfile.asset";

    [MenuItem("Prince's Palace/Build Render Pipeline")]
    public static void BuildRenderPipeline()
    {
        Directory.CreateDirectory(RenderingDir);

        // Idempotent, so the test harness can run it on every -BuildScenes
        // without churning the pipeline asset's GUID (which every quality level
        // and GraphicsSettings references) on each pass.
        if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) != null)
        {
            BuildVolumeProfile();
            BuildHitFlashMaterial();

            // Repaired on the EXISTING asset too, not only on a fresh build.
            // The defect below shipped into the committed Renderer2D, so a
            // project that already has one is exactly the case that needs it.
            EnsurePostProcessData(AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath));

            AssetDatabase.SaveAssets();
            Debug.Log("BUILD-COMPLETE: PipelineBuilder");
            return;
        }

        var rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
        EnsurePostProcessData(rendererData);
        AssetDatabase.CreateAsset(rendererData, RendererPath);

        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.name = "UrpPipeline";

        // A 2D game with no realtime lights has nothing to cast shadows and
        // nothing to receive them. Every one of these is pure frame cost here.
        pipeline.shadowCascadeCount = 1;
        pipeline.shadowDistance = 0f;
        pipeline.supportsCameraDepthTexture = false;
        pipeline.supportsCameraOpaqueTexture = false;

        // HDR is what makes Bloom behave like light rather than like a white
        // wash -- values above 1.0 survive to the post-processing pass instead
        // of clipping. It is the whole reason this migration buys anything
        // visually, so it is set explicitly rather than left to the default.
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 4;

        AssetDatabase.CreateAsset(pipeline, PipelinePath);
        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        // Every quality level, not just whichever one happens to be active in
        // this process -- a level left on Built-in renders the whole game
        // magenta the moment a player picks it.
        int previous = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(previous, applyExpensiveChanges: false);

        BuildVolumeProfile();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Render pipeline built: {PipelinePath} (Renderer2D, HDR on) and assigned to " +
                  $"GraphicsSettings + all {QualitySettings.names.Length} quality levels.");
        Debug.Log("BUILD-COMPLETE: PipelineBuilder");
    }

    // The post-processing stack, generated for the same reason the pipeline
    // asset is: a VolumeProfile is a binary asset full of tunable numbers, and
    // numbers nobody diffs are numbers that drift.
    //
    // This is the half of the URP migration that actually buys anything. URP
    // was installed and the canvases left in ScreenSpaceOverlay, which renders
    // AFTER post-processing -- so bloom could not touch a single pixel of the
    // game. SceneBuilder now puts the canvas in ScreenSpaceCamera and this
    // profile is what it renders through.
    private static void BuildVolumeProfile()
    {
        // The ASSET is reused (its GUID is referenced by every scene's
        // GlobalVolume) but its VALUES are re-applied from here every run. The
        // numbers live in code, so code has to win -- an early-return would mean
        // editing a threshold in this file and having nothing happen, which is
        // exactly the silent drift generating the asset was meant to prevent.
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        // Threshold above the flat colours the UI is drawn in, so the lantern
        // glows and the palace bloom while ordinary button faces and body text
        // do not. Text legibility is the real risk of putting UI through post-
        // processing in a text-dense game; a high threshold is the cheap
        // mitigation, and a separate Overlay canvas is the fallback if it is
        // not enough.
        var bloom = GetOrAdd<Bloom>(profile);

        // Threshold raised and intensity dropped from the first pass (0.90 /
        // 0.55). Bloom cannot tell near from far, so a low threshold blooms the
        // whole painted skyline as hard as the lantern by the player's feet and
        // the image flattens. Letting only genuinely bright pixels through, and
        // grading the glow alphas by depth in MainMenuAmbience, puts the
        // brightness where the distance is.
        bloom.threshold.Override(1.05f);

        // 0.285: a further 25% off 0.38, by eye on the real frame. The whole
        // sequence is worth keeping because it shows which knob did what --
        // 0.55 was too hot everywhere, 0.38 fixed the far field once the glow
        // alphas were depth-graded, and this last cut is taste rather than
        // correction.
        bloom.intensity.Override(0.285f);

        // Wider scatter, lower intensity: a soft halo around the near lights
        // rather than a hard bright rim on everything.
        bloom.scatter.Override(0.78f);
        
        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.55f);
        
        // Deliberately gentle. Tonemapping the UI is exactly what can wash out
        // a readable palette, so this warms and lifts rather than regrading.
        var colour = GetOrAdd<ColorAdjustments>(profile);
        colour.postExposure.Override(0.08f);
        colour.contrast.Override(6f);
        colour.saturation.Override(4f);
        
        // SetDirty on each COMPONENT, not just the profile.
        //
        // The components are sub-assets. Mutating one through TryGet and calling
        // SaveAssets is not enough - Unity has no idea it changed and writes
        // nothing, so the values here and the values on disk drift apart
        // silently. That is exactly what happened: the committed profile sat at
        // bloom 0.9/0.55/0.7 while this file said 1.05/0.285/0.78, and every
        // "re-tuned the bloom" run changed precisely nothing on disk.
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(colour);
        EditorUtility.SetDirty(profile);

        AssetDatabase.SaveAssets();
        Debug.Log($"Volume profile built: {VolumeProfilePath} " +
                  $"(bloom {bloom.intensity.value} @ {bloom.threshold.value} threshold, vignette, gentle grade)");
    }

    // Adding a second Bloom to a profile that already has one is legal and
    // silently wrong - the later override wins and the earlier one sits there
    // confusing anyone who opens the asset. Get-or-add keeps exactly one.
    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var existing)) return existing;
        var added = profile.Add<T>(overrides: true);
        AssetDatabase.AddObjectToAsset(added, profile);
        return added;
    }

    // The on-hit flash material, as a COMMITTED asset.
    //
    // v1 created this with `new Material(Resources.Load<Shader>(...))` inside
    // SceneBuilder, which meant a fresh material object got serialised into the
    // scene on every single build -- a new sub-asset, a new fileID, and a scene
    // diff that changed for no reason. One asset with one GUID, referenced by
    // both stages, is what the "generated, never hand-authored" rule actually
    // asks for.
    public const string HitFlashMaterialPath = "Assets/_Project/Resources/Materials/UIHitFlash.mat";

    private static void BuildHitFlashMaterial()
    {
        // LOADED BY PATH, not Shader.Find.
        //
        // Shader.Find only resolves shaders that are already loaded or listed in
        // Always Included Shaders, and in a batch-mode build neither is true --
        // so it returned null, no material was ever written, and the flash fell
        // back to the default UI shader. That shader MULTIPLIES its tint, so a
        // white flash on a dark sprite is the identity: the effect ran correctly
        // and was invisible.
        //
        // The shader file's own header says this ("Shader.Find would need it in
        // Always Included Shaders, which is a project-settings dependency this
        // avoids"). I ported that comment and then used Shader.Find anyway.
        const string shaderPath = "Assets/_Project/Resources/Shaders/UIHitFlash.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null)
        {
            Debug.LogWarning($"[PipelineBuilder] No shader at {shaderPath}; the hit flash will degrade to nothing.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(HitFlashMaterialPath));

        var existing = AssetDatabase.LoadAssetAtPath<Material>(HitFlashMaterialPath);
        if (existing != null)
        {
            // Re-pointed rather than recreated: replacing the asset would mint a
            // new GUID and orphan every reference the scenes already hold.
            existing.shader = shader;
            EditorUtility.SetDirty(existing);
            return;
        }

        AssetDatabase.CreateAsset(new Material(shader), HitFlashMaterialPath);
        AssetDatabase.SaveAssets();
    }

    // THE RENDERER NEEDS ITS PostProcessData OR POST-PROCESSING SILENTLY DOES
    // NOTHING. This is the single worst bug this project has had, because its
    // symptom is indistinguishable from success.
    //
    // ScriptableObject.CreateInstance<Renderer2DData>() leaves m_PostProcessData
    // null. Creating a renderer through Assets > Create does not, because that
    // path runs a factory which assigns the package's default. So the generated
    // renderer compiled, loaded, rendered, and quietly skipped bloom, vignette
    // and colour grading -- with no warning anywhere, because a renderer with no
    // post-processing shaders is a legitimate configuration.
    //
    // What that cost: every screenshot this project has judged was UNGRADED, the
    // Volume profile was tuned against renders that never applied it, and a
    // legibility measurement comparing volume-on to volume-off returned
    // byte-identical images -- which is how it was finally caught.
    //
    // Loaded through the stable `Packages/` virtual path rather than the hashed
    // PackageCache folder, which changes on every package update.
    private const string DefaultPostProcessDataPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

    private static void EnsurePostProcessData(Renderer2DData rendererData)
    {
        if (rendererData == null) return;

        var serialized = new SerializedObject(rendererData);
        var property = serialized.FindProperty("m_PostProcessData");
        if (property == null)
        {
            Debug.LogError("[PipelineBuilder] Renderer2DData has no m_PostProcessData field - URP's layout " +
                           "changed and post-processing is NOT running. Do not judge a screenshot until this is fixed.");
            return;
        }

        if (property.objectReferenceValue != null) return;

        var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
        if (data == null)
        {
            Debug.LogError($"[PipelineBuilder] No PostProcessData at {DefaultPostProcessDataPath} - bloom, " +
                           "vignette and grading will silently not run.");
            return;
        }

        property.objectReferenceValue = data;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        Debug.Log("[PipelineBuilder] Assigned the default PostProcessData - post-processing can now actually run.");
    }
}
