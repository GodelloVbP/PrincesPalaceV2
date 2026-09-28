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
    // The pool is the interesting part: the tree built every position a leg
    // COULD use, and the controller has to switch on exactly the ones this leg
    // actually has -- and re-anchor each column to its real width.
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

        private IEnumerator OpenTheMap(bool withRun = true)
        {
            if (withRun) RunManager.StartRun(4242);

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
            Assert.Less(shown, MapLayout.Capacity, "a leg does not fill every slot the pool provides");
        }

        [UnityTest]
        public IEnumerator TheRoomsAreJoinedByTrails()
        {
            // What makes this a map rather than a grid of buttons. Without the
            // trails nothing on screen says which room leads to which, and a
            // fork is indistinguishable from two unrelated rooms that happen to
            // share a column.
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

            var descent = RunManager.Map;
            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = descent.AtDepth(depth).ToList();
                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    var button = Named($"MapNode{MapLayout.IndexFor(depth, slot)}").GetComponent<Button>();
                    Assert.AreEqual(reachable.Contains(column[slot].Id), button.interactable,
                        $"depth {depth} slot {slot} offered the wrong thing");
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryRoomStandsInAPaintedClearing()
        {
            // A short column does not sit CENTRED: the canopy backdrop tiles
            // with holes punched in it at three fixed heights, and a
            // centred column of one would stand between two of them.
            yield return OpenTheMap();

            var descent = RunManager.Map;

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = descent.AtDepth(depth).ToList();
                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    var rect = (RectTransform)Named($"MapNode{MapLayout.IndexFor(depth, slot)}").transform;

                    Assert.AreEqual(MapLayout.ClearingRowY[slot], rect.anchoredPosition.y, 0.01f,
                        $"depth {depth} slot {slot} is not standing in its clearing");
                    Assert.AreEqual(MapLayout.ColumnX(depth), rect.anchoredPosition.x, 0.01f,
                        $"depth {depth} slot {slot} is not in its column");
                }
            }
        }

        [UnityTest]
        public IEnumerator TheWoodScrollsSoTheCurrentRoomSitsOnTheLeftClearing()
        {
            // The "everything squished onto one page" fix, asserted where it is
            // actually observable: against the REAL viewport width, which an
            // EditMode solve does not have. Nine columns at the art's own pitch
            // is ~7400 units of content, so the screen is a window over it
            // rather than a track scaled down to fit.
            yield return OpenTheMap();

            var content = (RectTransform)Named("MapContent").transform;
            var viewport = (RectTransform)Named("MapViewport").transform;

            Assert.Greater(content.sizeDelta.x, viewport.rect.width * 2f,
                "the leg is meant to be wider than the window, not squeezed into it");

            var current = RunManager.CurrentNode;
            float onScreen = MapLayout.ColumnX(current.Depth) + content.anchoredPosition.x;

            Assert.AreEqual(MapLayout.FollowOffset, onScreen, 0.01f,
                "the party's own room has to land on the left painted clearing");
        }

        [UnityTest]
        public IEnumerator ThePartysOwnRoomIsMarked()
        {
            yield return OpenTheMap();

            var current = RunManager.CurrentNode;
            var column = RunManager.Map.AtDepth(current.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == current.Id);

            Assert.IsTrue(Named($"MapNode{MapLayout.IndexFor(current.Depth, slot)}Marker").activeSelf);
        }

        [UnityTest]
        public IEnumerator EnteringAFightRoomLoadsTheFight()
        {
            yield return OpenTheMap();

            var fight = RunManager.Choices().FirstOrDefault(n =>
                n.Type == RoomType.Fight || n.Type == RoomType.EliteFight || n.Type == RoomType.Boss);

            // Asserted, not skipped. OpenTheMap seeds the run with 4242, so
            // the leg is the SAME every run. If a generator change makes
            // this seed's entry offer no fight, that is a fixture to
            // re-choose deliberately, not a test to switch off silently.
            Assert.IsNotNull(fight,
                "seed 4242's entry offers no fight room, so this test has nothing to enter - pick a seed that does");

            var column = RunManager.Map.AtDepth(fight.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == fight.Id);
            Named($"MapNode{MapLayout.IndexFor(fight.Depth, slot)}").GetComponent<Button>().onClick.Invoke();

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
            yield return OpenTheMap();

            // A room that leads nowhere else, so the arrival redraws the map
            // rather than loading a fight scene out from under the assertions.
            var quiet = RunManager.Choices().FirstOrDefault(n =>
                n.Type != RoomType.Fight && n.Type != RoomType.EliteFight && n.Type != RoomType.Boss);

            // ASSERTED, for the same reason as EnteringAFightRoomLoadsTheFight:
            // seed 4242 fixes the leg, so this was a deterministic skip wearing
            // the clothes of a conditional one.
            Assert.IsNotNull(quiet,
                "seed 4242's entry offers only fights, so the walk cannot be watched without a scene load - " +
                "pick a seed that offers a quiet room");

            var walker = (RectTransform)Named("MapWalker").transform;
            var startedAt = walker.anchoredPosition;

            var column = RunManager.Map.AtDepth(quiet.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == quiet.Id);
            Named($"MapNode{MapLayout.IndexFor(quiet.Depth, slot)}").GetComponent<Button>().onClick.Invoke();

            // Mid-walk: off the room it left, and not yet at the one it is
            // going to. Asserted because "moves" is the whole feature and a
            // figure that teleports on arrival would pass every other check
            // here.
            yield return null;
            yield return null;
            Assert.AreNotEqual(startedAt.x, walker.anchoredPosition.x, "the figure should be under way");

            yield return WaitForTheWalk();

            var expected = MapWalk.Standing(new UiVec(MapLayout.ColumnX(quiet.Depth), MapLayout.RowY(slot)));
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

            Assert.IsNotNull(distant, "a nine-column leg has rooms the entry cannot reach");

            var column = RunManager.Map.AtDepth(distant.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == distant.Id);
            Named($"MapNode{MapLayout.IndexFor(distant.Depth, slot)}").GetComponent<Button>().onClick.Invoke();

            yield return null;

            // Legality is checked BEFORE the walk. Without that the figure
            // would walk all the way to a room MoveTo then refuses to enter,
            // and the refusal would be invisible.
            Assert.IsFalse(_map.IsWalking, "an unreachable room must not start a walk");
            Assert.AreNotEqual(distant.Id, RunManager.CurrentNode.Id);
        }

        [UnityTest]
        public IEnumerator AbandoningEndsTheRunAndReturnsToTheHub()
        {
            yield return OpenTheMap();

            Named("AbandonRunButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(RunManager.HasRun);
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
