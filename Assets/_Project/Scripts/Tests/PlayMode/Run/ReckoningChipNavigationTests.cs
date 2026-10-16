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
    // THE BOARD'S COLUMN (docs/PLAN_REWARD_SCREENS.md C8): Continue, the
    // haul strip (this fight's chips, then the "Gained" receipts) as one
    // clamped rail, the portraits above it -- every row reachable from
    // Continue and back, with and without a haul and receipts; chip focus
    // opens the tooltip, which follows Left/Right; Submit on a chip returns to
    // Continue; portrait focus opens the board's detail. ReckoningBoardNavTests
    // pins the graph itself; this drives it through the production
    // NavigationInputModule with a scripted input source, on the shared Fight
    // scene.
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

        private static CombatReward Reward(bool potion = false)
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            reward.Ledger.Dealt("shawn", Domain.Stats.DamageType.Physical, 120);
            reward.Ledger.Took("shawn", 40, shielded: 17);
            if (potion)
                reward.Prizes.Add(new PaidPrize(Domain.Dungeon.PrizeKind.Consumable, Domain.Dungeon.PrizeKind.Consumable,
                    1, new[] { "test_potion" }));
            return reward;
        }

        private static List<string> RelicIds(int count)
        {
            var ids = ContentDatabase.Relics.Where(r => r != null).Select(r => r.id).Take(count).ToList();
            Assert.AreEqual(count, ids.Count, $"fixture: content has fewer than {count} relics");
            return ids;
        }

        // A Reckoning with `chips` receipts, and a potion in the haul when asked.
        private IEnumerator ShowTheReckoning(int chips, bool potion = false)
        {
            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();

            RunManager.StartRun(41UL);

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            var receipts = RelicIds(chips + 1).Skip(1).ToList();
            _reckoning.Show(ReckoningRequest.Full(Reward(potion), new List<PendingPage>(), receipts));
            yield return null;
            AssertSelected("ReckoningContinueButton", "the board opens on Continue");
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

        // The column, top to bottom, as ReckoningBoardNav declares it.
        private static List<string> Column(bool haul, int chips)
        {
            var column = new List<string> { "ReckoningPortrait0" };
            if (haul) column.Add("ReckoningHaulChip0");
            else if (chips > 0) column.Add("ReckoningGainedChip0");
            column.Add("ReckoningContinueButton");
            return column;
        }

        // Up from Continue to the portraits, asserting each stop, then Down back.
        private IEnumerator WalkTheColumn(List<string> column, string label)
        {
            for (int i = column.Count - 2; i >= 0; i--)
            {
                yield return Up();
                AssertSelected(column[i], $"{label}: Up from {column[i + 1]}");
            }

            yield return Up();
            AssertSelected(column[0], $"{label}: the portraits are the top");

            for (int i = 1; i < column.Count; i++)
            {
                yield return Down();
                AssertSelected(column[i], $"{label}: Down from {column[i - 1]}");
            }
        }

        [UnityTest]
        public IEnumerator TwoChips_EveryRowIsReachedFromContinueAndBack()
        {
            yield return ShowTheReckoning(chips: 2);
            yield return WalkTheColumn(Column(haul: false, chips: 2), "receipts only");
        }

        [UnityTest]
        public IEnumerator NoChips_TheColumnClosesUpOverTheMissingRail()
        {
            yield return ShowTheReckoning(chips: 0);
            yield return WalkTheColumn(Column(haul: false, chips: 0), "no haul");
        }

        [UnityTest]
        public IEnumerator AHaulAndReceipts_AreOneClampedRail_HaulFirst()
        {
            yield return ShowTheReckoning(chips: 1, potion: true);
            yield return WalkTheColumn(Column(haul: true, chips: 1), "haul and receipts");

            yield return Up();
            AssertSelected("ReckoningHaulChip0", "back on the haul");
            yield return Right();
            AssertSelected("ReckoningGainedChip0", "the receipts follow the haul on one rail");
            yield return Right();
            AssertSelected("ReckoningGainedChip0", "the rail clamps at its end");
            yield return Left();
            yield return Left();
            AssertSelected("ReckoningHaulChip0", "and at its start");
        }

        // The outcome form paid nothing: portraits and deeds only.
        [UnityTest]
        public IEnumerator TheOutcomeForm_IsPortraitsAndDeedsOnly()
        {
            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();
            RunManager.StartRun(41UL);

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            var tally = new List<TallyMember> { new TallyMember("sheep", "Shawn"), new TallyMember("owl", "Odette") };
            _reckoning.Show(ReckoningRequest.Outcome(FightEnding.Fell, new List<string>(), new List<string>(), tally,
                new Domain.Combat.CombatLedger()));
            yield return null;

            Assert.IsTrue(Node("ReckoningPortrait0").activeSelf);
            Assert.IsTrue(Node("ReckoningPortrait1").activeSelf);
            Assert.IsTrue(Node("ReckoningDeeds1").activeSelf);
            Assert.IsFalse(Node("ReckoningPortrait0RingGain").activeSelf, "no ring: nothing was paid");
            Assert.IsFalse(Node("ReckoningPortrait0Level").activeSelf, "no level badge");
            Assert.IsFalse(Node("ReckoningGoldStack").activeSelf, "no gold");
            Assert.IsFalse(Node("ReckoningHaul").activeSelf, "no haul");

            AssertSelected("ReckoningContinueButton", "entry is Continue");
            yield return Up();
            AssertSelected("ReckoningPortrait0", "Up from Continue is the portraits");
            yield return Right();
            AssertSelected("ReckoningPortrait1", "the portraits are a rail");
            yield return Right();
            AssertSelected("ReckoningPortrait0", "that wraps");
            yield return Down();
            AssertSelected("ReckoningContinueButton", "Down returns");
        }

        [UnityTest]
        public IEnumerator PortraitFocus_ShowsTheirDeedsSplitAndShielded()
        {
            yield return ShowTheReckoning(chips: 0);
            Assert.IsFalse(Node("ReckoningBoardTip").activeSelf, "nothing focused on a portrait yet");

            yield return Up();
            AssertSelected("ReckoningPortrait0", "precondition");
            Assert.IsTrue(Node("ReckoningBoardTip").activeSelf, "pad focus on a portrait shows its detail");
            Assert.AreEqual("Shawn", Node("ReckoningBoardTipTitle").GetComponent<TMP_Text>().text);
            string body = Node("ReckoningBoardTipBody").GetComponent<TMP_Text>().text;
            StringAssert.Contains("120 dealt  (120 physical / 0 other)", body);
            StringAssert.Contains("40 taken  (17 shielded)", body);

            yield return Down();
            Assert.IsFalse(Node("ReckoningBoardTip").activeSelf, "leaving the portrait hides it");
        }

        [UnityTest]
        public IEnumerator ChipFocus_ShowsItsTooltip_WhichFollowsLeftRight_AndSubmitReturnsToContinue()
        {
            yield return ShowTheReckoning(chips: 2);
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
