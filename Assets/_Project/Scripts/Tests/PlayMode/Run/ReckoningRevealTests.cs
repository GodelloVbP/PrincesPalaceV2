using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE REVEAL ON THE REAL SCREEN (docs/PLAN_REWARD_SCREENS.md C3, C4, C10):
    // the controller plays RevealSchedule, a press during a beat skips and
    // never acts, the guard swallows the repeat, a face-down card has no
    // detail, the screen's own focus opens none, and the detail never covers
    // another card. The schedule's times are EditMode's (RevealScheduleTests,
    // ReckoningTimingTests); here SpeedMultiplier 0 freezes the clock so a beat
    // is certainly running, and 40 plays it out.
    //
    // THE FIGHT SCENE IS SHARED (SharedScene); see ReckoningGamepadNavigationTests
    // for what is put away between tests.
    public class ReckoningRevealTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reveal-" + System.Guid.NewGuid().ToString("N"));
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

        // `beforeShow` runs after any earlier screen is put away, so a sound
        // observer hears this reveal and nothing left over from another test.
        private IEnumerator Show(IReadOnlyList<PendingPage> pages, float speed, System.Action beforeShow = null)
        {
            yield return SharedScene.Ensure("Fight");

            _input = NavSceneReuse.TakeOverInput();
            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            ReckoningController.SpeedMultiplier = speed;
            beforeShow?.Invoke();
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

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static bool TipShown => Part<RectTransform>("ReckoningCardTip").gameObject.activeSelf;

        // ---- the one gate, on the real schedule ----------------------------------------

        // The clock is frozen, so the first press skips and every later one
        // lands inside the guard: six presses, by mouse and by Submit, on a
        // card, a whetstone and the door, and nothing is taken, honed or left.
        [UnityTest]
        public IEnumerator DuringABeatAndItsGuard_NoPressActs_ByMouseOrSubmit()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var page = pages[0];
            int goldBefore = RunManager.Run.gold;
            yield return Show(pages, 0f);
            Assert.IsFalse(_reckoning.AcceptsChoices, "a beat is running");

            Click("ReckoningOffer0");
            Assert.IsFalse(_reckoning.AcceptsChoices, "the skip lands the row behind the guard");
            Submit("ReckoningOffer0");
            Click("ReckoningHone0");
            Submit("ReckoningHone0");
            Click("ReckoningLeaveButton");
            Submit("ReckoningLeaveButton");
            yield return Frames(4);

            Assert.IsTrue(page.Cards.All(c => c.Outcome == CardOutcome.Open), "nothing was taken, honed or sold");
            Assert.IsFalse(page.Closed, "the page was not left");
            Assert.AreEqual(goldBefore, RunManager.Run.gold);
            Assert.AreEqual(0, page.UnusedHones);
        }

        [UnityTest]
        public IEnumerator OnceTheGuardHasPassed_APressActsAtOnce()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            yield return Show(pages, 40f);
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);

            Submit("ReckoningOffer0");
            yield return Frames(2);
            Assert.AreEqual(CardOutcome.Taken, pages[0].Cards[0].Outcome);
        }

        // ---- the controller plays the schedule ------------------------------------------

        // A boss page always gets the cache. At the clock's start the map chip
        // is up and the cards are not dealt; a click on the catcher lands the
        // end state: chip and cache gone, every card face-up and visible.
        [UnityTest]
        public IEnumerator ABossPage_StartsWithTheChip_AndASkipLandsEveryCardFaceUp()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            yield return Show(pages, 0f);
            int n = pages[0].Cards.Count;

            Assert.IsTrue(Part<Image>("ReckoningChipGear").gameObject.activeSelf, "the map's gear chip flies in");
            Assert.IsFalse(Part<Button>("ReckoningCache").gameObject.activeSelf, "the cache stands only after the wipe");
            Assert.IsTrue(Part<Button>("ReckoningCatcher").gameObject.activeSelf);
            for (int i = 0; i < n; i++)
                Assert.AreEqual(0f, Part<CanvasGroup>($"ReckoningOffer{i}").alpha, 0.001f, $"card {i} is not dealt yet");

            Click("ReckoningCatcher");
            yield return null;

            Assert.IsFalse(Part<Image>("ReckoningChipGear").gameObject.activeSelf);
            Assert.IsFalse(Part<Button>("ReckoningCache").gameObject.activeSelf, "the cache opened");
            Assert.IsFalse(Part<Button>("ReckoningCatcher").gameObject.activeSelf);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1f, Part<CanvasGroup>($"ReckoningOffer{i}").alpha, 0.001f, $"card {i} is dealt");
                Assert.IsFalse(Part<Image>($"ReckoningOffer{i}Back").gameObject.activeSelf, $"card {i} is face-up");
                Assert.AreEqual(1f, Part<RectTransform>($"ReckoningOffer{i}").localScale.x, 0.001f);
            }
        }

        // Played out: the cache opens once, each of the five cards flips once,
        // one ember lights per choice, and the row ends choosable.
        [UnityTest]
        public IEnumerator ABossPage_PlayedOut_SoundsEachBeatOnce()
        {
            var heard = new List<Sound>();
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            yield return Show(pages, 40f, () => SoundController.Observer = s => heard.Add(s));
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);

            Assert.AreEqual(1, heard.Count(s => s == Sound.CacheOpen));
            Assert.AreEqual(5, heard.Count(s => s == Sound.CardFlip));
            Assert.AreEqual(3, heard.Count(s => s == Sound.EmberLight), "the boss page's three choices");
            CollectionAssert.DoesNotContain(heard, Sound.ButtonClick);
            CollectionAssert.DoesNotContain(heard, Sound.ClickFail);
            Assert.AreEqual(3, Enumerable.Range(0, 3).Count(e => Part<Image>($"ReckoningEmber{e}").color.a > 0.5f));
        }

        // A page shown again in the same session opens face-up, silently.
        [UnityTest]
        public IEnumerator APageAlreadyRevealed_OpensFaceUpWithNoBeat()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            yield return Show(pages, 40f);
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);

            _reckoning.Show(Reward(), pages);
            yield return null;
            Assert.IsTrue(_reckoning.AcceptsChoices);
        }

        // ---- the detail ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheScreensOwnFocus_OpensNoDetail()
        {
            yield return Show(ReckoningPageFixtures.BossAndTwoAnchors(), 40f);
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
            yield return Frames(2);

            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject, "the first card is focused");
            Assert.IsFalse(TipShown, "no detail until the player moves or hovers");
        }

        // A focus moved onto a face-down card shows nothing; the detail comes
        // up when the row lands.
        [UnityTest]
        public IEnumerator AFaceDownCard_ShowsNoDetail_UntilTheRowLands()
        {
            yield return Show(ReckoningPageFixtures.BossAndTwoAnchors(), 0f);

            EventSystem.current.SetSelectedGameObject(Part<Button>("ReckoningOffer2").gameObject);
            yield return Frames(2);
            Assert.IsFalse(TipShown, "a face-down card has nothing to describe");

            Click("ReckoningCatcher");
            yield return null;
            Assert.IsTrue(TipShown, "the card the player moved to describes itself once face-up");
        }

        // On the five-card row, every card's detail clears every other card.
        [UnityTest]
        public IEnumerator TheDetail_NeverCoversAnotherCard()
        {
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            yield return Show(pages, 40f);
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
            int n = pages[0].Cards.Count;
            Assert.AreEqual(5, n, "fixture: the five-card row");

            // Off the screen's own (quiet) focus first, so every card below is
            // a move of the player's.
            EventSystem.current.SetSelectedGameObject(Part<Button>("ReckoningLeaveButton").gameObject);
            yield return null;

            var tip = Part<RectTransform>("ReckoningCardTip");
            for (int i = n - 1; i >= 0; i--)
            {
                EventSystem.current.SetSelectedGameObject(Part<Button>($"ReckoningOffer{i}").gameObject);
                yield return Frames(2);
                Assert.IsTrue(TipShown, $"card {i}'s detail");

                var box = WorldRect(tip);
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    Assert.IsFalse(box.Overlaps(WorldRect(Part<RectTransform>($"ReckoningOffer{j}"))),
                        $"card {i}'s detail covers card {j}");
                }
            }
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
