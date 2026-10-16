using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 8: a real
    // pad-driven WIN, and the two reward screens it opens onto.
    //
    // Winning a fight opens the RECKONING (an item pick), proven at the
    // screen level by ReckoningGamepadNavigationTests. The REWARD TRACK
    // (RewardTrackController) is a separate screen reached from the System
    // Menu's Character tab, not opened automatically by a win.
    //
    // This class proves both, as two separate journeys: the fight-to-
    // Reckoning path stays one continuous session (win, take or skip,
    // Continue, land on the Map); the reward track is reconstructed
    // separately from the Hub with a level/claimed state built to owe
    // something, since crediting that debt during the fight above would
    // mean pinning an exact experience-to-level threshold this file has no
    // business asserting.
    public class JourneyVictoryToRewardScreensTests : JourneyFixture
    {
        // FightSettlementTests' own seed -- "a run known to generate a leg
        // with a reachable plain fight room, an elite and a boss."
        private const ulong Seed = 639228196442867409UL;

        // A round cap, not just a time cap -- the master brief's own
        // "capped at a literal round count" -- on top of the real-time
        // deadline every other journey wait already uses.
        private const int RoundCap = 30;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-victory-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            RewardTrackController.SpeedMultiplier = 40f;

            // PINNED SOLO -- FightSettlementTests' own header states why a
            // squad of three each landing a hit can distort the outcome a
            // single dial (level) is meant to control cleanly.
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            RewardTrackController.SpeedMultiplier = 1f;
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // FightSettlementTests.LevelTheSquadTo, reused verbatim rather than
        // re-derived: a real level-90 build (stats, not just the field),
        // which is the established, sanctioned lever this project's own
        // suite already uses to make a floor-1 room a certain win.
        private static void LevelTheSquadTo(int level)
        {
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.ClaimTrackRewards(RewardTracks.For(character), character.level);

                int scoreIndex = 0;
                while (character.unspentStatPoints > 0)
                {
                    var score = AbilityScores.All[scoreIndex % AbilityScores.All.Length];
                    if (!character.Invest(score)) break;
                    scoreIndex++;
                }
            }
        }

        private static RealmNode APlainFightRoom()
        {
            var choices = RunManager.Choices();
            Assert.IsNotEmpty(choices, "the leg offered nowhere to go");
            return choices.FirstOrDefault(n => n.Type == RoomType.Fight) ?? choices[0];
        }

        [UnityTest]
        public IEnumerator WinningARealFight_OpensTheReckoning_TakesOrSkipsAnOffer_ContinuesOntoTheMap()
        {
            SaveSlotManager.EnterSlot(0);
            RunManager.StartRun(Seed);
            LevelTheSquadTo(90);

            var room = APlainFightRoom();
            Assert.IsTrue(RunManager.MoveTo(room.Id), "could not move into the fight room");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the room built no session");
            var session = fight.Session;

            for (int i = 0; i < 120 && !session.IsPlayerTurn && !session.IsOver; i++) yield return null;

            TakeOverInput();
            yield return null;

            bool settled = false;
            fight.FightEnded += _ => settled = true;

            int rounds = 0;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy || !session.IsPlayerTurn) { yield return null; continue; }

                Assert.Less(rounds, RoundCap,
                    $"the fight has not ended after {RoundCap} rounds -- a level 90 solo squad against a " +
                    "floor-1 room should have won well before this cap");
                rounds++;

                yield return PressSubmit(); // ATTACK at Root skips straight to targeting
                yield return MoveDown(); // hover the first living enemy
                yield return PressSubmit(); // confirm the attack
            }

            Assert.IsTrue(session.IsOver,
                $"the fight never ended: IsBusy={fight.IsBusy}, IsPlayerTurn={session.IsPlayerTurn}");
            Assert.IsTrue(session.PlayerWon, "fixture: a level 90 solo squad should have won a floor-1 room");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "the fight is over and FightEnded never fired, so nothing settled it onto the run");

            yield return WaitUntil(() =>
            {
                var offer = Node("ReckoningOffer0");
                var continueButton = Node("ReckoningContinueButton");
                return (offer != null && offer.activeInHierarchy) ||
                       (continueButton != null && continueButton.activeInHierarchy);
            }, 5f, "a won fight should have opened the Reckoning");
            yield return null;
            TakeOverInput();

            var offer0 = Node("ReckoningOffer0");
            if (offer0 != null && offer0.activeInHierarchy)
            {
                AssertSelectedName("ReckoningOffer0", "the Reckoning opens on the choice, entry is the first card");

                yield return PressSubmit(); // takes the entry card, exactly once
                yield return null; // the reselection onto the page's way off follows the choice

                AssertSelectedName("ReckoningLeaveButton",
                    "the cards are inert once the choice is spent, so the dispatcher should have moved on to the page's way off");

                yield return PressSubmit(); // moves on to the summary
                yield return new WaitForSecondsRealtime(0.5f);

                AssertSelectedName("ReckoningContinueButton",
                    "the last page spent sweeps to the summary, whose entry is Continue");
            }
            else
            {
                AssertSelectedName("ReckoningContinueButton",
                    "no offers this roll -- Show() goes straight to the summary");
            }

            yield return PressSubmit(); // Dismissed -> LeaveFight -> Navigation.Go(Map), a REAL load since the run is still standing

            yield return WaitForScene("Map", 5f,
                "Continue on the Reckoning should raise Dismissed and load the Map -- the run is still standing " +
                "after a win (RunOrchestrator.SettleFight only ends it on a loss)");
            yield return null;
            yield return null;
            TakeOverInput();

            AssertTopIsNotFight("the Map should not carry Fight's own non-selecting context");
            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.IsNull(selected, "landing on the Map should leave nothing selected");
        }

        [UnityTest]
        public IEnumerator ReachingTheRewardTrack_ThroughTheSystemMenu_ScrollsAndCollectsExactlyOnce_CancelCloses()
        {
            SaveSlotManager.EnterSlot(0);
            MenuMemory.Clear();

            // OWED > 0: earned level 12, paid through 10 -- the same shape
            // RewardTrackGamepadNavigationTests proves at the screen level;
            // this segment's own claim is that the SAME real Hub scene reaches
            // it end to end.
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = 12;
                character.claimedTrackLevel = 10;
                character.unspentStatPoints = 0;
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");

            yield return PressSystemMenu(); // opens the menu on its default tab, Character outside a run
            Assert.IsTrue(menu.IsOpen, "Start should open the system menu from the hub");
            AssertSelectedName("DossierSection0", "opening should land on the rail's selected section, LOADOUT");

            // The rail is a list, and the section follows focus down it.
            yield return MoveDown();
            yield return MoveDown();
            yield return MoveDown();
            AssertSelectedName("DossierSection3", "three Downs should reach the REWARD TRACK row");

            // The section's own fly-in glides toward the current level as it
            // shows -- settled, so the ScrollTo assertion below is not racing it.
            yield return new WaitForSecondsRealtime(0.5f);

            // Right enters the track on the CURRENT level's disc: 12, the
            // fixture's own literal.
            const int currentLevel = 12;
            yield return MoveRight();
            AssertSelectedName($"TrackDot{currentLevel}", "Right from the rail should enter on the current level's disc");

            var content = GameObject.Find("TrackContent")?.GetComponent<RectTransform>();
            var viewport = GameObject.Find("TrackViewport")?.GetComponent<RectTransform>();
            Assert.IsNotNull(content, "the reward track has no content rect");
            Assert.IsNotNull(viewport, "the reward track has no viewport rect");

            yield return MoveRight(); // one Right press on the rail
            AssertSelectedName($"TrackDot{currentLevel + 1}", "one Right press on the rail should move to the next disc");

            float expected = RewardTrackLayout.ScrollFor(currentLevel + 1, viewport.rect.width);
            Assert.AreEqual(expected, content.anchoredPosition.x, 1f,
                "selecting the next dot should have scrolled the rail to it via ScrollTo, the same call a " +
                "mouse hover already makes");

            yield return MoveUp(); // Up from a disc reaches COLLECT while something is owed
            AssertSelectedName("TrackCollectButton", "Up from the rail should reach the collect button");

            var character0 = SaveSlotManager.CurrentSave.ActiveSquad().First();
            yield return PressSubmit(); // claims exactly once, through the earned level

            Assert.AreEqual(12, character0.claimedTrackLevel,
                "Submit on the collect button should claim through the earned level, exactly once");

            // The track is a section, not a level: with nothing pending, B
            // closes the menu (plan C6).
            yield return PressCancel();

            Assert.IsFalse(menu.IsOpen, "B with nothing pending should close the menu");
            AssertSelectedName("StartRunGate", "closing the menu should restore the hub's own entry, the gate");
        }
    }
}
