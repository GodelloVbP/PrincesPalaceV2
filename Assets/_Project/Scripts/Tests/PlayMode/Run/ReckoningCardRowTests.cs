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
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The card face and the row on the real Reckoning (docs/PLAN_REWARD_SCREENS.md
    // C5): cards group by wearer, the band and chevron are painted, and the pad
    // walks the cards in row order and then off the page.
    //
    // THE FIGHT SCENE IS SHARED (SharedScene); see ReckoningGamepadNavigationTests
    // for what is put away between tests.
    public class ReckoningCardRowTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-card-row-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;
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

        // A one-chip page whose cards are dealt bear, sheep, owl: the squad is
        // sheep, owl, bear, so the ROW is page 1, page 2, page 0.
        private static IReadOnlyList<PendingPage> ScrambledPage()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            SaveSlotManager.CurrentSave.selectedCharacterIds = new List<string> { "sheep", "owl", "bear" };
            pages[0].Cards[0].RecipientId = "bear";
            pages[0].Cards[1].RecipientId = "sheep";
            pages[0].Cards[2].RecipientId = "owl";
            return pages;
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

        private static float X(string card) => ((RectTransform)Part<Button>(card).transform).anchoredPosition.x;

        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return null;
            _input.ClearOneFrameFlags();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;

            // A frame at rest, so the next press is a new move rather than a
            // held stick.
            yield return null;
        }

        private static void AssertSelected(string name, string because)
        {
            var expected = Part<Selectable>(name);
            Assert.IsNotNull(expected, $"no control named {name}");
            Assert.AreEqual(expected.gameObject, EventSystem.current.currentSelectedGameObject, because);
        }

        [UnityTest]
        public IEnumerator CardsSitInRowOrder_NotPageOrder()
        {
            yield return Show(ScrambledPage());

            Assert.Less(X("ReckoningOffer1"), X("ReckoningOffer2"), "sheep's card is first in the row, owl's second");
            Assert.Less(X("ReckoningOffer2"), X("ReckoningOffer0"), "bear's card is last");
        }

        [UnityTest]
        public IEnumerator RightWalksTheRowThenLeavesThePage()
        {
            yield return Show(ScrambledPage());

            AssertSelected("ReckoningOffer1", "the screen opens on the leftmost card in row order");

            yield return Press(1f, 0f);
            AssertSelected("ReckoningOffer2", "Right goes to the next card in row order, not the next page index");

            yield return Press(1f, 0f);
            AssertSelected("ReckoningOffer0", "the third card in row order is page 0");

            yield return Press(1f, 0f);
            AssertSelected("ReckoningLeaveButton", "Right from the last card is the way off the page");
        }

        [UnityTest]
        public IEnumerator AFiveCardRowIsNarrowerThanAThreeCardRow()
        {
            yield return Show(ReckoningPageFixtures.OneGearPage());
            float three = ((RectTransform)Part<Button>("ReckoningOffer0").transform).sizeDelta.x;

            _reckoning.Show(Reward(), ReckoningPageFixtures.BossAndTwoAnchors());
            yield return null;
            float five = ((RectTransform)Part<Button>("ReckoningOffer0").transform).sizeDelta.x;

            Assert.Less(five, three);
            Assert.IsTrue(Part<Button>("ReckoningOffer4").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator AGearCardPaintsItsBandAndChevronAndARelicCardDoesNot()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            Assert.AreEqual(RewardPageKind.Gear, pages[0].Kind, "fixture: the first page is gear");
            yield return Show(pages);

            Assert.IsTrue(Part<TMP_Text>("ReckoningOffer0BandTier").gameObject.activeSelf);
            StringAssert.StartsWith("T", Part<TMP_Text>("ReckoningOffer0BandTier").text);
            StringAssert.StartsWith("+", Part<TMP_Text>("ReckoningOffer0BandPlus").text);
            Assert.IsTrue(Part<TMP_Text>("ReckoningOffer0Chevron").gameObject.activeSelf);
            StringAssert.DoesNotContain("+", Part<TMP_Text>("ReckoningOffer0Name").text, "honing is on the band");

            var relicPage = pages.First(p => p.Kind == RewardPageKind.Relic);
            _reckoning.Show(Reward(), new List<PendingPage> { relicPage });
            yield return null;

            Assert.IsFalse(Part<TMP_Text>("ReckoningOffer0BandTier").gameObject.activeSelf);
            Assert.IsFalse(Part<TMP_Text>("ReckoningOffer0Chevron").gameObject.activeSelf);
            Assert.IsFalse(Part<Image>("ReckoningOffer0WearerDisc").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator FocusingACardShowsTheDetailAndLeavingHidesIt()
        {
            yield return Show(ReckoningPageFixtures.OneGearPage());

            var tip = Part<RectTransform>("ReckoningCardTip");
            Assert.IsNotNull(tip, "the tree has no focus detail");

            yield return Press(1f, 0f);
            yield return null;
            Assert.IsTrue(tip.gameObject.activeSelf, "the focused card's detail is not showing");
            Assert.IsNotEmpty(Part<TMP_Text>("ReckoningCardTipTitle").text);
        }
    }
}
