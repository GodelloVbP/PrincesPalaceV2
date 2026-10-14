using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // The cache room open, then the map's top band once something is banked:
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter CacheCaptureTests
    public class CacheCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cache-shot-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator CaptureTheCacheAndTheBankedTopBand()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            ulong seed = 1;
            for (; seed < 4000UL; seed++)
            {
                if (RealmPaths.FirstOfType(RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0)),
                        RoomType.Cache) != null) break;
            }

            RunManager.StartRun(seed);
            var node = RealmPaths.FirstOfType(RunManager.Map, RoomType.Cache);
            RealmPaths.StandBefore(node);
            RunManager.Run.gold = 240;
            RunOrchestrator.ArriveAt(node);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            Directory.CreateDirectory(OutputDir);
            CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, "Cache_open.png"), 1920, 1080);

            var page = RunOrchestrator.CurrentEvent();
            var bank = page.Choices.First(c => c.Preview.Contains("50%"));
            RunOrchestrator.ChooseEventOption(bank.Index, page.PageId);
            RunOrchestrator.LeaveEvent();
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);

            CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, "Cache_banked_map.png"), 1920, 1080);
        }
    }
}
