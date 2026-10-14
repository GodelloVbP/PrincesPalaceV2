using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 3: Map's node
    // buttons as the Graph shape section 5 names -- UiNavLinkBuilder's own
    // header already says there is no Graph group kind, "Map's Graph shape
    // has no groups at all and is entirely this loop [Links]", so
    // MapController.RefreshNavLinks is EXPLICIT LINKS ONLY, rewritten every
    // Refresh() off RunManager.Choices().
    //
    // Pinned against seed 4242 (the same seed CancelOpensSystemMenuTests'
    // own MapCancel test already uses). Its first realm, printed once by a
    // throwaway diagnostic: the entry (id 0), then spine room id 1 at offset 1
    // with a pocket leaving it. A node's pool slot is its id, so the fixture
    // stands the party on id 1 (MapNode1) and the choices are the pocket's
    // anchor, id 2 (MapNode2, hanging left of the spine), and spine room
    // id 3 (MapNode3, on the spine): left to right, in that order. Literal
    // from here on, never recomputed from RunManager.Choices() in the
    // assertions themselves (CLAUDE.md gotcha 5's formula-pinning rule).
    public class MapGamepadNavigationTests
    {
        private string _root;
        private MapController _map;
        private ScriptedBaseInput _input;

        // A throwaway save root -- RunManager.StartRun writes real save
        // state, and this suite has no business touching the player's own
        // saves (SaveSlotFlowTests' own header makes the identical point).
        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;

            // The Submit test's arrival can land on a Fight room -- stubbed
            // rather than actually loaded, same reasoning as
            // SaveSlotFlowTests' own Continue tests (loading a scene mid-test
            // destroys the objects the assertions are about).
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE MAP IS SHARED ACROSS THIS FIXTURE (SharedScene). Every test but
        // the Submit walk only moves the selection, so the reset is the same
        // seed-4242 run started again and repainted through the map's own
        // public Refresh, plus NavSceneReuse's input and focus-memory reset.
        // The walk is the one thing Refresh cannot put back (the party token
        // and the pan move with it), so that test marks the scene dirty.
        private IEnumerator LoadTheMap()
        {
            RunManager.ResetForTests();
            RunManager.StartRun(4242);
            Assert.IsTrue(RunManager.MoveTo(1), "fixture: seed 4242's spine room 1 follows the entry");
            RunManager.ClearCurrentRoom();

            yield return SharedScene.Ensure("Map");

            _map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the map scene has no MapController");

            // Always repainted: a map left in the overview by the previous
            // test must come back, and the opening shot must not be running.
            if (_map.InOverview) _map.SetOverview(false);
            if (NavSceneReuse.Reused) _map.Refresh();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();

            yield return null;
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
        }

        private static GameObject Node(string name) => GameObject.Find(name);

        [UnityTest]
        public IEnumerator EntryIsTheLeftmostChoice_MapNode2()
        {
            SharedScene.MarkDirty("asserts the entry a freshly loaded map selects, not one a reset put back");
            yield return LoadTheMap();

            Assert.AreEqual(Node("MapNode2"), EventSystem.current.currentSelectedGameObject,
                "the current node's leftmost reachable choice is the declared entry -- " +
                "current itself is never interactable and would be a dead Submit");
        }

        [UnityTest]
        public IEnumerator Down_FromCurrent_ReachesTheFirstChoice()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode1"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("MapNode2"), EventSystem.current.currentSelectedGameObject,
                "Down from current should reach its first reachable choice");
        }

        [UnityTest]
        public IEnumerator Right_FromTheFirstChoice_ReachesTheSecond()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode2"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("MapNode3"), EventSystem.current.currentSelectedGameObject,
                "Right should step choice to choice in screen order (left to right)");
        }

        // A separate test rather than a third press chained onto the one
        // above, so each claim reads alone. (Chaining would be safe: the 0.1s
        // uGUI re-press gate is lifted by NavSceneReuse.TakeOverInput.)
        [UnityTest]
        public IEnumerator Left_FromTheSecondChoice_ReturnsToTheFirst()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode3"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("MapNode2"), EventSystem.current.currentSelectedGameObject,
                "Left from the second choice should step back to the first");
        }

        [UnityTest]
        public IEnumerator Up_FromAChoice_ReturnsToCurrent()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode3"));
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(Node("MapNode1"), EventSystem.current.currentSelectedGameObject,
                "Up from any choice should return to current, the one thing a Graph transition undoes");
        }

        // The property that makes the pad usable at all: from where the party
        // stands, following the d-pad's own links reaches EVERY room it can
        // walk to. Walked over the live Selectables' navigation, so a link the
        // controller forgot to write fails here whatever the tests above pin.
        [UnityTest]
        public IEnumerator EveryReachableChoiceCanBeReachedWithTheDpad()
        {
            yield return LoadTheMap();

            var reachable = ReachableByDpad(Node("MapNode1"));
            var choices = RunManager.Choices();
            foreach (var choice in choices)
            {
                Assert.IsTrue(reachable.Contains(Node("MapNode" + choice.Id)),
                    $"the d-pad cannot reach choice {choice.Id}");
            }

            Assert.GreaterOrEqual(choices.Count, 2, "vacuity: the fixture offers a real choice");
        }

        // Y: every room is selectable and the d-pad reaches all of them, with
        // the camera zoomed out; pressing one walks nowhere.
        [UnityTest]
        public IEnumerator Overview_LetsTheDpadReachEveryRoom_AndWalksNowhere()
        {
            yield return LoadTheMap();
            SharedScene.MarkDirty("toggles the overview and presses a room in it");

            _map.ToggleOverview();
            yield return null;
            yield return null;
            Assert.IsTrue(_map.InOverview);

            var shown = _map.GetComponentsInChildren<Button>(includeInactive: false)
                .Where(b => b.name.StartsWith("MapNode") && b.gameObject.activeInHierarchy)
                .ToList();
            Assert.AreEqual(RunManager.Map.Nodes.Count, shown.Count);

            var reachable = ReachableByDpad(Node("MapNode1"));
            foreach (var button in shown)
            {
                Assert.IsTrue(reachable.Contains(button.gameObject), $"{button.name} is out of the d-pad's reach");
            }

            // Submit on a room that is a choice in play still walks nowhere.
            EventSystem.current.SetSelectedGameObject(Node("MapNode2"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null;
            Assert.IsFalse(_map.IsWalking, "a press in the overview is read-only");
            Assert.AreEqual(1, RunManager.CurrentNode.Id);

            _map.ToggleOverview();
            yield return null;
            Assert.IsFalse(_map.InOverview);
        }

        // Every GameObject the d-pad can arrive at from `start`, by following
        // the explicit navigation the map wrote.
        private static HashSet<GameObject> ReachableByDpad(GameObject start)
        {
            var seen = new HashSet<GameObject> { start };
            var queue = new Queue<GameObject>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var navigation = queue.Dequeue().GetComponent<Selectable>().navigation;
                foreach (var next in new[] { navigation.selectOnUp, navigation.selectOnDown,
                             navigation.selectOnLeft, navigation.selectOnRight })
                {
                    if (next != null && seen.Add(next.gameObject)) queue.Enqueue(next.gameObject);
                }
            }

            return seen;
        }

        // The required action: Submit on the entry should choose that room --
        // RunManager.MoveTo through MapController.OnNodePressed's existing
        // click path, exactly as a mouse click already does, walk animation
        // and all.
        [UnityTest]
        public IEnumerator Submit_OnTheEntry_WalksToThatNode_ExactlyOnce()
        {
            yield return LoadTheMap();
            SharedScene.MarkDirty("walks the party to another node, which Refresh does not walk back");
            EventSystem.current.SetSelectedGameObject(Node("MapNode2"));
            yield return null;

            Assert.AreEqual(1, RunManager.CurrentNode.Id, "should still be standing on room 1 before Submit");

            _input.SubmitDown = true;
            yield return DriveFrame();

            int frames = 0;
            while (_map.IsWalking && frames < 3000)
            {
                yield return null;
                frames++;
            }

            Assert.IsFalse(_map.IsWalking, "the walk should have finished within 3000 frames");
            Assert.AreEqual(2, RunManager.CurrentNode.Id,
                "Submit on MapNode2 (id 2) should have moved the party there exactly once");
        }
    }
}
