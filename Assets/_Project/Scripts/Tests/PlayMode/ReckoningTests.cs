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
using PrincesPalace;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning, driven through the real fight scene.
    //
    // Fed a FIXTURE reward rather than a fought fight. What is under test is
    // the screen -- what it shows, what taking an item does, where dismissing
    // it goes -- and driving a whole encounter to reach it would make every
    // assertion here hostage to combat balance.
    public class ReckoningTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private string _wentTo;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reckoning-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            _wentTo = null;
            Navigation.LoadOverride = scene => _wentTo = scene;
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static CombatReward Reward(int gold = 40, params CharacterReward[] rows)
        {
            var reward = new CombatReward { GoldGained = gold };
            reward.Characters.AddRange(rows);
            return reward;
        }

        private static CharacterReward Row(string id, string name, int gained = 30,
            int levelBefore = 2, int levelAfter = 2, bool downed = false)
        {
            return new CharacterReward(id, name, levelBefore, 20, levelAfter, 50, 100, gained, 0, downed);
        }

        private IEnumerator OpenTheFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
        }

        private IEnumerator ShowIt(CombatReward reward, List<ItemOffer> offers = null)
        {
            yield return OpenTheFight();

            _reckoning.Show(reward, offers ?? new List<ItemOffer>());

            // Start() runs one frame AFTER SetActive, so the listeners do not
            // exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
        }

        // ---- what it shows ---------------------------------------------------------

        [UnityTest]
        public IEnumerator ItStartsHiddenAndShowOpensIt()
        {
            yield return OpenTheFight();
            Assert.IsFalse(_reckoning.gameObject.activeSelf, "the fight opens with the Reckoning down");

            _reckoning.Show(Reward(), new List<ItemOffer>());
            yield return null;

            Assert.IsTrue(_reckoning.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheGoldItShowsIsTheGoldTheFightPaid()
        {
            yield return ShowIt(Reward(gold: 137, rows: Row("shawn", "Shawn")));

            StringAssert.Contains("137", Named("ReckoningGoldLabel").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator OneRowPerFieldedCharacterAndTheRestStayHidden()
        {
            yield return ShowIt(Reward(rows: new[] { Row("shawn", "Shawn"), Row("other", "Other") }));

            Assert.IsTrue(Named("ReckoningRow0").activeSelf);
            Assert.IsTrue(Named("ReckoningRow1").activeSelf);

            for (int i = 2; i < ReckoningScreen.RowCount; i++)
            {
                Assert.IsFalse(Named($"ReckoningRow{i}").activeSelf,
                    $"row {i} is drawn empty rather than hidden");
            }
        }

        [UnityTest]
        public IEnumerator ADownedMemberGetsARowSayingSoRatherThanVanishing()
        {
            // Three went in and two came back with nothing explaining the
            // third is the confusion CharacterReward.IsDowned exists to stop.
            yield return ShowIt(Reward(rows: new[]
            {
                Row("shawn", "Shawn"),
                Row("ghost", "Ghost", gained: 0, downed: true),
            }));

            Assert.IsTrue(Named("ReckoningRow1").activeSelf, "the downed member still gets a row");
            StringAssert.Contains("DID NOT FIGHT", Named("ReckoningRow1Level").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator ALevelUpIsCalledOutOnTheRowThatEarnedIt()
        {
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn", levelBefore: 2, levelAfter: 3)));

            StringAssert.Contains("UP", Named("ReckoningRow0Level").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator TheRowCountCoversTheLargestPartyTheSaveCanField()
        {
            // The tree is fixed at build time. A squad bigger than RowCount
            // would silently lose its last members from the results.
            yield return OpenTheFight();

            Assert.GreaterOrEqual(ReckoningScreen.RowCount, Save.EffectiveMaxSquadSize());
        }

        // ---- the one decision --------------------------------------------------------

        private static List<ItemOffer> ThreeOffers()
        {
            var candidates = ItemOfferRoll.Candidates();
            Assert.IsNotEmpty(candidates, "fixture: content has equippable items");

            return candidates.Take(3).Select(c => c.WithPlus(2)).ToList();
        }

        [UnityTest]
        public IEnumerator TakingAnOfferPutsItInTheBagAtItsPlusAndSavesIt()
        {
            var offers = ThreeOffers();
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            int before = Save.stockpiledItems.Sum(e => e.count);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count));
            Assert.IsTrue(Save.stockpiledItems.Any(e => e.itemId == offers[0].ItemId && e.plus == 2),
                "the plus the offer was rolled at did not survive into the bag");

            SaveSlotManager.Forget();
            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count),
                "the pick never reached the file");
        }

        [UnityTest]
        public IEnumerator OnlyOneOfferCanBeTaken()
        {
            // It is a CHOICE. Two presses handing over two items would make the
            // decision decorative.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), ThreeOffers());

            int before = Save.stockpiledItems.Sum(e => e.count);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Named("ReckoningOffer1").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count));
            Assert.IsTrue(_reckoning.HasTakenAnItem);
        }

        [UnityTest]
        public IEnumerator TheOffersNotTakenStayOnScreenButStopResponding()
        {
            // Hiding them would erase the decision the player just made from
            // the screen that asked for it.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), ThreeOffers());

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(Named("ReckoningOffer1").activeSelf, "the offer not taken vanished");
            Assert.IsFalse(Named("ReckoningOffer1").GetComponent<Button>().interactable);
        }

        [UnityTest]
        public IEnumerator AFightWithNoOffersSkipsStraightToTheSummary()
        {
            // Reachable: ItemOfferTable returns fewer than three when the
            // candidate pool is genuinely smaller, and zero if content has no
            // equippables at all.
            //
            // Now that the choice is its OWN phase and there is no skip on it,
            // an empty offer list would be a phase with no way out at all --
            // so it is bypassed rather than shown empty. Stronger than the old
            // assertion, which only checked that a heading was hidden.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), new List<ItemOffer>());

            Assert.IsFalse(Named("ReckoningOfferPhase").activeSelf,
                "an offer phase with nothing on it has no way out");
            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf,
                "the screen has to land somewhere");
        }

        [UnityTest]
        public IEnumerator AnOfferNamingContentThatIsGoneDegradesRatherThanThrowing()
        {
            var offers = new List<ItemOffer> { new ItemOffer("no_such_item_at_all", 3) };

            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), offers);

            Assert.IsTrue(Named("ReckoningOffer0").activeSelf);
            Assert.IsNotEmpty(Named("ReckoningOffer0Name").GetComponent<TMP_Text>().text);
        }

            [UnityTest]
        public IEnumerator TheTwoBarSegmentsSitBesideEachOtherRatherThanOnTopOfEachOther()
        {
            // Both anchored from zero would put the bright segment ON the dim
            // one -- it is declared last, so it paints over -- and the "before"
            // half would never be visible. That is the entire reason
            // CharacterReward carries a before as well as an after, and a
            // composite of the layout is what caught it; every test passed.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            var before = Named("ReckoningRow0BarBefore").GetComponent<RectTransform>();
            var fill = Named("ReckoningRow0BarFill").GetComponent<RectTransform>();

            // Let the fill animation finish so the assertion is on the settled
            // state rather than on a frame partway through it.
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;

            Assert.AreEqual(0f, before.anchorMin.x, 0.001f, "the dim segment starts at the left edge");
            Assert.AreEqual(before.anchorMax.x, fill.anchorMin.x, 0.001f,
                "the earned segment must start exactly where the already-had segment ends");
            Assert.Greater(fill.anchorMax.x, fill.anchorMin.x,
                "a fight that granted experience drew a zero-width earned segment");
        }

        [UnityTest]
        public IEnumerator ALevelUpGivesTheWholeBarToWhatWasJustEarned()
        {
            // BarFillBefore01 returns 0 after a level-up, and that is not a
            // fudge: the level reset the bar, so every point now showing
            // genuinely was earned in this fight.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn", levelBefore: 2, levelAfter: 3)));

            var before = Named("ReckoningRow0BarBefore").GetComponent<RectTransform>();
            var fill = Named("ReckoningRow0BarFill").GetComponent<RectTransform>();

            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;

            Assert.AreEqual(0f, before.anchorMax.x, 0.001f, "nothing was carried over the level boundary");
            Assert.AreEqual(0f, fill.anchorMin.x, 0.001f);
        }

        // ---- leaving ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DismissingItLeavesTheFight()
        {
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            bool dismissed = false;
            _reckoning.Dismissed = () => dismissed = true;

            Named("ReckoningContinueButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(dismissed, "the continue button raised nothing");
        }

        [UnityTest]
        public IEnumerator TheExpandSettlesTheFrameAtFullSize()
        {
            // The animation is presentation only -- every number is already
            // correct before it starts -- but a frame left mid-expand would be
            // a permanently undersized panel.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")));

            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            var frame = Named("ReckoningFrame").GetComponent<RectTransform>();

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) yield return null;

            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelWidth,
                wipe.rect.width, 0.5f, "the panel is permanently narrower than it should be");
            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelHeight,
                wipe.rect.height, 0.5f);

            // The frame is revealed, never resized -- so full size here is not
            // a thing the animation had to arrive at, it is a thing nothing was
            // ever allowed to change.
            Assert.AreEqual(PrincesPalace.Domain.UiKit.Screens.ReckoningScreen.PanelWidth,
                frame.rect.width, 0.5f);
            Assert.AreEqual(Vector3.one, frame.localScale);
        }

        // ---- the reroll, level 40 of the reward track ------------------------------

        private void LevelTheSquadTo(int level)
        {
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
            }
        }

        [UnityTest]
        public IEnumerator TheRerollIsHiddenUntilTheTrackGrantsIt()
        {
            // HIDDEN rather than greyed out. A disabled button for a reward the
            // player has never heard of reads as something broken.
            RunManager.StartRun(1234);
            yield return ShowIt(Reward(), ThreeOffers());

            Assert.IsFalse(Named("ReckoningRerollButton").activeSelf,
                "a level-1 squad is being shown a reroll it has not earned");
        }

        [UnityTest]
        public IEnumerator ALevelFortySquadCanRerollTheOffer()
        {
            RunManager.StartRun(1234);
            yield return OpenTheFight();
            LevelTheSquadTo(40);

            // The source the fight would have supplied. Set here because this
            // test opens the Reckoning directly rather than by winning a fight.
            _reckoning.RerollSource = ThreeOffers;
            _reckoning.Show(Reward(), ThreeOffers());
            yield return null;
            yield return null;

            var button = Named("ReckoningRerollButton");
            Assert.IsTrue(button.activeSelf, "a level-40 squad is not shown the reroll it earned");
            Assert.AreEqual(0, RunManager.Run.offerRerollsUsed);

            button.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, RunManager.Run.offerRerollsUsed,
                "pressing reroll did not spend one");
        }

        [UnityTest]
        public IEnumerator TheRerollRunsOutAndStopsBeingPressable()
        {
            RunManager.StartRun(1234);
            yield return OpenTheFight();
            LevelTheSquadTo(40);

            _reckoning.RerollSource = ThreeOffers;
            _reckoning.Show(Reward(), ThreeOffers());
            yield return null;
            yield return null;

            var button = Named("ReckoningRerollButton").GetComponent<Button>();

            // One allowance at level 40, so the second press must do nothing.
            button.onClick.Invoke();
            yield return null;
            button.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, RunManager.Run.offerRerollsUsed,
                "the reroll was spent past its allowance");
            Assert.IsFalse(button.interactable, "an exhausted reroll is still pressable");
        }

        // The allowance is spent per RUN, not per fight -- so a second
        // Reckoning in the same descent starts from what is left, not from a
        // fresh count. That is the whole reason offerRerollsUsed lives on
        // RunSnapshot.
        [UnityTest]
        public IEnumerator RerollsDoNotComeBackForTheNextFight()
        {
            RunManager.StartRun(1234);
            yield return OpenTheFight();
            LevelTheSquadTo(40);

            _reckoning.RerollSource = ThreeOffers;
            _reckoning.Show(Reward(), ThreeOffers());
            yield return null;
            yield return null;

            Named("ReckoningRerollButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            // A second fight in the same descent.
            _reckoning.Show(Reward(), ThreeOffers());
            yield return null;

            Assert.IsFalse(Named("ReckoningRerollButton").GetComponent<Button>().interactable,
                "the next fight handed back a reroll the run had already spent");
        }
    }
}
