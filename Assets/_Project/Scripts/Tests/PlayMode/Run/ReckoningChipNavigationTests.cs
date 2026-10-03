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
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE RECKONING'S COLUMN WITH THE "GAINED" CHIPS (docs/PLAN_BUGS_QA.md
    // C2, T7): tabs, the relic pager when shown, the chip rail, Continue --
    // every element reachable from Continue and back, on every tab, with 0 and
    // 2 chips, with and without a second relic page; chip focus opens the
    // tooltip, which follows Left/Right; Submit on a chip returns to Continue.
    //
    // Driven through the production NavigationInputModule with a scripted
    // input source (ReckoningGamepadNavigationTests' shape), on the shared
    // Fight scene.
    public class ReckoningChipNavigationTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reck-chips-" + System.Guid.NewGuid().ToString("N"));
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
            if (_reckoning != null && _reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private static List<string> RelicIds(int count)
        {
            var ids = ContentDatabase.Relics.Where(r => r != null).Select(r => r.id).Take(count).ToList();
            Assert.AreEqual(count, ids.Count, $"fixture: content has fewer than {count} relics");
            return ids;
        }

        // A run holding `held` relics (more than a page's four shows the
        // pager on the relics tab), and a Reckoning with `chips` receipts.
        private IEnumerator ShowTheReckoning(int held, int chips)
        {
            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();

            RunManager.StartRun(41UL);
            RunManager.Run.relicIds = RelicIds(held);

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            var receipts = RelicIds(chips + 1).Skip(1).ToList();
            _reckoning.Show(ReckoningRequest.Full(Reward(), new List<ItemOffer>(), receipts));
            yield return null;
            AssertSelected("ReckoningContinueButton", "the summary opens on Continue");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private IEnumerator Up() => Press(0f, 1f);
        private IEnumerator Down() => Press(0f, -1f);
        private IEnumerator Left() => Press(-1f, 0f);
        private IEnumerator Right() => Press(1f, 0f);

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null;
        }

        private static string Selected => EventSystem.current.currentSelectedGameObject?.name;

        private static void AssertSelected(string name, string because) => Assert.AreEqual(name, Selected, because);

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())?.gameObject;

        private static string TooltipName =>
            Node("ReckoningGainedTooltipName")?.GetComponent<TMP_Text>()?.text;

        // The column on tab `tab`, top to bottom, as the C2 graph declares it.
        private static List<string> Column(int tab, bool pager, int chips)
        {
            var column = new List<string> { "ReckoningTab" + tab };
            if (pager) column.Add("ReckoningRelicPrevPage");
            if (chips > 0) column.Add("ReckoningGainedChip0");
            column.Add("ReckoningContinueButton");
            return column;
        }

        // Up from Continue to the tab, asserting each stop, then Down back.
        private IEnumerator WalkTheColumn(List<string> column, string label)
        {
            for (int i = column.Count - 2; i >= 0; i--)
            {
                yield return Up();
                AssertSelected(column[i], $"{label}: Up from {column[i + 1]}");
            }

            yield return Up();
            AssertSelected(column[0], $"{label}: the tabs are the top");

            for (int i = 1; i < column.Count; i++)
            {
                yield return Down();
                AssertSelected(column[i], $"{label}: Down from {column[i - 1]}");
            }
        }

        // From Continue to tab `tab`, press it, and back down to Continue.
        private IEnumerator OpenTab(int tab, int columnHeight)
        {
            // Up from the column lands on the tab that is open.
            for (int i = 0; i < columnHeight - 1; i++) yield return Up();
            StringAssert.StartsWith("ReckoningTab", Selected, "precondition: on the tab strip");
            int open = int.Parse(Selected.Substring("ReckoningTab".Length));
            for (int i = open; i < tab; i++) yield return Right();
            AssertSelected("ReckoningTab" + tab, "precondition: on the tab to open");
            yield return Submit();
            for (int i = 0; i < 4 && Selected != "ReckoningContinueButton"; i++) yield return Down();
            AssertSelected("ReckoningContinueButton", "back down to Continue");
        }

        [UnityTest]
        public IEnumerator EveryTab_TwoChips_ASecondRelicPage_EveryElementIsReachedFromContinueAndBack()
        {
            yield return ShowTheReckoning(held: 6, chips: 2);

            yield return WalkTheColumn(Column(0, pager: false, chips: 2), "spoils tab");

            yield return OpenTab(1, Column(0, false, 2).Count);
            yield return WalkTheColumn(Column(1, pager: true, chips: 2), "relics tab, two pages");

            yield return OpenTab(2, Column(1, true, 2).Count);
            yield return WalkTheColumn(Column(2, pager: false, chips: 2), "tally tab");
        }

        [UnityTest]
        public IEnumerator EveryTab_NoChips_ASecondRelicPage_TheColumnClosesUpOverTheMissingRail()
        {
            yield return ShowTheReckoning(held: 6, chips: 0);

            yield return WalkTheColumn(Column(0, pager: false, chips: 0), "spoils tab");

            yield return OpenTab(1, Column(0, false, 0).Count);
            yield return WalkTheColumn(Column(1, pager: true, chips: 0), "relics tab, two pages");

            yield return OpenTab(2, Column(1, true, 0).Count);
            yield return WalkTheColumn(Column(2, pager: false, chips: 0), "tally tab");
        }

        [UnityTest]
        public IEnumerator TheRelicsTab_OnOnePage_HasNoPagerBetweenTheTabsAndTheChips()
        {
            yield return ShowTheReckoning(held: 3, chips: 2);

            yield return OpenTab(1, Column(0, false, 2).Count);
            yield return WalkTheColumn(Column(1, pager: false, chips: 2), "relics tab, one page");
        }

        // The outcome form paid nothing, so it has no Spoils page: the tab is
        // hidden, the screen opens on Tally, and the pad cycles only the two
        // tabs that are shown.
        [UnityTest]
        public IEnumerator TheOutcomeForm_HidesSpoils_OpensOnTally_AndThePadCyclesOnlyTheShownTabs()
        {
            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();
            RunManager.StartRun(41UL);

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn") };
            _reckoning.Show(ReckoningRequest.Outcome(FightEnding.Fell, new List<string>(), new List<string>(), tally,
                new Domain.Combat.CombatLedger()));
            yield return null;

            Assert.IsFalse(Node("ReckoningTab0").activeSelf, "no Spoils tab on the outcome form");
            Assert.IsTrue(Node("ReckoningTallyPage").activeSelf, "it opens on Tally");
            Assert.IsFalse(Node("ReckoningSpoilsPage").activeSelf);
            Assert.AreEqual(-125f, ((RectTransform)Node("ReckoningTab1").transform).anchoredPosition.x, 0.01f,
                "the two shown tabs close up around the centre");
            Assert.AreEqual(125f, ((RectTransform)Node("ReckoningTab2").transform).anchoredPosition.x, 0.01f);

            AssertSelected("ReckoningContinueButton", "entry is Continue");
            yield return Up();
            AssertSelected("ReckoningTab2", "Up from Continue is the open tab, Tally");
            yield return Right();
            AssertSelected("ReckoningTab1", "the rail wraps over the two shown tabs only");
            yield return Right();
            AssertSelected("ReckoningTab2", "and back, never onto the hidden Spoils tab");
            yield return Left();
            AssertSelected("ReckoningTab1", "Left also skips it");

            // A full form after it gets the whole strip back where the tree put it.
            _reckoning.gameObject.SetActive(false);
            _reckoning.Show(ReckoningRequest.Full(Reward(), new List<ItemOffer>(), new List<string>()));
            yield return null;
            Assert.IsTrue(Node("ReckoningTab0").activeSelf);
            Assert.AreEqual(-250f, ((RectTransform)Node("ReckoningTab0").transform).anchoredPosition.x, 0.01f);
            Assert.AreEqual(0f, ((RectTransform)Node("ReckoningTab1").transform).anchoredPosition.x, 0.01f);
            Assert.IsTrue(Node("ReckoningSpoilsPage").activeSelf, "the full form opens on Spoils");
        }

        [UnityTest]
        public IEnumerator ChipFocus_ShowsItsTooltip_WhichFollowsLeftRight_AndSubmitReturnsToContinue()
        {
            yield return ShowTheReckoning(held: 3, chips: 2);
            var receipts = RelicIds(3).Skip(1).ToList();
            var names = receipts.Select(id => ContentDatabase.GetRelic(id).Data.DisplayName).ToList();

            Assert.IsFalse(Node("ReckoningGainedTooltip").activeSelf, "nothing focused on a chip yet");

            yield return Up();
            AssertSelected("ReckoningGainedChip0", "Up from Continue is the chip rail");
            Assert.IsTrue(Node("ReckoningGainedTooltip").activeSelf, "pad focus on a chip shows its tooltip");
            Assert.AreEqual(names[0], TooltipName);

            yield return Right();
            AssertSelected("ReckoningGainedChip1", "Right steps along the chips");
            Assert.AreEqual(names[1], TooltipName, "the tooltip follows the focus");

            yield return Right();
            AssertSelected("ReckoningGainedChip1", "the rail clamps at its end");

            yield return Left();
            yield return Left();
            AssertSelected("ReckoningGainedChip0", "and clamps at its start");

            yield return Submit();
            AssertSelected("ReckoningContinueButton", "Submit on a chip returns to the primary action");
            Assert.IsFalse(Node("ReckoningGainedTooltip").activeSelf, "leaving the chip hides its tooltip");
        }
    }
}
