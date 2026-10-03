using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // WHERE A FIGHT ENDS, PLAYED THROUGH THE REAL DOOR
    // (docs/PLAN_BUGS_QA.md C1-C3, T3 T4 T6 T10): the Fight scene
    // loads, FightBootstrap builds and wires the fight, the verbs are pressed
    // until FightEnded -> settlement -> routing has run, and the pad drives
    // the screen it lands on through the production NavigationInputModule.
    //
    // The Bellwether rows use the real bell_in_the_fog; one side is made
    // unkillable or one-hit so the ending is the one under test.
    //
    // T1 and T2 (a room fight won / lost) are JourneyVictoryToRewardScreensTests
    // and JourneyFightToHubOnDefeatTests, which play those routes the same way;
    // T5 is EventFightScreenTests.
    public class FightEndRoutingFlowTests
    {
        private const string Bell = "bell_in_the_fog";
        private const int Touch = 0;

        private string _root;
        private FightController _fight;
        private ScriptedBaseInput _input;
        private readonly List<string> _loaded = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-fight-end-flow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            _loaded.Clear();
            Navigation.LoadOverride = scene => _loaded.Add(scene);
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            ReckoningController.SpeedMultiplier = 40f;
            SaveData.TestSquadOfThreeEnabled = true;
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

        private static RunSnapshot Run => RunManager.Run;

        // The run read back from disk: what a reload would find.
        private static List<string> ReceiptsOnDisk()
        {
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            return new List<string>(RunManager.Run.pendingRelicReceipts);
        }

        // ---- T4 + T10: Bellwether Defeated ------------------------------------------------

        [UnityTest]
        public IEnumerator BellwetherDefeated_OpensTheReckoningWithBothChips_AndOnlyDismissalAcknowledgesThem()
        {
            StartTheBellFight();
            yield return OpenTheFight();

            Shawn().MaxHealth = Shawn().CurrentHealth = 1_000_000;
            Bellwether().CurrentHealth = 1;
            yield return PlayToTheEnd();

            var reckoning = TheReckoning();
            Assert.IsFalse(reckoning.IsOutcomeForm, "pays: true, so the full form");
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock", "bellwethers_bell" }, reckoning.DisplayedReceipts,
                "Gained, in grant order");
            CollectionAssert.AreEqual(new[] { "Toll of the Flock", "Bellwether's Bell" }, ShownChipNames(reckoning));

            // T10, "Reckoning open, not dismissed": opening it acknowledged
            // nothing, so a reload here still has both.
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock", "bellwethers_bell" }, ReceiptsOnDisk());

            // Through the pad: take the first offer, then Continue.
            _input = NavSceneReuse.TakeOverInput();
            yield return null;
            yield return Submit();
            yield return WaitReal(0.5f);
            Assert.AreEqual("ReckoningContinueButton", EventSystem.current.currentSelectedGameObject?.name,
                "precondition: the choice spent, the selection on Continue");
            yield return Submit();

            CollectionAssert.Contains(_loaded, Navigation.Map, "Continue leaves to the Map");

            // T10, "after dismissal": acknowledged, in the dismissal's own write.
            CollectionAssert.IsEmpty(ReceiptsOnDisk(), "the chips were shown and left, so nothing is pending");
            CollectionAssert.IsEmpty(RunOrchestrator.PendingRelicReceipts);
        }

        // ---- T3: Bellwether Survived -----------------------------------------------------

        [UnityTest]
        public IEnumerator BellwetherSurvived_ShowsTheTollChip_AndAfterDismissalTheEventSaysExpAndNoRelic()
        {
            StartTheBellFight();
            yield return OpenTheFight();

            Shawn().MaxHealth = Shawn().CurrentHealth = 1_000_000;
            Bellwether().MaxHealth = Bellwether().CurrentHealth = 1_000_000;
            _fight.Session.RoundLimit = 1;
            yield return PlayToTheEnd();

            Assert.AreEqual(Domain.Combat.Session.FightEndReason.Survived, _fight.Session.EndReason,
                "fixture: the fight did not end on the round limit");

            var reckoning = TheReckoning();
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock" }, reckoning.DisplayedReceipts);

            _input = NavSceneReuse.TakeOverInput();
            yield return null;
            yield return Submit();
            yield return WaitReal(0.5f);
            yield return Submit();
            CollectionAssert.Contains(_loaded, Navigation.Map);

            CollectionAssert.IsEmpty(RunOrchestrator.PendingRelicReceipts, "no chips on the event page after it");
            var view = RunOrchestrator.CurrentEvent();
            Assert.IsNotNull(view, "the event is still open on its Endure page");
            Assert.AreEqual("endure", view.PageId);
            Assert.AreEqual("Shawn +80 XP", view.EffectsLine, "the exp line, and no relic in it");
        }

        // ---- T6: a fight with no run ----------------------------------------------------

        [UnityTest]
        public IEnumerator APreviewFight_EndsOnContinue_AndThePadPressesIt()
        {
            Assert.IsFalse(RunManager.HasRun, "fixture: a preview has no run");
            LogAssert.ignoreFailingMessages = true;
            yield return OpenTheFight();
            LogAssert.ignoreFailingMessages = false;

            foreach (var hero in _fight.Session.Encounter.PlayerParty) hero.CurrentHealth = hero.MaxHealth = 1_000_000;
            foreach (var enemy in _fight.Session.Encounter.Enemies) enemy.CurrentHealth = 1;
            yield return PlayToTheEnd();

            Assert.IsFalse(TheReckoningOrNull()?.gameObject.activeSelf ?? false, "no run, no Reckoning");
            var continueButton = _fight.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "ContinueButton");
            Assert.IsTrue(continueButton.gameObject.activeInHierarchy, "Continue is the way out");

            _input = NavSceneReuse.TakeOverInput();
            yield return null;
            Assert.AreSame(continueButton.transform, NavigationInputModule.Marker.Target,
                "the focus marker stands on the one control Submit will press");
            yield return Submit();

            Assert.IsFalse(continueButton.gameObject.activeInHierarchy, "Submit pressed Continue");
            CollectionAssert.Contains(_loaded, Navigation.Hub, "with no run, leaving goes to the hub");
        }

        // ---- fixture ---------------------------------------------------------------------

        private static void StartTheBellFight()
        {
            RunManager.StartRun(29UL);
            Run.relicIds.Clear();
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Bell), "fixture: bell_in_the_fog is not in the built content");

            var picked = EventPicks.OnCurrentPage(Touch);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"fixture: the pick was refused ({picked.Reason})");
            Assert.IsTrue(RunOrchestrator.EventFightPending);
            SaveSlotManager.SaveCurrent();
        }

        private CombatantState Shawn() => _fight.Session.Encounter.PlayerParty.Single();

        private CombatantState Bellwether() => _fight.Session.Encounter.Enemies.Single();

        private static ReckoningController TheReckoningOrNull() =>
            UnityEngine.Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);

        private static ReckoningController TheReckoning()
        {
            var reckoning = TheReckoningOrNull();
            Assert.IsNotNull(reckoning, "the Fight scene has no Reckoning");
            Assert.IsTrue(reckoning.gameObject.activeSelf, "a fight inside a run opens the Reckoning");
            return reckoning;
        }

        private static List<string> ShownChipNames(ReckoningController reckoning) =>
            // activeSelf: the chips belong to the summary phase, which is
            // still hidden while an offer is being chosen.
            reckoning.GetComponentsInChildren<Button>(includeInactive: true)
                .Where(b => b.name.StartsWith("ReckoningGainedChip") && b.gameObject.activeSelf)
                .OrderBy(b => b.name)
                .Select(b => b.GetComponentsInChildren<TMP_Text>(true).First(t => t.name.EndsWith("Name")).text)
                .ToList();

        private IEnumerator OpenTheFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            Assert.IsTrue(_fight.HasSession, "no session was built");
        }

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();
            yield return null;
        }

        private static IEnumerator WaitReal(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }

        // EventFightScreenTests.PlayToTheEnd: Verb0 on the first plate until
        // the session is over and FightEnded has fired.
        private IEnumerator PlayToTheEnd()
        {
            bool settled = false;
            _fight.FightEnded += _ => settled = true;

            float deadline = Time.realtimeSinceStartup + 90f;

            while (!_fight.Session.IsOver && Time.realtimeSinceStartup < deadline)
            {
                if (_fight.IsBusy || !_fight.Session.IsPlayerTurn) { yield return null; continue; }

                var attack = _fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb0" && b.interactable);
                if (attack == null) { yield return null; continue; }

                attack.onClick.Invoke();
                yield return null;

                var target = _fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name.StartsWith("EnemyPlate") && b.interactable);
                if (target != null) target.onClick.Invoke();

                yield return null;
            }

            Assert.IsTrue(_fight.Session.IsOver, "the fight never ended");

            while (!settled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(settled, "FightEnded never fired");
            yield return null;
        }
    }
}
