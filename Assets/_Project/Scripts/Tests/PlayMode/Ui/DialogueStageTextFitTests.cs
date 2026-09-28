using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // THE STAGE'S TEXT AT ITS CONTENT CAPS, MEASURED IN THE REAL LABELS
    // (docs/PLAN_DIALOGUE_STAGE.md D4). UiTextFitAudit measures authored copy
    // at scene build and skips content text, so a dialogue line and an
    // epithet -- both content -- are measured here instead, with the SAME
    // measurement (UiTextFit): the emitted label's font, its authored size
    // with auto-size off, the style the stage draws it in.
    //
    // At 4:3 and 21:9. The box and its text are fixed-size nodes, so the
    // solver gives them the same canvas-unit rect at every audit frame; each
    // frame is checked to still give the rect the emitted label has, which
    // is what makes one TMP measurement valid at both.
    public class DialogueStageTextFitTests
    {
        // Contract 13's worst case: exactly MaxLineLength characters of
        // real prose (demo_dialogue's Odette line).
        private const string LongestLine =
            "Hold still, both of you. That lantern burns from the far side of the wall, so somebody in the next " +
            "reality lit it for us. Whoever they are, they are expecting company, and they are not a patient host.";

        // CharacterEntryResolver.MaxEpithetLength characters.
        private const string LongestEpithet = "Keeper of the Ninth Lantern Oath";

        // A result beat is the result plus an effects line under it
        // (EventController.PaintBeat), so its worst case is a line-cap result
        // AND an effects line at its budget (EventEffectSummary.MaxLength).
        private static string LongEffects => UiStrings.EventEffects.AuditSample;

        private EventController _panel;

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
        }

        private IEnumerator LoadTheMap()
        {
            yield return SharedScene.Ensure("Map");
            _panel = Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_panel, "the Map scene has no EventController");
        }

        private TMP_Text Label(string name)
        {
            var go = _panel.GetComponentsInChildren<Transform>(includeInactive: true).FirstOrDefault(t => t.name == name);
            Assert.IsNotNull(go, $"the event panel has no '{name}'");
            return go.GetComponent<TMP_Text>();
        }

        private static readonly (string Name, UiVec Frame)[] Frames =
        {
            ("4:3", UiFrames.FourThree),
            ("21:9", UiFrames.UltraWide),
        };

        // The solved rect of `name` at every checked frame is the emitted one.
        private static void AssertSameBoxAtEveryFrame(string name, TMP_Text label)
        {
            var box = UiTextFit.Box(label);
            foreach (var (frameName, frame) in Frames)
            {
                var solved = UiSolver.Solve(EventScreen.Build().Root, frame).Descendants().First(n => n.Name == name);
                Assert.AreEqual(box.x, solved.Rect.Width, 0.5f, $"{name} is a different width at {frameName}");
                Assert.AreEqual(box.y, solved.Rect.Height, 0.5f, $"{name} is a different height at {frameName}");
            }
        }

        private static void AssertFits(TMP_Text label, string sample, int fontSize, FontStyles style, string what)
        {
            bool autoSize = label.enableAutoSizing;
            float size = label.fontSize;
            var previousStyle = label.fontStyle;
            try
            {
                label.enableAutoSizing = false;
                label.fontSize = fontSize;
                label.fontStyle = style;

                bool fits = UiTextFit.Fits(label, sample, out var preferred);
                var box = UiTextFit.Box(label);
                Assert.IsTrue(fits,
                    $"{what}: needs {preferred.x:F0}x{preferred.y:F0} at font {fontSize} ({style}); " +
                    $"the box is {box.x:F0}x{box.y:F0}");
            }
            finally
            {
                label.enableAutoSizing = autoSize;
                label.fontSize = size;
                label.fontStyle = previousStyle;
            }
        }

        [UnityTest]
        public IEnumerator TheLongestLine_FitsTheDialogueBox_AtFourThreeAndUltraWide()
        {
            yield return LoadTheMap();
            Assert.AreEqual(EventEntryResolver.MaxLineLength, LongestLine.Length, "fixture: the sample is at the cap");
            Assert.AreEqual(PrincesPalace.Domain.Events.EventEffectSummary.MaxLength, LongEffects.Length,
                "fixture: the effects sample is at the budget");

            var line = Label("StageLineText");
            Assert.AreEqual(EventScreen.LineWidth, UiTextFit.Box(line).x, 0.5f);
            Assert.AreEqual(EventScreen.LineHeight, UiTextFit.Box(line).y, 0.5f);
            AssertSameBoxAtEveryFrame("StageLineText", line);

            AssertFits(line, LongestLine, EventScreen.LineFontSize, FontStyles.Normal, "a speaker's line");
            AssertFits(line, LongestLine, EventScreen.LineFontSize, FontStyles.Italic, "a narration line");

            // The result beat as PaintBeat builds it: italic by tag, then the
            // effects line in gold under it.
            string resultBeat = "<i>" + LongestLine + "</i>\n<color=" + FightHudPalette.GoldText + ">" + LongEffects + "</color>";
            AssertFits(line, resultBeat, EventScreen.LineFontSize, FontStyles.Normal, "a result with its effects line");
        }

        [UnityTest]
        public IEnumerator TheLongestEpithet_FitsTheNamePlate_AtFourThreeAndUltraWide()
        {
            yield return LoadTheMap();
            Assert.AreEqual(CharacterEntryResolver.MaxEpithetLength, LongestEpithet.Length, "fixture: the sample is at the cap");

            var epithet = Label("StageEpithet");
            AssertSameBoxAtEveryFrame("StageEpithet", epithet);
            AssertFits(epithet, LongestEpithet, EventScreen.EpithetFontSize, FontStyles.Normal, "the epithet");
        }
    }
}
