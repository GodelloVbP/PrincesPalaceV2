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
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning while its animations are still running.
    //
    // FIVE COROUTINES, FOUR STARTS, TWO STOPS -- the widest gap between starts
    // and stops anywhere in Core. Only `_animation` is held (PlayIn and
    // SweepToSummary share it, and each stops the other); FillBar, Sweep and
    // Flash are started loose and nothing tracks them. That is fine as long as
    // the one decision this screen has cannot be made twice, which is what the
    // `_taken` guard is for -- and nothing pinned it.
    //
    // The screen is also the last thing between a fight and the run's ledger,
    // so "half-applied" here is not a cosmetic state: it is a reward the player
    // watched land and the save does not have.
    //
    // scenarios A13, A14, C8, D3 (docs/hunt/SCENARIOS.md).
    public class ReckoningLifecycleTests
    {
        private string _root;
        private ReckoningController _reckoning;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reck-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };

            // The whole screen is unscaled -- see ReckoningController's own
            // SpeedMultiplier comment -- so this collapses the wipe, sweep and
            // bar-fill waits to well under a frame.
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- A13: Continue pressed while the sweep is still travelling ----------------

        // CONTINUE IS REACHABLE MID-SWEEP, which is the part that is not
        // obvious: SweepToSummary activates the summary phase on its FIRST
        // line, before it has moved anything, so the button rides in with the
        // panel and is pressable for the whole of the travel.
        //
        // TheSummaryLandsAtRestRatherThanPartWayThroughTheSweep asserts the end
        // of an UNinterrupted sweep. This asserts the same landing with the way
        // out pressed while it was still moving -- the summary has to arrive in
        // one place, not part-way through a second sweep, and nothing may be
        // left smeared by a motion blur that never reached its own end.
        [UnityTest]
        public IEnumerator ContinuePressedMidSweepStillLandsTheSummaryAtRest()
        {
            yield return OpenIt();

            int dismissals = 0;
            _reckoning.Dismissed = () => dismissals++;

            Click("ReckoningOffer0");
            yield return null;

            var summary = Named("ReckoningSummaryPhase").GetComponent<RectTransform>();
            Assert.IsTrue(summary.gameObject.activeSelf,
                "fixture: the summary is not on its way in, so Continue is not reachable yet");

            Click("ReckoningContinueButton");
            yield return null;

            yield return WaitForTheSweepToLand();

            Assert.AreEqual(0f, summary.anchoredPosition.x, 1f,
                "pressing Continue mid-sweep left the summary part-way through its travel");
            Assert.AreEqual(1f, summary.localScale.x, 0.001f,
                "the summary is still horizontally stretched by a motion blur the sweep never cleared");

            var group = summary.GetComponent<CanvasGroup>();
            if (group != null)
            {
                Assert.AreEqual(1f, group.alpha, 0.001f, "the summary is still dimmed by the sweep's blur");
            }

            Assert.AreEqual(1, dismissals, "Continue fired more than once, or not at all");
        }

        // ---- A14: a second offer chosen while the bars are filling ----------------------

        // `if (_taken ...) return` IS THE WHOLE GUARD, and everything else on
        // this phase is presentation: the offers stay visible after one is
        // chosen (hiding them would erase the decision the player just made)
        // and merely go non-interactable. A test that only checked the
        // interactable flag would be checking the presentation.
        [UnityTest]
        public IEnumerator ASecondOfferPressedWhileTheBarsFillTakesNothingAndLeavesTheCounterRight()
        {
            yield return OpenIt();

            Click("ReckoningOffer0");
            yield return null;

            Assert.IsTrue(_reckoning.HasTakenAnItem, "fixture: the first offer was not taken");

            string afterFirst = WhatTheSaveHolds();
            int itemsAfterFirst = ItemsHeld();

            // Straight into the bars, and press another card.
            Click("ReckoningOffer1");
            Click("ReckoningOffer2");
            yield return null;

            Assert.AreEqual(afterFirst, WhatTheSaveHolds(),
                "a second offer pressed while the summary was still animating changed what the save " +
                "holds, so the choice the player already made was replaced or added to");
            Assert.AreEqual(itemsAfterFirst, ItemsHeld(),
                "a second offer was actually taken, so this screen paid out twice for one fight");

            yield return WaitForTheSweepToLand();

            var gain = Named("ReckoningRow0Gain").GetComponent<TMP_Text>();
            yield return WaitForTheGainToLand(gain, "79");

            StringAssert.Contains("79", gain.text,
                "the gain counter did not end on the real number after the extra presses");
        }

        // ---- C8: a scene arriving over the sweep -----------------------------------------

        // THE REWARD IS EITHER FULLY APPLIED OR NOT APPLIED, NEVER HALF. Taking
        // an offer writes through RunOrchestrator.TakeOffer before any of the
        // animation starts, so a scene load landing mid-sweep must find the
        // save already whole -- the sweep is presentation over a decision that
        // has already been committed.
        [UnityTest]
        public IEnumerator ASceneArrivingOverTheSweepLeavesTheRewardWhole()
        {
            yield return OpenIt();

            int before = ItemsHeld();

            Click("ReckoningOffer0");
            yield return null;

            Assert.IsTrue(_reckoning.HasTakenAnItem, "fixture: nothing was taken");
            int after = ItemsHeld();
            Assert.AreEqual(before + 1, after, "fixture: taking an offer did not reach the save");

            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.AreEqual(after, ItemsHeld(),
                "the reward changed when the scene went out from under the sweep, so something was " +
                "still being applied while the screen was being destroyed");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- D3: a save taken during the sweep --------------------------------------------

        [UnityTest]
        public IEnumerator ASaveTakenDuringTheSweepHasTheRewardExactlyOnce()
        {
            yield return OpenIt();

            int before = ItemsHeld();

            Click("ReckoningOffer0");
            yield return null;

            int inMemory = ItemsHeld();
            Assert.AreEqual(before + 1, inMemory, "fixture: taking an offer did not reach the save");

            // Mid-sweep, off the cache and back off disk.
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            Assert.AreEqual(inMemory, ItemsHeld(),
                "the reward taken on the Reckoning did not survive a save taken while the summary " +
                "was still sweeping in - it is either missing or counted twice");
        }

        // ---- fixture ----------------------------------------------------------------------

        // Frames rather than seconds, for the reason ReckoningPhaseTests
        // records at length: a wall-clock deadline pays for a slow frame out of
        // the same budget the coroutine needs to finish in, and one GC pause
        // right after Show can burn the whole window before it gets its second
        // tick.
        private const float MinTestFps = 30f;
        private const int FrameMargin = 90;

        private static int MaxFrames(float animationSeconds) =>
            Mathf.CeilToInt(animationSeconds / ReckoningController.SpeedMultiplier * MinTestFps) + FrameMargin;

        private IEnumerator WaitForTheSweepToLand()
        {
            int frames = 0, maxFrames = MaxFrames(0.18f);
            while (Named("ReckoningOfferPhase").activeSelf && frames < maxFrames)
            {
                frames++;
                yield return null;
            }
        }

        private IEnumerator WaitForTheGainToLand(TMP_Text gain, string expected)
        {
            int frames = 0, maxFrames = MaxFrames(0.55f);
            while (!gain.text.Contains(expected) && frames < maxFrames)
            {
                frames++;
                yield return null;
            }
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private static List<ItemOffer> Offers()
        {
            var ids = Content.ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable)
                .Take(3)
                .Select(i => i.id)
                .ToList();

            return ids.Select(id => new ItemOffer(id, 1, 0)).ToList();
        }

        private IEnumerator OpenIt()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            _reckoning.Show(Reward(), Offers());
            yield return null;
        }

        // EVERYTHING THE SAVE HOLDS, bag and worn slots together. TakeOffer
        // puts the item in the bag and then auto-equips it into an empty slot
        // if the squad has one -- so counting only the bag would report a taken
        // item as missing, or as vanishing, depending on what the roster
        // happened to be wearing.
        private static int ItemsHeld()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            int held = save.stockpiledItems.Sum(entry => entry.count);

            foreach (var character in save.roster)
            {
                foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (!character.equipment.IsEmpty(slot)) held++;
                }
            }

            return held;
        }

        // The same holdings as a comparable string, so "did anything change"
        // is one assertion rather than a count that two opposite changes could
        // cancel out of.
        private static string WhatTheSaveHolds()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return "";

            var parts = save.stockpiledItems.Select(e => $"{e.itemId}x{e.count}").ToList();

            foreach (var character in save.roster)
            {
                foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (!character.equipment.IsEmpty(slot))
                    {
                        parts.Add($"{character.definitionId}:{slot}:{character.equipment.Get(slot)}");
                    }
                }
            }

            parts.Sort(System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"the Reckoning has no '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }
    }
}
