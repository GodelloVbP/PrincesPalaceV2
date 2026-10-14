using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // Captures the Reckoning's CHOICE phase, which no static capture can reach.
    //
    // ScreenshotTool renders Edit Mode and only knows top-level registered
    // panels; the Reckoning is a sub-panel inside the fight that starts
    // inactive and is driven by Show(). AUDIT #43 is the record of the tool
    // reporting success for this exact panel while never capturing it.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // ReckoningCaptureTests.
    public class ReckoningCaptureTests
    {
        private string _root;
        private ReckoningController _reckoning;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shot-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        // Real pages, rolled the way a won fight rolls them: one normal gear
        // chip is three cards for the squad's recipients.
        private static IReadOnlyList<PendingPage> Offers() => ReckoningPageFixtures.OneGearPage();

        [UnityTest]
        public IEnumerator CaptureTheChoicePhase()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            _reckoning.Show(Reward(), Offers());

            // REAL SECONDS, not a frame count. Every animation on this screen
            // runs on Time.unscaledDeltaTime, which reports wall-clock and is
            // NOT affected by Time.captureFramerate -- and a PlayMode frame in
            // batchmode costs well under a millisecond. "Wait 240 frames" was
            // therefore about a tenth of a second against a 0.34s wipe, and the
            // capture came out with the frame still clipped to a third of its
            // width, which reads as a broken panel rather than as an
            // unfinished animation.
            yield return new WaitForSecondsRealtime(2.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Reckoning_choice.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ReckoningCapture] wrote {path}");
        }

        // A boss and two anchors settled together: the gear page with five
        // cards and three choices, a card taken, then the relic page.
        [UnityTest]
        public IEnumerator CaptureTheBossSettlementPages()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            _reckoning.Show(Reward(), pages);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("Reckoning_boss_gear.png");

            Press("ReckoningOffer1");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_gear_taken.png");

            Press("ReckoningLeaveButton");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_gear_left.png");

            Press("ReckoningLeaveButton");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_relic.png");
        }

        private void Press(string name) =>
            _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == name).onClick.Invoke();

        private IEnumerator Shoot(string file)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, file);
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ReckoningCapture] wrote {path}");
            yield return null;
        }

        // The summary phase's Continue plate against the spoils and tally
        // rows, at 16:9 and 4:3. The canvas renders at 1 unit per pixel, so the
        // 4:3 shot is the same canvas cropped to 1440x1080.
        [UnityTest]
        public IEnumerator CaptureTheSummaryPages()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            var reward = Reward();
            reward.Characters.Add(new CharacterReward("odette", "Odette", 1, 0, 1, 40, 100, 40, expToNextBefore: 100));
            reward.Characters.Add(new CharacterReward("bjorn", "Bjorn", 1, 0, 1, 60, 100, 60, expToNextBefore: 100));

            _reckoning.Show(reward, Offers());
            yield return new WaitForSecondsRealtime(1f);

            var offer = _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "ReckoningOffer0");
            offer.onClick.Invoke();

            // The page waits to be moved on; the same button carries on.
            _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "ReckoningLeaveButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(3f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);
            Directory.CreateDirectory(OutputDir);

            var tabs = _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true);
            foreach (var page in new[] { (tab: "ReckoningTab0", label: "spoils"), (tab: "ReckoningTab2", label: "tally") })
            {
                tabs.First(b => b.name == page.tab).onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);

                CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, $"Reckoning_summary_{page.label}_16x9.png"), 1920, 1080);
                CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, $"Reckoning_summary_{page.label}_4x3.png"), 1440, 1080);
            }
        }
    }
}
