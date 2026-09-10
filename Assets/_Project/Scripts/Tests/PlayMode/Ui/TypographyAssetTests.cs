using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using TMPro;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // Engine-facing half of the typography role table: that the five
    // generated static SDF fonts and seven role materials
    // (TmpBootstrap.Typography.cs) actually exist on disk, carry the
    // required glyphs, and render with the briefed colours.
    //
    // Loaded straight off AssetDatabase by path, the same way
    // EnemyStanceCaptureTests reads a generated sheet -- not through
    // SceneBuilder.FontFor/MaterialFor, which live in the Editor assembly
    // and are not a reference PlayMode tests are allowed to take
    // (CODE_STANDARDS.md 1: reference direction only ever points toward
    // Domain, never toward Editor).
    public class TypographyAssetTests
    {
        private const string FontDir = "Assets/_Project/Fonts";
        private const string MaterialDir = "Assets/_Project/Fonts/Materials";

        // "ÀÉÑüß€£¥—–…"
        // == "AEEN with diacritics, u-umlaut, sharp s, euro, pound, yen,
        // em-dash, en-dash, ellipsis" -- the brief's own glyph sample.
        private const string RequiredGlyphs = "ÀÉÑüß€£¥—–…";

        private static readonly string[] FontAssetNames =
        {
            "Cinzel-SemiBold SDF",
            "SourceSans3-Regular SDF",
            "SourceSans3-SemiBold SDF",
            "SourceSans3-Bold SDF",
            "ChakraPetch-Medium SDF",
        };

        private static TMP_FontAsset LoadFont(string assetName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{assetName}.asset");
#else
            return null;
#endif
        }

        private static Material LoadMaterial(string roleName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{roleName}.mat");
#else
            return null;
#endif
        }

        [TestCaseSource(nameof(FontAssetNames))]
        public void FontAsset_ExistsAndIsStatic(string assetName)
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var font = LoadFont(assetName);
            Assert.IsNotNull(font, $"{assetName} was not found at {FontDir} -- run TmpBootstrap.GenerateTypographyAssets.");
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode,
                $"{assetName} should be static (committed once), not dynamic -- see AUDIT.md #47 for why that matters here.");
        }

        [TestCaseSource(nameof(FontAssetNames))]
        public void FontAsset_HasAsciiRange(string assetName)
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var font = LoadFont(assetName);
            Assert.IsNotNull(font);
            for (int c = 0x20; c <= 0x7E; c++)
            {
                Assert.IsTrue(font.HasCharacter((char)c, false, false),
                    $"{assetName} is missing ASCII 0x{c:X2} ('{(char)c}')");
            }
        }

        [TestCaseSource(nameof(FontAssetNames))]
        public void FontAsset_HasRequiredGlyphs(string assetName)
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var font = LoadFont(assetName);
            Assert.IsNotNull(font);
            foreach (char c in RequiredGlyphs)
            {
                Assert.IsTrue(font.HasCharacter(c, false, false),
                    $"{assetName} is missing required glyph U+{(int)c:X4} ('{c}')");
            }
        }

        private static object[] RoleMaterialCases =
        {
            new object[] { TypographyRole.CeremonialTitle, new Color32(0xD6, 0xA4, 0x51, 0xFF) },
            new object[] { TypographyRole.FunctionalHeading, new Color32(0xDD, 0xD5, 0xC4, 0xFF) },
            new object[] { TypographyRole.ButtonLabel, new Color32(0xF0, 0xD2, 0x9A, 0xFF) },
            new object[] { TypographyRole.Body, new Color32(0xDD, 0xD5, 0xC4, 0xFF) },
            new object[] { TypographyRole.TacticalData, new Color32(0xC6, 0x9A, 0xF1, 0xFF) },
            new object[] { TypographyRole.OnBarCaption, new Color32(0x08, 0x06, 0x10, 0xFF) },
            new object[] { TypographyRole.Alert, new Color32(0xE0, 0x6A, 0x61, 0xFF) },
        };

        [TestCaseSource(nameof(RoleMaterialCases))]
        public void Material_ExistsAndHasBriefedFaceColor(TypographyRole role, Color32 expectedFace)
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            string materialName = Typography.Specs[role].MaterialName;
            var mat = LoadMaterial(materialName);
            Assert.IsNotNull(mat, $"{materialName}.mat was not found at {MaterialDir} -- run TmpBootstrap.GenerateTypographyAssets.");

            var face = (Color32)mat.GetColor(ShaderUtilities.ID_FaceColor);
            Assert.AreEqual(expectedFace.r, face.r, 1, $"{materialName} face R");
            Assert.AreEqual(expectedFace.g, face.g, 1, $"{materialName} face G");
            Assert.AreEqual(expectedFace.b, face.b, 1, $"{materialName} face B");
        }

        // THE OUTLINE IS ACTUALLY SWITCHED ON, which is not what a width says.
        // TMP's shader branches on the OUTLINE_ON keyword, so _OutlineWidth
        // with no keyword is an authored value the renderer never reads. All
        // six older role materials have exactly that shape (TacticalData:
        // width 0.07, m_ValidKeywords holding UNDERLAY_ON alone), which is why
        // the PC plates spent two passes drawing their bar captions on a dark
        // chip instead of outlining them. This role exists to draw a number on
        // an arbitrary fill, so its edge is the whole product -- a width alone
        // would be a silent regression to the state that produced the chip.
        //
        // Literals, not values read back off the spec table: a test that
        // recomputed them from the same source it is checking would pass
        // however that source drifted (CLAUDE.md gotcha 5).
        [Test]
        public void OnBarCaptionMaterial_HasAnOutlineThatIsActuallyEnabled()
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var mat = LoadMaterial(Typography.Specs[TypographyRole.OnBarCaption].MaterialName);
            Assert.IsNotNull(mat, "OnBarCaption.mat was not found -- run TmpBootstrap.GenerateTypographyAssets.");

            Assert.IsTrue(mat.IsKeywordEnabled("OUTLINE_ON"),
                "OnBarCaption carries an outline width with no OUTLINE_ON keyword, which renders as no outline at all");
            Assert.AreEqual(0.25f, mat.GetFloat(ShaderUtilities.ID_OutlineWidth), 0.001f);
            Assert.AreEqual(0.16f, mat.GetFloat(ShaderUtilities.ID_FaceDilate), 0.001f);
        }

        // DARK FACE, LIGHT OUTLINE -- the opposite way round from every other
        // role, and the reason is arithmetic rather than taste. A light face
        // cannot clear WCAG AA on this screen's bar fills at any setting: pure
        // white measures 3.32:1 against HpBright and 2.58:1 against MpBright,
        // and the near-violet-white the captions used to carry only reaches
        // 2.87 and 2.23. A near-black face is 6.06 and 7.80 before
        // antialiasing takes its cut, and the light outline is what carries
        // the glyph over the near-black empty track once a bar drains.
        //
        // Pinned as a RELATION, not as two hex literals: which exact dark and
        // which exact light are a look decision the owner can retune, but a
        // preset whose face and outline drift toward each other has lost the
        // only property that lets one caption survive two backgrounds.
        [Test]
        public void OnBarCaptionMaterial_HasADarkFaceInsideALightOutline()
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var mat = LoadMaterial(Typography.Specs[TypographyRole.OnBarCaption].MaterialName);
            Assert.IsNotNull(mat);

            var face = (Color32)mat.GetColor(ShaderUtilities.ID_FaceColor);
            var outline = (Color32)mat.GetColor(ShaderUtilities.ID_OutlineColor);

            Assert.AreEqual(255, face.a, "a translucent face reads the fill through the glyph");
            Assert.AreEqual(255, outline.a,
                "a partially transparent outline is a partially transparent one-pixel ring at 14pt, which is no ring");

            Assert.LessOrEqual(Luminance(face), 0.05f,
                "the face has to be dark enough to separate the glyph from a bright authored fill");
            Assert.GreaterOrEqual(Luminance(outline), 0.70f,
                "the outline has to be light enough to carry the glyph over the near-black empty track");
        }

        // WCAG relative luminance. Spelled out here rather than imported
        // because nothing in Domain needs it -- this is the only assertion in
        // the tree that reasons about contrast.
        private static float Luminance(Color32 c)
        {
            float Channel(byte v)
            {
                float f = v / 255f;
                return f <= 0.03928f ? f / 12.92f : Mathf.Pow((f + 0.055f) / 1.055f, 2.4f);
            }

            return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
        }

        [TestCaseSource(nameof(RoleMaterialCases))]
        public void Material_ReferencesItsFontsAtlasTexture(TypographyRole role, Color32 _)
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var spec = Typography.Specs[role];
            var font = LoadFont(spec.FontAssetName);
            var mat = LoadMaterial(spec.MaterialName);
            Assert.IsNotNull(font);
            Assert.IsNotNull(mat);

            Assert.AreEqual(font.atlasTexture, mat.GetTexture(ShaderUtilities.ID_MainTex),
                $"{spec.MaterialName} must render with {spec.FontAssetName}'s own atlas texture, or glyphs come out blank/wrong.");
            Assert.AreEqual(font.material.GetFloat(ShaderUtilities.ID_GradientScale), mat.GetFloat(ShaderUtilities.ID_GradientScale),
                $"{spec.MaterialName}'s GradientScale must match {spec.FontAssetName}'s, or the SDF renders at the wrong softness.");
        }
    }
}
