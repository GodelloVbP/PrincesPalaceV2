using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // THE P4 CAPTURE CASES (docs/PLAN_BUGS_QA.md P4, PLAN_GIF_CAPTURE.md
    // `still`): the Reckoning in its full form with offers, its outcome form
    // (Fell), the summary with 0 and with 2 "Gained" chips, a chip's tooltip
    // opened by pad focus, and an event page with a receipt in the legacy
    // layout and on the dialogue stage. Pictures, not assertions: the tests
    // that pin the behaviour are FightEndRoutingFlowTests,
    // ReckoningChipNavigationTests and EventRelicChipTests.
    //
    // Graphics device only:
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter RelicReceiptCaptureTests
    //   tools/gif.ps1 still RelicReceiptCaptureTests
    // PNGs land in tools/screenshots/runtime/relic_receipts/.
    public class RelicReceiptCaptureTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private EventController _panel;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "relic_receipts"));

        private static readonly string[] Receipts = { "toll_of_the_flock", "bellwethers_bell" };

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-receipt-capture-" + Guid.NewGuid().ToString("N"));
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
            if (_panel != null && _panel.IsOpen) _panel.gameObject.SetActive(false);
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            ContentDatabase.Reset();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("sheep", "Shawn", 9, 40, 10, 12, 140, 112, expToNextBefore: 130));
            return reward;
        }

        private static IReadOnlyList<PendingPage> Offers() => ReckoningPageFixtures.OneGearPage();

        // ---- the Reckoning ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheReckoningForms()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter RelicReceiptCaptureTests");

            RunManager.StartRun(41UL);
            RunManager.Run.relicIds = new List<string>(Receipts);
            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();

            var reckoning = UnityEngine.Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning);

            yield return Show(reckoning, ReckoningRequest.Full(Reward(), Offers(), Receipts));
            yield return Shoot("Reckoning_full_offers.png");

            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn") };
            yield return Show(reckoning, ReckoningRequest.Outcome(FightEnding.Fell, new List<string>(),
                new List<string>(), tally, new CombatLedger()));
            yield return Shoot("Reckoning_outcome_fell.png");

            yield return Show(reckoning, ReckoningRequest.Full(Reward(), new List<PendingPage>(), new List<string>()));
            yield return Shoot("Reckoning_chips_0.png");

            yield return Show(reckoning, ReckoningRequest.Full(Reward(), new List<PendingPage>(), Receipts));
            yield return Shoot("Reckoning_chips_2.png");

            // Pad focus: Up from Continue onto the first chip.
            _input.Vertical = 1f;
            yield return null;
            _input.Vertical = 0f;
            yield return WaitReal(0.3f);
            Assert.AreEqual("ReckoningGainedChip0", EventSystem.current.currentSelectedGameObject?.name);
            yield return Shoot("Reckoning_chip_tooltip_pad.png");

            reckoning.gameObject.SetActive(false);
        }

        private IEnumerator Show(ReckoningController reckoning, ReckoningRequest request)
        {
            if (reckoning.gameObject.activeSelf) reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();
            reckoning.Show(request);

            // Past the 0.34s wipe and the bars.
            yield return WaitReal(2.5f);
        }

        // ---- the event page ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureTheEventPageWithAReceipt()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter RelicReceiptCaptureTests");

            FixtureEvents.Append(new Dictionary<string, string> { { "kinship", "Kinship" } }, new RawEventEntry
            {
                id = "receipt_capture", mayReturn = true, floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "legacy", title = "A Gift by the Gate",
                        body = "Someone left a small carved sheep on the fence post, warm as if a hand had only just let go of it.",
                        choices = new[]
                        {
                            new RawEventChoice { text = "Go on", outcomes = new[] { new RawEventOutcome { goTo = "staged" } } },
                            new RawEventChoice { text = "Leave it", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
                        },
                    },
                    new RawEventPage
                    {
                        id = "staged", title = "The Gate",
                        lines = new[] { new RawEventLine { speaker = "narration", expression = "", text = "The carving is warm, as if it had been waiting." } },
                        choices = new[] { new RawEventChoice { text = "Leave", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } } },
                    },
                },
            });

            RunManager.StartRun(11UL);
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug("receipt_capture"));
            RunManager.Run.pendingRelicReceipts = new List<string> { "kinship" };
            SaveSlotManager.SaveCurrent();

            yield return SharedScene.Ensure("Map");
            _input = NavSceneReuse.TakeOverInput();
            var map = UnityEngine.Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            _panel = UnityEngine.Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            if (_panel.IsOpen) _panel.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();
            map.Refresh();
            yield return null;

            _panel.Finished = map.Refresh;
            _panel.Open();
            yield return WaitReal(0.5f);
            yield return ShootFull("Event_legacy_receipt.png");

            // A receipt still pending on the staged page: put it back, since
            // leaving the legacy page acknowledged it.
            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();
            RunManager.Run.pendingRelicReceipts = new List<string> { "kinship" };
            _panel.Open();

            for (int i = 0; i < 30 && !RowsShown(); i++)
            {
                _input.SubmitDown = true;
                yield return null;
                _input.ClearOneFrameFlags();
                yield return WaitReal(DialoguePlayback.SlideSeconds + 0.05f);
            }

            Assert.IsTrue(RowsShown(), "the staged page's rows never appeared");
            yield return WaitReal(0.3f);
            yield return ShootFull("Event_staged_receipt.png");
        }

        private bool RowsShown() =>
            Enumerable.Range(0, EventScreen.ChoiceRowCount)
                .Select(i => _panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"EventChoice{i}"))
                .Any(t => t != null && t.gameObject.activeInHierarchy);

        // ---- shooting -----------------------------------------------------------------------

        private static IEnumerator WaitReal(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }

        private static Canvas RootCanvas() =>
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);

        private static IEnumerator Shoot(string file)
        {
            yield return null;
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, file);
            CanvasCapture.RenderToFile(RootCanvas(), path);
            FileAssert.Exists(path);
            Debug.Log($"[RelicReceiptCapture] wrote {path}");
        }

        private IEnumerator ShootFull(string file)
        {
            var canvas = _panel.GetComponentInParent<Canvas>(true).rootCanvas;
            int width = (int)Domain.UiKit.UiFrames.Reference.X;
            int height = (int)Domain.UiKit.UiFrames.Reference.Y;

            var rig = StageCaptureRig.FullFrame(canvas, width, height);
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
            string path = Path.Combine(OutputDir, file);
            CanvasCapture.RenderToFile(canvas, path, width, height);
            FileAssert.Exists(path);
            Debug.Log($"[RelicReceiptCapture] wrote {path}");
        }
    }
}
