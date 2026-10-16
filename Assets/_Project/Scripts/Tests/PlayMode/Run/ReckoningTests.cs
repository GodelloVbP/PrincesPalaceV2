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
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
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
            RunManager.ResetForTests();

            _wentTo = null;
            Navigation.LoadOverride = scene => _wentTo = scene;

            // ReckoningController's whole animation is unscaled -- see its
            // own SpeedMultiplier comment -- so this collapses the wipe,
            // sweep and bar-fill waits below to well under a frame.
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            ReckoningController.SpeedMultiplier = 1f;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // A FIXED WAIT STILL, not a poll -- there is no exposed "this bar is
        // still animating" flag to poll on (FillBar's coroutine reference is
        // private, and a level-up's Sweep-Flash-Sweep sequence PLATEAUS
        // between passes, which rules out watching the fill rect settle: it
        // legitimately stops moving mid-sequence during the flash).
        //
        // What changed is the duration. [SetUp]'s SpeedMultiplier = 40 runs
        // the whole Sweep/Flash/Sweep sequence (worst case here: two sweeps
        // and a flash, well under 0.55s + 0.09s of *scaled* clock) in low
        // tens of milliseconds of real time -- 0.2s real is roughly 6x that,
        // which still catches a real stall (the bar never reaching its final
        // span) while being ~7x faster than the flat 1.5s this replaced.
        private IEnumerator WaitForTheBarSequence()
        {
            float deadline = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

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

        // THE FIGHT SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene). Show()
        // resets everything the screen paints (reward, pages, page, phase,
        // tab), so the reset is only what Show does not own: the screen is put
        // back down -- OnDisable stops its animation and removes its nav
        // context -- and the Dismissed handler the scene wired is put back
        // over the one DismissingItLeavesTheFight installs.
        //
        // AND THE GLOOM. PlayIn reads its fade-up target off the dimmer's
        // CURRENT alpha, so a Show that cuts a running PlayIn short (a second
        // Show, or the screen going down mid-fade) leaves a lower alpha that
        // every later PlayIn in the same scene then treats as the target. A
        // fresh scene carries the authored value; a reused one gets it put
        // back from what this fixture read off its fresh load.
        private ReckoningController _capturedFor;
        private System.Action _sceneDismissed;
        private float _sceneGloom;

        private IEnumerator OpenTheFight()
        {
            yield return SharedScene.Ensure("Fight");

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            if (_capturedFor != _reckoning)
            {
                _capturedFor = _reckoning;
                _sceneDismissed = _reckoning.Dismissed;
                _sceneGloom = Dimmer().color.a;
            }
            else
            {
                // A reuse. A fresh scene is left exactly as it loaded, so
                // ItStartsHiddenAndShowOpensIt reads the scene, not this.
                _reckoning.Dismissed = _sceneDismissed;
                _reckoning.gameObject.SetActive(false);
                var dimmer = Dimmer();
                var colour = dimmer.color;
                colour.a = _sceneGloom;
                dimmer.color = colour;
            }
        }

        private Image Dimmer()
        {
            var dimmer = Named("ReckoningPanelDimmer")?.GetComponent<Image>();
            Assert.IsNotNull(dimmer, "the Reckoning has no ReckoningPanelDimmer");
            return dimmer;
        }

        private IEnumerator ShowIt(CombatReward reward, IReadOnlyList<PendingPage> pages = null)
        {
            yield return OpenTheFight();

            _reckoning.Show(reward, pages ?? new List<PendingPage>());

            // Start() runs one frame AFTER SetActive, so the listeners do not
            // exist yet (.claude/rules/tests.md).
            yield return null;
            yield return null;
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
        }

        // ---- what it shows ---------------------------------------------------------

        [UnityTest]
        public IEnumerator ItStartsHiddenAndShowOpensIt()
        {
            SharedScene.MarkDirty("asserts the Reckoning is down in a freshly loaded fight, not after a reset hid it");
            yield return OpenTheFight();
            Assert.IsFalse(_reckoning.gameObject.activeSelf, "the fight opens with the Reckoning down");

            _reckoning.Show(Reward(), new List<PendingPage>());
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

        // ---- the choices ---------------------------------------------------------------

        private static Character Recipient(PendingCard card) =>
            Save.roster.First(c => c.definitionId == card.RecipientId);

        private static ItemInstance Worn(PendingCard card) => Recipient(card).equipment.GetInstance(card.Slot);

        [UnityTest]
        public IEnumerator TakingACardEquipsItOnItsRecipientAtItsTierAndSavesIt()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(CardOutcome.Taken, card.Outcome);
            Assert.AreEqual(card.Copy.ItemId, Worn(card).ItemId, "the card did not land on its recipient");
            Assert.AreEqual(card.Copy.Tier, Worn(card).Tier, "the tier the card was rolled at did not survive");

            SaveSlotManager.Forget();
            Assert.AreEqual(card.Copy.ItemId, Worn(card).ItemId, "the pick never reached the file");
        }

        [UnityTest]
        public IEnumerator ATakenCardKeepsItsAffixRollOnTheWornCopy()
        {
            var pages = ReckoningPageFixtures.Settle(
                new List<EncounterClass> { EncounterClass.Boss }, strength: 3);
            var card = pages[0].Cards.OrderByDescending(c => c.Copy.ModifierIds.Count).First();
            Assert.Greater(card.Copy.ModifierIds.Count, 0, "fixture: a strength-3 boss page rolls an affixed card");
            int index = pages[0].Cards.IndexOf(card);
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Named($"ReckoningOffer{index}").GetComponent<Button>().onClick.Invoke();
            yield return null;

            CollectionAssert.AreEquivalent(card.Copy.ModifierIds, Worn(card).ModifierIds,
                "the card's rolled affixes did not survive onto the worn copy");
        }

        [UnityTest]
        public IEnumerator ACopyReplacedByATakeIsListedOnThePageAndKeptInTheBag()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];

            // The slot is filled first, so the take displaces something.
            var incumbent = ItemInstance.Of(card.Copy.ItemId, 0);
            Recipient(card).equipment.Put(card.Slot, incumbent);
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNotNull(card.Displaced, "the page did not record the copy it displaced");
            Assert.IsTrue(Save.stockpiledItems.Any(e => e.itemId == incumbent.ItemId && e.tier == 0),
                "the displaced copy is not in the bag");
            Assert.AreEqual(RarityColors.NameOf(incumbent), RarityColors.NameOf(card.Displaced));
        }

        [UnityTest]
        public IEnumerator OnlyAsManyCardsAsTheChoicesGrantCanBeTaken()
        {
            // It is a CHOICE. Two presses handing over two items on a
            // one-choice page would make the decision decorative.
            var pages = ReckoningPageFixtures.OneGearPage();
            Assert.AreEqual(1, pages[0].Choices, "fixture: one chip is one choice");
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Named("ReckoningOffer1").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(CardOutcome.Taken, pages[0].Cards[0].Outcome);
            Assert.AreEqual(CardOutcome.Open, pages[0].Cards[1].Outcome, "the second press also took");
            Assert.IsTrue(_reckoning.HasTakenAnItem);
        }

        [UnityTest]
        public IEnumerator TheCardsNotTakenStayOnScreenButStopResponding()
        {
            // Hiding them would erase the decision the player just made from
            // the screen that asked for it.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), ReckoningPageFixtures.OneGearPage());

            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(Named("ReckoningOffer1").activeSelf, "the card not taken vanished");
            Assert.IsFalse(Named("ReckoningOffer1").GetComponent<Button>().interactable);
            Assert.IsTrue(Named("ReckoningOfferPhase").activeSelf, "the page left before the player moved on");
        }

        [UnityTest]
        public IEnumerator AOneChipGearPageShowsThreeCardsAndABossPageFive()
        {
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), ReckoningPageFixtures.OneGearPage());
            Assert.AreEqual(3, Enumerable.Range(0, 5).Count(i => Named($"ReckoningOffer{i}").activeSelf));

            var boss = ReckoningPageFixtures.BossAndTwoAnchors();
            Assert.AreEqual(5, boss[0].Cards.Count, "fixture: three gear chips are five cards");
            _reckoning.Show(Reward(rows: Row("shawn", "Shawn")), boss);
            yield return null;

            Assert.AreEqual(5, Enumerable.Range(0, 5).Count(i => Named($"ReckoningOffer{i}").activeSelf));
            Assert.AreEqual(3, boss[0].ChoicesLeft, "fixture: three gear chips are three choices");
        }

        [UnityTest]
        public IEnumerator HoneInsteadHonesTheCardsSlotForItsRecipientAndDiscardsTheCard()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Assert.IsTrue(Named("ReckoningHone0").activeSelf, "a fresh slot offers hone instead");
            Named("ReckoningHone0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(CardOutcome.Honed, card.Outcome);
            Assert.AreEqual(1, SlotHoning.PlusOf(RunManager.Run.slotHoning, card.RecipientId, card.Slot));
            Assert.IsTrue(Recipient(card).equipment.IsEmpty(card.Slot) || Worn(card).Tier != card.Copy.Tier
                || Worn(card).ItemId != card.Copy.ItemId, "the card was equipped as well as discarded");
        }

        [UnityTest]
        public IEnumerator LeavingAPagePaysItsUnusedChoicesAndThenCarriesOn()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var card = pages[0].Cards[0];
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            Named("ReckoningLeaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(pages[0].Closed);
            Assert.AreEqual(1, pages[0].UnusedHones, "the unused choice should have honed the first card's slot");
            Assert.AreEqual(1, SlotHoning.PlusOf(RunManager.Run.slotHoning, card.RecipientId, card.Slot));
            Assert.IsTrue(Named("ReckoningOfferPhase").activeSelf, "the page shows what leaving paid before moving on");
            Assert.AreEqual(UiStrings.ReckoningForward.Format(),
                Named("ReckoningLeaveButton").GetComponentInChildren<TMP_Text>().text,
                "a spent page's door is the way forward");

            Named("ReckoningLeaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf, "the last page spent should land on the summary");
        }

        [UnityTest]
        public IEnumerator ARelicCardJoinsTheRunAndHasNothingToHone()
        {
            var pages = ReckoningPageFixtures.Settle(
                new List<EncounterClass> { EncounterClass.Elite }, new List<EncounterClass> { EncounterClass.Elite });
            var relicPage = pages.First(p => p.Kind == RewardPageKind.Relic);
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), pages);

            // The gear page first: leave it, then carry on.
            Named("ReckoningLeaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Named("ReckoningLeaveButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
            Assert.AreSame(relicPage, _reckoning.CurrentPage);
            Assert.IsFalse(Named("ReckoningHone0").activeSelf, "a relic card has nothing to hone");

            var card = relicPage.Cards[0];
            Named("ReckoningOffer0").GetComponent<Button>().onClick.Invoke();
            yield return null;

            CollectionAssert.Contains(RunManager.Run.relicIds, card.RelicId);
        }

        [UnityTest]
        public IEnumerator AFightWithNoPagesSkipsStraightToTheSummary()
        {
            // Reachable: a fight that paid no page (an emptied pool).
            //
            // Now that the choice is its OWN phase, a page list with nothing on
            // it would be a phase with no way out at all -- so it is bypassed
            // rather than shown empty.
            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), new List<PendingPage>());

            Assert.IsFalse(Named("ReckoningOfferPhase").activeSelf,
                "an offer phase with nothing on it has no way out");
            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf,
                "the screen has to land somewhere");
        }

        [UnityTest]
        public IEnumerator ACardNamingContentThatIsGoneDegradesRatherThanThrowing()
        {
            ReckoningPageFixtures.OneGearPage();
            var page = new PendingPage
            {
                SourceId = "normal_gear", Kind = RewardPageKind.Gear, Choices = 1, Step = 1,
            };
            page.Cards.Add(new PendingCard
            {
                Copy = ItemInstance.Of("no_such_item_at_all", 3), RecipientId = "shawn", Slot = EquipmentSlot.Weapon1,
            });

            yield return ShowIt(Reward(rows: Row("shawn", "Shawn")), new List<PendingPage> { page });

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
            yield return WaitForTheBarSequence();

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

            yield return WaitForTheBarSequence();

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

            // BOUNDED ON THE WIPE'S OWN WIDTH, not a fixed real-time guess.
            // PlayIn is a single monotonic ease with no plateau, so unlike the
            // bar-fill sequence above it is safe to poll directly: it either
            // reaches full width or the 2s ceiling fires and the assertions
            // below fail on the truth (a permanently narrow panel) rather
            // than on a timing guess.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (wipe.rect.width < ReckoningScreen.PanelWidth - 0.5f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

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

    }
}
