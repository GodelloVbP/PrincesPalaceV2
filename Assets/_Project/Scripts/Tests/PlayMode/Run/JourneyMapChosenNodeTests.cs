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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 7: the map's
    // own decision point where there is a REAL choice among reachable nodes,
    // driven by Move rather than accepted as the entry.
    //
    // Reaching an actual second fight would mean playing the first one to a
    // real win through the pad first (segment 8's scope), duplicating that
    // cost for no new claim. So this exercises the map's FIRST decision
    // point instead, seeded so the entry (depth 1, slot 0) is NOT the
    // target: a plain Fight room sits at a later slot, reachable only by
    // moving past the entry, which needs Move-driven aiming at a specific
    // room type.
    public class JourneyMapChosenNodeTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-mapchoice-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            HubController.MotionSpeedMultiplier = 100000f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A realm whose spine carries a plain Fight just past a pocket, so the
        // party chooses between the pocket (choice 0) and that fight (choice
        // 1) -- the map's own real decision, not the single-choice shortcut
        // segment 1 uses. Computed live, never pinned (RealmPaths.SeedWhere).
        private static (ulong seed, int slot) SeedWithAFightPastAPocket() =>
            (RealmPaths.SeedWhere(map => RealmPaths.FightPastAPocket(map) != null, "a plain Fight just past a pocket"),
             1);

        [UnityTest]
        public IEnumerator MovingPastTheEntry_ChoosingAFurtherFightRoom_AdvancesTheRun_LoadsTheNextFight()
        {
            var (seed, targetSlot) = SeedWithAFightPastAPocket();

            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            EngineRoots.GrantToSquad();
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            RunOrchestrator.StartRun(seed);

            // The choice is the pocket or the fight past it, made from the
            // spine room the pocket leaves.
            RealmPaths.StandBefore(RealmPaths.FightPastAPocket(RunManager.Map));

            yield return PressSubmit(); // StartOrResumeRun -> BeginDescentTransition -> EnterTheDescent
            yield return WaitUntil(() =>
            {
                var draft = Node("DraftCard0");
                return draft != null && draft.activeInHierarchy;
            }, 5f, "the gate never opened the relic draft on a fresh run");
            yield return null;

            yield return PressSubmit(); // Select(0)
            yield return MoveDown(); // Descend
            yield return PressSubmit(); // Commit -> Navigation.Go(Map)

            yield return WaitForScene("Map", 5f, "Descend should commit the draft and load the Map");
            yield return null;
            yield return null;
            TakeOverInput();

            var choices = RunManager.Choices().OrderBy(n => n.Offset).ThenBy(n => n.Id).ToList();
            Assert.GreaterOrEqual(choices.Count, 2, "fixture: this seed should offer a real choice");
            var target = choices[targetSlot];
            Assert.AreEqual(RoomType.Fight, target.Type,
                $"fixture: seed {seed} should put a plain Fight room at choice {targetSlot}");

            string entryName = RealmPaths.ButtonName(choices[0]);
            AssertSelectedName(entryName, "the current node's first reachable choice is the declared entry");

            // GENUINE AIMING: Down steps choice to choice, ordered by Slot
            // ascending (MapGamepadNavigationTests' own pinned rule) -- this
            // is the property segment 1's own single-choice seed cannot
            // exercise at all.
            for (int i = 0; i < targetSlot; i++) yield return MoveDown();

            string targetName = RealmPaths.ButtonName(target);
            AssertSelectedName(targetName, $"Move should have aimed the selection at slot {targetSlot}, not the entry");

            int beforeId = RunManager.CurrentNode.Id;
            Assert.AreNotEqual(target.Id, beforeId, "fixture: should not already be standing on the target");

            yield return PressSubmit(); // OnNodePressed -> RunOrchestrator.ArriveAt -> Arrival.Fight -> Navigation.Go(Fight)
            yield return WaitForScene("Fight", 10f,
                "Submit on the chosen node should walk to it and load the Fight scene");
            yield return null;
            yield return null;

            Assert.AreEqual(target.Id, RunManager.CurrentNode.Id,
                "the run's position should have advanced onto the CHOSEN node, not the entry");

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession,
                "the room built no session -- a run is under way and should have built one on arrival");

            AssertFightIsTopWithNoSelection(
                "the Fight scene should load with its own non-selecting NavContext on top and no EventSystem selection");
        }
    }
}
