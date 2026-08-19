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
            var withArt = Content.ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrWhiteSpace(i.iconPath))
                .ToList();

            var picks = new List<ItemDefinition>();
            foreach (int wanted in new[] { 0, 6, 10 })
            {
                var hit = withArt.OrderBy(i => System.Math.Abs(i.tier - wanted))
                    .FirstOrDefault(i => !picks.Contains(i));
                if (hit != null) picks.Add(hit);
            }

            Assert.AreEqual(3, picks.Count, "need three equippable items WITH art to photograph");
            Debug.Log("[ReckoningCapture] tiers: " + string.Join(", ", picks.Select(i => i.tier)));

            // A plus on the last one, so the "+N" suffix is in the shot too.
            return new List<ItemOffer>
            {
                new ItemOffer(picks[0].id, picks[0].tier, 0),
                new ItemOffer(picks[1].id, picks[1].tier, 1),
                new ItemOffer(picks[2].id, picks[2].tier, 3),
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
    }
}
