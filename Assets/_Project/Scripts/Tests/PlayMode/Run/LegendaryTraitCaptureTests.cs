using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // A boss gear page whose best card is Legendary, so the trait line shows.
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter LegendaryTraitCaptureTests.
    public class LegendaryTraitCaptureTests
    {
        private string _root;

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

        [UnityTest]
        public IEnumerator CaptureALegendaryCardWithItsTrait()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            LogAssert.Expect(LogType.Log, new Regex(@"\[RealmOpening\]"));
            RunManager.StartRun(ReckoningPageFixtures.Seed);
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();

            var reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning);
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            reckoning.Show(reward, pages);
            yield return new WaitForSecondsRealtime(2.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            string dir = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));
            Directory.CreateDirectory(dir);
            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "Legendary_trait_page.png"));
        }
    }
}
