using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // AN EVENT FIGHT'S END ON THE FIGHT SCREEN (docs/PLAN_EVENTS_BELL_AND_
    // CARAVAN.md 1.2, M2; docs/PLAN_BUGS_QA.md C1/C3). The routing
    // follows the SETTLEMENT, not the win flag: a `wake` loss ended no run, so
    // it is no defeat screen, and inside a run there is no Continue fallback.
    // Both a `wake` loss and a `pays: false` win open the Reckoning in its
    // outcome form: the ending named, nothing paid, and the pad's Submit on
    // its Continue reaches the Map (T5).
    //
    // Played through the real door, FightSettlementTests' way: the Fight
    // scene is loaded, FightBootstrap builds through
    // RunOrchestrator.CurrentEncounterRequest, and the verbs are pressed until
    // FightEnded has fired.
    public class EventFightScreenTests
    {
        private const string EventId = "m2_screen_fixture";
        private const int Doom = 0;      // wake, [sheep] vs three rats
        private const int QuietWin = 1;  // pays false, normal party vs one rat

        private string _root;
        private FightController _fight;
        private readonly System.Collections.Generic.List<string> _loaded = new System.Collections.Generic.List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-fight-screen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            _loaded.Clear();
            Navigation.LoadOverride = scene => _loaded.Add(scene);
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            ReckoningController.SpeedMultiplier = 40f;
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            ReckoningController.SpeedMultiplier = 1f;
            LogAssert.ignoreFailingMessages = false;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            ContentDatabase.Reset();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RawEventEntry Fixture() =>
            new RawEventEntry
            {
                id = EventId,
                mayReturn = true,
                floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "gate", title = "Gate", body = "B",
                        choices = new[]
                        {
                            Starts("Doom", "doom"),
                            Starts("Quiet", "quiet"),
                            new RawEventChoice { text = "Walk away", outcomes = new[] { new RawEventOutcome { goTo = "Leave" } } },
                        },
                    },
                },
                fights = new[]
                {
                    new RawEventFight
                    {
                        id = "doom", enemies = new[] { "rat", "rat", "rat" }, party = new[] { "sheep" },
                        onLoss = "wake", pays = false,
                        onDefeated = new RawEventOutcome { goTo = "Leave", result = "Won." },
                        onFell = new RawEventOutcome { goTo = "Leave", result = "Fell." },
                    },
                    new RawEventFight
                    {
                        id = "quiet", enemies = new[] { "rat" }, pays = false,
                        onDefeated = new RawEventOutcome { goTo = "Leave", result = "Won quietly." },
                    },
                },
            };

        private static RawEventChoice Starts(string text, string fightId) =>
            new RawEventChoice
            {
                text = text,
                outcomes = new[] { new RawEventOutcome { effects = new[] { new RawEventEffect { kind = "fight", fight = fightId } } } },
            };

        private static void StartTheFight(int choice)
        {
            FixtureEvents.Append(Fixture());
            RunManager.StartRun(59UL);
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(EventId), "fixture: the event did not open");

            var picked = EventPicks.OnCurrentPage(choice);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"fixture: the pick was refused ({picked.Reason})");
            Assert.IsTrue(RunOrchestrator.EventFightPending);
        }

        [UnityTest]
        public IEnumerator AWakeLoss_OpensTheOutcomeFormTitledFell_AndThePadLeavesToTheMap()
        {
            StartTheFight(Doom);

            // One HP against three rats: the loss is the fixture.
            var run = RunManager.Run;
            run.currentHealth ??= new System.Collections.Generic.List<RunHealthEntry>();
            run.currentHealth.RemoveAll(e => e.characterId == "sheep");
            run.currentHealth.Add(new RunHealthEntry { characterId = "sheep", hp = 1 });

            FightController fight = null;
            yield return OpenTheFight(f => fight = f);
            yield return PlayToTheEnd(fight);

            Assert.IsFalse(fight.Session.PlayerWon, "fixture: Shawn on 1 HP beat three rats");
            Assert.IsTrue(RunManager.HasRun, "a wake loss ended the run");

            var defeat = UnityEngine.Object.FindAnyObjectByType<DefeatController>(FindObjectsInactive.Include);
            Assert.IsNotNull(defeat, "fixture: the Fight scene has no defeat screen to keep closed");
            Assert.IsFalse(defeat.gameObject.activeSelf, "the defeat screen opened for a run that did not end");

            var reckoning = AssertOutcomeForm("FELL");
            Assert.IsEmpty(reckoning.DisplayedReceipts, "Fell grants nothing, so there is nothing to show");

            Assert.AreEqual(1, run.currentHealth.First(e => e.characterId == "sheep").hp);
            Assert.AreEqual("Fell.", run.eventResult, "the event plays the Fell text when the Map reopens it");

            // The pad, through the production dispatcher: Submit on the
            // outcome form's entry, which is its Continue.
            var input = NavSceneReuse.TakeOverInput();
            yield return null;
            Assert.AreEqual("ReckoningContinueButton", UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.name);
            input.SubmitDown = true;
            yield return null;
            input.ClearOneFrameFlags();

            CollectionAssert.Contains(_loaded, Navigation.Map, "Continue on the outcome form leaves to the Map");
        }

        [UnityTest]
        public IEnumerator ANonPayingWin_OpensTheOutcomeFormTitledVictory()
        {
            StartTheFight(QuietWin);
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad()) character.level = 40;

            FightController fight = null;
            yield return OpenTheFight(f => fight = f);

            // The win is the fixture, not the subject: a floor-1 rat against a
            // level-40 hero, made certain.
            foreach (var hero in fight.Session.Encounter.PlayerParty) hero.CurrentHealth = hero.MaxHealth = 1_000_000;
            foreach (var enemy in fight.Session.Encounter.Enemies) enemy.CurrentHealth = 1;

            yield return PlayToTheEnd(fight);

            Assert.IsTrue(fight.Session.PlayerWon, "fixture: the rat won");

            AssertOutcomeForm("VICTORY");
            Assert.AreEqual("Won quietly.", RunManager.Run.eventResult);
        }

        // The Reckoning took the fight over in its outcome form: titled for
        // the ending, no gold, and the fight's own Continue left down.
        private ReckoningController AssertOutcomeForm(string title)
        {
            var reckoning = UnityEngine.Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning, "fixture: the Fight scene has no Reckoning");
            Assert.IsTrue(reckoning.gameObject.activeSelf, "a fight inside a run opens the Reckoning");
            Assert.IsTrue(reckoning.IsOutcomeForm, "nothing was paid, so the outcome form");

            var texts = reckoning.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true);
            Assert.AreEqual(title, texts.First(t => t.name == "ReckoningTitle").text);
            Assert.IsFalse(texts.First(t => t.name == "ReckoningGoldLabel").gameObject.activeSelf, "no gold on the outcome form");

            Assert.IsFalse(ContinueShown(_fight), "inside a run there is no Continue fallback");
            return reckoning;
        }

        private static bool ContinueShown(FightController fight) =>
            fight.GetComponentsInChildren<Button>(includeInactive: true)
                .Any(b => b.name == "ContinueButton" && b.gameObject.activeInHierarchy);

        private IEnumerator OpenTheFight(Action<FightController> found)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the event fight built no session");
            _fight = fight;
            found(fight);
        }

        // FightSettlementTests.PlayToTheEnd: press Verb0 on the front enemy
        // until the session is over and FightEnded has fired.
        private IEnumerator PlayToTheEnd(FightController fight)
        {
            bool settled = false;
            fight.FightEnded += _ => settled = true;

            float deadline = Time.realtimeSinceStartup + 60f;

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

            Assert.IsTrue(fight.Session.IsOver, "the fight never ended");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "FightEnded never fired");

            // The Continue fallback is set in the same pass as FightEnded;
            // one frame lets any UI that follows it settle.
            yield return null;
        }
    }
}
