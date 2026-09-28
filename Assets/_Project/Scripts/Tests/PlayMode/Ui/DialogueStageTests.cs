using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // THE DIALOGUE STAGE, PLAYED (docs/PLAN_DIALOGUE_STAGE.md D3, checked in
    // D4): the Unity-only half of the playback -- real pointer presses through
    // the real NavigationInputModule, Submit through its claim, the system
    // menu's suspension, the mirrored bust, and what a pick, a resume and a
    // conclusion paint. DialoguePlaybackTests pins the state machine itself
    // on the fast host.
    //
    // THE EVENT IS BUILT HERE, not read from events.json: a raw entry
    // resolved by the real resolver and appended to the loaded catalogue for
    // one test (FixtureEvents), so authoring demo_dialogue or petting_zoo can
    // never move these tests. The Map scene is EventPanelTests' rig: shared
    // across the fixture, put back per test, and EventSystem.current pointed
    // at the scene's own module every time (the fixture hazard: a stale
    // current from another scene swallows every press).
    public class DialogueStageTests
    {
        private const string FixtureId = "dialogue_stage_fixture";
        private const string LegacyFixtureId = "dialogue_stage_legacy_fixture";

        private const string IntroLine = "The lantern sways at the far end of the landing.";
        private const string ShawnLine = "Another landing. Prince says this one has <i>excellent</i> monsters.";
        private const string OdetteLine =
            "Hold still, both of you. That lantern burns from the far side of the wall, so somebody in the next " +
            "reality lit it for us. Whoever they are, they are expecting company, and they are not patient.";
        private const string ShawnAgainLine = "If that lantern starts talking, I am not answering it.";

        private const string FollowText = "Follow the light";
        private const string FollowResult = "The lantern leads you down a stair of old stone.";
        private const string FarewellLine = "At the bottom the lantern goes out.";
        private const string LeaveResult = "You leave the landing with the lantern's warmth still in your paws.";

        private string _root;
        private ScriptedBaseInput _input;
        private MapController _map;
        private EventController _panel;
        private SystemMenuController _menu;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dialogue-stage-" + System.Guid.NewGuid().ToString("N"));
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

        // ---- fixture ------------------------------------------------------------------

        private static RawEventLine Line(string speaker, string text, string expression = "") =>
            new RawEventLine { speaker = speaker, expression = expression, text = text };

        // demo_dialogue's shape, cut to what these tests read: a narration
        // intro, Shawn left, Shawn again with a new expression (the in-place
        // swap), Odette right (undeclared sides, so she is mirrored), and the
        // long line last; a locked row, an open row whose outcome has a
        // result and gold, and Leave; then a narration page that leaves with
        // a result.
        private static RawEventEntry StageFixture() =>
            new RawEventEntry
            {
                id = FixtureId,
                floors = new[] { 999 },
                requires = new[]
                {
                    new RawEventRequirement { kind = "inParty", character = "sheep" },
                    new RawEventRequirement { kind = "inParty", character = "owl" },
                },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "landing",
                        title = "A Lit Lantern",
                        body = "B",
                        lines = new[]
                        {
                            Line("narration", IntroLine),
                            Line("sheep", ShawnLine, "happy"),
                            Line("sheep", ShawnAgainLine, "annoyed"),
                            Line("owl", OdetteLine, "neutral"),
                        },
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Buy the lantern outright",
                                requires = new[] { new RawEventRequirement { kind = "gold", min = 99999, reason = "Too dear" } },
                                outcomes = new[] { new RawEventOutcome { goTo = "Leave" } },
                            },
                            new RawEventChoice
                            {
                                text = FollowText,
                                outcomes = new[]
                                {
                                    new RawEventOutcome
                                    {
                                        effects = new[] { new RawEventEffect { kind = "gold", amount = 5 } },
                                        result = FollowResult,
                                        goTo = "farewell",
                                    },
                                },
                            },
                            new RawEventChoice
                            {
                                text = "Leave",
                                outcomes = new[] { new RawEventOutcome { goTo = "Leave" } },
                            },
                        },
                    },
                    new RawEventPage
                    {
                        id = "farewell",
                        title = "Out of the Dark",
                        body = "B",
                        lines = new[] { Line("narration", FarewellLine) },
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Leave",
                                outcomes = new[] { new RawEventOutcome { result = LeaveResult, goTo = "Leave" } },
                            },
                        },
                    },
                },
            };

        // A line-less page: EventController paints its rows straight away,
        // through the legacy text column, with no dialogue stage and no
        // AcceptsChoicePress arming in between.
        private static RawEventEntry LegacyStageFixture() =>
            new RawEventEntry
            {
                id = LegacyFixtureId,
                floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "first",
                        title = "First",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Onward",
                                effects = new[] { new RawEventEffect { kind = "gold", amount = 5 } },
                                outcomes = new[] { new RawEventOutcome { goTo = "second" } },
                            },
                        },
                    },
                    new RawEventPage
                    {
                        id = "second",
                        title = "Second",
                        body = "B",
                        choices = new[]
                        {
                            new RawEventChoice
                            {
                                text = "Back",
                                effects = new[] { new RawEventEffect { kind = "gold", amount = 7 } },
                                outcomes = new[] { new RawEventOutcome { goTo = "first" } },
                            },
                        },
                    },
                },
            };

        // ---- event speakers (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.3, M6) ----------

        private const string SpeakerFixtureId = "dialogue_stage_speaker_fixture";
        private const string KeeperLine = "Mind the lamp. It remembers every hand that trimmed it.";
        private const string StrangerLine = "I was never here, and neither were you.";
        private const string EntrancedLine = "Do you hear it? Somewhere past the fog, a bell.";

        // Two event-local speakers on one page: the keeper, cast right, whose
        // declared "grim" face has no file and falls back to a real neutral
        // bust (the bear's folder stands in for commissioned art); and the
        // stranger, cast left, whose folder does not exist at all -- the name
        // plate and text with no bust. Shawn's `entranced` has no file yet and
        // walks the same fallback to his neutral.
        private static RawEventEntry SpeakerFixture() =>
            new RawEventEntry
            {
                id = SpeakerFixtureId,
                floors = new[] { 999 },
                requires = new[] { new RawEventRequirement { kind = "inParty", character = "sheep" } },
                speakers = new[]
                {
                    new RawEventSpeaker
                    {
                        id = "keeper", name = "Old Wick", epithet = "Keeper of the Lamp",
                        bustPath = "Portraits/Dialogue/bear", expressions = new[] { "grim", "neutral" },
                    },
                    new RawEventSpeaker
                    {
                        id = "stranger", name = "The Stranger", bustPath = "Portraits/Dialogue/no_such_speaker",
                    },
                },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "lamp",
                        title = "The Lamp Keeper",
                        body = "B",
                        cast = new[]
                        {
                            new RawEventCastMember { character = "keeper", side = "right" },
                            new RawEventCastMember { character = "stranger", side = "left" },
                        },
                        lines = new[]
                        {
                            Line("keeper", KeeperLine, "grim"),
                            Line("stranger", StrangerLine),
                            Line("sheep", EntrancedLine, "entranced"),
                        },
                        choices = new[]
                        {
                            new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
                        },
                    },
                },
            };

        private static RunSnapshot Run => RunManager.Run;

        // The Map with the fixture event open on its first page and the
        // panel painted. The walk into the room is EventPanelTests' business;
        // OpenEventForDebug leaves the state a real arrival leaves.
        private IEnumerator OpenTheStage() => OpenTheStage(StageFixture());

        private IEnumerator OpenTheStage(RawEventEntry fixture)
        {
            FixtureEvents.Append(fixture);
            RunManager.StartRun(11UL);
            Run.gold = 100;
            SaveSlotManager.SaveCurrent();

            yield return SharedScene.Ensure("Map");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the map scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            var stale = module.GetComponent<ScriptedBaseInput>();
            if (stale != null) Object.DestroyImmediate(stale);
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            Assert.IsNotNull(_panel, "the Map scene has no EventController");
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _map.Refresh();
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(fixture.id), "fixture: the stage event did not open");
            yield return ReopenThePanel();
        }

        // The panel's own door, which is also what a resume goes through.
        private IEnumerator ReopenThePanel()
        {
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _panel.Finished = _map.Refresh;
            _panel.Open();
            yield return null;
        }

        // A resume from the save: the panel down, the save and run dropped
        // from memory and read back off disk, the panel opened again.
        private IEnumerator ResumeFromTheSave()
        {
            _panel.gameObject.SetActive(false);
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Assert.IsTrue(RunOrchestrator.EventIsOpen, "fixture: the event survives the reload");
            yield return ReopenThePanel();
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

        private Button Row(string text)
        {
            var row = ActiveRows().FirstOrDefault(b => RowText(b) == text);
            Assert.IsNotNull(row, $"no visible row reads '{text}'");
            return row;
        }

        private Image ActiveBust() =>
            new[] { "StageBustA", "StageBustB" }
                .Select(n => Find(n)?.GetComponent<Image>())
                .SingleOrDefault(i => i != null && i.gameObject.activeInHierarchy);

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

        // Presses Submit, a slide's worth apart, until the stage shows `text`.
        // Stops on the frame it appears, so the caller sees its first state.
        private IEnumerator AdvanceTo(string text)
        {
            for (int presses = 0; presses < 30 && LineLabel.text != text; presses++)
            {
                yield return Submit();
                if (LineLabel.text == text) yield break;
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            Assert.AreEqual(text, LineLabel.text, "never reached the line");
        }

        // Every line through, to the frame the rows appear.
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

        private Vector2 ScreenPointOf(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        private GameObject TopHitAt(Vector2 screenPoint)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        // A real left press: down on one frame, up on the next.
        private IEnumerator PressAt(Vector2 screenPoint)
        {
            _input.MousePosition = screenPoint;
            yield return DriveFrame();
            _input.MouseButton0Down = true;
            _input.MouseButton0Held = true;
            yield return DriveFrame();
            _input.MouseButton0Held = false;
            _input.MouseButton0Up = true;
            yield return DriveFrame();
        }

        // ---- pointer ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator ALeftPressOnTheStageAdvances_AndOnceTheRowsShowAClickHitsARowNotTheStage()
        {
            yield return OpenTheStage();
            var stage = (RectTransform)Find("DialogueStage").transform;
            Vector2 centre = ScreenPointOf(stage);

            Assert.AreEqual(IntroLine, LineLabel.text, "the first beat is the intro narration");
            Assert.AreEqual(stage.gameObject, TopHitAt(centre), "while lines play, a press lands on the stage catcher");

            yield return PressAt(centre);
            Assert.IsTrue(LineIsFull, "the first press completes the line");
            Assert.AreEqual(IntroLine, LineLabel.text);

            yield return PressAt(centre);
            Assert.AreEqual(ShawnLine, LineLabel.text, "the next press moves on");

            for (int presses = 0; presses < 30 && !RowsShown; presses++)
            {
                yield return PressAt(centre);
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            Assert.IsTrue(RowsShown, "stage presses never reached the rows");
            Assert.AreEqual("landing", Run.eventPageId, "the press that opened the rows picked nothing");

            var follow = Row(FollowText);
            Vector2 onRow = ScreenPointOf((RectTransform)follow.transform);
            var hit = TopHitAt(onRow);
            Assert.IsNotNull(hit);
            Assert.IsTrue(hit.transform.IsChildOf(follow.transform),
                $"a click on the row lands on '{hit.name}', not the row");

            yield return PressAt(onRow);
            Assert.AreEqual("farewell", Run.eventPageId, "the click picked the row");
        }

        // ---- Submit -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator HoldingSubmitThroughTheLastLine_NeverPicks_TheNextPressDoes_OnTheFirstEnabledRow()
        {
            yield return OpenTheStage();
            yield return AdvanceTo(OdetteLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            yield return Submit(); // completes the last line
            Assert.IsTrue(LineIsFull);
            Assert.IsFalse(RowsShown);

            // The press that finishes the page opens the rows...
            _input.SubmitDown = true;
            yield return DriveFrame();
            Assert.IsTrue(RowsShown);
            Assert.AreEqual("landing", Run.eventPageId, "the press that opened the rows also picked");

            // ...and held (no new press edge) it never picks.
            for (int frame = 0; frame < 10; frame++) yield return DriveFrame();
            Assert.AreEqual("landing", Run.eventPageId, "a held Submit picked a choice");

            // Focus is on the first ENABLED row: row 0 is the locked one.
            var first = ActiveRows()[0];
            Assert.IsFalse(first.interactable, "fixture: the first row is the locked one");
            Assert.AreEqual(Row(FollowText).gameObject, EventSystem.current.currentSelectedGameObject);

            yield return Submit();
            Assert.AreEqual("farewell", Run.eventPageId, "the next press picks the focused row");
        }

        [UnityTest]
        public IEnumerator TheDpadDoesNothingWhileLinesPlay()
        {
            yield return OpenTheStage();
            yield return AdvanceTo(ShawnLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);

            foreach (var move in new[] { (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
            {
                _input.Horizontal = move.Item1;
                _input.Vertical = move.Item2;
                yield return DriveFrame();
                _input.Horizontal = 0f;
                _input.Vertical = 0f;
                yield return DriveFrame();
            }

            Assert.AreEqual(ShawnLine, LineLabel.text, "a D-pad press moved the dialogue");
            Assert.IsFalse(RowsShown, "a D-pad press showed the rows");
            Assert.IsNull(EventSystem.current.currentSelectedGameObject, "a D-pad press selected something");
        }

        // ---- same-frame double press ------------------------------------------------

        // A Submit and a mouse click landing on the same frame both reach
        // EventController's choice handler. On a legacy (line-less) page the
        // rows are up immediately -- no AcceptsChoicePress arming stands
        // between them -- and Press's own Paint() already moved the painted
        // page id before the second press's ChooseEventOption reads it, so
        // RunOrchestrator's StalePage guard sees a current page and cannot
        // catch it. Only EventController's own one-choose-per-frame guard can.
        [UnityTest]
        public IEnumerator ADoublePressOnTheSameFrame_OnALegacyPage_AppliesOnlyTheFirst()
        {
            FixtureEvents.Append(LegacyStageFixture());
            RunManager.StartRun(11UL);
            Run.gold = 100;
            SaveSlotManager.SaveCurrent();

            yield return SharedScene.Ensure("Map");
            _map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            Assert.IsNotNull(_panel, "the Map scene has no EventController");
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _map.Refresh();
            yield return null;

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(LegacyFixtureId), "fixture: the legacy event did not open");
            yield return ReopenThePanel();
            Assert.IsFalse(Find("DialogueStage").activeInHierarchy, "fixture: a line-less page skips the stage");

            var press = typeof(EventController).GetMethod("Press", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(press, "EventController.Press was renamed or removed");

            // Both calls land with no frame boundary between them -- exactly
            // what a same-frame Submit + click does, since the second press
            // reaches Press before Time.frameCount can move on.
            press.Invoke(_panel, new object[] { 0 });
            press.Invoke(_panel, new object[] { 0 });

            Assert.AreEqual("second", Run.eventPageId, "the page the first press opened stays");
            Assert.AreEqual(105, Run.gold, "only the first press paid; the second never reached page 'second'");
        }

        // ---- the system menu ------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnOpenSystemMenuSuspendsTheTypewriter_AndClosingItResumes()
        {
            yield return OpenTheStage();
            yield return AdvanceTo(OdetteLine);

            // Past the slide and partway into a ~3 s reveal.
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.4f);
            Assert.IsFalse(LineIsFull, "fixture: Odette's line is long enough to still be revealing");

            _input.SystemMenuDown = true;
            yield return DriveFrame();
            Assert.IsTrue(_menu.IsOpen, "Start opens the system menu over the stage");

            int atOpen = LineLabel.maxVisibleCharacters;
            yield return WaitReal(0.5f);
            Assert.AreEqual(atOpen, LineLabel.maxVisibleCharacters, "the typewriter ran under the menu");

            _menu.Close();
            yield return WaitReal(0.5f);
            Assert.Greater(LineLabel.maxVisibleCharacters, atOpen, "closing the menu did not resume the typewriter");
        }

        // ---- the backdrop -------------------------------------------------------------

        // A stage resized under an open page re-covers on the next frame. The
        // cover used to be solved once at paint, so the capture rig's 4:3
        // pass (and a real window resize) kept the 16:9 size and showed bare
        // ground above and below it. Dungeon.png is 1672x941: at 1920x1440
        // the height binds, 941 * 1440/941 = 1440 and 1672 * 1440/941 =
        // 2558.64 (DialogueStageLayoutTests' own worked figures).
        [UnityTest]
        public IEnumerator AStageResizedUnderAnOpenPage_ReCoversTheBackdrop()
        {
            yield return OpenTheStage();

            var stage = (RectTransform)Find("DialogueStage").transform;
            var backdrop = Find("StageBackdrop").GetComponent<Image>();
            Assert.IsTrue(backdrop.enabled && backdrop.sprite != null, "fixture: the default backdrop is baked");

            var anchorMin = stage.anchorMin;
            var anchorMax = stage.anchorMax;
            var sizeDelta = stage.sizeDelta;
            var position = stage.anchoredPosition;
            try
            {
                stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
                stage.anchoredPosition = Vector2.zero;
                stage.sizeDelta = new Vector2(1920f, 1440f);
                yield return null;
                yield return null;

                var size = backdrop.rectTransform.sizeDelta;
                Assert.AreEqual(2558.64f, size.x, 0.05f, "the cover did not follow the stage's new width");
                Assert.AreEqual(1440f, size.y, 0.05f, "the cover did not follow the stage's new height");
            }
            finally
            {
                stage.anchorMin = anchorMin;
                stage.anchorMax = anchorMax;
                stage.sizeDelta = sizeDelta;
                stage.anchoredPosition = position;
            }
        }

        // ---- busts ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRightSideBustIsMirrored_TheLeftSideOneIsNot()
        {
            yield return OpenTheStage();

            yield return AdvanceTo(ShawnLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            var left = ActiveBust();
            Assert.IsNotNull(left, "fixture: Shawn has a happy bust");
            Assert.Greater(left.rectTransform.localScale.x, 0f, "a left-side speaker shows as painted");

            yield return AdvanceTo(OdetteLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            var right = ActiveBust();
            Assert.IsNotNull(right, "fixture: Odette has a neutral bust");
            Assert.Less(right.rectTransform.localScale.x, 0f, "a right-side speaker is mirrored to face inward");
        }

        [UnityTest]
        public IEnumerator TheSameSpeakerWithANewExpression_SwapsInPlace()
        {
            yield return OpenTheStage();
            yield return AdvanceTo(ShawnLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            var before = ActiveBust();
            Assert.IsNotNull(before, "fixture: Shawn has a happy bust");
            var sprite = before.sprite;
            var rest = before.rectTransform.anchoredPosition;

            yield return AdvanceTo(ShawnAgainLine);

            var after = ActiveBust();
            Assert.AreSame(before, after, "the same bust stays on screen");
            Assert.AreNotSame(sprite, after.sprite, "the expression changed");
            Assert.AreEqual(rest, after.rectTransform.anchoredPosition, "no slide: the swap is in place");
        }

        private TMP_Text PlateName => Find("StageName").GetComponent<TMP_Text>();
        private TMP_Text PlateEpithet => Find("StageEpithet").GetComponent<TMP_Text>();

        // An event's own speaker stands exactly like a party bust: on its cast
        // side, mirrored on the right, its own name and epithet on the plate;
        // with no art on any rung it keeps the plate and loses only the bust.
        [UnityTest]
        public IEnumerator AnEventSpeakerShowsOnEitherSide_WithItsPlate_AndMissingArtFallsBack()
        {
            yield return OpenTheStage(SpeakerFixture());

            yield return AdvanceTo(KeeperLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            var keeper = ActiveBust();
            Assert.IsNotNull(keeper, "the keeper's missing 'grim' falls back to the neutral in its folder");
            Assert.AreEqual("neutral", keeper.sprite.name, "the fallback rung, not the requested one");
            Assert.Less(keeper.rectTransform.localScale.x, 0f, "cast right: mirrored to face inward");
            Assert.AreEqual("Old Wick", PlateName.text);
            Assert.IsTrue(PlateEpithet.gameObject.activeSelf, "an epithet shows its line");
            Assert.AreEqual("Keeper of the Lamp", PlateEpithet.text);

            yield return AdvanceTo(StrangerLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            Assert.IsNull(ActiveBust(), "no file on any rung: no bust, and never a white quad");
            Assert.IsTrue(Find("StageNamePlate").activeSelf, "the plate still says who is talking");
            Assert.AreEqual("The Stranger", PlateName.text);
            Assert.IsFalse(PlateEpithet.gameObject.activeSelf, "no epithet authored, no second line");

            yield return AdvanceTo(EntrancedLine);
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            var shawn = ActiveBust();
            Assert.IsNotNull(shawn, "Shawn's 'entranced' walks the same path to his neutral until its art lands");
            Assert.Greater(shawn.rectTransform.localScale.x, 0f, "a left-side speaker shows as painted");
            Assert.AreEqual("Shawn", PlateName.text);
        }

        // ---- after a pick ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator APick_PlaysTheResultFirst_ThenTheDestinationLines()
        {
            yield return OpenTheStage();
            yield return AdvanceToChoices();
            yield return Submit(); // Follow the light, the focused row

            Assert.AreEqual("farewell", Run.eventPageId);
            Assert.IsFalse(RowsShown, "the destination's rows wait for its lines");
            StringAssert.Contains(FollowResult, LineLabel.text, "the result plays first");
            StringAssert.Contains("+5 gold", LineLabel.text, "with the effects line under it");
            Assert.IsNull(ActiveBust(), "the result is narration: no bust");

            yield return AdvanceTo(FarewellLine);
        }

        [UnityTest]
        public IEnumerator AResume_ReplaysTheResult_ThenLineOne_AndAppliesNothingAgain()
        {
            yield return OpenTheStage();
            yield return AdvanceToChoices();
            yield return Submit();
            Assert.AreEqual(105, Run.gold, "fixture: the pick paid once");

            yield return ResumeFromTheSave();

            Assert.AreEqual("farewell", Run.eventPageId);
            StringAssert.Contains(FollowResult, LineLabel.text, "a resume replays the result first");
            Assert.AreEqual(105, Run.gold, "replay is presentation only");

            yield return AdvanceTo(FarewellLine);
            Assert.AreEqual(105, Run.gold);
        }

        [UnityTest]
        public IEnumerator AConcludedEvent_ShowsTheResultOnTheStage_WithOnlyLeave_LiveAndAfterAResume()
        {
            yield return OpenTheStage();
            yield return AdvanceToChoices();
            yield return Submit(); // to farewell
            yield return AdvanceToChoices();
            yield return Submit(); // its Leave, which has a result

            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded, "fixture: the farewell Leave concludes");
            yield return AssertConcludedStage();

            yield return ResumeFromTheSave();
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Concluded);
            yield return AssertConcludedStage();
        }

        private IEnumerator AssertConcludedStage()
        {
            Assert.IsTrue(Find("DialogueStage").activeInHierarchy, "a concluded event with lines stays on the stage");
            Assert.IsFalse(Find("EventTextColumn").activeInHierarchy, "the legacy text column stays down");
            StringAssert.Contains(LeaveResult, LineLabel.text);
            Assert.IsNull(ActiveBust(), "the concluded result is narration: no bust");

            yield return AdvanceToChoices();
            var rows = ActiveRows();
            Assert.AreEqual(1, rows.Length, "a concluded event offers exactly one thing to do");
            Assert.AreEqual("Leave", RowText(rows[0]));
        }
    }
}
