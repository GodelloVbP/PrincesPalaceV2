using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Captures the map WITH A REALM ON IT.
    //
    // The Edit-Mode screenshot tool renders a scene at rest, and at rest this
    // screen is a pool of hidden rooms and an unscrolled wood -- correct, and
    // nothing like what the player sees. Every interesting property of this
    // screen (where the camera parked, whether the chips and pips read, how
    // the heart looks with its anchors) is a runtime fact, so the only useful
    // picture of it is one taken mid-run.
    public class MapCaptureTests
    {
        private static readonly string ShotDir =
            Path.Combine(Application.dataPath, "..", "tools", "screenshots", "runtime");

        private static void Prepare(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(root);
            SaveSystem.RootOverride = root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Directory.CreateDirectory(ShotDir);
        }

        private static void Cleanup()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        private static IEnumerator LoadMap()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            // The opening shot would put the camera on the heart.
            var map = Object.FindAnyObjectByType<MapController>();
            map.Refresh();
            yield return null;
        }

        private static void Shoot(string file) =>
            CanvasCapture.RenderToFile(Object.FindAnyObjectByType<Canvas>(), Path.Combine(ShotDir, file), 1920, 1080);

        private static void Focus(string buttonName)
        {
            var button = Object.FindAnyObjectByType<MapController>().GetComponentsInChildren<Transform>(true)
                .First(t => t.name == buttonName).gameObject;

            // The map selects its leftmost choice on load; firing the event
            // again shows the card its repaint closed.
            if (EventSystem.current.currentSelectedGameObject == button)
            {
                ExecuteEvents.Execute(button, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
                return;
            }

            EventSystem.current.SetSelectedGameObject(button);
        }

        [UnityTest]
        public IEnumerator CaptureFloorOne()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            Prepare("pp-map-shot");
            RunManager.StartRun(4242);
            yield return LoadMap();

            Shoot("MapFloor1.png");

            // One room on, where the party chooses between the pocket and the
            // spine: both routes side by side, and the pocket under fog while
            // the spine choice is in focus.
            var map = Object.FindAnyObjectByType<MapController>();
            Assert.IsTrue(RunManager.MoveTo(1));
            RunManager.ClearCurrentRoom();
            map.Refresh();
            var graph = MapGraph.Of(RunManager.Map);
            var right = RunManager.Choices().OrderBy(n => graph.PositionOf(n.Id).X).Last();
            Focus(RealmPaths.ButtonName(right));
            yield return null;
            yield return null;
            Shoot("MapFloor1Compare.png");

            // And MID-WALK, the only moment the figure is anywhere the standing
            // shot cannot show it.
            var quiet = RunManager.Choices().FirstOrDefault(n =>
                n.Type != RoomType.Fight && n.Type != RoomType.EliteFight && n.Type != RoomType.Boss)
                ?? RunManager.Choices().First();
            var button = map.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == RealmPaths.ButtonName(quiet)).GetComponent<UnityEngine.UI.Button>();
            button.onClick.Invoke();
            for (float waited = 0f; waited < 0.2f && map.IsWalking; waited += Time.deltaTime) yield return null;
            Shoot("MapMidWalk.png");

            Cleanup();
            Assert.Pass();
        }

        // A floor-14 realm: forks, long pockets, five anchors. P2's column
        // projection stacked five rooms in three slots here with overlapping
        // tiles and labels.
        [UnityTest]
        public IEnumerator CaptureACrowdedRealm()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            Prepare("pp-map-shot-crowded");
            RunManager.StartRun(4242);
            var run = RunManager.Run;
            run.legStartStep = 13 * RealmMap.SpineLength;
            run.step = run.legStartStep;
            run.floor = 14;
            run.clearedNodeIds.Clear();
            var map = RunManager.Map;
            run.currentNodeId = map.Entry.Id;

            // Standing before the fork that sits beside the most rooms.
            var graph = MapGraph.Of(map);
            var crowded = map.Nodes.Where(n => n.Role != RealmNodeRole.Pocket && n.Next.Count >= 2)
                .OrderByDescending(n => n.Next.Count).ThenBy(n => n.Id).FirstOrDefault();
            if (crowded != null) RealmPaths.StandBefore(crowded.Next.Select(map.Node).First());

            yield return LoadMap();

            var controller = Object.FindAnyObjectByType<MapController>();
            var reachable = RunManager.Choices().Select(n => n.Id).ToList();
            foreach (var node in map.Nodes)
            {
                var button = controller.GetComponentsInChildren<Transform>(true)
                    .First(t => t.name == RealmPaths.ButtonName(node)).GetComponent<UnityEngine.UI.Button>();
                Assert.IsTrue(button.gameObject.activeInHierarchy, $"room {node.Id} is not drawn");
                Assert.AreEqual(reachable.Contains(node.Id), button.interactable, $"room {node.Id} clickability");
            }

            Shoot("MapCrowdedRealm.png");

            controller.SetOverview(true);
            yield return null;
            yield return null;
            Shoot("MapCrowdedOverview.png");

            Cleanup();
            Assert.Pass($"{map.Nodes.Count} rooms, {graph.Rows} rows");
        }

        // A realm half walked: the spine to its middle, one anchor fallen. The
        // easing mask shows as the wood lifted and warmed around every room
        // walked and around the heart.
        [UnityTest]
        public IEnumerator CaptureARealmHalfWalked()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            Prepare("pp-map-shot-eased");
            RunManager.StartRun(4242);
            var run = RunManager.Run;
            run.legStartStep = 13 * RealmMap.SpineLength;
            run.step = run.legStartStep;
            run.floor = 14;
            run.clearedNodeIds.Clear();
            var map = RunManager.Map;
            run.currentNodeId = map.Entry.Id;

            var anchors = map.Nodes.Where(n => n.IsAnchor).OrderBy(n => n.Id).ToList();
            run.slainAnchorIds.Add(anchors[0].Id);

            var middle = map.Nodes.First(n => n.Offset == 4 && n.Role != RealmNodeRole.Pocket);
            RealmPaths.StandBefore(middle);
            yield return LoadMap();

            var controller = Object.FindAnyObjectByType<MapController>();
            Shoot("MapEasedWalk.png");

            controller.SetOverview(true);
            yield return null;
            yield return null;
            Shoot("MapEasedOverview.png");

            Cleanup();
            Assert.Pass($"{controller.EasedDiscs.Count} discs");
        }

        // The party at the last room before the boss with anchors still alive
        // in their pockets, and with one slain so a root has withered.
        [UnityTest]
        public IEnumerator CaptureTheHeartWithItsAnchors()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op.");
                yield break;
            }

            Prepare("pp-map-shot-heart");
            RunManager.StartRun(4242);
            var run = RunManager.Run;
            run.legStartStep = 13 * RealmMap.SpineLength;
            run.step = run.legStartStep;
            run.floor = 14;
            run.clearedNodeIds.Clear();
            var map = RunManager.Map;
            run.currentNodeId = map.Entry.Id;

            var anchors = map.Nodes.Where(n => n.IsAnchor).OrderBy(n => n.Id).ToList();
            Assert.GreaterOrEqual(anchors.Count, 2, "fixture: a floor-14 realm carries several anchors");
            run.slainAnchorIds.Add(anchors[0].Id);

            RealmPaths.StandBefore(map.Boss);
            yield return LoadMap();

            Shoot("MapHeart.png");

            Cleanup();
            Assert.Pass($"{anchors.Count} anchors, one slain");
        }
    }
}
