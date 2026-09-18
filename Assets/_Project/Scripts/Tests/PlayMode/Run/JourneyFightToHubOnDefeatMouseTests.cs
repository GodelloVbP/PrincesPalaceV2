using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 4's mouse-only
    // regression -- same deliberate loss JourneyFightToHubOnDefeatTests
    // drives (see that file's own header for why a loss, not a win or a
    // flee, is the one route this screen offers to the Hub at all), replayed
    // with the mouse: ATTACK-then-target collapses from a Submit/Move/Submit
    // triple into two clicks (Verb0, then EnemyPlate0 -- FightController.
    // Input.cs's own "FOUR SURFACES, ONE CONFIRM"), and Defeat's own Return
    // is clicked directly rather than reached with a Rail Move from Inspect.
    public class JourneyFightToHubOnDefeatMouseTests : JourneyFixture
    {
        private const ulong Seed = 639228196442867409UL;

        private string _root;
        private FightController _fight;
        private Domain.Combat.Session.FightSession _session;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-defeat-mouse-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static DescentNode APlainFightRoom()
        {
            var choices = RunManager.Choices();
            Assert.IsNotEmpty(choices, "the leg offered nowhere to go");
            return choices.FirstOrDefault(n => n.Type == RoomType.Fight) ?? choices[0];
        }

        private static void WalkInOnOneHitPoint()
        {
            var run = RunManager.Run;
            run.currentHealth ??= new System.Collections.Generic.List<RunHealthEntry>();
            run.currentHealth.Clear();

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                run.currentHealth.Add(new RunHealthEntry { characterId = character.definitionId, hp = 1 });
            }
        }

        private IEnumerator OpenTheDoomedFight()
        {
            RunManager.StartRun(Seed);
            var room = APlainFightRoom();
            Assert.IsTrue(RunManager.MoveTo(room.Id), "could not move into the fight room");
            WalkInOnOneHitPoint();

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            Assert.IsTrue(_fight.HasSession, "the room built no session");
            _session = _fight.Session;

            for (int i = 0; i < 120 && !_session.IsPlayerTurn && !_session.IsOver; i++) yield return null;

            TakeOverInput();

            // A fresh scene's own layout can still be mid-settle the frame it

            // activates -- this suite found that gap under the full parallel

            // gate (never under a single-class or single-area slice), so every

            // mouse click aimed at a screen coordinate waits real time here

            // first, not just the two engine frames TakeOverInput's own callers

            // already pay.

            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
        }

        private IEnumerator PlayUntilTheFightEnds(float timeoutSeconds)
        {
            bool settled = false;
            _fight.FightEnded += _ => settled = true;

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!_session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (_fight.IsBusy || !_session.IsPlayerTurn) { yield return null; continue; }

                yield return Click(Node("Verb0")); // ATTACK at Root skips straight to targeting
                yield return Click(Node("EnemyPlate0")); // one click both names and confirms the target
            }

            Assert.IsTrue(_session.IsOver,
                $"the fight never ended: IsBusy={_fight.IsBusy}, IsPlayerTurn={_session.IsPlayerTurn}");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "the fight is over and FightEnded never fired, so nothing settled it onto the run");
        }

        [UnityTest]
        public IEnumerator ADeliberateLoss_OpensDefeat_ClickReturnReachesTheHub_MouseOnly()
        {
            yield return OpenTheDoomedFight();

            yield return PlayUntilTheFightEnds(30f);

            Assert.IsFalse(_session.PlayerWon,
                "fixture: a solo squad walking in on 1 HP should lose this floor-1 room");
            Assert.IsFalse(RunManager.HasRun, "a lost fight should have ended the run (RunOrchestrator.SettleFight)");

            yield return WaitUntil(() =>
            {
                var inspect = Node("DefeatInspectButton");
                return inspect != null && inspect.activeInHierarchy;
            }, 5f, "the loss should have opened the Defeat screen");
            yield return null;

            // Return clicked directly -- there is no "walk the Rail" for a
            // mouse, only the control itself.
            yield return Click(Node("DefeatReturnButton"));

            yield return WaitForScene("Hub", 5f,
                "clicking Return should raise Dismissed and load the Hub -- LeaveFight goes to the Hub " +
                "specifically because the loss already ended the run (RunManager.HasRun is false)");
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
            // No entry-selection assertion -- nothing has been clicked on the
            // Hub yet (this file's own header on why that claim is pad-only).
            Assert.AreEqual("Hub", SceneManager.GetActiveScene().name, "should have landed on the Hub");
        }
    }
}
