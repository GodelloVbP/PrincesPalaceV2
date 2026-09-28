using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M6 CAPTURES (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M6): the 16:9 art
    // on the dialogue stage and in the line-less layout, an event speaker on
    // either side with the missing-art fallback, and the fight HUD's round
    // counter -- each at every UiFrames aspect. Pictures, not assertions:
    // FightRoundCounterTests and DialogueStageTests pin the behaviour; what can
    // only be seen is whether the 1280x720 piece and the counter sit right.
    //
    // The set piece is the Map scene's own baked default backdrop, handed to
    // the panel's art table under the fixture's key -- a real 16:9 painting at
    // the frame's real size. (Written before M9a; the real Bell's delivered
    // art is captured by BellInTheFogCaptureTests.)
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.EventsM6CaptureTests
    // PNGs land in tools/screenshots/runtime/events_m6/ in the runner copy; a
    // set worth keeping is copied to docs/captures/events-m6/.
    public class EventsM6CaptureTests
    {
        private const string FixtureId = "events_m6_capture";
        private const string ArtKey = "Assets/_Project/Art/Events/events_m6_capture/lamp.png";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "events_m6"));

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
        private Action _undo;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-events-m6-capture-" + Guid.NewGuid().ToString("N"));
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
            _undo?.Invoke();
            _undo = null;
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel != null && _panel.IsOpen) _panel.gameObject.SetActive(false);
            SoundController.StopAmbience();
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RawEventLine Line(string speaker, string text, string expression = "") =>
            new RawEventLine { speaker = speaker, expression = expression, text = text };

        private static RawEventEntry Fixture() =>
            new RawEventEntry
            {
                id = FixtureId,
                floors = new[] { 999 },
                requires = new[] { new RawEventRequirement { kind = "inParty", character = "sheep" } },
                speakers = new[]
                {
                    new RawEventSpeaker
                    {
                        id = "keeper", name = "Old Wick", epithet = "Keeper of the Lamp",
                        bustPath = "Portraits/Dialogue/bear", expressions = new[] { "grim", "neutral" },
                    },
                    new RawEventSpeaker { id = "stranger", name = "The Stranger", bustPath = "Portraits/Dialogue/__none__" },
                },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "lamp",
                        artPath = ArtKey,
                        title = "The Lamp Keeper",
                        body = "B",
                        cast = new[]
                        {
                            new RawEventCastMember { character = "keeper", side = "right" },
                            new RawEventCastMember { character = "stranger", side = "left" },
                        },
                        lines = new[]
                        {
                            Line("keeper", "Mind the lamp. It remembers every hand that trimmed it.", "grim"),
                            Line("stranger", "I was never here, and neither were you."),
                            Line("sheep", "Do you hear it? Somewhere past the fog, a bell.", "entranced"),
                        },
                        choices = new[]
                        {
                            new RawEventChoice { text = "Onward", outcomes = new[] { new RawEventOutcome { goTo = "legacy" } } },
                        },
                    },
                    new RawEventPage
                    {
                        id = "legacy",
                        artPath = ArtKey,
                        title = "A Quiet Landing",
                        body = "The lamp gutters. Nobody is left to trim it, and the stair goes on down.",
                        choices = new[]
                        {
                            new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
                        },
                    },
                },
            };

        // ---- the event ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheStageSpeakersAndTheLegacyFrame()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.EventsM6CaptureTests");

            FixtureEvents.Append(Fixture());
            RunManager.StartRun(11UL);
            SaveSlotManager.SaveCurrent();
            yield return SharedScene.Ensure("Map");

            var module = UnityEngine.Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            EventSystem.current = module.GetComponent<EventSystem>();
            var stale = module.GetComponent<ScriptedBaseInput>();
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _map = UnityEngine.Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = UnityEngine.Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            _menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _map.Refresh();
            yield return null;

            // The set piece: the scene's first baked backdrop under the fixture's key.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var eventArt = typeof(EventController).GetField("eventArt", flags);
            var backdropArt = typeof(EventController).GetField("backdropArt", flags);
            var baked = (IconEntry[])backdropArt.GetValue(_panel);
            Assert.IsTrue(baked != null && baked.Length > 0, "fixture: the Map scene bakes no backdrop to borrow");
            var before = (IconEntry[])eventArt.GetValue(_panel);
            eventArt.SetValue(_panel, (before ?? new IconEntry[0]).Concat(new[] { new IconEntry(ArtKey, baked[0].Sprite) }).ToArray());
            _undo = () => eventArt.SetValue(_panel, before);

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(FixtureId), "fixture: the event did not open");
            _panel.Finished = _map.Refresh;
            _panel.Open();
            yield return null;
            _canvas = _panel.GetComponentInParent<Canvas>(true).rootCanvas;

            yield return CompleteLineContaining("Mind the lamp");
            yield return Shoot("a_speaker_right_fallback");

            yield return CompleteLineContaining("never here");
            yield return Shoot("b_speaker_left_no_art");

            yield return CompleteLineContaining("past the fog");
            yield return Shoot("c_shawn_entranced_fallback");

            yield return AdvanceToChoices();
            yield return Submit(); // Onward, to the line-less page
            yield return WaitReal(0.3f);
            Assert.AreEqual("legacy", RunManager.Run.eventPageId);
            yield return Shoot("d_legacy_frame_960x540");
        }

        // ---- the fight ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheRoundCounter()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.EventsM6CaptureTests");

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            var fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);
            UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>()?.Flush();

            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, PreviewFight.EnemiesWithArt(1),
                new Domain.Rng.SeededRandom(20260928), relicIds: null, depthStep: 0);
            Assert.IsNotNull(built?.Session);
            built.Session.RoundLimit = 10;
            built.Session.Begin();

            var bell = new FightRoundPresentation(roundLimit: 10, label: "Toll");
            fight.Bind(built.Session, EncounterClass.Normal, null, bell);
            fight.BindPartyArt(built.Party, built.PartyArt);
            yield return WaitReal(0.5f);

            _canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            yield return Shoot("e_fight_toll_1");

            // The longest label content can author, at a two-digit round.
            var longest = new FightRoundPresentation(roundLimit: 10,
                label: UiStrings.FightRoundCounter.AuditSample.Substring(0, EventEntryResolver.MaxRoundLabelLength));
            fight.Bind(built.Session, EncounterClass.Normal, null, longest);
            yield return WaitReal(0.3f);
            yield return Shoot("f_fight_longest_label");

            fight.Bind(built.Session, EncounterClass.Normal, null, FightRoundPresentation.None);
            yield return WaitReal(0.3f);
            yield return ShootAt("g_room_fight_no_counter", Aspects[0].Name, Aspects[0].Frame);
        }

        // ---- driving ----------------------------------------------------------------

        private GameObject Find(string name) =>
            _panel.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private TMP_Text LineLabel => Find("StageLineText").GetComponent<TMP_Text>();

        private bool LineIsFull => LineLabel.maxVisibleCharacters >= DialogueText.VisibleLength(LineLabel.text);

        private bool RowsShown =>
            Enumerable.Range(0, Domain.UiKit.Screens.EventScreen.ChoiceRowCount)
                .Select(i => Find($"EventChoice{i}"))
                .Any(g => g != null && g.activeInHierarchy);

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

        private IEnumerator Shoot(string state)
        {
            foreach (var (name, frame) in Aspects) yield return ShootAt(state, name, frame);
        }

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
            string path = Path.Combine(OutputDir, $"m6_{state}_{aspectName}.png");
            CanvasCapture.RenderToFile(_canvas, path, width, height);
            FileAssert.Exists(path);
            Debug.Log($"[EventsM6Capture] wrote {path}");
        }
    }
}
