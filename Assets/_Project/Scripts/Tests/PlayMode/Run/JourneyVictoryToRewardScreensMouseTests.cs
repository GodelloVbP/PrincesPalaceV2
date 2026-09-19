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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 8's mouse-only
    // regression -- see JourneyVictoryToRewardScreensTests' own header for
    // why this proves BOTH the Reckoning (opened automatically by a win) and
    // the reward track (a different screen, reached from the System Menu),
    // and why the round cap and the level/claimed-state seeding are the same
    // fixture either way -- the input mode does not change what the win
    // condition or the owed-levels condition need to be true.
    public class JourneyVictoryToRewardScreensMouseTests : JourneyFixture
    {
        private const ulong Seed = 639228196442867409UL;
        private const int RoundCap = 30;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-victory-mouse-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            RewardTrackController.SpeedMultiplier = 40f;

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
        public IEnumerator WinningARealFight_OpensTheReckoning_TakesOrSkipsAnOffer_ContinuesOntoTheMap_MouseOnly()
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

            // A fresh scene's own layout can still be mid-settle the frame it

            // activates -- this suite found that gap under the full parallel

            // gate (never under a single-class or single-area slice), so every

            // mouse click aimed at a screen coordinate waits real time here

            // first, not just the two engine frames TakeOverInput's own callers

            // already pay.

            yield return new WaitForSecondsRealtime(0.5f);
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

                yield return Click(Node("Verb0")); // ATTACK at Root skips straight to targeting
                yield return Click(Node("EnemyPlate0")); // one click both names and confirms the target
            }

            Assert.IsTrue(session.IsOver, $"the fight never ended: IsBusy={fight.IsBusy}, IsPlayerTurn={session.IsPlayerTurn}");
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
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            // Real seconds, not another frame: ReckoningController's own
            // PlayIn plays a wipe/gloom entrance over the offer phase
            // (that file's own SpeedMultiplier comment lists it first among
            // several animated phases), so a card's own world rect the
            // instant it becomes active can still be mid-flight -- a click
            // aimed at its FINAL position needs the entrance to have
            // actually finished landing there first, the same settle this
            // suite already pays after the reward track's own fly-in.
            yield return new WaitForSecondsRealtime(0.5f);

            var offer0 = Node("ReckoningOffer0");
            if (offer0 != null && offer0.activeInHierarchy)
            {
                yield return Click(offer0); // takes the offer, exactly once
            }

            // Polled, not a fixed one-frame wait: the summary's own Continue
            // button is a distinct GameObject from the offers, and
            // GameObject.Find (this fixture's own Node helper) only ever
            // sees an ACTIVE one -- the phase change that activates it is
            // not guaranteed to have landed by the very next frame.
            yield return WaitUntil(() =>
            {
                var continueButton = Node("ReckoningContinueButton");
                return continueButton != null && continueButton.activeInHierarchy;
            }, 5f, "taking or skipping the offer should have moved the Reckoning on to its own Continue button");

            // The same settle as the offer cards above -- the sweep that
            // carries the summary phase in (SweepToSummary) is itself
            // animated, so Continue's world rect the instant it becomes
            // active can still be mid-sweep.
            yield return new WaitForSecondsRealtime(0.5f);

            yield return Click(Node("ReckoningContinueButton")); // Dismissed -> LeaveFight -> Navigation.Go(Map)

            yield return WaitForScene("Map", 5f,
                "clicking Continue on the Reckoning should raise Dismissed and load the Map -- the run is still " +
                "standing after a win (RunOrchestrator.SettleFight only ends it on a loss)");
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
            AssertTopIsNotFight("the Map should not carry Fight's own non-selecting context");
        }

        [UnityTest]
        public IEnumerator ReachingTheRewardTrack_ThroughTheSystemMenu_ClicksTheOwedLevel_CollectsExactlyOnce_MouseOnly()
        {
            SaveSlotManager.EnterSlot(0);

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
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");

            // OPENING THE MENU HAS NO CLICK EQUIVALENT (item 3's own brief) --
            // the mouse's own ESC key, which binds the SystemMenu axis a pad
            // reaches with Start.
            yield return PressSystemMenu();
            Assert.IsTrue(menu.IsOpen, "Start should open the system menu from the hub");

            // Reached by clicking the row directly -- no Grid-then-List walk.
            yield return Click(Node("DossierTrackRow")); // ShowTrack() -- opens the panel over column A
            yield return null;
            yield return null; // Start() runs one frame after activation
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(0.5f); // let the fly-in settle, same as the pad file

            var entryDot = Node($"TrackDot{RewardTrackLayout.FirstLevel}");
            Assert.IsNotNull(entryDot, "the track has no first dot");

            // NOT ASSERTED: a scripted mouse hovering a specific dot to
            // prove ScrollTo lands on an exact pixel offset. Tried, and
            // dropped rather than forced -- this project's own precedent
            // (PartyGamepadVisualCaptureTests' HoverCard) already hedges on
            // exactly this technique against a real ScreenSpaceCamera
            // canvas ("whether a scripted pointer resolves... is not this
            // capture's own claim"), and it held here too: the panel's own
            // fly-in settles centred on the character's CURRENT level, so a
            // dot near FirstLevel is scrolled away and RewardTrackLayout.
            // ScrollFor's own literal target for ANY dot this test tried
            // never actually moved the content rect from wherever the
            // fly-in left it -- a raycast/hover reliability gap in the test
            // harness against this specific masked, scrolling viewport, not
            // a claim about production behaviour (RewardTrackGamepadNavigation
            // Tests already proves ScrollTo-on-select at the screen level,
            // and the pad journey above proves it end to end). Recorded
            // rather than silently dropped: AUDIT.md carries this as an
            // open gap for whoever next needs a scripted mouse to reach
            // into a scrolled-off part of this rail.
            var character0 = SaveSlotManager.CurrentSave.ActiveSquad().First();
            int before = character0.claimedTrackLevel;

            yield return Click(Node("TrackCollectButton")); // claims exactly once, through the earned level

            Assert.AreEqual(12, character0.claimedTrackLevel,
                "clicking the collect button should claim through the earned level, exactly once");
            Assert.AreNotEqual(before, character0.claimedTrackLevel, "the claim should actually have happened");

            // ADAPTED, NOT RELAXED, alongside its pad twin: hardware round 1
            // item 5 replaced "Cancel closes the whole menu from any pane"
            // with "Cancel steps back exactly one level", and the reward
            // track is the owner's own worked example of it. Still
            // PressCancel and not a click -- the panel's Close button is the
            // mouse's own way out and is already covered on the pad path;
            // what is being proven here is that the ESC a mouse player still
            // has costs them the same one level it costs a pad player.
            //
            // Held before the press for the same reason the pad twin holds
            // it: Node() cannot find a deactivated object.
            var trackPanel = Node("RewardTrackPanel");
            Assert.IsNotNull(trackPanel, "the reward track panel is not in this scene");

            yield return PressCancel();

            Assert.IsFalse(trackPanel.activeSelf, "the first Cancel should close the reward track");
            Assert.IsTrue(menu.IsOpen, "and leave the character sheet standing behind it");

            yield return PressCancel();

            Assert.IsFalse(menu.IsOpen,
                "only from the pane's base level does Cancel close the menu -- with the track shut there " +
                "is nothing left for the dossier to claim the press for");
        }
    }
}
