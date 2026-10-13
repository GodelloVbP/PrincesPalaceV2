using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Captures the map WITH A LEG ON IT.
    //
    // The Edit-Mode screenshot tool renders a scene at rest, and at rest this
    // screen is a pool of hidden rooms and an unscrolled wood -- correct, and
    // nothing like what the player sees. Every interesting property of this
    // screen (which clearing a room stands in, where the camera parked, whether
    // the fog covers the boss) is a runtime fact, so the only useful picture of
    // it is one taken mid-run.
    //
    // Same fixed seed the flow tests use, so the picture and the assertions are
    // of the same leg.
    public class MapCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureTheMap()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            var root = Path.Combine(Path.GetTempPath(), "pp-map-shot");
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var dir = Path.Combine(Application.dataPath, "..", "tools", "screenshots", "runtime");
            Directory.CreateDirectory(dir);
            var canvas = Object.FindAnyObjectByType<Canvas>();

            CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "MapWithALeg.png"), 1920, 1080);

            // And again MID-WALK, which is the only moment the figure is
            // anywhere the standing shot cannot show it: on the trail, between
            // two clearings, with the camera moving under it.
            var map = Object.FindAnyObjectByType<MapController>();
            var quiet = RunManager.Choices().FirstOrDefault(n =>
                n.Type != PrincesPalace.Domain.Dungeon.RoomType.Fight
                && n.Type != PrincesPalace.Domain.Dungeon.RoomType.EliteFight
                && n.Type != PrincesPalace.Domain.Dungeon.RoomType.Boss)
                ?? RunManager.Choices().FirstOrDefault();

            if (quiet != null)
            {
                var button = map.GetComponentsInChildren<Transform>(true)
                    .First(t => t.name == RealmPaths.ButtonName(quiet))
                    .GetComponent<UnityEngine.UI.Button>();

                button.onClick.Invoke();

                // Roughly half way through a step, so the figure is clear of
                // both clearings rather than one pixel off either.
                for (float waited = 0f; waited < 0.35f && map.IsWalking; waited += Time.deltaTime)
                {
                    yield return null;
                }

                CanvasCapture.RenderToFile(canvas, Path.Combine(dir, "MapMidWalk.png"), 1920, 1080);
            }

            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Assert.Pass();
        }

        // The column projection at its fullest (PLAN_RUN_MAP section 8): a floor-14
        // realm, the party standing before its most crowded column, so a
        // fork and a long pocket share the column on screen. Every room in it
        // must be drawn, and every one the party can walk to clickable.
        [UnityTest]
        public IEnumerator CaptureACrowdedRealm()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            var root = Path.Combine(Path.GetTempPath(), "pp-map-shot-crowded");
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            RunManager.StartRun(4242);
            var run = RunManager.Run;
            run.legStartStep = 13 * PrincesPalace.Domain.Dungeon.RealmMap.SpineLength;
            run.step = run.legStartStep;
            run.floor = 14;
            run.clearedNodeIds.Clear();
            var map = RunManager.Map;
            run.currentNodeId = map.Entry.Id;

            var columns = PrincesPalace.Domain.UiKit.MapColumns.Of(map);
            int crowded = Enumerable.Range(1, PrincesPalace.Domain.UiKit.MapColumns.Count - 1)
                .OrderByDescending(columns.CountIn).First();
            var first = columns.Column(crowded).First(n => n.Role != PrincesPalace.Domain.Dungeon.RealmNodeRole.Pocket);
            RealmPaths.StandBefore(first);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<MapController>();
            var reachable = RunManager.Choices().Select(n => n.Id).ToList();
            foreach (var node in columns.Column(crowded))
            {
                var button = controller.GetComponentsInChildren<Transform>(true)
                    .First(t => t.name == RealmPaths.ButtonName(node)).GetComponent<UnityEngine.UI.Button>();
                Assert.IsTrue(button.gameObject.activeInHierarchy, $"room {node.Id} is not drawn");
                Assert.AreEqual(reachable.Contains(node.Id), button.interactable, $"room {node.Id} clickability");
            }

            var dir = Path.Combine(Application.dataPath, "..", "tools", "screenshots", "runtime");
            Directory.CreateDirectory(dir);
            CanvasCapture.RenderToFile(Object.FindAnyObjectByType<Canvas>(),
                Path.Combine(dir, "MapCrowdedRealm.png"), 1920, 1080);

            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Assert.Pass($"column {crowded} holds {columns.CountIn(crowded)} rooms");
        }
    }
}
