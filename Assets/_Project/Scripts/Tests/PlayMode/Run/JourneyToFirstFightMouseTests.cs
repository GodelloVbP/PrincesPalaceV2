using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: the mouse-only
    // regression for segment 1, replaying JourneyToFirstFightTests' journey
    // with the mouse only. Shares that fixture's own assertion helpers
    // rather than restating their bodies -- only the interaction shape
    // differs between the two files.
    //
    // A mouse click reaches a control directly, so every Move-then-Submit
    // walk in the pad file collapses to one Click on the final target.
    // Entry selection right after a screen opens is not asserted here,
    // since that is a fact about a stick player who has pressed nothing
    // yet; this file's assertions pick up once a click has happened
    // (model state and the visible result).
    public class JourneyToFirstFightMouseTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-firstfight-mouse-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            HubController.MotionSpeedMultiplier = 100000f;
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Identical search to the pad file's own SeedWithAPlainFightAtDepth1Entry
        // -- this segment's own claim is about the INPUT MODE, not a different
        // map shape, so the fixture precondition stays the same seed search.
        private static ulong SeedWithAPlainFightAtDepth1Entry()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
                var entry = map.AtDepth(1).FirstOrDefault();
                if (entry != null && entry.Type == RoomType.Fight) return seed;
            }

            throw new AssertionException(
                "No seed under 4000 puts a plain Fight room at the map's own entry (depth 1, slot 0).");
        }

        [UnityTest]
        public IEnumerator MainMenuToNewGame_ThroughTheHubGateAndTheDraft_ReachesTheFirstFight_MouseOnly()
        {
            ulong seed = SeedWithAPlainFightAtDepth1Entry();

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            yield return null;

            // No entry-selection assertion here -- see this file's own header.
            yield return Click(Node("PlayButton"));
            yield return null; // the save-slot panel opens the frame after the click resolves
            yield return Click(Node("Slot0Button")); // Choose(0) -> EnterSlot(0) -> Navigation.Go(Hub), a REAL load

            yield return WaitForScene("Hub", 5f, "clicking an empty slot should enter it and load the Hub");
            yield return null;
            yield return null;
            EngineRoots.GrantToSquad();
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            RunOrchestrator.StartRun(seed);

            yield return Click(Node("StartRunGate")); // StartOrResumeRun -> BeginDescentTransition -> EnterTheDescent
            yield return WaitUntil(() =>
            {
                var draft = Node("DraftCard0");
                return draft != null && draft.activeInHierarchy;
            }, 5f, "clicking the gate never opened the relic draft on a run that has not drafted one yet");
            yield return null;

            yield return Click(Node("DraftCard0")); // Select(0) -- takes the entry card, exactly once
            yield return Click(Node("DraftDescendButton")); // Commit() -> FinishDraft, then Navigation.Go(Map)

            Assert.AreEqual(1, RunManager.Run.relicIds.Count,
                "clicking the entry card then Descend should have selected it exactly once and committed it onto the run");

            yield return WaitForScene("Map", 5f, "Descend should commit the draft and load the Map");
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var entryNode = RunManager.Map.AtDepth(1).First();
            Assert.AreEqual(RoomType.Fight, entryNode.Type,
                $"fixture: seed {seed} should put a plain Fight room at the map's own entry (depth 1, slot 0)");

            string entryName = "MapNode" + MapLayout.IndexFor(entryNode.Depth, entryNode.Slot);

            yield return Click(Node(entryName)); // OnNodePressed -> RunOrchestrator.ArriveAt -> Arrival.Fight -> Navigation.Go(Fight)
            yield return WaitForScene("Fight", 10f, "clicking the entry should walk to it and load the Fight scene");
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession,
                "the room built no session -- FightBootstrap.Start should have called RunOrchestrator.BuildFight() " +
                "since a run is under way");

            AssertFightIsTopWithNoSelection(
                "the Fight scene should load with its own non-selecting NavContext on top and no EventSystem " +
                "selection, mouse-only same as on the pad");
        }
    }
}
