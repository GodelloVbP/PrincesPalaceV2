using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE CHOICE ACTIONS ON THE REAL SCREEN (docs/PLAN_REWARD_SCREENS.md C6):
    // take, hone instead, the coin case and leave drive the real RewardPages and
    // leave the save they always did; the slot bands answer the focused
    // whetstone and door; spent cards read as their outcome; and one gate (Act)
    // refuses every press while a reveal beat runs.
    public class ReckoningChoiceTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-choice-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private IEnumerator Show(IReadOnlyList<PendingPage> pages)
        {
            yield return SharedScene.Ensure("Fight");

            _input = NavSceneReuse.TakeOverInput();
            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            _reckoning.Show(Reward(), pages);
            yield return null;
            yield return null;
        }

        private static T Part<T>(string name) where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(c => c.name == name && c.gameObject.scene.IsValid());

        private static void Click(string name) => Part<Button>(name).onClick.Invoke();

        private static void Submit(string name) =>
            ExecuteEvents.Execute(Part<Button>(name).gameObject, new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler);

        // Flights end in well under a frame at 40x; a few frames settle them.
        private static IEnumerator Settle()
        {
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Focus(string name)
        {
            EventSystem.current.SetSelectedGameObject(Part<Button>(name).gameObject);
            yield return null;
            yield return null;
        }

        private static RunSnapshot Run => RunManager.Run;

        private static string BandPlus(int card) => Part<TMP_Text>($"ReckoningOffer{card}BandPlus").text;

        private static Color32 BandColour(int card) => Part<Image>($"ReckoningOffer{card}Band").color;

        private static readonly Color32 Rest = new Color32(29, 18, 38, 230);
        private static readonly Color32 Spark = new Color32(107, 79, 31, 230);
        private static readonly Color32 Taken = new Color32(64, 48, 22, 230);

        // ---- take --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TakeEquipsTheCopy_BagsTheDisplacedOne_AndLeavesAnEmptyPlinthWithALitBand()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var page = pages[0];
            var card = page.Cards[0];
            var save = SaveSlotManager.CurrentSave;
            var recipient = save.roster.First(c => c.definitionId == card.RecipientId);
            var incumbent = ItemInstance.Of(card.Copy.ItemId, 0);
            recipient.equipment.Put(card.Slot, incumbent);
            int bagBefore = save.stockpiledItems.Sum(e => e.count);
            yield return Show(pages);
            Assert.IsTrue(Part<Image>("ReckoningOffer0Icon").enabled, "fixture: the item has art, so 'empty plinth' is not vacuous");

            Click("ReckoningOffer0");
            yield return Settle();

            Assert.AreEqual(CardOutcome.Taken, card.Outcome);
            Assert.AreEqual(card.Copy.ItemId, recipient.equipment.GetInstance(card.Slot).ItemId);
            Assert.AreEqual(card.Copy.Tier, recipient.equipment.GetInstance(card.Slot).Tier);
            Assert.AreEqual(incumbent.ItemId, card.Displaced.ItemId, "the page records the copy it displaced");
            Assert.AreEqual(bagBefore + 1, save.stockpiledItems.Sum(e => e.count), "the displaced copy is in the bag");
            Assert.IsTrue(save.stockpiledItems.Any(e => e.itemId == incumbent.ItemId && e.tier == 0));

            Assert.IsFalse(Part<Image>("ReckoningOffer0Icon").enabled, "a taken card's plinth is empty");
            Assert.AreEqual(Taken, BandColour(0), "its band stays lit: the honing came with the slot");
            Assert.IsFalse(Part<Button>("ReckoningOffer0").interactable);
            Assert.IsFalse(Part<Button>("ReckoningHone0").gameObject.activeSelf, "no whetstone under a spent card");
        }

        [UnityTest]
        public IEnumerator ASecondCardForTheSameSlotRepaintsAgainstTheNewlyWornItem()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            var page = pages[0];
            var first = page.Cards[0];
            var second = page.Cards[1];
            second.RecipientId = first.RecipientId;
            second.Slot = first.Slot;
            second.Copy = ItemInstance.Of(first.Copy.ItemId, first.Copy.Tier + 1);
            var recipient = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == first.RecipientId);
            recipient.equipment.Put(first.Slot, null);
            yield return Show(pages);

            Assert.IsFalse(Part<Image>("ReckoningOffer1Ghost").enabled, "fixture: an empty slot has no ghost");

            Click("ReckoningOffer0");
            yield return Settle();

            Assert.IsTrue(Part<Image>("ReckoningOffer1Ghost").enabled, "the second card now compares with the taken item");
            Assert.AreEqual(CardOutcome.Open, second.Outcome);
            Assert.IsTrue(Part<Button>("ReckoningOffer1").interactable);
        }

        // ---- hone instead --------------------------------------------------------------

        [UnityTest]
        public IEnumerator HoneInsteadHonesTheSlot_CrumblesTheItemAndTicksTheBand()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];
            yield return Show(pages);
            Assert.AreEqual("+0", BandPlus(0));

            Click("ReckoningHone0");
            yield return Settle();

            Assert.AreEqual(CardOutcome.Honed, card.Outcome);
            Assert.AreEqual(1, SlotHoning.PlusOf(Run.slotHoning, card.RecipientId, card.Slot));
            Assert.AreEqual("+1", BandPlus(0), "the numeral ticks up on the wearer's slot band");
            var icon = Part<Image>("ReckoningOffer0Icon");
            Assert.AreEqual(0.25f, icon.color.a, 0.001f, "dust");
            Assert.AreEqual(0.6f, icon.rectTransform.localScale.x, 0.001f);
        }

        [UnityTest]
        public IEnumerator FocusingTheWhetstoneShowsPlusNToPlusNPlusOneAndTheWornStatsNowToNext()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];
            var recipient = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == card.RecipientId);
            recipient.equipment.Put(card.Slot, ItemInstance.Of(card.Copy.ItemId, 3));
            yield return Show(pages);

            yield return Focus("ReckoningHone0");

            Assert.AreEqual("+0>+1", BandPlus(0));
            Assert.AreEqual(Spark, BandColour(0), "the band shows its waiting spark");
            Assert.IsTrue(Part<RectTransform>("ReckoningCardTip").gameObject.activeSelf);
            StringAssert.Contains("→", Part<TMP_Text>("ReckoningCardTipBody").text, "now -> next");
            Assert.AreEqual(0, SlotHoning.PlusOf(Run.slotHoning, card.RecipientId, card.Slot), "a preview writes nothing");
            Assert.AreEqual(0, ContentDatabase.HoningOf(recipient, card.Slot), "and the preview is disposed");

            yield return Focus("ReckoningOffer0");

            Assert.AreEqual("+0", BandPlus(0));
            Assert.AreEqual(Rest, BandColour(0));
        }

        // ---- the coin case ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator WhenEveryShownSlotIsAtMaxPlusTheWhetstoneIsACoinStackThatPaysTheCardsPrice()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var page = pages[0];
            foreach (var c in page.Cards)
            {
                for (int i = 0; i < ItemUpgrade.MaxPlus; i++) SlotHoning.Hone(Run.slotHoning, c.RecipientId, c.Slot);
            }

            var card = page.Cards[0];
            int goldBefore = Run.gold;
            yield return Show(pages);

            var label = Part<Button>("ReckoningHone0").GetComponentInChildren<TMP_Text>();
            StringAssert.EndsWith("g", label.text, "the coin stack shows the amount");

            Click("ReckoningHone0");
            yield return Settle();

            Assert.AreEqual(CardOutcome.Paid, card.Outcome);
            Assert.Greater(card.GoldPaid, 0);
            Assert.AreEqual(goldBefore + card.GoldPaid, Run.gold, "the price is credited once");
            Assert.AreEqual(ItemUpgrade.MaxPlus, SlotHoning.PlusOf(Run.slotHoning, card.RecipientId, card.Slot));
            Assert.AreEqual(UiStrings.ReckoningCoinsMark.Format(card.GoldPaid), Part<TMP_Text>("ReckoningOffer0Delta").text,
                "a sold card leaves its coin mark");
        }

        // ---- leave ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheDoorFaceShowsWhatLeavingPays_AndItsSparksLandOnATakenCardsBand()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            var page = pages[0];
            var first = page.Cards[0];
            yield return Show(pages);

            Click("ReckoningOffer0");
            yield return Settle();
            Assert.AreEqual(2, page.ChoicesLeft);
            Assert.AreEqual(UiStrings.ReckoningLeaveHones.Format(2),
                Part<Button>("ReckoningLeaveButton").GetComponentInChildren<TMP_Text>().text,
                "two unused choices, two whetstones");

            yield return Focus("ReckoningLeaveButton");

            Assert.AreEqual(Spark, BandColour(0), "the taken card's band keeps its waiting spark");
            Assert.AreEqual("+0>+2", BandPlus(0));

            Click("ReckoningLeaveButton");
            yield return Settle();

            Assert.IsTrue(page.Closed);
            Assert.AreEqual(2, page.UnusedHones);
            Assert.AreEqual(2, SlotHoning.PlusOf(Run.slotHoning, first.RecipientId, first.Slot));
            Assert.AreEqual("+2", BandPlus(0));
            Assert.AreEqual(UiStrings.ReckoningForward.Format(),
                Part<Button>("ReckoningLeaveButton").GetComponentInChildren<TMP_Text>().text,
                "a spent page's door is the way forward");

            var unspent = Part<Image>("ReckoningOffer1Icon");
            Assert.AreEqual(0.45f, unspent.color.a, 0.001f, "cards nobody chose are dimmed");
        }

        [UnityTest]
        public IEnumerator ARelicPageDoorPaysCoinsAndNoBandLights()
        {
            var chips = new List<EncounterClass> { EncounterClass.Elite };
            var pages = ReckoningPageFixtures.Settle(new List<EncounterClass>(), chips);
            var page = pages.Single(p => p.Kind == RewardPageKind.Relic);
            yield return Show(pages);

            var preview = RewardPages.PreviewLeave(SaveSlotManager.CurrentSave, page);
            Assert.Greater(preview.Gold, 0, "fixture: an unused relic choice pays");
            Assert.AreEqual(UiStrings.ReckoningCoinsMark.Format(preview.Gold),
                Part<Button>("ReckoningLeaveButton").GetComponentInChildren<TMP_Text>().text);

            int goldBefore = Run.gold;
            Click("ReckoningLeaveButton");
            yield return Settle();

            Assert.AreEqual(goldBefore + preview.Gold, Run.gold);
        }

        // ---- the one gate ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WhileABeatRunsNoControlTakesHonesOrLeaves_ByMouseOrSubmit()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var page = pages[0];
            int goldBefore = Run.gold;
            yield return Show(pages);

            int skipped = 0;
            _reckoning.BeatRunning = () => true;
            _reckoning.SkipBeat = () => skipped++;

            Click("ReckoningOffer0");
            Submit("ReckoningOffer0");
            Click("ReckoningHone0");
            Submit("ReckoningHone0");
            Click("ReckoningLeaveButton");
            Submit("ReckoningLeaveButton");
            yield return Settle();

            Assert.AreEqual(6, skipped, "each press completes the beat");
            Assert.IsTrue(page.Cards.All(c => c.Outcome == CardOutcome.Open), "nothing was taken, honed or sold");
            Assert.IsFalse(page.Closed, "the page was not left");
            Assert.AreEqual(goldBefore, Run.gold);
            Assert.AreEqual(0, page.UnusedHones);

            _reckoning.BeatRunning = () => false;
            Submit("ReckoningOffer0");
            yield return Settle();

            Assert.AreEqual(CardOutcome.Taken, page.Cards[0].Outcome, "with no beat running the press acts at once");
        }

        // ---- the old words are gone --------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheHeadingTheFavorLineTheLedgerAndTheActionCaptionsAreGone()
        {
            yield return Show(ReckoningPageFixtures.OneGearPage());

            Assert.IsNull(Part<TMP_Text>("ReckoningOfferHeading"));
            Assert.IsNull(Part<TMP_Text>("ReckoningOfferFavor"));
            Assert.IsNull(Part<TMP_Text>("ReckoningOfferLedger"));
            Assert.IsNull(Part<TMP_Text>("ReckoningOffer0Action"));
        }
    }
}
