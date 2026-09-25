using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // THE DIALOGUE STAGE'S NAMED CAPTURES (docs/PLAN_DIALOGUE_STAGE.md D4):
    // demo_dialogue as events.json ships it, played through the real panel
    // with Submit presses, photographed at every state the plan names and at
    // every UiFrames aspect. A capture rather than an assertion: what can be
    // wrong here is a bust cut edge mid-screen or a plate over the box, which
    // no value check sees.
    //
    // EACH SHOT HOLDS THE CANVAS AT ITS ASPECT FOR A FEW FRAMES FIRST
    // (StageCaptureRig.FullFrame), so the FocusMarker -- placed in LateUpdate
    // against the canvas rect -- is placed for that aspect, then renders
    // through CanvasCapture so post-processing is what ships.
    //
    // Graphics device only:
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter DialogueStageCaptureTests
    // PNGs land in tools/screenshots/runtime/dialogue_stage/, which the next
    // -Runtime run wipes; a set worth keeping is copied to docs/captures/.
    public class DialogueStageCaptureTests
    {
        private const string DemoId = "demo_dialogue";
        private const string LegacyId = "demo_wishing_well";

        // CharacterEntryResolver.MaxEpithetLength characters, the sample
        // DialogueStageTextFitTests measures.
        private const string LongestEpithet = "Keeper of the Ninth Lantern Oath";

        // A folder with nothing in it: every expression and the neutral
        // fallback miss, so the line plays with a plate and no bust.
        private const string NoBustFolder = "Portraits/Dialogue/__capture_missing__";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "dialogue_stage"));

        private static readonly (string Name, UiVec Frame)[] Aspects =
        {
            ("16x9", UiFrames.Reference),
            ("21x9", UiFrames.UltraWide),
            ("4x3", UiFrames.FourThree),
            ("16x10", UiFrames.SixteenTen),
        };

        private string _root;
        private ScriptedBaseInput _input;
        private MapController _map;
        private EventController _panel;
        private SystemMenuController _menu;
        private Canvas _canvas;
        private Action _undoOverride;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dialogue-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            // The character rows are the loaded Resources assets themselves,
            // which ContentDatabase.Reset does not reload from disk: an
            // override left in place would ride into every later test.
            _undoOverride?.Invoke();
            _undoOverride = null;

            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel != null && _panel.IsOpen) _panel.gameObject.SetActive(false);
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the captures -------------------------------------------------------------

        // a, b, c, d, e, f, g in one playthrough of demo_dialogue as shipped.
        [UnityTest]
        public IEnumerator CaptureTheDemoDialogue()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DialogueStageCaptureTests");

            yield return OpenTheEvent(DemoId);
            Assert.IsTrue(SaveSlotManager.CurrentSave.ActiveSquadIds().Contains("bear"),
                "fixture: Bjorn must be in the squad for the bearer branch");

            // c: the opening narration.
            yield return CompleteLineContaining("A lantern sways");
            Assert.IsNull(ActiveBust(), "narration has no bust");
            yield return Shoot("c_narration");

            // a: Shawn, left, happy.
            yield return CompleteLineContaining("Another reality, another landing");
            AssertBust("happy", mirrored: false);
            yield return Shoot("a_shawn_left");

            // b: Odette, right (mirrored), the 200-character line.
            yield return CompleteLineContaining("Hold still, both of you");
            Assert.AreEqual(EventEntryResolver.MaxLineLength, DialogueText.VisibleLength(LineLabel.text),
                "fixture: Odette's line is the 200-character cap");
            AssertBust("neutral", mirrored: true);
            yield return Shoot("b_odette_right_200");

            // e: the rows -- locked Buy, hidden Ask (stays hidden), Follow, Leave.
            yield return Submit();
            Assert.IsTrue(RowsShown, "the press after the last line opens the rows");
            yield return WaitReal(0.3f);
            var rows = ActiveRows();
            Assert.IsTrue(rows.Any(r => !r.interactable), "fixture: one row is locked");
            Assert.IsFalse(rows.Any(r => RowText(r).Contains("Ask the lantern")), "the hidden row stays hidden");

            // The shot is half about the marker's margin from the screen edge.
            var marker = NavigationInputModule.Marker;
            Assert.IsTrue(marker != null && marker.IsShown, "the focus marker is not drawn on the rows");
            yield return Shoot("e_choices");

            // f: the pick's result, with its effects line.
            yield return Submit(); // Follow the light, the focused row
            Assert.AreEqual("bearer", RunManager.Run.eventPageId, "Bjorn in the squad takes the bearer outcome");
            yield return CompleteLineContaining("The lantern leads you");
            StringAssert.Contains("gold", LineLabel.text, "the effects line under the result");
            yield return Shoot("f_result");

            // d: Bjorn asks for happy, has only neutral.
            yield return CompleteLineContaining("lamp oil");
            AssertBust("neutral", mirrored: false);
            yield return Shoot("d_bjorn_fallback");

            // g: through farewell to the concluded stage.
            yield return AdvanceToChoices();
            yield return Submit(); // Press on
            Assert.AreEqual("farewell", RunManager.Run.eventPageId);
            yield return AdvanceToChoices();
            yield return Submit(); // Leave, with a result
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded, "fixture: farewell's Leave concludes");
            yield return CompleteLineContaining("You leave with the lantern");
            yield return Submit();
            Assert.IsTrue(RowsShown, "the concluded result then offers Leave");
            yield return WaitReal(0.3f);
            Assert.AreEqual(1, ActiveRows().Length, "the concluded stage offers Leave alone");
            yield return Shoot("g_concluded");
        }

        // h and the epithet: the character rows overridden for this test
        // only -- Odette's bust folder pointed at nothing, Shawn given the
        // longest epithet the resolver allows.
        [UnityTest]
        public IEnumerator CaptureTheMissingBustAndTheLongestEpithet()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DialogueStageCaptureTests");

            Override("sheep", c => c.Epithet, (c, v) => c.Epithet = v, LongestEpithet);
            Override("owl", c => c.DialogueBustPath, (c, v) => c.DialogueBustPath = v, NoBustFolder);

            yield return OpenTheEvent(DemoId);

            yield return CompleteLineContaining("Another reality, another landing");
            Assert.IsTrue(Find("StageEpithet").activeInHierarchy, "the epithet override reached the plate");
            yield return Shoot("epithet_longest");

            yield return CompleteLineContaining("Hold still, both of you");
            Assert.IsNull(ActiveBust(), "the missing folder leaves no bust");

            // Framed as narration is (centred box), but the name stays.
            Assert.AreEqual(0.5f, Find("StageBox").GetComponent<RectTransform>().anchorMin.x,
                "a speaker with no bust gets the centred box");
            Assert.IsTrue(Find("StageNamePlate").activeInHierarchy, "contract 15: the name still shows");
            yield return Shoot("h_missing_bust");
        }

        // i: the legacy layout for comparison, 16:9 only.
        [UnityTest]
        public IEnumerator CaptureTheLegacyPage()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DialogueStageCaptureTests");

            yield return OpenTheEvent(LegacyId);
            Assert.IsFalse(Find("DialogueStage").activeInHierarchy, "a line-less event paints the legacy layout");
            yield return WaitReal(0.3f);
            yield return ShootAt("i_legacy_wishing_well", Aspects[0].Name, Aspects[0].Frame);
        }

        // ---- driving ------------------------------------------------------------------

        private IEnumerator OpenTheEvent(string eventId)
        {
            RunManager.StartRun(11UL);
            RunManager.Run.gold = 100;
            SaveSlotManager.SaveCurrent();

            yield return SharedScene.Ensure("Map");

            var module = UnityEngine.Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the map scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            var stale = module.GetComponent<ScriptedBaseInput>();
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _map = UnityEngine.Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = UnityEngine.Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            _menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            Assert.IsNotNull(_panel, "the Map scene has no EventController");
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _map.Refresh();
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(eventId), $"'{eventId}' did not open");
            _panel.Finished = _map.Refresh;
            _panel.Open();
            yield return null;

            _canvas = _panel.GetComponentInParent<Canvas>(true).rootCanvas;
            Assert.IsNotNull(_canvas, "the event panel has no root canvas");
        }

        private void Override(string characterId, Func<ResolvedCharacter, string> get,
            Action<ResolvedCharacter, string> set, string value)
        {
            var character = ContentDatabase.GetCharacter(characterId)?.Data;
            Assert.IsNotNull(character, $"fixture: no '{characterId}' in content");
            string before = get(character);
            set(character, value);
            var previous = _undoOverride;
            _undoOverride = () => { set(character, before); previous?.Invoke(); };
        }

        private GameObject Find(string name) =>
            _panel.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private TMP_Text LineLabel => Find("StageLineText").GetComponent<TMP_Text>();

        private bool LineIsFull => LineLabel.maxVisibleCharacters >= DialogueText.VisibleLength(LineLabel.text);

        private Button[] ActiveRows() =>
            Enumerable.Range(0, EventScreen.ChoiceRowCount)
                .Select(i => Find($"EventChoice{i}")?.GetComponent<Button>())
                .Where(b => b != null && b.gameObject.activeInHierarchy)
                .ToArray();

        private bool RowsShown => ActiveRows().Length > 0;

        private static string RowText(Button row) => row.transform.Find(row.name + "Text").GetComponent<TMP_Text>().text;

        private Image ActiveBust() =>
            new[] { "StageBustA", "StageBustB" }
                .Select(n => Find(n)?.GetComponent<Image>())
                .SingleOrDefault(i => i != null && i.gameObject.activeInHierarchy);

        private void AssertBust(string spriteName, bool mirrored)
        {
            var bust = ActiveBust();
            Assert.IsNotNull(bust, $"expected a '{spriteName}' bust on screen");
            Assert.AreEqual(spriteName, bust.sprite.name, "the bust drawn");
            Assert.AreEqual(mirrored, bust.rectTransform.localScale.x < 0f, "the bust's mirror");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private static IEnumerator WaitReal(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        // Presses until the stage shows a line containing `fragment`, lets its
        // slide finish, then one more press completes the reveal if the
        // typewriter has not. No waits beyond the slide.
        private IEnumerator CompleteLineContaining(string fragment)
        {
            for (int presses = 0; presses < 30 && !LineLabel.text.Contains(fragment); presses++)
            {
                yield return Submit();
                if (LineLabel.text.Contains(fragment)) break;
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            StringAssert.Contains(fragment, LineLabel.text, "never reached the line");
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            if (!LineIsFull) yield return Submit();
            Assert.IsTrue(LineIsFull, "the line is fully revealed");
            Assert.IsTrue(LineLabel.text.Contains(fragment), "the completing press moved past the line");
        }

        private IEnumerator AdvanceToChoices()
        {
            for (int presses = 0; presses < 30 && !RowsShown; presses++)
            {
                yield return Submit();
                if (RowsShown) yield break;
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            Assert.IsTrue(RowsShown, "the rows never appeared");
        }

        // ---- shooting -----------------------------------------------------------------

        private IEnumerator Shoot(string state)
        {
            foreach (var (name, frame) in Aspects) yield return ShootAt(state, name, frame);
        }

        // Three frames with the canvas held at the aspect (layout, then the
        // marker's LateUpdate against the new rect), then the real render.
        private IEnumerator ShootAt(string state, string aspectName, UiVec frame)
        {
            int width = (int)frame.X;
            int height = (int)frame.Y;

            var rig = StageCaptureRig.FullFrame(_canvas, width, height);
            try
            {
                yield return null;
                yield return null;
                yield return null;
            }
            finally
            {
                rig.Restore();
            }

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, $"dialogue_{state}_{aspectName}.png");
            CanvasCapture.RenderToFile(_canvas, path, width, height);
            FileAssert.Exists(path);

            var line = Find("StageLineText")?.GetComponent<TMP_Text>();
            string overflow = line != null && line.gameObject.activeInHierarchy
                ? $" line overflowing={line.isTextOverflowing} preferredH={line.preferredHeight:F0} boxH={line.rectTransform.rect.height:F0}"
                : "";
            Debug.Log($"[DialogueStageCapture] wrote {path}{overflow}");
        }
    }
}
