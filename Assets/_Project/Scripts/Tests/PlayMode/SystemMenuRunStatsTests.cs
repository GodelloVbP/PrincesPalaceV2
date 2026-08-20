using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The system menu's Run statistics pane.
    //
    // THE RULE THIS PANE IS HELD TO is the design's own: every row binds to a
    // tracked field or gets cut. That is not a thing anyone can keep true by
    // remembering, so it is mechanised here -- every key the table declares is
    // swept through the binding, and a row that stops resolving fails a test
    // rather than printing a plausible number.
    //
    // The dossier's Dodge, Carried, Shop-price and Morale rows are what this
    // exists to prevent: four rows that read like data and were furniture.
    public class SystemMenuRunStatsTests
    {
        private string _root;
        private SystemMenuController _menu;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-runstats-tests-" + System.Guid.NewGuid().ToString("N"));
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

        // ---- the rule, mechanised ------------------------------------------------

        [Test]
        public void EveryDeclaredRowResolvesToAFigure()
        {
            var run = ARunThatHasDoneThings();

            foreach (var row in RunStatRows.AllRows)
            {
                Assert.IsTrue(RunStatsController.TryFigure(run, row.Key, out _),
                    $"the '{row.Key}' row has no tracked field behind it. Bind it or cut the row - " +
                    "a figure the game does not record is worse than an absent one, because the " +
                    "player believes it.");
            }
        }

        // NON-VACUITY. Without this, the sweep above would still pass if
        // TryFigure returned true for everything, which is exactly the shape a
        // careless default arm would take.
        [Test]
        public void AKeyNothingSuppliesIsRefusedRatherThanZeroed()
        {
            var run = ARunThatHasDoneThings();

            Assert.IsFalse(RunStatsController.TryFigure(run, "morale", out _),
                "an unbound key answered with a number");
            Assert.AreEqual("-", RunStatsController.Figure(run, "morale"),
                "an unbound row printed a figure instead of admitting it has none");
        }

        // ---- the figures are the run's own ----------------------------------------

        [Test]
        public void TheBattleFiguresAreTheWholePartysLedgerAddedUp()
        {
            var run = ARunThatHasDoneThings();

            // 300 + 40 physical, 120 + 10 other, across two characters.
            Assert.AreEqual("470", RunStatsController.Figure(run, "damage_dealt"));
            Assert.AreEqual("340", RunStatsController.Figure(run, "damage_physical"));
            Assert.AreEqual("130", RunStatsController.Figure(run, "damage_other"));
            Assert.AreEqual("95", RunStatsController.Figure(run, "damage_taken"));
            Assert.AreEqual("60", RunStatsController.Figure(run, "healed"));
            Assert.AreEqual("25", RunStatsController.Figure(run, "shielded"));
            Assert.AreEqual("7", RunStatsController.Figure(run, "kills"));
            Assert.AreEqual("1", RunStatsController.Figure(run, "times_downed"));
        }

        // deepestStep, not step. The party stands where they stand; the question
        // this row asks is how far down they got before they stopped.
        [Test]
        public void TheDepthShownIsTheDeepestReachedRatherThanWhereTheyStand()
        {
            var run = ARunThatHasDoneThings();
            run.step = 3;
            run.deepestStep = 11;

            Assert.AreEqual("11", RunStatsController.Figure(run, "deepest_room"));
        }

        // Held and earned diverge the moment anything is spent, and a shop
        // exists. One of them alone leaves the other unanswerable.
        [Test]
        public void GoldHeldAndGoldEarnedAreDifferentQuestions()
        {
            var run = ARunThatHasDoneThings();

            Assert.AreEqual("128", RunStatsController.Figure(run, "gold_held"));
            Assert.AreEqual("640", RunStatsController.Figure(run, "gold_earned"));
        }

        // Stacks count by their contents. Three salves in one slot are three
        // items in the pack, which is what the row asks.
        [Test]
        public void ThePackCountsItemsRatherThanStacks()
        {
            var run = ARunThatHasDoneThings();
            Assert.AreEqual("5", RunStatsController.Figure(run, "pack"));
        }

        [Test]
        public void FiguresPastAThousandAreGrouped()
        {
            var run = ARunThatHasDoneThings();
            run.ledger.Add(new RunLedgerEntry { characterId = "c", physicalDealt = 8000 });

            Assert.AreEqual("8,470", RunStatsController.Figure(run, "damage_dealt"),
                "a five-figure damage total is printed as a number you have to count");
        }

        // The pane sits on a run-only tab, so this should be unreachable through
        // the UI -- but Select() takes an index and nothing stops a caller
        // passing this one. Eighteen zeroes is a claim that a run went badly.
        [Test]
        public void WithNoRunEveryFigureReadsAsAbsentRatherThanZero()
        {
            foreach (var row in RunStatRows.AllRows)
            {
                Assert.AreEqual("-", RunStatsController.Figure(null, row.Key),
                    $"the '{row.Key}' row printed a number for a run that does not exist");
                Assert.AreEqual("-", RunStatsController.Figure(new RunSnapshot { hasRun = false }, row.Key),
                    $"the '{row.Key}' row printed a number for a run that has ended");
            }
        }

        // ---- and it reaches the screen ---------------------------------------------

        [UnityTest]
        public IEnumerator ThePaneFillsItselfFromTheRunWhenItIsOpened()
        {
            RunManager.StartRun(4242);
            RunLedger.RecordRoom(RunManager.Run, won: true, goldGained: 40, expGained: 15, step: 3);
            RunLedger.RecordRoom(RunManager.Run, won: true, goldGained: 25, expGained: 10, step: 5);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the Map scene has no SystemMenuController");

            _menu.Open();
            _menu.Select(SystemMenuTab.RunStats);
            yield return null;
            yield return null;

            Assert.AreEqual("2", TextOf("RunStatsValuerooms_cleared"),
                "the pane did not read the two rooms this run cleared");
            Assert.AreEqual("65", TextOf("RunStatsValuegold_earned"),
                "the pane did not read the gold this run earned");
            Assert.AreEqual("5", TextOf("RunStatsValuedeepest_room"),
                "the pane did not read how deep this run got");
        }

        // Only in a descent, and the tab bar is what enforces it -- but a pane
        // that CAN be selected with no run has to survive being selected.
        [UnityTest]
        public IEnumerator ThePaneSurvivesBeingOpenedWithNoRun()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            _menu.Open();
            _menu.Select(SystemMenuTab.RunStats);
            yield return null;
            yield return null;

            Assert.AreEqual("-", TextOf("RunStatsValuerooms_cleared"),
                "the pane claimed a run that does not exist");
        }

        // ---- fixture -----------------------------------------------------------------

        private string TextOf(string nodeName)
        {
            var go = _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == nodeName)?.gameObject;
            Assert.IsNotNull(go, $"the Run statistics pane has no '{nodeName}'");

            var label = go.GetComponent<TMP_Text>();
            Assert.IsNotNull(label, $"'{nodeName}' carries no text");
            return label.text;
        }

        // A run with a history, written literally rather than computed. Nothing
        // here re-derives a production sum to build its own expectation -- that
        // makes a test a tautology, and it has already masked a rounding flake
        // in this project once.
        private static RunSnapshot ARunThatHasDoneThings() => new RunSnapshot
        {
            hasRun = true,
            floor = 2,
            step = 9,
            deepestStep = 9,
            roomsCleared = 6,
            gold = 128,
            goldEarned = 640,
            expEarned = 210,
            bossesKilled = new List<string> { "boss_a" },
            relicIds = new List<string> { "relic_a", "relic_b" },
            inventory = new List<InventoryEntry>
            {
                new InventoryEntry { itemId = "salve", count = 3 },
                new InventoryEntry { itemId = "blade", count = 2 },
            },
            ledger = new List<RunLedgerEntry>
            {
                new RunLedgerEntry
                {
                    characterId = "a",
                    physicalDealt = 300, otherDealt = 120, damageTaken = 80,
                    healed = 40, shielded = 25, kills = 5, timesDowned = 1,
                },
                new RunLedgerEntry
                {
                    characterId = "b",
                    physicalDealt = 40, otherDealt = 10, damageTaken = 15,
                    healed = 20, shielded = 0, kills = 2, timesDowned = 0,
                },
            },
        };
    }
}
