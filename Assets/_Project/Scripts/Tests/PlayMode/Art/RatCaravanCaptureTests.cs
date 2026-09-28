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
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M7b CAPTURES (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M7b): the real
    // rat_caravan in the real Map scene -- the caravan page with the merchant
    // speaking, the shelf with its title and keeper panel (plain, then with
    // Odette's marks), the page after each browse, and the robbery's result
    // and page -- each at every UiFrames aspect. Pictures, not assertions:
    // RatCaravanRunTests and CaravanShelfScreenTests pin the behaviour.
    //
    // The caravan art is delivered (M9b): road.png behind, caravan.png and
    // robbed.png as the set pieces, and the merchant's three busts on the
    // stage and in the shelf's keeper panel. The M9b set is copied to
    // docs/captures/events-art/.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RatCaravanCaptureTests
    // PNGs land in tools/screenshots/runtime/events_m7b/ in the runner copy; a
    // set worth keeping is copied to docs/captures/events-m7b/.
    public class RatCaravanCaptureTests
    {
        private const string Caravan = "rat_caravan";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "events_m7b"));

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
        private ShopController _shop;
        private SystemMenuController _menu;
        private Canvas _canvas;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-events-m7b-capture-" + Guid.NewGuid().ToString("N"));
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
            if (_shop != null) _shop.gameObject.SetActive(false);
            SaveData.TestSquadOfThreeEnabled = null;
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator CaptureTheCaravanItsShelfAndTheRobbery()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RatCaravanCaptureTests");

            RunManager.StartRun(21UL);
            RunManager.Run.gold = 5000;
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
            _shop = UnityEngine.Object.FindAnyObjectByType<ShopController>(FindObjectsInactive.Include);
            _menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (_menu != null && _menu.IsOpen) _menu.Close();
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            _map.Refresh();
            yield return null;
            _canvas = _panel.GetComponentInParent<Canvas>(true).rootCanvas;

            // The caravan page: the merchant's pitch.
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Caravan), "fixture: rat_caravan is not in the built content");
            yield return OpenTheEvent();
            yield return CompleteLineContaining("Customers!");
            yield return Shoot("a_caravan_merchant_grinning");

            // Browse: the shelf, with the caravan's title and the keeper panel.
            yield return PickInCode(0);
            Assert.IsTrue(_shop.gameObject.activeSelf, "Browse did not hand over to the shelf");
            yield return WaitReal(0.3f);
            yield return Shoot("b_shelf_plain");

            // Leave the shelf: after_browse.
            yield return LeaveTheShelf();
            Assert.AreEqual("after_browse", RunManager.Run.eventPageId);
            yield return CompleteLineContaining("Pleasure doing business");
            yield return Shoot("c_after_browse_merchant_neutral");

            // Walk on, and the caravan again at the same spot: Browse with Odette.
            yield return PickInCode(0);
            RunOrchestrator.LeaveEvent();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Caravan));
            yield return PickInCode(1);
            Assert.IsTrue(_shop.gameObject.activeSelf, "Browse with Odette did not hand over to the shelf");
            yield return WaitReal(0.3f);
            yield return Shoot("d_shelf_revealed");

            yield return LeaveTheShelf();
            Assert.AreEqual("after_odette", RunManager.Run.eventPageId);
            yield return CompleteLineContaining("rivets are painted on");
            yield return Shoot("e_after_odette_odette");
            yield return CompleteLineContaining("Sharp eyes");
            yield return Shoot("f_after_odette_merchant_grinning");

            // Rob him, won in code, then the result and the robbed page.
            yield return PickInCode(2, reopen: false);
            Assert.IsTrue(RunOrchestrator.EventFightPending);
            WinTheRobInCode();
            Assert.AreEqual("robbed", RunManager.Run.eventPageId);
            yield return OpenTheEvent();
            yield return WaitReal(DialoguePlayback.SlideSeconds + 0.1f);
            if (!LineIsFull) yield return Submit();
            yield return WaitReal(0.2f);
            yield return Shoot("g_rob_result_lost_piece");
            yield return CompleteLineContaining("Take the lot");
            yield return Shoot("h_robbed_merchant_hostile");
        }

        // ---- driving ----------------------------------------------------------------

        private IEnumerator OpenTheEvent()
        {
            _map.OpenEvent();
            yield return null;
            yield return WaitReal(0.2f);
        }

        // A pick made the way the bot makes it, with the panel down, then the
        // panel reopened through the map, which hands over to a pending shelf.
        private IEnumerator PickInCode(int row, bool reopen = true)
        {
            if (_panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
            var picked = EventPicks.OnCurrentPage(row);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"{picked.Reason}");
            if (reopen) yield return OpenTheEvent();
            else yield return null;
        }

        private IEnumerator LeaveTheShelf()
        {
            var leave = _shop.GetComponentsInChildren<Button>(true).First(b => b.name == "ShopLeaveButton");
            leave.onClick.Invoke();
            leave.onClick.Invoke();
            yield return null;
            yield return WaitReal(0.2f);
            Assert.IsFalse(RunOrchestrator.EventShelfPending, "LEAVE did not leave the shelf");
            Assert.IsTrue(_panel.gameObject.activeSelf, "leaving the shelf did not return to the event");
        }

        private static void WinTheRobInCode()
        {
            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the rat pack did not build");
            var session = built.Session;
            foreach (var member in session.Encounter.PlayerParty)
            {
                member.MaxHealth = 1_000_000;
                member.CurrentHealth = 1_000_000;
            }

            foreach (var rat in session.Encounter.Enemies) rat.CurrentHealth = 1;

            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 400 && !session.IsOver; i++)
            {
                if (session.IsPlayerTurn) session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);
            RunOrchestrator.SettleFight(session, session.PlayerWon);
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
            string path = Path.Combine(OutputDir, $"m7b_{state}_{aspectName}.png");
            CanvasCapture.RenderToFile(_canvas, path, width, height);
            FileAssert.Exists(path);
            Debug.Log($"[RatCaravanCapture] wrote {path}");
        }
    }
}
