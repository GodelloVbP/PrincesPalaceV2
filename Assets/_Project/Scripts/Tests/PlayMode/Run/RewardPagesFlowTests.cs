using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE I2b GATE (docs/PLAN_ITEMS.md section 4): a boss and two anchors
    // settle together, the player takes on page 1, the Reckoning is entered
    // again (the pages live in memory on the run), and the unused choices are
    // left behind -- driven through the screen, not through RewardPages, so
    // what is asserted is what a player's presses do.
    public class RewardPagesFlowTests
    {
        private string _root;
        private ReckoningController _reckoning;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-pages-flow-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Press(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private string Text(string name) => Named(name).GetComponent<TMP_Text>().text;

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private IEnumerator Open(IReadOnlyList<PendingPage> pages)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            _reckoning.Show(Reward(), pages);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABossAndTwoAnchors_TakeOnPageOne_ReEnter_LeaveWithUnusedChoices()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            var run = RunManager.Run;
            var gear = pages.Single(p => p.Kind == RewardPageKind.Gear);
            var relic = pages.Single(p => p.Kind == RewardPageKind.Relic);

            Assert.AreEqual((5, 3), (gear.Cards.Count, gear.Choices), "fixture: three gear chips are five cards, three choices");
            Assert.AreEqual(3, relic.Choices, "fixture: three relic chips are three choices");
            Assert.AreSame(pages, run.pendingPages, "the settlement queued its pages on the run");
            int goldBefore = run.gold;

            yield return Open(pages);

            // ---- page 1: gear, take one --------------------------------------------------
            StringAssert.Contains("BOSS GEAR PAGE", Text("ReckoningOfferHeading"));
            StringAssert.Contains("3 CHOICES LEFT", Text("ReckoningOfferHeading"));
            StringAssert.Contains("PRINCE'S FAVOR", Text("ReckoningOfferFavor"));
            Assert.AreEqual(5, Enumerable.Range(0, 5).Count(i => Named($"ReckoningOffer{i}").activeSelf));

            var taken = gear.Cards[0];
            Press("ReckoningOffer0");
            yield return null;

            Assert.AreEqual(CardOutcome.Taken, taken.Outcome);
            var recipient = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == taken.RecipientId);
            Assert.AreEqual(taken.Copy.ItemId, recipient.equipment.GetInstance(taken.Slot).ItemId,
                "the taken card is worn by its recipient");
            StringAssert.Contains("2 CHOICES LEFT", Text("ReckoningOfferHeading"));

            // ---- re-enter: the pages survive in memory ------------------------------------
            _reckoning.gameObject.SetActive(false);
            _reckoning.Show(Reward(), RewardPages.Pages(run));
            yield return null;
            yield return null;

            Assert.AreSame(gear, _reckoning.CurrentPage, "re-entering resumes at the first page with choices left");
            StringAssert.Contains("2 CHOICES LEFT", Text("ReckoningOfferHeading"));
            StringAssert.Contains("TAKEN", Text("ReckoningOffer0Action"));
            Assert.IsFalse(Named("ReckoningOffer0").GetComponent<Button>().interactable, "a spent card stays spent");

            // ---- leave page 1 with two choices unused -------------------------------------
            Press("ReckoningLeaveButton");
            yield return null;

            Assert.IsTrue(gear.Closed);
            Assert.AreEqual(2, gear.UnusedHones, "each unused choice hones the first card's slot");
            Assert.AreEqual(2, SlotHoning.PlusOf(run.slotHoning, taken.RecipientId, taken.Slot));
            StringAssert.Contains("honed 2", Text("ReckoningOfferLedger"));

            // ---- page 2: relics, take one, leave two --------------------------------------
            Press("ReckoningLeaveButton");
            yield return null;

            Assert.AreSame(relic, _reckoning.CurrentPage);
            StringAssert.Contains("BOSS RELIC PAGE", Text("ReckoningOfferHeading"));
            StringAssert.Contains("3 CHOICES LEFT", Text("ReckoningOfferHeading"));

            var chosen = relic.Cards[0];
            Press("ReckoningOffer0");
            yield return null;
            CollectionAssert.Contains(run.relicIds, chosen.RelicId);

            int goldBeforeLeaving = run.gold;
            Press("ReckoningLeaveButton");
            yield return null;

            Assert.IsTrue(relic.Closed);
            Assert.Greater(relic.UnusedGold, 0, "two unused relic choices pay gold");
            Assert.AreEqual(0, relic.UnusedGold % 2, "the same amount for each unused choice");
            Assert.AreEqual(goldBeforeLeaving + relic.UnusedGold, run.gold, "leaving credits exactly what it paid");
            Assert.AreEqual(goldBefore + relic.UnusedGold, run.gold, "and nothing else paid in between");
            StringAssert.Contains("paid", Text("ReckoningOfferLedger"));

            // ---- both pages spent: on to the summary ---------------------------------------
            Press("ReckoningLeaveButton");
            yield return null;
            yield return null;

            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf);
            Assert.IsNull(RewardPages.Current(run), "no page has choices left");
            Assert.IsTrue(pages.All(p => p.Closed && p.ChoicesLeft == 0));
        }
    }
}
