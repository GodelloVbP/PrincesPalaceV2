using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// Static-weight SDF font assets and per-role TMP material presets, generated
// from script rather than the Font Asset Creator window -- same reasoning as
// the rest of this file: "someone clicked it once" is not a reproducible
// project state.
//
// Static rather than dynamic (unlike the existing ChakraPetch-Regular SDF,
// see AUDIT.md #47) on purpose: these five are meant to be committed once and
// stop moving. A dynamic atlas rewrites its own glyph table as a side effect
// of runtime text rendering, which is exactly the git noise #47 describes --
// worth accepting for one working font, not worth multiplying by five.
//
// Real weight files rather than TMP's synthetic bold/italic slant: a shape
// class actually drawn at that weight, not a faked outline-thickened glyph.
public static partial class TmpBootstrap
{
    // Private to this topic -- see CODE_STANDARDS.md 4 on partial-class rules.
    // Font/material output paths and atlas settings are shared with the rest
    // of TmpBootstrap and live in the root file as TypographyMaterialDir /
    // TypographyAtlasSize / TypographyAtlasPadding / TypographyRenderMode /
    // LiberationSansSdfPath (FontOutputDir and SourceFontDir are the same
    // ones the existing ChakraPetch-Regular path already uses -- these five
    // just group by family subfolder under SourceFontDir).
    private struct FontSpec
    {
        public string AssetName;
        public string TtfRelativePath;
        public int StartSamplingSize;
    }

    private struct MaterialSpec
    {
        public string RoleName;
        public string FontAssetName;
        public Color FaceColor;
        public float FaceDilate;
        public Color OutlineColor;
        public float OutlineWidth;
        public bool UnderlayOn;
        public Color UnderlayColor;
        public float UnderlayOffsetX;
        public float UnderlayOffsetY;
        public float UnderlaySoftness;
    }

    [MenuItem("Prince's Palace/Generate Typography Fonts")]
    public static void GenerateTypographyAssets()
    {
        TmpBootstrap.EnsureEssentialResources();
        Directory.CreateDirectory(FontOutputDir);
        Directory.CreateDirectory(TypographyMaterialDir);

        var liberationFallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansSdfPath);
        if (liberationFallback == null)
        {
            Debug.LogWarning($"[TmpBootstrap.Typography] No fallback font at {LiberationSansSdfPath} -- generated fonts will have no fallback.");
        }

        string characterSet = BuildCharacterSet();

        var specs = new[]
        {
            new FontSpec { AssetName = "Cinzel-SemiBold SDF", TtfRelativePath = "Cinzel/Cinzel-SemiBold.ttf", StartSamplingSize = 90 },
            new FontSpec { AssetName = "SourceSans3-Regular SDF", TtfRelativePath = "SourceSans3/SourceSans3-Regular.ttf", StartSamplingSize = 90 },
            new FontSpec { AssetName = "SourceSans3-SemiBold SDF", TtfRelativePath = "SourceSans3/SourceSans3-SemiBold.ttf", StartSamplingSize = 90 },
            new FontSpec { AssetName = "SourceSans3-Bold SDF", TtfRelativePath = "SourceSans3/SourceSans3-Bold.ttf", StartSamplingSize = 90 },
            new FontSpec { AssetName = "ChakraPetch-Medium SDF", TtfRelativePath = "ChakraPetch/ChakraPetch-Medium.ttf", StartSamplingSize = 90 },
        };

        var generated = new Dictionary<string, TMP_FontAsset>();
        foreach (var spec in specs)
        {
            var font = GenerateStaticFontAsset(spec, characterSet, liberationFallback);
            if (font != null) generated[spec.AssetName] = font;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var materialSpecs = BuildMaterialSpecs();
        foreach (var matSpec in materialSpecs)
        {
            if (!generated.TryGetValue(matSpec.FontAssetName, out var font) || font == null)
            {
                Debug.LogWarning($"[TmpBootstrap.Typography] Skipping material '{matSpec.RoleName}' -- its font '{matSpec.FontAssetName}' was not generated.");
                continue;
            }
            GenerateMaterialPreset(matSpec, font);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("BUILD-COMPLETE: TmpBootstrap.Typography");
    }

    // ASCII, Latin-1 Supplement, General Punctuation (U+2000-206F) and
    // Currency Symbols (U+20A0-20CF) -- the range this project's UI actually
    // needs (accented names, curly quotes/en-dash/em-dash/ellipsis, currency
    // glyphs for any future economy UI), not the full Unicode block.
    //
    // A handful of code points in those ranges are format/control characters
    // with NO visual glyph by definition (soft hyphen, zero-width
    // space/joiners, line/paragraph separators, bidi marks) -- no typeface
    // can rasterise them, so TryAddCharacters reports them missing at every
    // sampling size down to the floor. The first version of this method did
    // not exclude them and every one of the five fonts failed to generate at
    // all, because a permanent miss looks identical to an atlas-overflow miss
    // to the shrink loop below. Excluded here instead of chasing that at the
    // loop.
    private static readonly System.Collections.Generic.HashSet<int> NonRenderableCodePoints =
        new System.Collections.Generic.HashSet<int>
        {
            0xAD, // soft hyphen
            0x2028, 0x2029, // line/paragraph separator
            0x200B, 0x200C, 0x200D, 0x200E, 0x200F, // zero-width space/joiners, LTR/RTL marks
            0x2060, 0x2061, 0x2062, 0x2063, 0x2064, // word joiner, invisible operators
            0x2066, 0x2067, 0x2068, 0x2069, 0x206A, 0x206B, 0x206C, 0x206D, 0x206E, 0x206F, // bidi isolates/format
        };

    private static string BuildCharacterSet()
    {
        var sb = new StringBuilder();
        void AddRange(int from, int to)
        {
            for (int c = from; c <= to; c++)
            {
                if (!NonRenderableCodePoints.Contains(c)) sb.Append((char)c);
            }
        }
        AddRange(0x20, 0x7E);
        AddRange(0xA0, 0xFF);
        AddRange(0x2000, 0x206F);
        AddRange(0x20A0, 0x20CF);
        return sb.ToString();
    }

    // Starts at the spec's sampling size and, ONLY if the miss count shrinks
    // as the size drops, keeps stepping down -- that shape (fewer misses at a
    // smaller size) is what atlas overflow looks like. What was actually
    // measured generating these five (a constant miss count -- e.g. 120/328
    // for Cinzel-SemiBold -- at every size from 90pt down to the 30pt floor)
    // is the OTHER shape: the typeface genuinely has no glyph for those code
    // points (mostly the Currency Symbols block, which no single typeface
    // covers in full, and some General Punctuation marks). Shrinking cannot
    // fix that, so the loop stops at the first attempt whose miss count does
    // not improve on the one before it, keeps that font, and logs exactly
    // which characters it lacks rather than discarding a working font over
    // gaps no sampling size would have closed.
    private static TMP_FontAsset GenerateStaticFontAsset(FontSpec spec, string characterSet, TMP_FontAsset fallback)
    {
        string ttfPath = $"{SourceFontDir}/{spec.TtfRelativePath}";
        var source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (source == null)
        {
            Debug.LogWarning($"[TmpBootstrap.Typography] No font at {ttfPath} -- skipping {spec.AssetName}.");
            return null;
        }

        string assetPath = $"{FontOutputDir}/{spec.AssetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        TMP_FontAsset font = null;
        string missingCharacters = string.Empty;
        int trySize = spec.StartSamplingSize;
        int acceptedSize = trySize;
        int previousMissCount = int.MaxValue;
        const int floorSize = 30;

        while (trySize >= floorSize)
        {
            var candidate = TMP_FontAsset.CreateFontAsset(
                source, trySize, TypographyAtlasPadding, TypographyRenderMode,
                TypographyAtlasSize, TypographyAtlasSize, AtlasPopulationMode.Dynamic);
            if (candidate == null)
            {
                Debug.LogWarning($"[TmpBootstrap.Typography] CreateFontAsset failed for {ttfPath}.");
                return null;
            }

            candidate.TryAddCharacters(characterSet, out string missingAtThisSize);
            int missCount = missingAtThisSize?.Length ?? 0;

            if (missCount >= previousMissCount)
            {
                // No improvement over the previous (larger) size -- these are
                // typeface gaps, not atlas overflow. Keep the PREVIOUS
                // candidate (already held in `font`, at `acceptedSize`) and
                // stop.
                Object.DestroyImmediate(candidate);
                break;
            }

            if (font != null) Object.DestroyImmediate(font);
            font = candidate;
            acceptedSize = trySize;
            missingCharacters = missingAtThisSize;
            previousMissCount = missCount;

            if (missCount == 0) break;
            trySize -= 10;
        }

        if (font == null)
        {
            Debug.LogWarning($"[TmpBootstrap.Typography] Could not generate {spec.AssetName} at any sampling size down to {floorSize}pt.");
            return null;
        }

        if (acceptedSize != spec.StartSamplingSize)
        {
            Debug.LogWarning($"[TmpBootstrap.Typography] {spec.AssetName}: lowered sampling point size from {spec.StartSamplingSize} to {acceptedSize}pt (fewer atlas misses there).");
        }
        if (!string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning($"[TmpBootstrap.Typography] {spec.AssetName}: {missingCharacters.Length}/{characterSet.Length} requested characters have no glyph in this typeface (kept at {acceptedSize}pt anyway): " +
                string.Join(" ", System.Linq.Enumerable.Select(missingCharacters, c => $"U+{(int)c:X4}")));
        }

        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.name = spec.AssetName;

        if (fallback != null)
        {
            font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        }

        AssetDatabase.CreateAsset(font, assetPath);
        if (font.atlasTextures != null)
        {
            foreach (var tex in font.atlasTextures)
            {
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, font);
            }
        }
        if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);

        EditorUtility.SetDirty(font);
        Debug.Log($"[TmpBootstrap.Typography] Generated {assetPath} at {acceptedSize}pt.");
        return font;
    }

    // Values not called out by the typography brief (Body's outline alpha;
    // TacticalData's and Alert's underlay offset/softness) default to the
    // same shape the specified roles share -- a y -0.25 offset and 0.08
    // softness -- rather than TMP's own defaults, so the unbriefed roles
    // still sit in the same visual family as the briefed ones.
    private static List<MaterialSpec> BuildMaterialSpecs()
    {
        return new List<MaterialSpec>
        {
            new MaterialSpec
            {
                RoleName = "CeremonialTitle",
                FontAssetName = "Cinzel-SemiBold SDF",
                FaceColor = HexColor("D6A451", 0xFF),
                FaceDilate = 0.05f,
                OutlineColor = HexColor("080708", 0xE6),
                OutlineWidth = 0.08f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0xB3), // 70%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.35f,
                UnderlaySoftness = 0.10f,
            },
            new MaterialSpec
            {
                RoleName = "FunctionalHeading",
                FontAssetName = "SourceSans3-SemiBold SDF",
                FaceColor = HexColor("DDD5C4", 0xFF),
                FaceDilate = 0.04f,
                OutlineColor = HexColor("080708", 0xCC),
                OutlineWidth = 0.06f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0x4D), // 30%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.25f,
                UnderlaySoftness = 0.08f,
            },
            new MaterialSpec
            {
                RoleName = "ButtonLabel",
                FontAssetName = "SourceSans3-Bold SDF",
                FaceColor = HexColor("F0D29A", 0xFF),
                FaceDilate = 0.06f,
                OutlineColor = HexColor("080708", 0xE6),
                OutlineWidth = 0.10f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0xB3), // 70%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.30f,
                UnderlaySoftness = 0.08f,
            },
            new MaterialSpec
            {
                RoleName = "Body",
                FontAssetName = "SourceSans3-Regular SDF",
                FaceColor = HexColor("DDD5C4", 0xFF),
                FaceDilate = 0f,
                OutlineColor = HexColor("080708", 0x99), // unspecified; kept subtle for the thin 0.02 width
                OutlineWidth = 0.02f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0x33), // 20%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.15f,
                UnderlaySoftness = 0.08f,
            },
            new MaterialSpec
            {
                RoleName = "TacticalData",
                FontAssetName = "ChakraPetch-Medium SDF",
                FaceColor = HexColor("C69AF1", 0xFF),
                FaceDilate = 0.03f,
                OutlineColor = HexColor("080708", 0xCC),
                OutlineWidth = 0.07f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0x40), // 25%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.25f,
                UnderlaySoftness = 0.08f,
            },
            // A NUMBER DRAWN ON A COLOURED FILL, which is a different problem
            // from every role above it: those all sit on a plate or a panel
            // whose colour the kit chose, so a face colour can be picked to
            // contrast with it. This one sits on whatever hex a pools.json row
            // authored -- and on the near-black empty track behind it once the
            // bar drains, so the SAME glyph has to survive two backgrounds
            // that are not related to each other.
            //
            // DARK FACE, LIGHT OUTLINE, which is the opposite of every other
            // role here and is arithmetic rather than taste. Measured off the
            // party_formation capture: a light violet-white face against
            // HpBright tops out at 2.87:1 and against MpBright at 2.23:1 --
            // and PURE WHITE only reaches 3.32 and 2.58. No material setting
            // puts a light caption over 4.5:1 on these fills, which is why
            // the first two passes of this column reached for a dark chip
            // under the text instead and ended up drawing a full HP bar as
            // half drained. A near-black face is 6.06:1 on HpBright and
            // 7.80:1 on MpBright before antialiasing takes its cut.
            //
            // 0.16 AND 0.25 ARE A MEASURED BALANCE POINT, not defaults. Face
            // dilate and outline width trade against each other at 14pt,
            // where a stroke is barely a pixel and neither the face nor the
            // ring can be solid at once -- five settings were captured and
            // measured, and they see-saw: 0.15/0.22 read 4.93:1 on the fills
            // and 2.31:1 on the empty track, 0.08/0.34 read 2.89 and 5.03.
            // 0.16/0.25 is the setting that clears 4.5:1 on all four fill
            // samples (4.72-6.00) with the best empty-track number available
            // at that point, 2.76:1.
            //
            // THE EMPTY TRACK IS THE SIDE THAT PAYS, and it is the right side
            // to: Ui.Meter drains from the RIGHT, so the LEFT end this
            // caption is pinned to is filled for every value above roughly
            // 40%. A caption meets bare track only on a badly hurt bar or on
            // Bjorn's fury, which opens each fight empty -- where the glyph
            // renders as a hollow light outline. Readable, visibly weaker,
            // and not fixable from this table; a lighter FightHudPalette.
            // Track would fix it and is the owner's call.
            //
            // NO UNDERLAY. A drop shadow is directional and this glyph has no
            // fixed background to drop onto; at 14pt the softest useful
            // setting is a smear. Declared off rather than left at alpha 0,
            // which would be dead machinery reading as a decision.
            new MaterialSpec
            {
                RoleName = "OnBarCaption",
                FontAssetName = "ChakraPetch-Medium SDF",
                FaceColor = HexColor("080610", 0xFF),
                FaceDilate = 0.16f,
                OutlineColor = HexColor("F4EBFF", 0xFF),
                OutlineWidth = 0.25f,
                UnderlayOn = false,
                UnderlayColor = HexColor("000000", 0x00),
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = 0f,
                UnderlaySoftness = 0f,
            },
            new MaterialSpec
            {
                RoleName = "Alert",
                FontAssetName = "SourceSans3-Bold SDF",
                FaceColor = HexColor("E06A61", 0xFF),
                FaceDilate = 0.05f,
                OutlineColor = HexColor("100606", 0xE6),
                OutlineWidth = 0.09f,
                UnderlayOn = true,
                UnderlayColor = HexColor("000000", 0x4D), // 30%
                UnderlayOffsetX = 0f,
                UnderlayOffsetY = -0.25f,
                UnderlaySoftness = 0.08f,
            },
        };
    }

    private static Color HexColor(string hex, byte alpha)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        c.a = alpha / 255f;
        return c;
    }

    // Instantiates a COPY of the font's own default material rather than
    // editing it in place -- CreateFontAsset's material is the font asset's
    // own default and other UI may reference it directly; a role preset must
    // never be the thing that changes what "the font's default material"
    // renders as. The copy inherits _MainTex, _GradientScale and the
    // texture-width/height properties for free, which is what keeps a preset
    // from rendering wrong if the font's atlas size or padding ever changes.
    private static void GenerateMaterialPreset(MaterialSpec spec, TMP_FontAsset font)
    {
        string matPath = $"{TypographyMaterialDir}/{spec.RoleName}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) return;

        var mat = new Material(font.material);
        mat.name = spec.RoleName;

        mat.SetColor(ShaderUtilities.ID_FaceColor, spec.FaceColor);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, spec.FaceDilate);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, spec.OutlineColor);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, spec.OutlineWidth);

        // OUTLINE_ON, AND IT WAS THE MISSING HALF. Setting _OutlineWidth and
        // _OutlineColor does nothing on its own -- TMP's shader branches on
        // the keyword, so a width with no keyword is an authored value the
        // renderer never reads. Every one of the six materials this method
        // wrote before today has that shape (TacticalData: width 0.07,
        // m_ValidKeywords holding UNDERLAY_ON alone), which is why the PC
        // plates ended up drawing their bar captions on an opaque chip
        // instead of outlining them -- see FightScreen's PcValueGround, now
        // gone, and its own note saying so.
        //
        // The six already on disk are NOT rewritten by fixing this: the
        // method returns early for a material that exists, so the change
        // reaches new presets only. That is deliberate rather than shy --
        // switching an outline on across every title, heading, button and
        // body label in the game is a look change on every screen and the
        // owner's call, and those six .mat files are the owner's own
        // uncommitted work besides. Filed rather than done.
        if (spec.OutlineWidth > 0f) mat.EnableKeyword("OUTLINE_ON");

        if (spec.UnderlayOn)
        {
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, spec.UnderlayColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, spec.UnderlayOffsetX);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, spec.UnderlayOffsetY);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, spec.UnderlaySoftness);
        }

        AssetDatabase.CreateAsset(mat, matPath);
        EditorUtility.SetDirty(mat);
        Debug.Log($"[TmpBootstrap.Typography] Generated {matPath}.");
    }
}
