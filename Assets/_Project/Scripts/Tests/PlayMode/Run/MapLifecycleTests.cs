using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The walk, interrupted.
    //
    // MapFlowTests drives one walk from the click to the arrival and asserts
    // where the figure ends up. Everything here is about the window BETWEEN
    // those two moments, which is the only window in this screen where the map
    // and the run disagree about where the party is: the figure has left the
    // room it was in and the run still says it is there, because Arrive is
    // what moves it.
    //
    // That window is where a save, a second click or a scene change can do
    // damage -- and it is a whole second wide, which is not a race so much as
    // an ordinary thing a player can do.
    //
    // scenarios A16, C11, D1 (docs/hunt/SCENARIOS.md). B24 is deliberately
    // absent: MapController declares no OnEnable/OnDisable at all, so there is
    // no restore path to test, and the useful artefact is the argument that
    // nothing disables it -- SCENARIOS.md says so, and a test over an
    // unreachable path is a tautology.
    public class MapLifecycleTests
    {
        private string _root;
        private MapController _map;
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-life-" + System.Guid.NewGuid().ToString("N"));
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
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- A16: a second room clicked mid-walk ---------------------------------

        // ONE WALK AT A TIME, and OnNodePressed's `if (IsWalking) return` is
        // the whole of it. Without the guard the second click would start a
        // second WalkAndArrive from a room the party is halfway out of; the two
        // coroutines would write the same figure on alternating frames, and
        // BOTH would call Arrive at their own ends -- two moves through
        // RunOrchestrator for one player decision, the second of them from a
        // room that is no longer adjacent.
        //
        // MapFlowTests.AnUnreachableRoomIsNotWalkedTo refuses before starting,
        // which is a different guard entirely: that one is about legality, this
        // one is about a walk already being under way.
        [UnityTest]
        public IEnumerator ASecondRoomClickedMidWalkIsIgnoredRatherThanQueued()
        {
            yield return OpenTheMap();

            var choices = RunManager.Choices().ToList();
            Assert.GreaterOrEqual(choices.Count, 2,
                "seed 4242's entry offers fewer than two rooms, so there is no second click to make");

            // The one that is walked to has to be a room that does NOT load a
            // scene on arrival, or the assertions below run against a map that
            // has been torn down.
            var target = choices.FirstOrDefault(IsQuiet);
            Assert.IsNotNull(target,
                "seed 4242's entry offers only fights, so the walk cannot be watched - pick a seed " +
                "that offers a quiet room");

            var other = choices.First(n => n.Id != target.Id);
            int from = RunManager.CurrentNode.Id;

            ClickRoom(target);
            Assert.IsTrue(_map.IsWalking, "fixture: the first click did not start a walk");

            yield return null;
            yield return null;

            // The second decision, arriving while the first is still under way.
            ClickRoom(other);

            Assert.IsTrue(_map.IsWalking, "the second click cancelled the walk that was under way");
            Assert.AreEqual(from, RunManager.CurrentNode.Id,
                "the second click moved the party while the first walk was still running");

            yield return WaitForTheWalk();

            Assert.AreEqual(target.Id, RunManager.CurrentNode.Id,
                "the party arrived at the room clicked SECOND, so the second click started a walk of " +
                "its own from a room they were halfway out of");

            // And the second room is still on offer rather than consumed: one
            // click, one decision.
            Assert.AreNotEqual(other.Id, RunManager.CurrentNode.Id);
        }

        // ---- D1: a save taken mid-walk -------------------------------------------------

        // THE RUN IS THE AUTHORITY ON WHERE THE PARTY IS, and the figure on the
        // map is a drawing of it. Between the click and the arrival those two
        // deliberately disagree -- the figure is somewhere on a trail and
        // RunManager.CurrentNode still names the room it left -- so a save
        // taken in that window has exactly one honest answer, and it is the
        // room the party is IN.
        //
        // The failure this rules out is the expensive kind: a save that
        // recorded the destination would, on reload, put the party in a room
        // whose content never resolved. The room is entered by Arrive and by
        // nothing else, so the arrival would simply have been skipped -- a free
        // step down, or a fight that never happened.
        [UnityTest]
        public IEnumerator ASaveTakenMidWalkRecordsTheRoomThePartyIsInNotTheOneTheyAreWalkingTo()
        {
            yield return OpenTheMap();

            var target = RunManager.Choices().FirstOrDefault(IsQuiet);
            Assert.IsNotNull(target, "seed 4242's entry offers no quiet room to walk to");

            int from = RunManager.CurrentNode.Id;
            Assert.AreNotEqual(from, target.Id, "fixture: the walk goes nowhere");

            ClickRoom(target);
            yield return null;
            yield return null;
            Assert.IsTrue(_map.IsWalking, "fixture: the save is not being taken mid-walk");

            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            Assert.IsTrue(RunManager.HasRun, "the save taken mid-walk came back with no run at all");

            // Read through RunManager.CurrentNode rather than off the raw
            // field: currentNodeId carries -1 as "the entry", and a test that
            // compared the sentinel would be asserting the storage rather than
            // the answer every caller actually gets.
            Assert.AreEqual(from, RunManager.CurrentNode.Id,
                "the save recorded the room the party was WALKING TO rather than the one they were " +
                "in, so a reload here skips the arrival and the room's content never resolves");
        }

        // ---- C11: the scene going out from under a live walk ------------------------------

        // A WALK IS A COROUTINE ON A COMPONENT IN THE SCENE, so a scene change
        // takes it with it. What has to hold is that it takes it QUIETLY -- no
        // write to a destroyed transform, no arrival fired into a torn-down run
        // -- and that the party has not moved, because Arrive is what moves
        // them and Arrive never ran.
        //
        // A PlayMode test fails on an unexpected LogError, so the reproduction
        // is most of the assertion.
        [UnityTest]
        public IEnumerator ASceneLoadedOverALiveWalkLeavesThePartyWhereTheyWere()
        {
            yield return OpenTheMap();

            var target = RunManager.Choices().FirstOrDefault(IsQuiet)
                         ?? RunManager.Choices().FirstOrDefault();
            Assert.IsNotNull(target, "seed 4242's entry offers nowhere to go");

            int from = RunManager.CurrentNode.Id;

            ClickRoom(target);
            yield return null;
            yield return null;
            Assert.IsTrue(_map.IsWalking, "fixture: no walk was under way when the scene changed");

            // Out from under it. Straight through SceneManager rather than
            // Navigation, which this fixture has stubbed to a recorder.
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreEqual(from, RunManager.CurrentNode.Id,
                "the party moved after the map was destroyed, so a walk fired its arrival into a " +
                "scene that no longer exists");
            CollectionAssert.IsEmpty(_navigated,
                "the destroyed walk still asked Navigation to go somewhere");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- fixture ------------------------------------------------------------------------

        private IEnumerator OpenTheMap()
        {
            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
        }

        private static bool IsQuiet(DescentNode node) =>
            node.Type != RoomType.Fight && node.Type != RoomType.EliteFight && node.Type != RoomType.Boss;

        private void ClickRoom(DescentNode node)
        {
            var column = RunManager.Map.AtDepth(node.Depth).ToList();
            int slot = column.FindIndex(n => n.Id == node.Id);

            var go = _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == $"MapNode{MapLayout.IndexFor(node.Depth, slot)}")?.gameObject;

            Assert.IsNotNull(go, $"the map has no node button for room {node.Id}");
            go.GetComponent<Button>().onClick.Invoke();
        }

        // Bounded rather than "yield until", so a walk that never finishes
        // fails this test instead of hanging the whole PlayMode run -- the same
        // shape MapFlowTests uses.
        private IEnumerator WaitForTheWalk()
        {
            for (float waited = 0f; waited < 5f && _map != null && _map.IsWalking; waited += Time.deltaTime)
            {
                yield return null;
            }

            Assert.IsFalse(_map.IsWalking, "the walk should have finished well inside 5 seconds");
        }
    }
}
