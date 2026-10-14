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

        // A DELIBERATELY MIXED set: the three offers should not all be the same
        // shape, because the whole point of the capture is whether a long thin
        // dagger and a squat pair of boots both sit right in the same card.
        // THREE DIFFERENT RARITY BANDS, deliberately.
        //
        // Rarity comes from the item's tier (RarityBands.For), and Common's
        // colour is near-white -- three Commons on a violet panel is the one
        // sample that cannot show whether the rarity glow works at all, which
        // is exactly what the first capture of this screen produced.
        private static List<ItemOffer> Offers()
        {
            var picks = Content.ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .Take(3)
                .ToList();

            Assert.AreEqual(3, picks.Count, "need three equippable items WITH art to photograph");

            // Three tiers across the art ladder, and a plus on the last two,
            // so the "+N" suffix is in the shot too.
            return new List<ItemOffer>
            {
                new ItemOffer(picks[0].id, 0, 0),
                new ItemOffer(picks[1].id, 6, 1),
                new ItemOffer(picks[2].id, 10, 3),
            };
        }

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

            // And again with the middle card hovered, which is the state the
            // comparison box exists for. Driven through the component rather
            // than by faking a pointer, because the capture has no real cursor.
            var hovers = _reckoning.GetComponentsInChildren<HoverIndex>(includeInactive: true)
                .OrderBy(h => h.Index).ToList();
            Assert.IsNotEmpty(hovers, "no HoverIndex was attached to any offer card");
            hovers[0].OnPointerEnter(null);
            yield return new WaitForSecondsRealtime(0.4f);

            string hovered = Path.Combine(OutputDir, "Reckoning_hover.png");
            CanvasCapture.RenderToFile(canvas, hovered);
            Assert.IsTrue(File.Exists(hovered));
            Debug.Log($"[ReckoningCapture] wrote {hovered}");
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
