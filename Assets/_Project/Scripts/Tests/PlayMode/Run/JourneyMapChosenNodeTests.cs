using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 7: the map's
    // own decision point where there is a REAL choice among reachable nodes,
    // driven by Move rather than accepted as the entry.
    //
    // DEVIATION FROM THE BRIEF'S OWN "the run's second traversal", NAMED
    // RATHER THAN LITERALLY REPLAYED: reaching an actual second fight would
    // mean playing the first one to a real win through the pad first, which
    // is segment 8's own scope (LevelTheSquadTo, a bounded multi-round
    // fight) -- doing that again here to reach the SAME kind of decision a
    // second time would duplicate that cost for no new claim. What this
    // segment's own sizing note (this plan's status header) actually asks
    // for is the property segment 1 deliberately sidesteps: "there is no
    // independent way for this segment to steer Move presses toward one
    // otherwise" -- so this is the map's FIRST decision point, seeded so the
    // entry (depth 1, slot 0) is NOT the target: a plain Fight room sits at
    // a LATER slot, reachable only by moving past the entry, which is
    // exactly the "genuinely needs Move-driven aiming at a specific room
    // type" segment 1's own shortcut cannot exercise.
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

        // The first seed under 4000 whose leg-0 map offers at least two
        // choices at depth 1 with a plain Fight room at some slot OTHER than
        // the entry (slot 0) -- the map's own real decision, not the
        // single-choice shortcut segment 1 uses. Computed live, never pinned
        // as a magic literal (this project's own established seed-search
        // idiom, e.g. ShopGamepadNavigationTests' SeedWithAShopInTheFirstColumn).
        private static (ulong seed, int slot) SeedWithAPlainFightPastTheEntryAtDepth1()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
                var choices = map.AtDepth(1).ToList();
                if (choices.Count < 2) continue;

                for (int slot = 1; slot < choices.Count; slot++)
                {
                    if (choices[slot].Type == RoomType.Fight) return (seed, slot);
                }
            }

            throw new AssertionException(
                "No seed under 4000 offers a plain Fight room past the map's own entry at depth 1.");
        }

        [UnityTest]
        public IEnumerator MovingPastTheEntry_ChoosingAFurtherFightRoom_AdvancesTheRun_LoadsTheNextFight()
        {
            var (seed, targetSlot) = SeedWithAPlainFightPastTheEntryAtDepth1();

            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            RunOrchestrator.StartRun(seed);

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

            var choices = RunManager.Choices();
            Assert.GreaterOrEqual(choices.Count, 2, "fixture: this seed should offer a real choice at depth 1");
            var target = choices[targetSlot];
            Assert.AreEqual(RoomType.Fight, target.Type,
                $"fixture: seed {seed} should put a plain Fight room at depth 1, slot {targetSlot}");

            string entryName = "MapNode" + MapLayout.IndexFor(1, 0);
            AssertSelectedName(entryName, "the current node's first reachable choice is the declared entry");

            // GENUINE AIMING: Down steps choice to choice, ordered by Slot
            // ascending (MapGamepadNavigationTests' own pinned rule) -- this
            // is the property segment 1's own single-choice seed cannot
            // exercise at all.
            for (int i = 0; i < targetSlot; i++) yield return MoveDown();

            string targetName = "MapNode" + MapLayout.IndexFor(target.Depth, target.Slot);
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
