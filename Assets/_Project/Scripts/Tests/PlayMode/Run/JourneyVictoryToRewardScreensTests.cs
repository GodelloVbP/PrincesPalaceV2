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
    // DEVIATION, NAMED RATHER THAN GUESSED PAST: the brief's own phrase
    // "fight victory -> reward track" names ONE screen, but the real game
    // has TWO, and they are not the same thing. The instant a fight is won,
    // FightController.OpenReckoning shows the RECKONING (an item pick) --
    // that is what "Move along ... Submit to take one ... Continue" (this
    // plan's own section 13 checklist, the Victory bullet) actually
    // describes, and ReckoningGamepadNavigationTests already proves its
    // rail/ScrollTo-shaped claims at the screen level. The REWARD TRACK
    // (RewardTrackController, "Move along its rail... ScrollTo followed...
    // literal level... Submit collect if owed" -- language that only makes
    // sense against a LEVEL rail, which the Reckoning has none of) is a
    // different screen entirely, reached from the System Menu's own
    // Character & Inventory tab (DossierTrackRow), not opened automatically
    // by a win. Grepped: nothing in FightController/FightBootstrap ever
    // calls RewardTrackController -- confirmed rather than assumed.
    //
    // So this segment proves BOTH, as two separate journeys sharing one
    // class: the fight-to-Reckoning path really is one continuous session
    // (win, take or skip, Continue, land on the Map); the reward track is
    // reconstructed separately, from the Hub, with a level/claimed state
    // built to owe something -- crediting that same debt DURING the pad-
    // driven fight above would mean pinning an exact experience-to-level
    // threshold this file has no business asserting, and
    // RewardTrackGamepadNavigationTests already proves the rail/ScrollTo
    // mechanism itself; what this segment adds is that the SAME real Hub
    // scene reaches it end to end through the dispatcher.
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

        private static DescentNode APlainFightRoom()
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

                yield return PressSubmit(); // takes the entry offer, exactly once
                yield return null; // the reselection onto Continue follows the phase change

                AssertSelectedName("ReckoningContinueButton",
                    "the offers are inert once one is taken, so the dispatcher should have moved on to Continue");
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
            Assert.IsNotNull(selected, "landing on the Map should leave something selected");
            StringAssert.StartsWith("MapNode", selected.name, "the Map's own entry is a node");
        }

        [UnityTest]
        public IEnumerator ReachingTheRewardTrack_ThroughTheSystemMenu_ScrollsAndCollectsExactlyOnce_CancelStepsBackOneLevelAtATime()
        {
            SaveSlotManager.EnterSlot(0);

            // OWED > 0: earned level 12, paid through 10 -- the same shape
            // RewardTrackGamepadNavigationTests.ReachingAndSubmittingTheCollectButton
            // already proves at the screen level; this segment's own claim
            // is that the SAME real Hub scene reaches it end to end.
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

            yield return PressSystemMenu(); // opens the menu on its default tab, Character & Inventory outside a run
            Assert.IsTrue(menu.IsOpen, "Start should open the system menu from the hub");

            yield return MoveDown(); // the tab's own Down lands on the first equipment slot, Head
            AssertSelectedName("DossierSlotHead", "the tab bar's Down should land on the first equipment slot");

            // RE-PATHED, not relaxed, by hardware round 1 item 6: this used to
            // be Down then one Left, because index pairing sent Necklace's
            // Left 188 units down the screen to the reward-track row. Now that
            // a sideways press keeps the player's height, Left from anywhere
            // in the top half of the loadout reaches the roster pager, which
            // is what is actually beside it -- so the route to a nav row is
            // down column A, which is where the nav rows are.
            //
            // The pager is NOT in this journey's graph: it is hidden for a
            // squad that has nobody else to page to, so the whole of column A
            // here is the three nav rows and Head's nearest neighbour among
            // them is the topmost. DossierGamepadNavigationTests' own ring
            // walks the pager case, on a save that has one.
            yield return MoveLeft(); // Head hands off to column A at the nearest control it has
            AssertSelectedName("DossierSpellsRow",
                "Left from the loadout should reach the nearest column-A control, which with no roster " +
                "pager drawn is the first nav row");

            yield return MoveDown(); // and the rows are a List, top to bottom as drawn
            AssertSelectedName("DossierTrackRow", "Down should step the nav rows to the reward-track row");

            yield return PressSubmit(); // ShowTrack() -- opens the panel over column A
            yield return null;
            yield return null; // Start() runs one frame after activation
            TakeOverInput();

            // The panel's own fly-in glides toward the current level on open
            // -- settled here the same way RewardTrackGamepadNavigationTests
            // does, so the ScrollTo assertion below is not racing it.
            yield return new WaitForSecondsRealtime(0.5f);

            // NEW CONTRACT (owner, 2026-09-23): opening the track focuses the
            // CURRENT level's dot (RewardTrackController.CurrentLevelDot()),
            // not the track's first dot -- the fixture set every squad
            // member's level to 12 above, so 12 is that dot, taken as the
            // fixture's own literal rather than re-derived by calling the
            // controller's private method from outside it.
            const int currentLevel = 12;
            var entryDot = Node($"TrackDot{currentLevel}");
            Assert.IsNotNull(entryDot, "the track has no dot for the fixture's current level");
            AssertSelectedName(entryDot.name, "opening the track should focus the current level's own dot");

            var content = GameObject.Find("TrackContent")?.GetComponent<RectTransform>();
            var viewport = GameObject.Find("TrackViewport")?.GetComponent<RectTransform>();
            Assert.IsNotNull(content, "the reward track has no content rect");
            Assert.IsNotNull(viewport, "the reward track has no viewport rect");

            yield return MoveRight(); // one Right press on the rail

            var nextDot = Node($"TrackDot{currentLevel + 1}");
            AssertSelectedName(nextDot.name, "one Right press on the rail should move selection to the next dot");

            float expected = RewardTrackLayout.ScrollFor(currentLevel + 1, viewport.rect.width);
            Assert.AreEqual(expected, content.anchoredPosition.x, 1f,
                "selecting the next dot should have scrolled the rail to it via ScrollTo, the same call a " +
                "mouse hover already makes");

            yield return MoveDown(); // Down from any dot reaches the ribbon slider
            AssertSelectedName("TrackRibbonGrab", "Down from the rail should reach the ribbon slider");

            yield return MoveDown(); // and Down from the ribbon reaches the collect button
            AssertSelectedName("TrackCollectButton", "Down from the ribbon should reach the collect button");

            var character0 = SaveSlotManager.CurrentSave.ActiveSquad().First();
            int before = character0.claimedTrackLevel;

            yield return PressSubmit(); // claims exactly once, through the earned level

            Assert.AreEqual(12, character0.claimedTrackLevel,
                "Submit on the collect button should claim through the earned level, exactly once");
            Assert.AreNotEqual(before, character0.claimedTrackLevel, "the claim should actually have happened");

            // ADAPTED, NOT RELAXED. This used to assert that one Cancel from
            // inside the track closed the whole menu, on the "this pane does
            // not claim Cancel" rule the pack and spells panels then shared.
            // The hardware play-test rejected exactly that behaviour, naming
            // this exact screen pair: "Back (B) should not close a screen but
            // take you back first (e.g. reward track to character sheet
            // screen)." The question is the one this test always asked -- what
            // does one Cancel press cost from in here -- and the answer is now
            // one level rather than three.
            //
            // Node() is GameObject.Find, which sees only active objects, so
            // the panel is held BEFORE the press that deactivates it.
            var trackPanel = Node("RewardTrackPanel");
            Assert.IsNotNull(trackPanel, "the reward track panel is not in this scene");

            yield return PressCancel();

            Assert.IsFalse(trackPanel.activeSelf, "the first Cancel should close the reward track");
            Assert.IsTrue(menu.IsOpen, "and leave the character sheet standing behind it");
            AssertSelectedName("DossierTrackRow",
                "with the row that opened the track selected again, ready to be pressed a second time");

            // And the second press, which is what proves the claim is per
            // LEVEL rather than a flat "the dossier never closes the menu".
            yield return PressCancel();

            Assert.IsFalse(menu.IsOpen,
                "only from the pane's base level does Cancel close the menu -- with nothing left to step " +
                "back out of, the dossier declines the press and SystemMenu's own Close runs");
            AssertSelectedName("StartRunGate", "closing the menu should restore the hub's own entry, the gate");
        }
    }
}
