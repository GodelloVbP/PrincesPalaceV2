using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
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
        public IEnumerator AColumnIsCentredOnItsRealWidth()
        {
            // The tree placed every node at the position a FULL column would put
            // it in; the controller re-anchors through the same MapLayout.RowY.
            // A two-room column has to sit centred rather than leaving a gap
            // where the third would have been.
            yield return OpenTheMap();

            var descent = RunManager.Map;
            var narrow = Enumerable.Range(0, MapLayout.Columns)
                .Select(d => descent.AtDepth(d).ToList())
                .FirstOrDefault(c => c.Count == 1);

            Assert.IsNotNull(narrow, "every leg has at least one single-room column");

            var node = narrow[0];
            var rect = (RectTransform)Named($"MapNode{MapLayout.IndexFor(node.Depth, 0)}").transform;

            Assert.AreEqual(0f, rect.anchoredPosition.y, 0.01f,
                "a column of one sits on the spine");
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

            if (fight == null)
            {
                Assert.Ignore("this leg offers no fight from the entry");
                yield break;
            }

            var column = RunManager.Map.AtDepth(fight.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == fight.Id);
            Named($"MapNode{MapLayout.IndexFor(fight.Depth, slot)}").GetComponent<Button>().onClick.Invoke();

            CollectionAssert.Contains(_navigated, Navigation.Fight);
            Assert.AreEqual(fight.Id, RunManager.CurrentNode.Id, "the party moved before the scene changed");
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
