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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 4: finishing
    // the fight and reaching the Hub by gamepad, matching what the mouse
    // can reach.
    //
    // A deliberate loss is the only route this screen offers to the Hub:
    // FightScreen has no flee verb, and a win returns to the Map instead
    // (FightController.LeaveFight goes to Map while a run is active). This
    // drives FightSettlementTests' WalkInOn/solo-squad pattern through the
    // pad instead of Button.onClick.
    public class JourneyFightToHubOnDefeatTests : JourneyFixture
    {
        // FightSettlementTests' own seed -- "a run known to generate a leg
        // with a reachable plain fight room, an elite and a boss".
        private const ulong Seed = 639228196442867409UL;

        private string _root;
        private FightController _fight;
        private Domain.Combat.Session.FightSession _session;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-defeat-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // PINNED SOLO -- FightSettlementTests' own header states why: a
            // squad of three each landing a hit before the enemy's own turn
            // can kill an ogre outright even at 1 HP each, which is not the
            // guaranteed loss this segment needs to reach Defeat at all.
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

        // Carried health of 1 -- FightSettlementTests.WalkInOn's own shape,
        // the two dials this project's own established pattern steers a
        // fight's outcome with (character level, carried health) rather than
        // a dedicated "force a loss" seam that does not exist.
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
            yield return null;
        }

        // ATTACK -> hover the first living enemy -> confirm, every player
        // turn, through the real dispatcher exactly as segment 2 does --
        // repeated because a 1 HP squad's own turn still has to be spent
        // before the enemy's reply can end it (Fight has no way to simply
        // stand still and lose faster).
        //
        // Waits on FightEnded, not merely on Session.IsOver -- IsOver can
        // turn true the instant the session resolves the killing blow, one
        // or more frames BEFORE OnPlaybackFinished's own beat-drain catches
        // up and actually raises FightEnded (which is what runs
        // RunOrchestrator.SettleFight and opens Defeat/the Reckoning).
        // FightSettlementTests.PlayToTheEnd's own two-stage wait is the
        // precedent for exactly this gap.
        private IEnumerator PlayUntilTheFightEnds(float timeoutSeconds)
        {
            bool settled = false;
            _fight.FightEnded += _ => settled = true;

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!_session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (_fight.IsBusy || !_session.IsPlayerTurn) { yield return null; continue; }

                yield return PressSubmit(); // ATTACK at Root skips straight to targeting
                yield return MoveDown(); // hover the first living enemy
                yield return PressSubmit(); // confirm the attack
            }

            Assert.IsTrue(_session.IsOver,
                $"the fight never ended: IsBusy={_fight.IsBusy}, IsPlayerTurn={_session.IsPlayerTurn}");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "the fight is over and FightEnded never fired, so nothing settled it onto the run");
        }

        [UnityTest]
        public IEnumerator ADeliberateLoss_OpensDefeat_ReturnAndSubmitReachesTheHub()
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

            AssertSelectedName("DefeatInspectButton", "Inspect sits left of Return and should be the entry");

            yield return MoveRight();
            AssertSelectedName("DefeatReturnButton", "Right from Inspect should reach Return, the Rail's own next member");

            yield return PressSubmit(); // raises Dismissed -> LeaveFight -> Navigation.Go(Hub), a REAL load
            yield return WaitForScene("Hub", 5f,
                "Submit on Return should raise Dismissed and load the Hub -- LeaveFight goes to the Hub " +
                "specifically because the loss already ended the run (RunManager.HasRun is false)");
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's stated primary action and its declared entry");
        }
    }
}
