using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // QA 2026-09-26: ChakraPetch draws D/0, B/8 and S/5 as one glyph each, so
    // "INT-D" read as "INT-0". ScalingGrades.Display wraps every grade letter
    // in a <font> tag and GradeFontResolver answers it. The EditMode tests pin
    // the STRING; this pins what TMP actually DOES with it, on the two
    // surfaces the defect was reported on -- the dossier's attribute line and
    // an item tooltip -- because an unresolved <font> tag is not an error in
    // TMP, it is printed as literal text.
    //
    // The real UI font, loaded by path the way DossierTooltipTextFitTests
    // does, so the letter is measured against the face it has to stand apart
    // from.
    public class GradeLetterFaceTests
    {
        private const string UiFontPath = "Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset";

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            GradeFontResolver.Register();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private TMP_Text Render(string richText)
        {
#if UNITY_EDITOR
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontPath);
#else
            TMP_FontAsset font = null;
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            Assert.IsNotNull(font, $"fixture: {UiFontPath} must exist");

            // Under a Canvas: TextMeshProUGUI skips mesh generation without
            // one, and textInfo then comes back empty -- which would make
            // every assertion below agree with nothing.
            _go = new GameObject("GradeLetterFaceProbe", typeof(Canvas));
            var child = new GameObject("Text", typeof(RectTransform));
            child.transform.SetParent(_go.transform, false);
            var text = child.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 18;
            ((RectTransform)text.transform).sizeDelta = new Vector2(2000f, 400f);
            text.text = richText;
            text.ForceMeshUpdate();
            return text;
        }

        // Every visible character: the grade letters in the grade face,
        // everything else in the UI font, and no tag text leaking through.
        private static void AssertGradeLettersSwapFace(TMP_Text text, string expectedVisible, int expectedGradeCount)
        {
            var info = text.textInfo;
            string rendered = new string(Enumerable.Range(0, info.characterCount)
                .Select(i => info.characterInfo[i].character).ToArray());
            Assert.AreEqual(expectedVisible, rendered,
                "the <font> tag must be consumed, not printed -- an unresolved tag renders as literal text");

            int gradeFace = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (c.fontAsset != null && c.fontAsset.name == ScalingGrades.DisplayFontName)
                {
                    gradeFace++;
                    StringAssert.Contains(c.character.ToString(), "SABCDE",
                        $"only a grade letter may be in the grade face; got '{c.character}' at {i}");
                }
            }

            Assert.AreEqual(expectedGradeCount, gradeFace,
                $"expected {expectedGradeCount} grade letter(s) in {ScalingGrades.DisplayFontName} in \"{rendered}\"");
        }

        [Test]
        public void DossierIntelligenceLine_DrawsTheDInTheGradeFace()
        {
            var scores = new AbilityScoreBlock(strength: 12, dexterity: 10, constitution: 14, wisdom: 12, intelligence: 8, charisma: 10);
            var skillScaling = new ScalingSet(
                ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.D),
                ScalingProfile.None,
                ScalingProfile.None);

            var text = Render(AbilityEffectDescriptions.Intelligence(scores, skillScaling));

            AssertGradeLettersSwapFace(text, "spell scaling: INT-D (x0.97)", 1);
        }

        [Test]
        public void ItemTooltip_ScalingLine_DrawsEveryGradeLetterInTheGradeFace()
        {
            var item = ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable
                && i.scaling.Describe().Length > 0);
            Assert.IsNotNull(item, "fixture: content has an equippable item that scales on something");

            int grades = AbilityScores.All.Count(s => item.scaling[s] != ScalingGrade.None)
                       + AbilityScores.All.Count(s => item.spellScaling[s] != ScalingGrade.None);

            var text = Render(ItemDescription.CardSummary(item));

            var info = text.textInfo;
            string rendered = new string(Enumerable.Range(0, info.characterCount)
                .Select(i => info.characterInfo[i].character).ToArray());
            StringAssert.DoesNotContain("font=", rendered, "the <font> tag leaked into the tooltip as text");
            StringAssert.Contains("SCALES", rendered);

            int gradeFace = Enumerable.Range(0, info.characterCount)
                .Count(i => info.characterInfo[i].fontAsset != null
                         && info.characterInfo[i].fontAsset.name == ScalingGrades.DisplayFontName);
            Assert.AreEqual(grades, gradeFace,
                $"{item.id}: every grade letter on \"{rendered}\" must be in {ScalingGrades.DisplayFontName}");
        }
    }
}
