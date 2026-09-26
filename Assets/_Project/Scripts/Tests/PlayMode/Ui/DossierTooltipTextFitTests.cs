using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // "Readable, roughly square item tooltips" (owner item, 2026-09-22): the
    // dossier's tooltip body moved off its old 280-wide, font-13 box onto
    // ItemComparisonPanel's shared shape -- a 420x420 panel whose body comes
    // out at 380 wide, font 15. UiTextFitAudit cannot see this move at all:
    // ItemComparisonPanel's own header says why (the body is
    // UiString.Runtime, catalogue content its "content-derived text is data,
    // not authored copy" rule explicitly excludes). This is the check that
    // closes that gap, the same way SystemMenuLabelWidthTests closes the
    // symmetrical one for tracked labels: a real TMP_Text, the real font, the
    // real wrap width, TMP's own measurement -- never a hand-rolled glyph
    // count.
    //
    // PlayMode, not EditMode, for the same reason ItemModifierTooltipTests
    // sits here: ContentDatabase.Items/.Characters need Resources to load
    // from, which tools/domain-tests' dotnet host cannot give them (its own
    // csproj header: "the split is read off the csproj", and Resources.Load
    // is one of the three named reasons a class stays Unity-only). Loading
    // the font straight off AssetDatabase by path, the way TypographyAssetTests
    // does, rather than through TmpBootstrap/SceneBuilder -- CODE_STANDARDS.md
    // 1's reference direction (PlayMode never points at Editor).
    //
    // WORST CASE, not a typical one: a Convergent (3-slot) candidate rolled
    // three DIFFERENT affixes from what is already worn (also Convergent), on
    // a WEAPON slot so WeaponDamageLine's own line is present too -- every
    // section ItemStatLines.Body can print, printing at once (stat bonuses,
    // scaling, requirement, the full VS.-EQUIPPED delta, the weapon DMG line,
    // three AFFIX gain lines and three "Losing:" lines). If this fits, every
    // real drop fits.
    public class DossierTooltipTextFitTests
    {
        private const string FontPath = "Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset";

        private static TMP_FontAsset LoadFont()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#else
            return null;
#endif
        }

        private static ItemDefinition EquippableInSlot(EquipmentSlot slot) =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.equipSlot == slot);

        private static ItemDefinition AnotherEquippableInSlot(EquipmentSlot slot, ItemDefinition notThis) =>
            ContentDatabase.Items.FirstOrDefault(i =>
                i != null && i.IsEquippable && i.equipSlot == slot && i.id != notThis.id);

        [Test]
        public void TheWorstCaseComparisonBody_FitsThePanelsBodyBoxAtTheNewFontSize()
        {
#if !UNITY_EDITOR
            Assert.Ignore("AssetDatabase is editor-only.");
#endif
            var candidate = EquippableInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(candidate, "fixture: content has a Weapon1-slot equippable");

            var worn = AnotherEquippableInSlot(EquipmentSlot.Weapon1, candidate);
            Assert.IsNotNull(worn, "fixture: content has a second, different Weapon1-slot equippable");

            var characterDef = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(characterDef, "fixture: content has at least one character");
            var character = new Character(characterDef.id);

            // Three affixes worn, three different ones rolled on the
            // candidate -- no overlap, so every one of the six prints its own
            // line (three gains, three "Losing:") rather than a shared affix
            // collapsing to a single neutral line.
            var wornModifiers = new List<string> { "emberguard", "permafrost", "moonveil" };
            character.equipment.Set(worn.equipSlot, worn.id, modifierIds: wornModifiers,
                riftTier: (int)RiftTier.Convergent);

            var candidateModifiers = new List<string> { "fiery", "frosty", "astral" };
            string body = ItemDescription.ComparisonBody(character, candidate, candidatePlus: 3,
                riftTier: RiftTier.Convergent, modifierIds: candidateModifiers);

            Assert.IsNotEmpty(body, "fixture: the worst-case body must not degrade to empty");

            // The panel's OWN arithmetic, not a re-derivation of Pad/
            // TitleToBodyGap here -- if ItemComparisonPanel's numbers move,
            // this test moves with them instead of silently checking stale
            // ones. Hex values are irrelevant to sizing, so placeholders.
            var built = ItemComparisonPanel.Build("DossierTooltipFitProbe", Place.At(0f, 0f),
                new UiVec(420f, 420f), titleHeight: 30f, titleFontSize: 20, titleHex: "#FFFFFF",
                bodyFontSize: 15, bodyHex: "#FFFFFF");

            float bodyWidth = built.Body.Size.X;
            float bodyHeight = built.Body.Size.Y;

            var font = LoadFont();
            Assert.IsNotNull(font, $"fixture: {FontPath} must exist -- SceneBuilder generates it at build time");

            // Under a Canvas: TextMeshProUGUI skips mesh generation without
            // one, and textInfo then comes back empty -- which would let
            // lineCount <= capacity pass on NOTHING, the way GradeLetterFaceTests
            // parents its probe.
            var go = new GameObject("DossierTooltipFitProbe", typeof(Canvas));
            try
            {
                var child = new GameObject("Text", typeof(RectTransform));
                child.transform.SetParent(go.transform, false);
                var text = child.AddComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSize = built.Body.FontSize;
                text.text = body;

                // Wide enough on Y that nothing here can clip the
                // MEASUREMENT itself -- what is under test is whether the
                // wrapped content fits bodyHeight, not whether TMP's own
                // overflow handling hides the evidence that it does not.
                var rect = (RectTransform)text.transform;
                rect.sizeDelta = new Vector2(bodyWidth, 4000f);
                text.ForceMeshUpdate();

                int lineCount = text.textInfo.lineCount;
                Assert.Greater(lineCount, 0,
                    "fixture: TMP produced no mesh -- the fit assertion below would pass on an empty mesh");
                float lineHeight = text.textInfo.lineInfo[0].lineHeight;
                int capacity = Mathf.FloorToInt(bodyHeight / lineHeight);

                var preferred = text.GetPreferredValues(body, bodyWidth, 0f);

                Assert.LessOrEqual(lineCount, capacity,
                    $"the worst-case body wraps to {lineCount} lines at {bodyWidth:F0}px wide, font " +
                    $"{text.fontSize:F0} -- the {bodyHeight:F0}px-tall body box holds only {capacity} " +
                    $"lines at {lineHeight:F1}px each ({preferred.y:F1}px measured, " +
                    $"{body.Split('\n').Length} authored line(s) before wrap).");
                Assert.LessOrEqual(preferred.y, bodyHeight + 1f,
                    $"the worst-case body measures {preferred.y:F1}px tall against a {bodyHeight:F0}px box.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
