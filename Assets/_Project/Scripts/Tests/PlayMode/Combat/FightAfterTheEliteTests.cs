using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
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
    // The leg boundary is the interesting part. Every leg ends on its boss now
    // (D6/Phase 5B: "boss every floor"; the elite sits mid-leg instead), so
    // winning the boss advances the leg -- and everything after that runs
    // against a freshly generated map with a new legStartStep. Several tests
    // below still call AdvanceLeg directly rather than walking to the actual
    // forced room first, which is deliberate: AdvanceLeg's own seam does not
    // care which room forced the boundary.
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
            ReckoningController.SpeedMultiplier = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE CHARGE HAS TO REACH THE SESSION FROM PRODUCTION.
        //
        // FightOutcomeTests drive the revive by setting SecondLifeCharges
        // themselves, so every one of them would pass just as happily if
        // FightBootstrap never handed it in and the reward silently did nothing
        // in the real game. This is the only place a fight is built through the
        // real door with a real run behind it. A test may not do for
        // production what production must do for itself.
        [UnityTest]
        public IEnumerator AFightBuiltForALevelNinetySquadCarriesItsSecondLife()
        {
            RunManager.StartRun(639228196442867409UL);

            // LEVELLED AND COLLECTED. Reaching 90 is not enough on its own:
            // every reward on the track, the second life included, is summed
            // against claimedTrackLevel rather than level, so a squad that
            // never pressed collect has earned nothing. That is the design (the
            // collect button is what hands rewards over), and pinning it here
            // is what stops a future "level is enough" shortcut going
            // unnoticed.
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = 90;
                character.ClaimTrackRewards(RewardTracks.For(character), character.level);
            }

            var choices = RunManager.Choices();
            var fightRoom = choices.FirstOrDefault(n => n.Type == RoomType.Fight) ?? choices[0];
            Assert.IsTrue(RunManager.MoveTo(fightRoom.Id));

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);
            Assert.IsTrue(fight.HasSession, "the room built no session");

            // ONE PER COLLECTING MEMBER (§6): the source is per-character and
            // only the spend is squad-wide.
            Assert.AreEqual(SaveSlotManager.CurrentSave.ActiveSquad().Count, fight.Session.SecondLifeCharges,
                "the fight opened without the second lives the squad has earned, so the reward " +
                "does nothing in the actual game");
        }

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

            // THE THIRD TERM OF CanAct, which this file's own commit message
            // named as the one never asserted -- and which is what actually
            // broke. A fight that opens on a monster's turn with nothing to
            // resolve it is not busy, is not over, and has a session; it simply
            // never asks the player for anything. FightBootstrap calls
            // FightSession.Begin now, and this is what says so through the real
            // door rather than through the Begin every test used to call for
            // itself.
            Assert.IsTrue(fight.Session.IsPlayerTurn || fight.Session.IsOver,
                "the fight opened on a monster's turn and nothing resolved it, so the player is never " +
                "asked to act -- no intent icons, no movement, every verb dead");

            // The verbs a player would actually press.
            var verbs = fight.GetComponentsInChildren<Button>(includeInactive: false)
                .Where(b => b.name.StartsWith("Verb"))
                .ToList();

            CollectionAssert.IsNotEmpty(verbs, "the fight drew no command verbs");
            Assert.IsTrue(verbs.Any(v => v.interactable),
                "not one command verb is interactable, so the player cannot do anything");
        }

        // THE WATCHDOG, PROVEN TO FIRE.
        //
        // A rescue nobody has watched work is worth nothing, and this one is
        // expected to find nothing forever now that FightBootstrap opens its
        // fights -- so the only way it stays trustworthy is a test that puts a
        // controller into the exact broken state and watches it come back.
        //
        // The state is AUDIT.md #46 reproduced deliberately: a session bound
        // without Begin, on an encounter a monster opens. Before the fix that
        // was every fight where a monster was faster than the party.
        [UnityTest]
        public IEnumerator AFightBoundWithoutBeingOpened_RecoversInsteadOfDeadlocking()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            // Narrow speed gap: the monster opens and takes ONE turn. A wide one
            // gives it a run of consecutive turns and kills the hero, which
            // asserts something else -- see FightSessionTests for that mistake.
            var hero = new CombatantState("Hero", true, 500, 20, 20, 10);
            var quick = new CombatantState("Quick", false, 200, 10, 1, 11);
            var session = new FightSession(
                new CombatEncounter(new[] { hero }, new[] { quick }), null, null, new SeededRandom(3));

            Assert.IsFalse(session.IsPlayerTurn, "fixture check: the monster must hold turn one");

            // BOUND WITHOUT Begin. The bug, exactly.
            fight.Bind(session, EncounterClass.Normal);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "sitting on an enemy turn that nothing is resolving"));

            yield return null;
            yield return null;

            Assert.IsTrue(session.IsPlayerTurn || session.IsOver,
                "the watchdog did not hand the turn back, so the fight is still frozen with every " +
                "verb disabled -- which is what a player reports as 'the buttons do nothing'");
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
        // The elite is where this bites first: it is the first fight in a
        // fresh run whose reward carries a choice (VictoryRewards.
        // GuaranteesWeapon fires for boss or elite alike), so it is the
        // first time the Reckoning opens on its offer phase rather than
        // straight onto the summary.
        [UnityTest]
        public IEnumerator TheEliteCanBeLeftWhenItIsOver()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // The Reckoning that opens once this fight is won runs its own
            // unscaled wipe/sweep/bar-fill (ReckoningController.
            // SpeedMultiplier) -- without this the "something must be
            // pressable" check below would sit through that animation at
            // real speed on top of the fight itself.
            ReckoningController.SpeedMultiplier = 40f;

            RunManager.StartRun(639228196442867409UL);

            // Stand the party in leg 1's elite -- step 4, the forced room
            // mid-leg (D6/Phase 5B: the boss now takes the leg-ending slot
            // at step 8; the elite moved to the midpoint).
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

            // Let whatever takes over finish arriving, THEN wait for it to
            // become pressable -- polled on the actual state the assertion
            // below checks, rather than a flat sleep sized for the
            // Reckoning's animation. [SetUp]'s SpeedMultiplier = 40 (set
            // above) already collapses that animation to well under a
            // frame; this loop just bounds how long a genuine regression
            // gets to hang before failing.
            float settle = Time.realtimeSinceStartup + 5f;
            while (fight.IsBusy && Time.realtimeSinceStartup < settle) yield return null;

            List<string> live;
            float pressableDeadline = Time.realtimeSinceStartup + 2f;
            do
            {
                live = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                    .Select(b => b.name)
                    .ToList();
                if (live.Count > 0) break;
                yield return null;
            } while (Time.realtimeSinceStartup < pressableDeadline);

            ReckoningController.SpeedMultiplier = 1f;

            // SOMETHING has to be pressable. Win or lose, the fight is over and
            // the player must be able to leave it.
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
