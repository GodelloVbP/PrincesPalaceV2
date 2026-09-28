using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M9a CAPTURES (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M9a): the real
    // bell_in_the_fog with its delivered art -- the bell page (Shawn's
    // entranced bust over stump_bell), the Bellwether fight as FightBootstrap
    // opens it (fog clearing, the Bellwether's stills, the toll counter and the
    // flock overlay at toll 1 and toll 2), and the Endure and Break pages -- at
    // 16:9 and 4:3. Pictures, not assertions: BellInTheFogRunTests and
    // FightRoundCounterTests pin the behaviour.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellInTheFogCaptureTests
    // PNGs land in tools/screenshots/runtime/events_art/ in the runner copy; a
    // set worth keeping is copied to docs/captures/events-art/.
    public class BellInTheFogCaptureTests
    {
        private const string Bell = "bell_in_the_fog";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "events_art"));

        private static readonly (string Name, UiVec Frame)[] Aspects =
        {
            ("16x9", UiFrames.Reference),
            ("4x3", UiFrames.FourThree),
        };

        private string _root;
        private ScriptedBaseInput _input;
        private MapController _map;
        private EventController _panel;
        private SystemMenuController _menu;
        private Canvas _canvas;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-events-art-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void Restore()
        {
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel != null && _panel.IsOpen) _panel.gameObject.SetActive(false);
            SoundController.StopAmbience();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SaveData.TestSquadOfThreeEnabled = null;
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the pages ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheBellPage_ThenEndure()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellInTheFogCaptureTests");

            yield return OpenTheMap(29UL);

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Bell), "fixture: bell_in_the_fog is not in the built content");
            yield return OpenTheEvent();
            yield return CompleteLineContaining("Do you hear it?");
            yield return Shoot("a_bell_shawn_entranced");

            yield return TouchTheBell();
            var session = BuildTheBellwether();
            Pad(session.Encounter.PlayerParty.Single(), 1_000_000);
            Pad(session.Encounter.Enemies.Single(), 1_000_000);
            Drive(session);
            Assert.AreEqual(FightEndReason.Survived, session.EndReason);
            RunOrchestrator.SettleFight(session, session.PlayerWon);
            Assert.AreEqual("endure", RunManager.Run.eventPageId);

            yield return OpenTheEvent();
            yield return CompleteLineContaining("Hundreds of him");
            yield return Shoot("d_endure_narration");
            yield return CompleteLineContaining("...Oh.");
            yield return Shoot("e_endure_shawn_entranced");
        }

        [UnityTest]
        public IEnumerator CaptureTheBreak()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellInTheFogCaptureTests");

            yield return OpenTheMap(31UL);

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Bell), "fixture: bell_in_the_fog is not in the built content");
            yield return TouchTheBell();
            var session = BuildTheBellwether();
            Pad(session.Encounter.PlayerParty.Single(), 1_000_000);
            session.Encounter.Enemies.Single().CurrentHealth = 1;
            Drive(session);
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);
            RunOrchestrator.SettleFight(session, session.PlayerWon);
            Assert.AreEqual("break", RunManager.Run.eventPageId);

            yield return OpenTheEvent();
            yield return CompleteLineContaining("the bell cracks");
            yield return Shoot("f_break_narration");
            yield return CompleteLineContaining("I didn't mean to");
            yield return Shoot("g_break_shawn_nervous");
        }

        // ---- the fight ----------------------------------------------------------------

        // The fight exactly as the game opens it: the Bell's Touch leaves the
        // fight pending and FightBootstrap builds and binds it on scene load,
        // with the request's backdrop, counter, overlay and ambience.
        [UnityTest]
        public IEnumerator CaptureTheBellwetherFight()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellInTheFogCaptureTests");

            RunManager.StartRun(29UL);
            SaveSlotManager.SaveCurrent();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Bell), "fixture: bell_in_the_fog is not in the built content");
            var picked = EventPicks.OnCurrentPage(0);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"{picked.Reason}");
            Assert.IsTrue(RunOrchestrator.EventFightPending, "Touch the bell left no fight pending");

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            var fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);
            Assert.IsTrue(fight.HasSession, "FightBootstrap did not open the Bellwether");
            yield return WaitWhileBusy(fight);
            yield return WaitReal(0.5f);

            _canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            yield return Shoot("b_fight_toll_1");

            // One swing and the reply: toll 2, the flock one step closer.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            Named(fight, "Verb0").GetComponent<Button>().onClick.Invoke();
            Named(fight, "EnemyPlate0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return WaitWhileBusy(fight);
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            yield return WaitReal(0.5f);
            yield return Shoot("c_fight_toll_2");
        }

        // ---- driving ----------------------------------------------------------------

        private IEnumerator OpenTheMap(ulong seed)
        {
            RunManager.StartRun(seed);
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
            _canvas = _panel.GetComponentInParent<Canvas>(true).rootCanvas;
        }

        private IEnumerator OpenTheEvent()
        {
            _map.OpenEvent();
            yield return null;
            yield return WaitReal(0.2f);
        }

        // The pick the bot makes, with the panel down.
        private IEnumerator TouchTheBell()
        {
            if (_panel != null && _panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
            var picked = EventPicks.OnCurrentPage(0);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"{picked.Reason}");
            Assert.IsTrue(RunOrchestrator.EventFightPending, "Touch the bell left no fight pending");
            yield return null;
        }

        private static FightSession BuildTheBellwether()
        {
            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the Bellwether fight did not build");
            return built.Session;
        }

        private static void Pad(CombatantState combatant, int health)
        {
            combatant.MaxHealth = health;
            combatant.CurrentHealth = health;
        }

        private static void Drive(FightSession session)
        {
            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 400 && !session.IsOver; i++)
            {
                if (session.IsPlayerTurn) session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.IsTrue(session.IsOver, "fixture: the fight did not end in 400 swings");
        }

        private static GameObject Named(FightController fight, string name) =>
            fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private static IEnumerator WaitWhileBusy(FightController fight)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(fight.IsBusy, "fixture: the fight's playback never finished");
        }

        private GameObject Find(string name) =>
            _panel.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private TMP_Text LineLabel => Find("StageLineText").GetComponent<TMP_Text>();

        private bool LineIsFull => LineLabel.maxVisibleCharacters >= DialogueText.VisibleLength(LineLabel.text);

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
            string path = Path.Combine(OutputDir, $"bell_{state}_{aspectName}.png");
            CanvasCapture.RenderToFile(_canvas, path, width, height);
            FileAssert.Exists(path);
            Debug.Log($"[BellInTheFogCapture] wrote {path}");
        }
    }
}
