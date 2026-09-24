using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The last hop of the combat ledger: settlement -> defeat screen.
    //
    // DefeatScreenTests audits the TREE and nothing drove the CONTROLLER at
    // all, which is the same shape as the relic bug -- both ends built, tested
    // separately, and nothing asserting they were joined. The numbers a whole
    // run's worth of counting produces are only worth anything if they arrive
    // on screen.
    public class DefeatScreenWiringTests
    {
        private string _root;
        private DefeatController _defeat;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-defeat-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();

            // Every global this fixture touched, plus the ones it did not --
            // one call, so the list cannot go stale here while it grows
            // somewhere else. See TestGlobals.
            TestGlobals.ResetAll();

            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _defeat.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        // THE FIGHT IS SHARED ACROSS THIS FIXTURE (SharedScene). Every test
        // Shows the screen with nearly the same settlement, so what the last
        // Show painted would satisfy the next test's label checks even if its
        // own Show painted nothing. The screen is hidden again, and every
        // label and row Paint is responsible for is blanked and hidden, so
        // each test sees only what its own Show wrote.
        private static readonly System.Text.RegularExpressions.Regex PaintedLabel =
            new System.Text.RegularExpressions.Regex(@"^(DefeatGoldLost|DefeatExp|DefeatDepth|DefeatEmbers|DefeatRow\d+(Stats|Name))$");
        private static readonly System.Text.RegularExpressions.Regex PaintedRow =
            new System.Text.RegularExpressions.Regex(@"^DefeatRow\d+$");

        private IEnumerator OpenTheFight()
        {
            yield return SharedScene.Ensure("Fight");

            _defeat = Object.FindAnyObjectByType<DefeatController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_defeat, "the defeat screen was never wired into the fight scene");

            _defeat.gameObject.SetActive(false);
            foreach (var label in _defeat.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                if (PaintedLabel.IsMatch(label.name)) label.text = "";
            foreach (var node in _defeat.GetComponentsInChildren<Transform>(includeInactive: true))
                if (PaintedRow.IsMatch(node.name)) node.gameObject.SetActive(false);
        }

        // A settlement whose numbers are distinctive enough that finding them
        // on screen cannot be a coincidence.
        private static RunSettlement.Result Settlement(string characterId)
        {
            var result = new RunSettlement.Result
            {
                GoldLost = 247,
                RoomsCleared = 13,
                DeepestStep = 29,
                ExpEarned = 861,
                EmbersEarned = 2,
                Ledger = new List<RunLedgerEntry>
                {
                    new RunLedgerEntry
                    {
                        characterId = characterId,
                        physicalDealt = 1234,
                        otherDealt = 567,
                        damageTaken = 890,
                        healed = 345,
                    },
                },
            };

            return result;
        }

        [UnityTest]
        public IEnumerator TheScreenStartsHiddenAndShowOpensIt()
        {
            SharedScene.MarkDirty("asserts the defeat screen is hidden in a freshly loaded fight, which OpenTheFight's reset would force");
            yield return OpenTheFight();
            Assert.IsFalse(_defeat.gameObject.activeSelf);

            _defeat.Show(Settlement("shawn"));
            yield return null;

            Assert.IsTrue(_defeat.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator WhatWasLostAndKeptReachesTheLabels()
        {
            yield return OpenTheFight();

            _defeat.Show(Settlement("shawn"));
            yield return null;

            StringAssert.Contains("247", TextOf("DefeatGoldLost"), "the forfeited gold never arrived");
            StringAssert.Contains("861", TextOf("DefeatExp"));
            StringAssert.Contains("29", TextOf("DefeatDepth"));
            StringAssert.Contains("13", TextOf("DefeatDepth"));
            StringAssert.Contains("2", TextOf("DefeatEmbers"));
        }

        [UnityTest]
        public IEnumerator ARunsCombatLedgerArrivesOnTheCharacterRow()
        {
            // THE hop this class exists for. Every number below was counted by
            // CombatLedger during a fight, folded into the run, carried out by
            // RunSettlement, and has to survive one more step to mean anything.
            yield return OpenTheFight();

            string id = SaveSlotManager.CurrentSave.ActiveSquad().First().definitionId;

            _defeat.Show(Settlement(id));
            yield return null;

            Assert.IsTrue(Named("DefeatRow0").activeSelf, "the squad's own row is not showing");

            string stats = TextOf("DefeatRow0Stats");
            StringAssert.Contains("1801", stats, "total dealt (1234 + 567) is missing");
            StringAssert.Contains("1234", stats, "physical split is missing");
            StringAssert.Contains("567", stats, "elemental split is missing");
            StringAssert.Contains("890", stats, "damage taken is missing");
            StringAssert.Contains("345", stats, "healing is missing");
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoLedgerLineReadsAsZeroesRatherThanBlank()
        {
            // A squad member who was benched all run is a true statement worth
            // rendering. A missing row is a bug the player has to guess at.
            yield return OpenTheFight();

            _defeat.Show(Settlement("somebody_who_was_not_there"));
            yield return null;

            Assert.IsTrue(Named("DefeatRow0").activeSelf);
            StringAssert.Contains("0", TextOf("DefeatRow0Stats"));
            Assert.IsNotEmpty(TextOf("DefeatRow0Name"), "the row still names who it is about");
        }

        [UnityTest]
        public IEnumerator NoNewBossesSaysSoRatherThanShowingAZero()
        {
            // "+0 EMBERS" reads as the payout being broken. A sentence explains
            // why there was nothing to pay, and teaches the ember source.
            yield return OpenTheFight();

            var settlement = Settlement("shawn");
            settlement.EmbersEarned = 0;

            _defeat.Show(settlement);
            yield return null;

            StringAssert.DoesNotContain("+0", TextOf("DefeatEmbers"));
        }

        [UnityTest]
        public IEnumerator ShowingANullSettlementDegradesRatherThanThrowing()
        {
            yield return OpenTheFight();

            Assert.DoesNotThrow(() => _defeat.Show(null));
            yield return null;

            Assert.IsTrue(_defeat.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator BothExitsRaiseTheirOwnEvent()
        {
            // Two different intentions -- leave, and go look at what survived.
            yield return OpenTheFight();

            int dismissed = 0, inspected = 0;
            _defeat.Dismissed = () => dismissed++;
            _defeat.InspectRequested = () => inspected++;

            _defeat.Show(Settlement("shawn"));
            yield return null;

            Named("DefeatReturnButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Named("DefeatInspectButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.AreEqual(1, dismissed);
            Assert.AreEqual(1, inspected);
        }
    }
}
