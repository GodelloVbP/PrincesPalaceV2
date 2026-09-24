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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 1: Main Menu
    // -> New Game -> the first fight -- section 13's own play-test checklist,
    // first four bullets, automated end to end through the real production
    // dispatcher and REAL scene loads (Navigation.Go's own unstubbed
    // SceneManager.LoadScene -- Navigation.LoadOverride is never set in this
    // file, unlike every single-screen gamepad-nav test in this project).
    //
    // THE REAL FLOW IS LONGER THAN "Move to New Game, Submit": a fresh save
    // has not drafted a relic yet, so HubController.EnterTheDescent always
    // opens the relic draft between the gate and the Map -- it is not a
    // screen this journey can skip (RelicDraftController's own header: "a
    // draft you can navigate around is not a draft", so it must be spent,
    // never merely closed). This segment drives all of it: Play -> an empty
    // save slot -> Hub -> the descent gate -> the relic draft -> Descend ->
    // the Map -> a plain Fight room's own entry node -> the Fight scene.
    //
    // THE SEED IS CHOSEN, NOT LEFT TO HubController.EnterTheDescent's OWN
    // RunManager.NewSeed(): this suite drives every press through
    // ScriptedBaseInput and never teleports EventSystem selection or calls a
    // controller method directly, so reaching a KNOWN room type on the Map
    // needs the map's own ENTRY (the current node's first reachable choice,
    // MapGamepadNavigationTests' own pinned rule) to already BE a plain
    // Fight room -- there is no independent way for this segment to steer
    // Move presses toward one otherwise (that is segment 7's own point, on
    // the run's SECOND traversal). SeedWithAPlainFightAtDepth1Entry finds
    // one live, the same shape ShopGamepadNavigationTests' own
    // SeedWithAShopInTheFirstColumn already uses for the identical reason.
    //
    // THE SEED IS APPLIED AFTER SaveSlotManager.EnterSlot(0), NOT BEFORE: a
    // run started before the slot is entered is exactly the "abandoned by a
    // crash" case SaveSlotManager.EnterSlot's own SettleOnOpening exists to
    // end (AUDIT #117) -- seeding earlier would have this segment's own
    // fixture immediately settle the run it just started, on the very next
    // Submit. RunOrchestrator.StartRun runs directly against the save once
    // the Hub has loaded, which is squarely the "set up state through the
    // orchestrator before the segment's own interaction starts" allowance --
    // pressing the gate itself stays entirely on the pad.
    public class JourneyToFirstFightTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-firstfight-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            // BeginDescentTransition strings three real-time coroutine phases
            // together; stepping between them still costs Unity's coroutine
            // driver a real engine frame apiece however high this goes
            // (RelicDraftGamepadNavigationTests' own comment on why this is
            // set absurdly high rather than merely fast).
            HubController.MotionSpeedMultiplier = 100000f;
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // The FIRST seed under 4000 whose leg-0 map puts a plain Fight room
        // (never Elite/Boss -- this segment wants the ordinary first fight,
        // not the hardest room the generator could offer) at depth 1's own
        // entry slot (DescentMap.AtDepth already orders by Slot ascending,
        // so .First() IS the entry MapGamepadNavigationTests' own comment
        // names). Computed live rather than pinned as a magic literal --
        // ShopGamepadNavigationTests' SeedWithAShopInTheFirstColumn is the
        // precedent for exactly this shape.
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
        public IEnumerator MainMenuToNewGame_ThroughTheHubGateAndTheDraft_ReachesTheFirstFight()
        {
            ulong seed = SeedWithAPlainFightAtDepth1Entry();

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            // Set BEFORE letting any frame run -- MainMenuGamepadNavigationTests'
            // own LoadMenu makes the identical call for the identical reason
            // (a previously loaded scene's EventSystem can still be current).
            TakeOverInput();
            yield return null;
            yield return null;

            AssertSelectedName("PlayButton", "with no save to continue, Play is the entry");

            yield return PressSubmit();
            yield return null; // the reselection onto Slot0Button is next frame
            AssertSelectedName("Slot0Button",
                "Submit on Play should open the save-slot panel with its first slot as entry");

            yield return PressSubmit(); // SaveSlotController.Choose(0) -> EnterSlot(0) -> Navigation.Go(Hub), a REAL load
            yield return WaitForScene("Hub", 5f, "Submit on an empty slot should enter it and load the Hub");
            yield return null;
            yield return null; // Start() runs one frame after activation
            TakeOverInput();

            AssertSelectedName("StartRunGate",
                "the gate is the hub's stated primary action and its declared entry");

            // Prepared here, not driven: see this file's own header for why
            // seeding happens now rather than before EnterSlot, and why it is
            // orchestrator state rather than a pad press.
            RunOrchestrator.StartRun(seed);

            yield return PressSubmit(); // StartOrResumeRun -> BeginDescentTransition -> EnterTheDescent
            yield return WaitUntil(() =>
            {
                var draft = Node("DraftCard0");
                return draft != null && draft.activeInHierarchy;
            }, 5f, "the gate never opened the relic draft on a run that has not drafted one yet");
            yield return null;

            AssertSelectedName("DraftCard0", "RefreshNavigation's own entry is the first card");

            yield return PressSubmit(); // Select(0) -- takes the entry card, exactly once
            yield return MoveDown(); // Down from a card reaches Descend, RefreshNavigation's own explicit link
            AssertSelectedName("DraftDescendButton", "should be standing on Descend before committing the draft");

            yield return PressSubmit(); // Commit() -> FinishDraft, then Finished() -> Navigation.Go(Map), a REAL load

            // Read straight after the press, before the scene changes:
            // Commit() writes relicIds synchronously (RelicDraftController's
            // own TakeRelic call), the same ordering
            // RelicDraftGamepadNavigationTests' own Submit test pins.
            Assert.AreEqual(1, RunManager.Run.relicIds.Count,
                "Submit on the entry card should have selected it exactly once, and Descend should have " +
                "committed it onto the run");

            yield return WaitForScene("Map", 5f, "Descend should commit the draft and load the Map");
            yield return null;
            yield return null;
            TakeOverInput();

            var entryNode = RunManager.Map.AtDepth(1).First();
            Assert.AreEqual(RoomType.Fight, entryNode.Type,
                $"fixture: seed {seed} should put a plain Fight room at the map's own entry (depth 1, slot 0)");

            // The node's GameObject name is "MapNode" + MapLayout.IndexFor
            // (depth, slot) -- the screen's own layout index, NOT node.Id
            // (MapScreen.cs's BuildNode/BuildButton call sites).
            string entryName = "MapNode" + MapLayout.IndexFor(entryNode.Depth, entryNode.Slot);
            AssertSelectedName(entryName,
                "the current node's first reachable choice is the declared entry");

            yield return PressSubmit(); // OnNodePressed -> RunOrchestrator.ArriveAt -> Arrival.Fight -> Navigation.Go(Fight)
            yield return WaitForScene("Fight", 10f,
                "Submit on the entry should walk to it and load the Fight scene");
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession,
                "the room built no session -- FightBootstrap.Start should have called RunOrchestrator.BuildFight() " +
                "since a run is under way");

            AssertFightIsTopWithNoSelection(
                "the Fight scene should load with its own non-selecting NavContext on top and no EventSystem selection");
        }
    }
}
