using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using TMPro;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // Engine-facing half of the typography role table: that the five
    // generated static SDF fonts and six role materials
    // (TmpBootstrap.Typography.cs) actually exist on disk, carry the
    // required glyphs, and render with the briefed colours.
    //
    // Loaded straight off AssetDatabase by path, the same way
    // RigRawSpriteTests reads a generated atlas -- not through
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
