using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The spec's press counts for the validation tasks that are not the two
    // LOADOUT ones (JourneyCharacterLoadoutTests holds 4 and 5), section 10 of
    // docs/SPEC_SYSTEM_MENU.md, on a pad, from the plan's fixed start: Shawn,
    // Odette and Bjorn seated (Odette at the rear), two relics, menu memory
    // cleared, in the stated scene: a Fight at turn 1 for tasks 1-3, the Map for
    // 6-8. A count above the spec's is a regression.
    //
    // Every press goes through the REAL dispatcher (JourneyFixture); nothing
    // calls a controller. A task counts as done when what it asks for is in
    // front of the player, not merely when focus moved.
    public class JourneySystemMenuPressCountTests : JourneyFixture
    {
        private string _root;
        private int _presses;
        private string _wentTo;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-presscount-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            MenuMemory.Clear();
            _wentTo = null;
            Navigation.LoadOverride = scene => _wentTo = scene;
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator Count(IEnumerator press)
        {
            _presses++;
            yield return press;
        }

        // ---- the fixed starts -------------------------------------------------------------

        // Bjorn in front, Shawn in the middle, Odette at the rear; two relics.
        private static void SeatTheSquadAndCarryTwoRelics()
        {
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "bear", "sheep", "owl" };

            RunManager.StartRun(4242UL);
            var run = RunManager.Run;
            run.relicIds.Clear();
            run.relicIds.AddRange(ContentDatabase.Relics.Where(r => r != null).Take(2).Select(r => r.id));
            SaveSlotManager.SaveCurrent();
        }

        private SystemMenuController _menu;
        private Domain.Combat.Session.FightSession _session;

        private IEnumerator FixedStartInAFightAtTurnOne()
        {
            SeatTheSquadAndCarryTwoRelics();

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the Fight scene carries no SystemMenuController");

            var enemies = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            var built = FightEncounterAdapter.Build(new List<string> { "bear", "sheep", "owl" }, enemies,
                new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            _session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !_session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(_session.IsPlayerTurn, "never reached a player turn to press Start on");

            TakeOverInput();
            yield return null;
            _presses = 0;
        }

        private IEnumerator FixedStartOnTheMap()
        {
            SeatTheSquadAndCarryTwoRelics();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the Map scene carries no SystemMenuController");

            TakeOverInput();
            yield return null;
            _presses = 0;
        }

        private static string TextOf(string name)
        {
            var node = Node(name);
            Assert.IsNotNull(node, $"nothing active is named {name}");
            return node.GetComponent<TMP_Text>().text;
        }

        // ---- task 1: mid-fight, what does this relic do? ---------------------------------------
        //
        // Start opens on RUN in a fight (nothing remembered) and lands on the
        // first relic row: 1.
        [UnityTest]
        public IEnumerator Task1_WhatARelicDoesMidFight_TakesOnePress()
        {
            yield return FixedStartInAFightAtTurnOne();

            yield return Count(PressSystemMenu());

            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab, "a fight opens on RUN when nothing is remembered");
            AssertSelectedName("RunRelic0", "Start lands on the first relic row");
            Assert.IsNotEmpty(TextOf("RunRelic0Name"), "the relic's name is not on screen");
            Assert.IsNotEmpty(TextOf("RunRelic0Body"), "what the relic does is not on screen");
            Assert.LessOrEqual(_presses, 1, "the spec counts one press for task 1");
        }

        // ---- task 2: mid-fight, what does this enemy status mean? ------------------------------
        //
        // Start, RT (GLOSSARY), RB (Statuses), Right (the list), then Down to
        // the status: 4 + n. The third status (n = 2) stands for any of them.
        [UnityTest]
        public IEnumerator Task2_WhatAnEnemyStatusMeansMidFight_TakesFourPlusItsRow()
        {
            yield return FixedStartInAFightAtTurnOne();
            const int n = 2;

            yield return Count(PressSystemMenu());
            yield return Count(PressTriggerRight());
            Assert.AreEqual(SystemMenuTab.Glossary, _menu.SelectedTab, "RT did not reach GLOSSARY");

            yield return Count(PressTabNext());
            AssertSelectedName("GlossaryCategory1", "RB did not step to the second category, Statuses");

            yield return Count(MoveRight());
            AssertSelectedName("GlossaryRow0", "Right did not enter the list");

            for (int i = 0; i < n; i++) yield return Count(MoveDown());
            AssertSelectedName($"GlossaryRow{n}", "Down did not reach the status");

            string status = TextOf($"GlossaryRow{n}Name");
            Assert.AreEqual(status, TextOf("GlossaryDetailName"), "the detail is not showing the focused status");
            Assert.IsNotEmpty(TextOf("GlossaryDetailBody"), "what the status means is not on screen");
            Assert.LessOrEqual(_presses, 4 + n, "the spec counts 4 + n presses for task 2");
        }

        // ---- task 3: mid-fight, how much health does Odette have? ------------------------------
        //
        // Start, LT (PARTY): 2, with her HP on the seat.
        [UnityTest]
        public IEnumerator Task3_OdettesHealthMidFight_TakesTwoPresses()
        {
            yield return FixedStartInAFightAtTurnOne();

            yield return Count(PressSystemMenu());
            yield return Count(PressTriggerLeft());

            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab, "LT from RUN did not reach PARTY");
            Assert.AreEqual("Odette", TextOf("PartySeat2Name"), "fixture: Odette is not the rear seat");
            Assert.IsTrue(_menu.FightSource.TryGetMember("owl", out var facts), "the fight has no facts for Odette");
            Assert.Greater(facts.Health, 0);
            Assert.AreEqual($"HP {facts.Health} / {facts.MaxHealth}", TextOf("PartySeat2Hp"),
                "the seat does not show the health the fight is showing");
            Assert.LessOrEqual(_presses, 2, "the spec counts two presses for task 3");
        }

        // ---- task 6: spend a stat point ---------------------------------------------------------
        //
        // Start, Down (ATTRIBUTES), Right (the first +), A: 4.
        [UnityTest]
        public IEnumerator Task6_SpendAStatPoint_TakesFourPresses()
        {
            yield return FixedStartOnTheMap();
            var odette = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "owl");
            odette.unspentStatPoints = 1;
            odette.investedAbilityScores = default;

            yield return Count(PressSystemMenu());
            AssertSelectedName("DossierSection0", "Start lands on the rail's selected section, LOADOUT");
            Assert.AreEqual("Odette", TextOf("DossierName"), "the strip does not open on Odette");

            yield return Count(MoveDown());
            AssertSelectedName("DossierSection1", "Down did not reach ATTRIBUTES");

            yield return Count(MoveRight());
            AssertSelectedName("DossierAttrPlus0", "Right did not enter the first attribute's +");

            yield return Count(PressSubmit());

            Assert.AreEqual(0, odette.unspentStatPoints, "the point was not spent");
            Assert.AreEqual(1, System.Enum.GetValues(typeof(AbilityScore)).Cast<AbilityScore>()
                    .Sum(score => odette.investedAbilityScores[score]),
                "the point did not land in a score");
            Assert.LessOrEqual(_presses, 4, "the spec counts four presses for task 6");
        }

        // ---- task 7: Odette to the front --------------------------------------------------------
        //
        // Start, RT (PARTY), A (carry the rear seat), Right x2, A: 6.
        [UnityTest]
        public IEnumerator Task7_OdetteToTheFront_TakesSixPresses()
        {
            yield return FixedStartOnTheMap();

            yield return Count(PressSystemMenu());
            yield return Count(PressTriggerRight());
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab, "RT did not reach PARTY");
            AssertSelectedName("PartySeat2Button", "PARTY opens on the rear seat, which is Odette's");

            yield return Count(PressSubmit());
            yield return Count(MoveRight());
            yield return Count(MoveRight());
            AssertSelectedName("PartySeat0Button", "Right x2 did not reach the front seat");

            yield return Count(PressSubmit());

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front], "Odette is not in front");
            Assert.LessOrEqual(_presses, 6, "the spec counts six presses for task 7");
        }

        // ---- task 8: leave to the title screen --------------------------------------------------
        //
        // Start, LT (wrap to EXIT), A (arm), A (fire): 4.
        [UnityTest]
        public IEnumerator Task8_LeaveToTheTitle_TakesFourPresses()
        {
            yield return FixedStartOnTheMap();

            yield return Count(PressSystemMenu());
            yield return Count(PressTriggerLeft());
            Assert.AreEqual(SystemMenuTab.Exit, _menu.SelectedTab, "LT from the first tab did not wrap to EXIT");
            AssertSelectedName("ExitTitle", "EXIT opens on the title row");

            yield return Count(PressSubmit());
            Assert.IsNull(_wentTo, "the first press must only arm leaving");

            yield return Count(PressSubmit());

            Assert.AreEqual(Navigation.MainMenu, _wentTo, "the second press did not leave for the title");
            Assert.LessOrEqual(_presses, 4, "the spec counts four presses for task 8");
        }
    }
}
