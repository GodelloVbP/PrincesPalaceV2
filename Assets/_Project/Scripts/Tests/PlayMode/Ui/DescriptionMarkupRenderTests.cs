using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // What TMP actually DOES with a description's tokens once UiText.SetContent
    // has expanded them. The EditMode tests pin the STRING; an unresolved
    // <font> or <sprite> tag is not an error in TMP, it is printed as literal
    // text, so this measures the laid-out characters on a label with the
    // project's real UI font.
    public class DescriptionMarkupRenderTests
    {
        private const string UiFontPath = "Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset";
        private const float FontSize = 18f;

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            TmpAssetResolver.Register();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private TMP_Text NewLabel(float width = 400f, float height = 60f)
        {
#if UNITY_EDITOR
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontPath);
#else
            TMP_FontAsset font = null;
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            Assert.IsNotNull(font, $"fixture: {UiFontPath} must exist");

            // Under a Canvas: TextMeshProUGUI skips mesh generation without
            // one, and textInfo then comes back empty.
            _go = new GameObject("DescriptionMarkupProbe", typeof(Canvas));
            var child = new GameObject("Text", typeof(RectTransform));
            child.transform.SetParent(_go.transform, false);
            var text = child.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = FontSize;
            text.enableAutoSizing = false;
            ((RectTransform)text.transform).sizeDelta = new Vector2(width, height);
            return text;
        }

        // The text characters TMP laid out, sprites excluded.
        private static string Glyphs(TMP_Text text)
        {
            var info = text.textInfo;
            var sb = new StringBuilder();
            for (int i = 0; i < info.characterCount; i++)
            {
                if (info.characterInfo[i].elementType != TMP_TextElementType.Sprite) sb.Append(info.characterInfo[i].character);
            }

            return sb.ToString();
        }

        private static int SpriteCount(TMP_Text text) =>
            Enumerable.Range(0, text.textInfo.characterCount)
                .Count(i => text.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite);

        [Test]
        public void TheShippedIconAsset_HoldsEveryRegistryIcon()
        {
            Assert.IsNotNull(TmpAssetResolver.IconSprites(),
                "Resources/" + TmpAssetResolver.IconSpriteAssetPath + " is missing or lacks a registry icon; run Prince's Palace/Generate Description Icons");
            Assert.IsNotNull(TmpAssetResolver.Font(DescriptionMarkup.BoldFontName));
        }

        [Test]
        public void SetContent_BoldAndIcon_RendersABoldRunAndOneSpriteWithNoRawTokens()
        {
            var text = NewLabel();
            text.SetContent("Deals **Vulnerable** {fire}");
            text.ForceMeshUpdate();

            Assert.AreEqual("Deals Vulnerable ", Glyphs(text), "no '<', '**' or '{' may reach the screen");
            Assert.AreEqual(1, SpriteCount(text));

            var info = text.textInfo;
            int bold = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (c.elementType == TMP_TextElementType.Sprite) continue;

                bool inBold = i >= 6 && i < 16; // "Deals " is 6 characters, "Vulnerable" 10
                bool isBoldFace = c.fontAsset != null && c.fontAsset.name == DescriptionMarkup.BoldFontName;
                Assert.AreEqual(inBold, isBoldFace, $"character {i} '{c.character}' bold-face mismatch");
                if (isBoldFace) bold++;
            }

            Assert.AreEqual(10, bold, "the bold run is exactly \"Vulnerable\"");
        }

        [Test]
        public void TheIcon_SitsOnTheBaselineAtRoughlyCapHeight()
        {
            var text = NewLabel();
            text.SetContent("Fire {fire}");
            text.ForceMeshUpdate();

            var info = text.textInfo;
            int at = Enumerable.Range(0, info.characterCount).First(i => info.characterInfo[i].elementType == TMP_TextElementType.Sprite);
            var sprite = info.characterInfo[at];
            var capital = info.characterInfo[0];

            float height = sprite.topRight.y - sprite.bottomLeft.y;
            float width = sprite.topRight.x - sprite.bottomLeft.x;
            Debug.Log($"[DescriptionMarkupRenderTests] icon {width:F2}x{height:F2}px at size {FontSize}: bottom {sprite.bottomLeft.y - sprite.baseLine:F2} above baseline; capital 'F' top {capital.topRight.y - capital.baseLine:F2}");

            Assert.AreEqual(width, height, 0.1f, "the icon keeps its square aspect");
            Assert.AreEqual(0f, sprite.bottomLeft.y - sprite.baseLine, 0.6f, "the icon's bottom edge is the text baseline");
            Assert.AreEqual(capital.topRight.y - capital.baseLine, height, FontSize * 0.1f, "the icon is about as tall as a capital letter");
        }

        [Test]
        public void SetContent_TextWithBoldAndIcon_FitsItsBox()
        {
            var text = NewLabel(width: 300f, height: 40f);
            string rendered = DescriptionRenderer.Render("Deals **Vulnerable** {fire}");

            Assert.IsTrue(UiTextFit.Fits(text, rendered, out var preferred),
                $"\"{rendered}\" measures {preferred} in a {UiTextFit.Box(text)} box");
        }

        // Two runtime fonts on one label: TMP's material cache throws when
        // both carry the same material name, so each is named from its tag.
        [Test]
        public void TheBoldFaceAndTheGradeFace_CanShareALabel()
        {
            var text = NewLabel();
            text.SetContent("**Bold** INT-" + PrincesPalace.Domain.Stats.ScalingGrades.Display(PrincesPalace.Domain.Stats.ScalingGrade.D));
            Assert.DoesNotThrow(() => text.ForceMeshUpdate());

            var faces = Enumerable.Range(0, text.textInfo.characterCount)
                .Select(i => text.textInfo.characterInfo[i].fontAsset.name).Distinct().ToArray();
            CollectionAssert.Contains(faces, DescriptionMarkup.BoldFontName);
            CollectionAssert.Contains(faces, PrincesPalace.Domain.Stats.ScalingGrades.DisplayFontName);
        }

        [Test]
        public void SetContent_TextWithoutTokens_ReachesTheLabelUnchanged()
        {
            var text = NewLabel();
            const string dialogue = "<i>Baa,</i> she said {0} 100% *sigh*";
            text.SetContent(dialogue);

            Assert.AreEqual(dialogue, text.text);
        }

        [Test]
        public void WithNoSpriteAssetOrBoldFace_TheTokensDegradeToPlainWords()
        {
            var text = NewLabel();
            text.text = DescriptionRenderer.Render("Deals **Vulnerable** {fire}", DescriptionRenderCaps.Plain);
            text.ForceMeshUpdate();

            Assert.AreEqual("Deals Vulnerable Fire", Glyphs(text));
            Assert.AreEqual(0, SpriteCount(text));
            Assert.IsFalse(Enumerable.Range(0, text.textInfo.characterCount)
                .Any(i => text.textInfo.characterInfo[i].fontAsset != null
                          && text.textInfo.characterInfo[i].fontAsset.name == DescriptionMarkup.BoldFontName));
        }
    }
}
