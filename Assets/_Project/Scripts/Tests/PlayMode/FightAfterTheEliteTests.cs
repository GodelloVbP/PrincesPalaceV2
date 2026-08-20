using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // ENTERING A ROOM THE WAY THE GAME DOES, which nothing else here did.
    //
    // Every other fight test builds a session by hand and calls Bind. That
    // skips FightBootstrap entirely -- the thing that reads the save, resolves
    // which room the party is standing in, rolls the roster and wires the
    // controller. So "after the elite, going into a fight it just hangs and you
    // cant do anything anymore" was invisible: the tests were entering a fight
    // by a door the player never uses.
    //
    // The leg boundary is the interesting part. The elite is the last room of
    // leg 1, so winning it advances the leg -- and everything after that runs
    // against a freshly generated map with a new legStartStep.
    public class FightAfterTheEliteTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-elite-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator TheFirstRoomOfTheSecondLegIsPlayable()
        {
            // The run state off the machine that reported this, rebuilt through
            // the real API rather than hand-written into the save.
            RunManager.StartRun(639228196442867409UL);
            RunManager.AdvanceLeg();

            var choices = RunManager.Choices();
            Assert.IsNotEmpty(choices, "the second leg offered nowhere to go");

            var fightRoom = choices.FirstOrDefault(n => n.Type == RoomType.Fight) ?? choices[0];
            Assert.IsTrue(RunManager.MoveTo(fightRoom.Id), "could not move into the room");

            // THE REAL DOOR: loading the scene runs FightBootstrap.Start.
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            Assert.IsTrue(fight.HasSession,
                $"the room ({fightRoom.Type}) built no session, so the stage is empty and there is " +
                "nothing to act on");

            Assert.IsFalse(fight.IsBusy,
                "the fight opened already busy, so every verb is disabled and nothing responds");

            // The verbs a player would actually press.
            var verbs = fight.GetComponentsInChildren<Button>(includeInactive: false)
                .Where(b => b.name.StartsWith("Verb"))
                .ToList();

            CollectionAssert.IsNotEmpty(verbs, "the fight drew no command verbs");
            Assert.IsTrue(verbs.Any(v => v.interactable),
                "not one command verb is interactable, so the player cannot do anything");
        }

        // The same door, for the ROOM TYPES a leg can actually put in front of
        // the party. A room that builds no session is a stage with nothing on
        // it and no way forward -- the shape of the report.
        [UnityTest]
        public IEnumerator EveryRoomTheSecondLegOffersBuildsSomethingToDo()
        {
            RunManager.StartRun(639228196442867409UL);
            RunManager.AdvanceLeg();

            var entry = RunManager.CurrentNode;
            Assert.IsNotNull(entry, "the second leg has no entry node");

            var stalled = new System.Collections.Generic.List<string>();

            foreach (var node in RunManager.Choices().ToList())
            {
                if (node.Type != RoomType.Fight && node.Type != RoomType.EliteFight) continue;

                RunManager.Run.currentNodeId = node.Id;
                RunManager.Run.step = RunManager.Run.legStartStep + node.Depth;

                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var fight = Object.FindAnyObjectByType<FightController>();
                if (fight == null || !fight.HasSession) stalled.Add($"{node.Type} node {node.Id}");
            }

            CollectionAssert.IsEmpty(stalled,
                "these rooms open onto an empty stage with nothing to act on: " +
                string.Join(", ", stalled));
        }
        // PLAYED TO THE END, which nothing in the suite did.
        //
        // Everything else takes ONE swing and checks the beat resolved. That
        // cannot see a fight that stops giving the player turns partway
        // through -- and "the buttons just dont work" is what that looks like:
        // CanAct is session AND not-over AND IS-PLAYER-TURN AND not-busy, and
        // only the first, second and fourth had ever been asserted.
        [UnityTest]
        public IEnumerator TheFightCanBePlayedToItsEnd()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            RunManager.StartRun(639228196442867409UL);
            RunManager.AdvanceLeg();

            var room = RunManager.Choices().FirstOrDefault(n => n.Type == RoomType.Fight)
                       ?? RunManager.Choices()[0];
            RunManager.MoveTo(room.Id);

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsTrue(fight.HasSession, "the room built no session");

            int turnsTaken = 0;
            float deadline = Time.realtimeSinceStartup + 30f;

            while (!fight.Session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy) { yield return null; continue; }

                if (!fight.Session.IsPlayerTurn)
                {
                    // The enemy side resolves synchronously, so this should
                    // never persist. If it does, the turn never comes back and
                    // every verb stays dead.
                    yield return null;
                    continue;
                }

                var attack = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb0" && b.interactable);

                Assert.IsNotNull(attack,
                    $"it is the player's turn and the attack verb is not usable after " +
                    $"{turnsTaken} turns - this is the fight going dead under the player");

                attack.onClick.Invoke();
                yield return null;

                var target = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name.StartsWith("EnemyPlate") && b.interactable);
                if (target != null) target.onClick.Invoke();

                turnsTaken++;
                yield return null;
            }

            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            Assert.IsTrue(fight.Session.IsOver,
                $"the fight never ended: {turnsTaken} turns taken, IsBusy={fight.IsBusy}, " +
                $"IsPlayerTurn={fight.Session.IsPlayerTurn}. The player is stuck in it.");
        }

        // GETTING OUT OF THE ELITE, which is the one path nothing has covered.
        //
        // When a fight ends, OpenReckoning (or OpenDefeat) is asked to take
        // over, and if it says it did, the verb column is HIDDEN and the
        // Continue button is hidden with it. So if whatever took over cannot
        // then be dismissed, there is nothing left on screen to press at all --
        // "it just hangs and you cant do anything anymore".
        //
        // The elite is where this bites first: it is the last room of leg 1 and
        // the fight whose reward carries a choice, so it is the first time the
        // Reckoning opens on its offer phase rather than straight onto the
        // summary.
        [UnityTest]
        public IEnumerator TheEliteCanBeLeftWhenItIsOver()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            RunManager.StartRun(639228196442867409UL);

            // Stand the party in leg 1's elite -- step 8, the forced room the
            // leg ends on.
            var map = RunManager.Map;
            var elite = map.Nodes.FirstOrDefault(n => n.Type == RoomType.EliteFight);
            Assert.IsNotNull(elite, "leg 1 has no elite, so this test is not testing what it says");

            RunManager.Run.currentNodeId = elite.Id;
            RunManager.Run.step = RunManager.Run.legStartStep + elite.Depth;

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsTrue(fight.HasSession, "the elite built no session");

            float deadline = Time.realtimeSinceStartup + 40f;
            while (!fight.Session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy || !fight.Session.IsPlayerTurn) { yield return null; continue; }

                var attack = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb0" && b.interactable);
                if (attack == null) { yield return null; continue; }

                attack.onClick.Invoke();
                yield return null;

                var target = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name.StartsWith("EnemyPlate") && b.interactable);
                if (target != null) target.onClick.Invoke();
                yield return null;
            }

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Assert.IsTrue(fight.Session.IsOver, "the elite never finished");

            // Let whatever takes over finish arriving.
            float settle = Time.realtimeSinceStartup + 5f;
            while (fight.IsBusy && Time.realtimeSinceStartup < settle) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);

            // SOMETHING has to be pressable. Win or lose, the fight is over and
            // the player must be able to leave it.
            var live = fight.GetComponentsInChildren<Button>(includeInactive: false)
                .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                .Select(b => b.name)
                .ToList();

            CollectionAssert.IsNotEmpty(live,
                $"the elite is over (won={fight.Session.PlayerWon}) and there is not one pressable " +
                "button on the screen - the verb column is hidden, Continue is hidden, and whatever " +
                "took over cannot be dismissed. The player is stuck in the fight.");
        }

        // ---- the stage is the ceiling for the party too --------------------------
        //
        // A hero with no slot fights from off screen exactly as a monster would.
        // The enemy roll is CLAMPED to the stage; FieldableParty deliberately is
        // not -- nobody notices two monsters instead of three, but a player whose
        // recruited character silently fails to turn up notices very much, and
        // choosing WHICH of the squad sits out is a design decision rather than a
        // clamp.
        //
        // So the mismatch is made unshippable here: grow the roster past the
        // stage and this fails, with somewhere to put the decision.
        [Test]
        public void TheBiggestSquadTheSaveAllowsStillFitsTheStage()
        {
            var full = SaveData.CreateNew();
            full.purchasedUpgradeIds.Add(SaveData.ExtraRecruitSlotUpgradeId);

            int max = full.EffectiveMaxSquadSize();

            Assert.LessOrEqual(max, FightHudSpec.StageSlotsPerSide,
                $"the save allows a squad of {max} against {FightHudSpec.StageSlotsPerSide} stage " +
                "slots. FieldableParty does not clamp, on purpose, so the extra hero would fight " +
                "from off screen - either widen the stage or decide who sits out.");
        }

    }
}
