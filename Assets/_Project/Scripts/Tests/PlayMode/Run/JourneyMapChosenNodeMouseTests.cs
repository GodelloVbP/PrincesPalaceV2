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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 7's mouse-only
    // regression. The pad's own point is that Move has to AIM at a node past
    // the entry (JourneyMapChosenNodeTests' own header) -- a mouse has no
    // such thing to prove: it clicks whichever node it wants directly, entry
    // or not. So the claim this file proves is narrower and still real: a
    // click on a REACHABLE node that is not the current node still resolves
    // through OnNodePressed -> RunOrchestrator.ArriveAt exactly the same way,
    // walking there and loading the next fight -- clicking is never
    // shortcut-only to the entry.
    public class JourneyMapChosenNodeMouseTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-mapchoice-mouse-" + System.Guid.NewGuid().ToString("N"));
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

        private static (ulong seed, int slot) SeedWithAPlainFightPastTheEntryAtDepth1()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
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
        public IEnumerator ClickingAFurtherFightRoom_AdvancesTheRun_LoadsTheNextFight_MouseOnly()
        {
            var (seed, targetSlot) = SeedWithAPlainFightPastTheEntryAtDepth1();

            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
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
            }, 5f, "clicking the gate never opened the relic draft on a fresh run");
            yield return null;

            yield return Click(Node("DraftCard0"));
            yield return Click(Node("DraftDescendButton"));

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
            var choices = RunManager.Choices();
            Assert.GreaterOrEqual(choices.Count, 2, "fixture: this seed should offer a real choice at depth 1");
            var target = choices[targetSlot];
            Assert.AreEqual(RoomType.Fight, target.Type,
                $"fixture: seed {seed} should put a plain Fight room at depth 1, slot {targetSlot}");

            int beforeId = RunManager.CurrentNode.Id;
            Assert.AreNotEqual(target.Id, beforeId, "fixture: should not already be standing on the target");

            string targetName = "MapNode" + MapLayout.IndexFor(target.Depth, target.Slot);

            // THE WHOLE CLAIM: click a node that is NOT the current one and
            // NOT the declared entry -- no Move-driven aiming is involved at
            // all, only the click itself.
            yield return Click(Node(targetName));
            yield return WaitForScene("Fight", 10f, "clicking the chosen node should walk to it and load the Fight scene");
            yield return null;
            yield return null;

            Assert.AreEqual(target.Id, RunManager.CurrentNode.Id,
                "the run's position should have advanced onto the CLICKED node, not the entry");

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession,
                "the room built no session -- a run is under way and should have built one on arrival");

            AssertFightIsTopWithNoSelection(
                "the Fight scene should load with its own non-selecting NavContext on top and no EventSystem " +
                "selection, mouse-only same as on the pad");
        }
    }
}
