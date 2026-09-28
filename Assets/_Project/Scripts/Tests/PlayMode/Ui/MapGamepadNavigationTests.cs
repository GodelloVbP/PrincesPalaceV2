using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 3: Map's node
    // buttons as the Graph shape section 5 names -- UiNavLinkBuilder's own
    // header already says there is no Graph group kind, "Map's Graph shape
    // has no groups at all and is entirely this loop [Links]", so
    // MapController.RefreshNavLinks is EXPLICIT LINKS ONLY, rewritten every
    // Refresh() off RunManager.Choices() (`MapController.cs:245`).
    //
    // Pinned against seed 4242 (the same seed CancelOpensSystemMenuTests'
    // own MapCancel test already uses): a throwaway diagnostic run (deleted
    // once read, never committed) printed the entry node at depth 0 slot 0
    // (MapNode0, MapLayout.Rows == 3 so IndexFor(0,0) == 0) with three
    // choices at depth 1, slots 0/1/2, ids 1/2/3 -- MapNode3/MapNode4/MapNode5.
    // Literal from here on, never recomputed from RunManager.Choices() in the
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

            yield return SharedScene.Ensure("Map");

            _map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_map, "the map scene has no MapController");
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
        public IEnumerator EntryIsTheFirstReachableChoice_MapNode3()
        {
            SharedScene.MarkDirty("asserts the entry a freshly loaded map selects, not one a reset put back");
            yield return LoadTheMap();

            Assert.AreEqual(Node("MapNode3"), EventSystem.current.currentSelectedGameObject,
                "the current node's first reachable choice (depth 1, slot 0) is the declared entry -- " +
                "current itself is never interactable and would be a dead Submit");
        }

        [UnityTest]
        public IEnumerator Right_FromCurrent_ReachesTheFirstChoice()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode0"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("MapNode3"), EventSystem.current.currentSelectedGameObject,
                "Right from current should reach its first reachable choice");
        }

        [UnityTest]
        public IEnumerator Down_FromTheFirstChoice_ReachesTheSecond()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode3"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("MapNode4"), EventSystem.current.currentSelectedGameObject,
                "Down should step choice to choice, ordered by Slot ascending (top to bottom on screen)");
        }

        // A separate test rather than a third press chained onto the one
        // above, so each claim reads alone. (Chaining would be safe: the 0.1s
        // uGUI re-press gate is lifted by NavSceneReuse.TakeOverInput.)
        [UnityTest]
        public IEnumerator Left_FromTheSecondChoice_ReturnsToCurrent()
        {
            yield return LoadTheMap();
            EventSystem.current.SetSelectedGameObject(Node("MapNode4"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("MapNode0"), EventSystem.current.currentSelectedGameObject,
                "Left from any choice should return to current, the one thing a Graph transition undoes");
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
            EventSystem.current.SetSelectedGameObject(Node("MapNode3"));
            yield return null;

            Assert.AreEqual(0, RunManager.CurrentNode.Id, "should still be standing at the entry before Submit");

            _input.SubmitDown = true;
            yield return DriveFrame();

            int frames = 0;
            while (_map.IsWalking && frames < 3000)
            {
                yield return null;
                frames++;
            }

            Assert.IsFalse(_map.IsWalking, "the walk should have finished within 3000 frames");
            Assert.AreEqual(1, RunManager.CurrentNode.Id,
                "Submit on MapNode3 (depth 1, slot 0, id 1) should have moved the party there exactly once");
        }
    }
}
