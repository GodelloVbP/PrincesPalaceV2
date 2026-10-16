using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The descent map, driven through the real scene.
    //
    // The pool is the interesting part: the tree built a slot for every node a
    // realm COULD have (one per id), and the controller has to switch on
    // exactly the ones this realm has and place each where MapGraph says.
    public class MapFlowTests
    {
        private string _root;
        private MapController _map;
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator OpenTheMap(bool withRun = true, ulong seed = 4242)
        {
            if (withRun) RunManager.StartRun(seed);

            // THE MAP IS SHARED ACROSS THIS FIXTURE (SharedScene). It reads the
            // run live and has no state of its own that outlives a repaint, so
            // the reset is the repaint its own Start() does on a fresh scene:
            // Refresh() re-lays every node and trail for the run [SetUp] and
            // the line above just made (or for none), and, with no walk in
            // flight, snaps the figure and the camera back to the entry.
            yield return SharedScene.Ensure("Map");

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            Assert.IsFalse(_map.IsWalking, "fixture: the previous test left a walk in flight");
            _map.Refresh();
        }

        [UnityTest]
        public IEnumerator OnlyTheRoomsThisLegActuallyHasAreShown()
        {
            // Most of the pool is empty in any given leg. An unused slot is
            // hidden, not dimmed: it is not a room the player failed to reach,
            // it is a room that does not exist.
            yield return OpenTheMap();

            int shown = _map.GetComponentsInChildren<Button>(includeInactive: true)
                .Count(b => b.name.StartsWith("MapNode") && b.gameObject.activeSelf);

            Assert.AreEqual(RunManager.Map.Nodes.Count, shown);
            Assert.Less(shown, MapLayout.MaxNodes, "a realm does not fill every slot the pool provides");
        }

        [UnityTest]
        public IEnumerator TheRoomsAreJoinedByTrails()
        {
            // What makes this a map rather than a grid of buttons. Without the
            // trails nothing on screen says which room leads to which, and a
            // fork is indistinguishable from two unrelated rooms that happen to
            // share a row.
            yield return OpenTheMap();

            // Found by name rather than off the field: MapController's arrays
            // are internal and InternalsVisibleTo names only the Editor
            // assembly, which is why every other test in this file walks the
            // hierarchy too.
            var live = _map.GetComponentsInChildren<Image>(includeInactive: true)
                .Where(i => i.name.StartsWith("MapTrail") && i.gameObject.activeSelf)
                .ToList();
            Assert.IsNotEmpty(live, "no trail was drawn between any two rooms");

            // Every drawn piece has real geometry. One left at its placeholder
            // 8x8 is a segment the layout never reached, and it would sit on
            // the map as a stray dot.
            foreach (var piece in live)
            {
                Assert.Greater(piece.rectTransform.sizeDelta.x, 8f,
                    $"{piece.name} kept its placeholder length");
            }

            // And they are actually turned. Six quads all at zero degrees is a
            // straight line drawn the expensive way, which is exactly what a
            // broken bezier would look like.
            Assert.IsTrue(live.Any(p =>
                {
                    float z = p.rectTransform.localEulerAngles.z;
                    return Mathf.Abs(z) > 0.5f && Mathf.Abs(z - 360f) > 0.5f;
                }),
                "every trail piece is perfectly horizontal, so the curve is not curving");
        }

        [UnityTest]
        public IEnumerator WithNoRunNothingIsOffered()
        {
            yield return OpenTheMap(withRun: false);

            int shown = _map.GetComponentsInChildren<Button>(includeInactive: true)
                .Count(b => b.name.StartsWith("MapNode") && b.gameObject.activeSelf);

            Assert.AreEqual(0, shown);
        }

        [UnityTest]
        public IEnumerator OnlyReachableRoomsAreClickable()
        {
            // The map is the authority on adjacency. A button that visibly does
            // nothing is worse than one that is plainly not offered.
            yield return OpenTheMap();

            var reachable = new HashSet<int>(RunManager.Choices().Select(n => n.Id));
            Assert.IsNotEmpty(reachable, "the entry has somewhere to go");

            foreach (var node in RunManager.Map.Nodes)
            {
                var button = Named(RealmPaths.ButtonName(node)).GetComponent<Button>();
                Assert.AreEqual(reachable.Contains(node.Id), button.interactable,
                    $"room {node.Id} at offset {node.Offset} offered the wrong thing");
            }
        }

        [UnityTest]
        public IEnumerator EveryRoomStandsWhereTheLayoutPutsIt()
        {
            yield return OpenTheMap();

            var graph = MapGraph.Of(RunManager.Map);
            foreach (var node in RunManager.Map.Nodes)
            {
                var rect = (RectTransform)Named(RealmPaths.ButtonName(node)).transform;
                var at = graph.PositionOf(node.Id);

                Assert.AreEqual(at.X, rect.anchoredPosition.x, 0.01f, $"room {node.Id} is not in its lane");
                Assert.AreEqual(at.Y, rect.anchoredPosition.y, 0.01f, $"room {node.Id} is not in its row");
                Assert.AreEqual(MapLayout.TileWidthFor(node.Type), rect.sizeDelta.x, 0.01f,
                    $"room {node.Id} ({node.Type}) is the wrong size");
            }
        }

        [UnityTest]
        public IEnumerator TheWoodScrollsSoTheCurrentRoomSitsNearTheTop()
        {
            // The camera pins the party's room near the top of the window and
            // the realm runs on below it: scrolling down is going deeper.
            // Asserted against the REAL viewport height, which an EditMode
            // solve does not have.
            yield return OpenTheMap();

            var content = (RectTransform)Named("MapContent").transform;
            var viewport = (RectTransform)Named("MapViewport").transform;

            Assert.Greater(content.sizeDelta.y, viewport.rect.height * 2f,
                "the realm is meant to be taller than the window, not squeezed into it");

            // Walk the party two rows down, and the camera follows.
            var third = RunManager.Map.Nodes.Where(n => n.Offset == 2).OrderBy(n => n.Id).First();
            RealmPaths.StandBefore(third);
            RunManager.MoveTo(third.Id);
            RunManager.ClearCurrentRoom();
            _map.Refresh();

            float rowsDown = -MapGraph.Of(RunManager.Map).PositionOf(third.Id).Y;
            float onScreen = rowsDown - content.anchoredPosition.y;
            Assert.AreEqual(MapLayout.FollowOffset, onScreen, 0.01f,
                "the party's own room has to sit at the camera's follow line");
        }

        [UnityTest]
        public IEnumerator ThePartysOwnRoomIsMarked()
        {
            yield return OpenTheMap();

            Assert.IsTrue(Named(RealmPaths.ButtonName(RunManager.CurrentNode) + "Marker").activeSelf);
        }

        [UnityTest]
        public IEnumerator EnteringAFightRoomLoadsTheFight()
        {
            yield return OpenTheMap();

            // The realm's first plain fight, walked up to through MoveTo and
            // repainted, so it is one click away. Asserted, not skipped:
            // OpenTheMap seeds 4242, so the realm is the same every run.
            var fight = RealmPaths.FirstOfType(RunManager.Map, RoomType.Fight);
            Assert.IsNotNull(fight, "seed 4242's first realm has no fight room - pick a seed that does");
            RealmPaths.StandBefore(fight);
            _map.Refresh();

            Named(RealmPaths.ButtonName(fight)).GetComponent<Button>().onClick.Invoke();

            // The room fires on arrival, not on the click: the party walks
            // there first.
            Assert.IsTrue(_map.IsWalking, "clicking a room starts a walk to it");
            CollectionAssert.IsEmpty(_navigated, "and nothing loads while they are still walking");

            yield return WaitForTheWalk();

            CollectionAssert.Contains(_navigated, Navigation.Fight);
            Assert.AreEqual(fight.Id, RunManager.CurrentNode.Id, "the party moved before the scene changed");
        }

        // Bounded rather than "yield until", so a walk that never finishes
        // fails this test instead of hanging the whole PlayMode run.
        private IEnumerator WaitForTheWalk()
        {
            for (float waited = 0f; waited < 5f && _map.IsWalking; waited += Time.deltaTime)
            {
                yield return null;
            }

            Assert.IsFalse(_map.IsWalking, "the walk should have finished well inside 5 seconds");
        }

        [UnityTest]
        public IEnumerator TheFigureWalksTheTrailAndArrivesStandingInTheRoom()
        {
            // A room that leads nowhere else, so the arrival redraws the map
            // rather than loading a fight scene out from under the assertions:
            // a treasure, which resolves on the spot, opening its pocket so the
            // walk crosses from one row to the next.
            yield return OpenTheMap(seed: RealmPaths.SeedWhere(
                m => RealmPaths.PocketHead(m, RoomType.Treasure) != null, "a treasure opening a pocket"));
            var quiet = RealmPaths.PocketHead(RunManager.Map, RoomType.Treasure);
            RealmPaths.StandBefore(quiet);
            _map.Refresh();

            var walker = (RectTransform)Named("MapWalker").transform;
            var startedAt = walker.anchoredPosition;

            Named(RealmPaths.ButtonName(quiet)).GetComponent<Button>().onClick.Invoke();

            // Mid-walk: off the room it left, and not yet at the one it is
            // going to. Asserted because "moves" is the whole feature and a
            // figure that teleports on arrival would pass every other check
            // here.
            yield return null;
            yield return null;
            Assert.AreNotEqual(startedAt.y, walker.anchoredPosition.y, "the figure should be under way");

            yield return WaitForTheWalk();

            var expected = MapWalk.Standing(MapGraph.Of(RunManager.Map).PositionOf(quiet.Id));
            Assert.AreEqual(expected.X, walker.anchoredPosition.x, 0.01f, "arrived at the wrong room");
            Assert.AreEqual(expected.Y, walker.anchoredPosition.y, 0.01f, "arrived at the wrong height");
            Assert.AreEqual(quiet.Id, RunManager.CurrentNode.Id, "arriving is what moves the party");
        }

        [UnityTest]
        public IEnumerator AnUnreachableRoomIsNotWalkedTo()
        {
            yield return OpenTheMap();

            var reachable = new HashSet<int>(RunManager.Choices().Select(n => n.Id));
            var distant = RunManager.Map.Nodes.FirstOrDefault(n =>
                !reachable.Contains(n.Id) && n.Id != RunManager.CurrentNode.Id);

            Assert.IsNotNull(distant, "a realm has rooms the entry cannot reach in one move");

            Named(RealmPaths.ButtonName(distant)).GetComponent<Button>().onClick.Invoke();

            yield return null;

            // Legality is checked BEFORE the walk. Without that the figure
            // would walk all the way to a room MoveTo then refuses to enter,
            // and the refusal would be invisible.
            Assert.IsFalse(_map.IsWalking, "an unreachable room must not start a walk");
            Assert.AreNotEqual(distant.Id, RunManager.CurrentNode.Id);
        }

        // The map has no abandon button: the system menu's Exit pane is the one
        // way out of a descent, and only its hold abandons (AUDIT #219).
        [UnityTest]
        public IEnumerator OnlyTheExitPanesHoldEndsTheRunFromTheMap()
        {
            yield return OpenTheMap();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsTrue(SystemMenuController.OpenFromRoot(menu), "the system menu did not open over the map");
            menu.Select(SystemMenuTab.Exit);
            yield return null;
            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Exit), menu.SelectedIndex);
            Assert.IsTrue(RunManager.HasRun, "opening the Exit pane ended the run");
            CollectionAssert.IsEmpty(_navigated, "opening the Exit pane left the map");

            var hold = menu.GetComponentsInChildren<HoldToConfirm>(includeInactive: true)
                .First(h => h.name == "ExitAbandonHold");
            hold.Begin();
            hold.Advance(hold.Seconds * 0.5f);
            hold.Cancel();
            Assert.IsTrue(RunManager.HasRun, "half a hold abandoned the run");

            hold.Begin();
            hold.Advance(hold.Seconds);
            yield return null;

            Assert.IsFalse(RunManager.HasRun, "the full hold did not end the run");
            CollectionAssert.Contains(_navigated, Navigation.Hub);
        }

        [UnityTest]
        public IEnumerator TheHeaderReadsTheRun()
        {
            yield return OpenTheMap();
            RunManager.BankPayout(77);
            _map.Refresh();

            StringAssert.Contains("77", Named("MapGoldLabel").GetComponent<TMP_Text>().text);
        }
    }
}
