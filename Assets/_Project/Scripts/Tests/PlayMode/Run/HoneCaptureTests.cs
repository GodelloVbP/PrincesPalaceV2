using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // THE CAMERA FOR THE HONING MODAL (shop HONE, Rest room) AND THE DEFEAT
    // SCREEN'S BANKED-GOLD LINE. All three are nested panels the Edit-Mode
    // render cannot reach.
    //
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter HoneCaptureTests
    public class HoneCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hone-shot-" + System.Guid.NewGuid().ToString("N"));
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

        private static ulong SeedWithAShop()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = RealmMapGenerator.GenerateFor(seed, 0, RealmProfiles.ForRealm(0));
                if (RealmPaths.FirstOfType(map, RoomType.Shop) != null) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        private static void Shoot(string name)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "no root canvas");
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, name);
            CanvasCapture.RenderToFile(canvas, path, 1920, 1080);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[HoneCapture] wrote {path}");
        }

        private static void HoneSome(RunSnapshot run, string id)
        {
            SlotHoning.Hone(run.slotHoning, id, EquipmentSlot.Weapon1);
            SlotHoning.Hone(run.slotHoning, id, EquipmentSlot.Weapon1);
            SlotHoning.Hone(run.slotHoning, id, EquipmentSlot.Torso);
        }

        [UnityTest]
        public IEnumerator CaptureTheShopHoneModal()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            RunManager.StartRun(SeedWithAShop());
            var node = RealmPaths.FirstOfType(RunManager.Map, RoomType.Shop);
            RealmPaths.StandBefore(node);
            RunOrchestrator.ArriveAt(node);
            RunManager.Run.gold = 2000;
            RunManager.Run.step = 8;
            HoneSome(RunManager.Run, SaveSlotManager.CurrentSave.ActiveSquad().First().definitionId);
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include).OpenShop();
            yield return null;
            yield return null;

            Shoot("Shop_actions.png");

            var hone = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).FirstOrDefault(b => b.name == "ShopHoneButton");
            Assert.IsNotNull(hone, "the shop drew no ShopHoneButton");
            hone.onClick.Invoke();
            yield return null;
            yield return null;

            Shoot("Hone_shop.png");
        }

        [UnityTest]
        public IEnumerator CaptureTheRestPage()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            RunManager.StartRun(1UL);
            var rest = RunManager.Choices().First();
            rest.Type = RoomType.Rest;
            Assert.AreEqual(RunOrchestrator.Arrival.Rest, RunOrchestrator.ArriveAt(rest));
            HoneSome(RunManager.Run, SaveSlotManager.CurrentSave.ActiveSquad().First().definitionId);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Shoot("Hone_rest.png");
        }

        [UnityTest]
        public IEnumerator CaptureTheDefeatScreenWithBankedGold()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore("No graphics device.");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var defeat = Object.FindAnyObjectByType<DefeatController>(FindObjectsInactive.Include);
            Assert.IsNotNull(defeat, "the fight scene has no DefeatController");

            defeat.Show(new RunSettlement.Result
            {
                GoldLost = 247,
                GoldBanked = 135,
                RoomsCleared = 13,
                DeepestStep = 29,
                ExpEarned = 861,
                EmbersEarned = 2,
            });
            yield return null;
            yield return null;

            Shoot("Defeat_banked.png");
        }
    }
}
