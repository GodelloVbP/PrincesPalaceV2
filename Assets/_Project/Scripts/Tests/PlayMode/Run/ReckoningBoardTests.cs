using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's arrival and board (docs/PLAN_REWARD_SCREENS.md C1, C2,
    // C8, section 4.13): the crest, the gold stack counting from GoldBefore,
    // chips that paid as gold dissolving, the haul strip, the deeds, the
    // outcome form's medallion, and a menu opened over a running beat.
    // ReckoningTests holds the rings' three experience states.
    public class ReckoningBoardTests
    {
        private string _root;
        private ReckoningController _reckoning;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-board-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
            RunManager.StartRun(4242);
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            ReckoningController.SpeedMultiplier = 1f;
            ReckoningController.DeltaOverride = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        private IEnumerator Open()
        {
            yield return SharedScene.Ensure("Fight");
            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            _reckoning.gameObject.SetActive(false);
        }

        private IEnumerator Settled()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_reckoning.BoardSettled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_reckoning.BoardSettled, "the board never settled");
        }

        // Distinctive ledger numbers, so finding them on screen is no accident.
        private static CombatReward Reward(EncounterClass? encounter = null)
        {
            var reward = new CombatReward { GoldGained = 37, GoldBefore = 240, EncounterClass = encounter };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 3, 10, 3, 60, 100, 50));
            reward.Ledger.Dealt("shawn", DamageType.Physical, 1234);
            reward.Ledger.Dealt("shawn", DamageType.Fire, 567);
            reward.Ledger.Took("shawn", 890, shielded: 210);
            reward.Ledger.Restored("shawn", 345);
            reward.Ledger.ScoredKill("shawn");
            reward.Ledger.ScoredKill("shawn");
            return reward;
        }

        // The settlement record of a node whose Gear chip overflowed the page
        // limit and whose Book chip met an exhausted pool (both paid gold), a
        // potion and a book drawn, and the fight's own gold.
        private static CombatReward Haul()
        {
            var reward = Reward();
            reward.Prizes.Add(new PaidPrize(PrizeKind.Item, PrizeKind.Gold, 25));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Book, PrizeKind.Gold, 12));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Consumable, PrizeKind.Consumable, 1, new[] { "test_potion" }));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Book, PrizeKind.Book, 0, new[] { "test_book" }));
            reward.Prizes.Add(new PaidPrize(PrizeKind.None, PrizeKind.Gold, 37));
            return reward;
        }

        // ---- the gold stack and the haul (C2) ------------------------------------------

        [UnityTest]
        public IEnumerator TheGoldStackCountsFromGoldBeforeToEverythingPaidAsGold()
        {
            yield return Open();

            // The clock held still for the first look: PlayIn's first step runs
            // inside Show, so on the wall clock the count is already a tick or
            // two in.
            ReckoningController.DeltaOverride = () => 0f;
            _reckoning.Show(Haul(), new List<PendingPage>());
            yield return null;
            Assert.AreEqual("240", TextOf("ReckoningGoldCount"), "the count starts where the run stood");
            ReckoningController.DeltaOverride = null;

            yield return Settled();

            Assert.AreEqual("314", TextOf("ReckoningGoldCount"), "240 + 37 fight gold + 25 overflow + 12 exhausted pool");
        }

        [UnityTest]
        public IEnumerator AChipThatPaidAsGold_DissolvesAtTheStack_InPayOrder()
        {
            yield return Open();
            _reckoning.Show(Haul(), new List<PendingPage>());

            CollectionAssert.AreEqual(new[] { PrizeKind.Item, PrizeKind.Book }, _reckoning.DissolvedChips);
            yield return Settled();
            Assert.IsFalse(Named("ReckoningGoldDissolve").activeSelf, "the dissolve ends inside the wipe");
        }

        [UnityTest]
        public IEnumerator ThePotionAndTheBook_DropIntoTheHaulStrip()
        {
            yield return Open();
            _reckoning.Show(Haul(), new List<PendingPage>());
            yield return Settled();

            Assert.IsTrue(Named("ReckoningHaulChip0").activeSelf, "the potion");
            Assert.IsTrue(Named("ReckoningHaulChip1").activeSelf, "the book");
            Assert.IsFalse(Named("ReckoningHaulChip2").activeSelf, "nothing else was held");
            Assert.AreEqual("glyph_potion", Named("ReckoningHaulChip0Icon").GetComponent<Image>().sprite?.name);
            Assert.AreEqual("map_chip_book", Named("ReckoningHaulChip1Icon").GetComponent<Image>().sprite?.name);
            Assert.AreEqual(0f, ((RectTransform)Named("ReckoningHaulChip0").transform).anchoredPosition.y, 0.01f,
                "landed");
        }

        // ---- the board (C8) ----------------------------------------------------------

        [UnityTest]
        public IEnumerator TheDeedsAreDealtTakenHealedAndFelled()
        {
            yield return Open();
            _reckoning.Show(Reward(), new List<PendingPage>());
            yield return Settled();

            Assert.AreEqual("1801", TextOf("ReckoningDeeds0Count0"), "sword: dealt");
            Assert.AreEqual("890", TextOf("ReckoningDeeds0Count1"), "cracked shield: taken");
            Assert.AreEqual("345", TextOf("ReckoningDeeds0Count2"), "leaf: healed");
            Assert.AreEqual("2", TextOf("ReckoningDeeds0Count3"), "skull: felled");
            Assert.AreEqual("glyph_shield_cracked", Named("ReckoningDeeds0Glyph1").GetComponent<Image>().sprite?.name);
        }

        [UnityTest]
        public IEnumerator NoHealing_NoLeaf_AndAMemberWithNoLedgerLineReadsAsZeroes()
        {
            yield return Open();
            var reward = new CombatReward { GoldGained = 5 };
            reward.Characters.Add(new CharacterReward("owl", "Odette", 3, 10, 3, 60, 100, 50));
            _reckoning.Show(reward, new List<PendingPage>());
            yield return Settled();

            Assert.AreEqual("0", TextOf("ReckoningDeeds0Count0"));
            Assert.IsFalse(Named("ReckoningDeeds0Glyph2").GetComponent<Image>().enabled, "no leaf when nothing was healed");
            Assert.AreEqual("0", TextOf("ReckoningDeeds0Count3"));
        }

        // ---- the crest (C1) ----------------------------------------------------------

        [UnityTest]
        public IEnumerator TheCrestCarriesTheRoomsMedallion()
        {
            yield return Open();
            _reckoning.Show(Reward(EncounterClass.Boss), new List<PendingPage>());
            yield return Settled();

            Assert.AreEqual("map_node_boss", Named("ReckoningCrestMedallion").GetComponent<Image>().sprite?.name);
            Assert.IsFalse(Named("ReckoningCrestCrack").activeSelf);

            _reckoning.gameObject.SetActive(false);
            _reckoning.Show(Reward(null), new List<PendingPage>());
            yield return Settled();
            Assert.AreEqual("map_node_fight", Named("ReckoningCrestMedallion").GetComponent<Image>().sprite?.name,
                "an event fight shows the plain fight medallion");
        }

        private IEnumerator ShowOutcome(FightEnding ending)
        {
            yield return Open();
            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn") };
            _reckoning.Show(ReckoningRequest.Outcome(ending, new List<string>(), new List<string>(), tally, new CombatLedger()));
            yield return Settled();
        }

        [UnityTest]
        public IEnumerator Fell_CracksTheMedallion()
        {
            yield return ShowOutcome(FightEnding.Fell);

            Assert.IsTrue(Named("ReckoningCrestCrack").activeSelf, "the medallion is cracked");
            Assert.AreEqual("reward_crack", Named("ReckoningCrestCrack").GetComponent<Image>().sprite?.name);
        }

        [UnityTest]
        public IEnumerator Endured_DimsTheMedallionButLeavesItWhole()
        {
            yield return ShowOutcome(FightEnding.Endured);

            Assert.IsFalse(Named("ReckoningCrestCrack").activeSelf, "whole");
            Assert.Less(Named("ReckoningCrestMedallion").GetComponent<Image>().color.r, 0.6f, "dimmed");
        }

        [UnityTest]
        public IEnumerator APaidNothingVictory_IsThePlainMedallion()
        {
            yield return ShowOutcome(FightEnding.Victory);

            Assert.IsFalse(Named("ReckoningCrestCrack").activeSelf);
            Assert.AreEqual(Color.white, Named("ReckoningCrestMedallion").GetComponent<Image>().color);
        }

        // ---- a menu over a running beat (section 4.13) ---------------------------------

        [UnityTest]
        public IEnumerator AMenuOpenedOverARunningBeat_SettlesItFirst()
        {
            yield return Open();
            ReckoningController.SpeedMultiplier = 1f;
            var reward = Haul();
            reward.Characters.Clear();
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 2, 20, 3, 50, 100, 130, expToNextBefore: 100));
            _reckoning.Show(reward, new List<PendingPage>());
            yield return null;
            Assert.IsFalse(_reckoning.BoardSettled, "precondition: the wipe and the rings are running");

            var stack = NavigationInputModule.Contexts;
            Assert.IsNotNull(stack, "fixture: the scene has no navigation stack");
            var menu = new NavContext(null, new Dictionary<string, object>(), cancel: () => { });
            stack.Push(menu);
            try
            {
                Assert.IsTrue(_reckoning.BoardSettled, "the menu found a beat still running");
                Assert.AreEqual("314", TextOf("ReckoningGoldCount"), "the count is in");
                Assert.AreEqual("3", TextOf("ReckoningPortrait0Level"), "the level-up landed");
                Assert.AreEqual(0.5f, Named("ReckoningPortrait0RingGain").GetComponent<Image>().fillAmount, 0.001f);
                Assert.IsTrue(Named("ReckoningHaulChip1").activeSelf);
            }
            finally
            {
                stack.Remove(menu);
            }
        }

        // ---- the frame ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheFrameSettlesAtRestRatherThanStayingLifted()
        {
            yield return Open();
            _reckoning.Show(Reward(), new List<PendingPage>());
            yield return Settled();

            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            Assert.AreEqual(0f, wipe.anchoredPosition.y, 0.01f, "the frame never finished lifting");
            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelWidth, wipe.rect.width, 1f);
        }

        [UnityTest]
        public IEnumerator TheGloomEndsFullyOnRatherThanPartWayThrough()
        {
            yield return Open();
            _reckoning.Show(Reward(), new List<PendingPage>());
            yield return Settled();

            Assert.Greater(Named("ReckoningPanelDimmer").GetComponent<Image>().color.a, 0.6f,
                "the gloom never finished fading up");
        }
    }
}
