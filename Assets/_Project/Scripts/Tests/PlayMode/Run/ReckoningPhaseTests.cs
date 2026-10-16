using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's two phases: choose, then read.
    //
    // The ordering reversed deliberately. It was summary-then-choice on the
    // argument that a screen should end on a decision -- but seen on a real
    // fight, three offers crammed beside an experience bar made both halves
    // worse, and the choice is what the player is actually waiting for. The
    // cost of the reversal is that the summary can be skipped past, which is
    // why it is now worth reading rather than sharing a panel.
    public class ReckoningPhaseTests
    {
        private string _root;
        private ReckoningController _reckoning;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-phase-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };

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

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        // Taking is one press; the page then waits to be moved on, and the
        // same button that leaves a page carries on once nothing is left. The
        // press waits for the reveal, which would otherwise only skip it.
        private IEnumerator TakeAndMoveOn()
        {
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
            Click("ReckoningOffer0");
            Click("ReckoningLeaveButton");
        }

        // Why frames, not seconds. A wall-clock deadline races a coroutine
        // that only gets to advance once per Update, paying for a slow
        // frame out of the SAME budget it needs to finish in: one GC pause
        // or JIT hiccup right after Show() can burn the whole window before
        // the coroutine gets its second tick, which reads as the animation
        // never having started.
        //
        // A frame count does not have this problem: a slow frame still only
        // costs ONE tick of the budget, however long it took in wall time,
        // so the bound below is generous in the currency that actually
        // matters (Updates), not in the one contention can eat into.
        //
        // MaxFrames turns an animation's own (unscaled, pre-multiplier)
        // duration into that budget. At [SetUp]'s SpeedMultiplier of 40x,
        // durations under a second finish inside one or two Updates even on
        // a merely-ordinary machine -- the division term is a courtesy, not
        // where the safety margin lives. FrameMargin is where it lives: 90
        // Updates of slack, enough to absorb several bad frames in a row
        // without turning into a silent hang if the animation genuinely
        // never starts (the loop still exits, and callers assert on the
        // state they polled for, same as before).
        private const float MinTestFps = 30f;
        private const int FrameMargin = 90;

        private static int MaxFrames(float animationSeconds) =>
            Mathf.CeilToInt(animationSeconds / ReckoningController.SpeedMultiplier * MinTestFps) + FrameMargin;

        // POLLED ON THE OFFER PHASE ITSELF. SweepToSummary sets it inactive
        // as its very last step -- after StartBars() has already been kicked
        // off, so this also guarantees the bars have started where that
        // matters below -- and it is a one-way flag rather than a value that
        // plateaus, so it is safe to poll directly. SweepSeconds is 0.18s.
        private IEnumerator WaitForTheSweepToLand()
        {
            int frames = 0, maxFrames = MaxFrames(0.18f);
            while (Named("ReckoningOfferPhase").activeSelf && frames < maxFrames)
            {
                frames++;
                yield return null;
            }
        }

        // POLLED ON THE BOARD'S OWN FLAG: the rings are a function of one
        // clock, so "settled" is a real end, not a pause between passes.
        private IEnumerator WaitForTheBoardToSettle()
        {
            int frames = 0, maxFrames = MaxFrames(1.2f);
            while (!_reckoning.BoardSettled && frames < maxFrames)
            {
                frames++;
                yield return null;
            }
        }

        // BOUNDED ON THE WIPE'S OWN WIDTH, same shape as ReckoningTests'
        // equivalent poll -- PlayIn is a single monotonic ease with no
        // plateau, so it is safe to poll directly: it either reaches full
        // width or the ceiling fires and callers see the truth (a still-
        // narrow panel, a glow that never faded up) instead of a timing
        // guess. PlayIn runs for max(WipeSeconds, GloomSeconds) = 0.34s.
        private IEnumerator WaitForThePlayInToLand()
        {
            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            int frames = 0, maxFrames = MaxFrames(0.34f);
            while (wipe.rect.width < ReckoningScreen.PanelWidth - 0.5f && frames < maxFrames)
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

        private static IReadOnlyList<PendingPage> Offers() => ReckoningPageFixtures.OneGearPage();

        // THE FIGHT SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene). Show()
        // re-seats both phases and restarts PlayIn, so the reset is only
        // putting the screen back down first: OnDisable stops the previous
        // test's animation and removes its nav context, and Show then opens
        // from inactive exactly as it does on a fresh scene. A no-op on a
        // fresh load, where the Reckoning starts down.
        //
        // AND THE GLOOM. PlayIn reads its fade-up target off the dimmer's
        // CURRENT alpha, so a Show that cuts a running PlayIn short (a second
        // Show, or the screen going down mid-fade) leaves a lower alpha that
        // every later PlayIn in the same scene then treats as the target. A
        // fresh scene carries the authored value; a reused one gets it put
        // back from what this fixture read off its fresh load.
        private ReckoningController _capturedFor;
        private float _sceneGloom;

        private IEnumerator OpenIt(IReadOnlyList<PendingPage> offers)
        {
            yield return SharedScene.Ensure("Fight");

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            _reckoning.gameObject.SetActive(false);

            var dimmer = Named("ReckoningPanelDimmer")?.GetComponent<Image>();
            Assert.IsNotNull(dimmer, "the Reckoning has no ReckoningPanelDimmer");
            if (_capturedFor != _reckoning)
            {
                _capturedFor = _reckoning;
                _sceneGloom = dimmer.color.a;
            }
            else
            {
                var colour = dimmer.color;
                colour.a = _sceneGloom;
                dimmer.color = colour;
            }

            _reckoning.Show(Reward(), offers);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItOpensOnTheChoiceNotOnTheSummary()
        {
            yield return OpenIt(Offers());

            Assert.IsTrue(Named("ReckoningOfferPhase").activeSelf);
            Assert.IsFalse(Named("ReckoningSummaryPhase").activeSelf);
        }

        [UnityTest]
        public IEnumerator TheChoicePhaseHasNoWayOutButChoosing()
        {
            // No skip, by design: you always take something. If a Continue
            // button ever appears on this phase it becomes optional again.
            yield return OpenIt(Offers());

            var continueButton = Named("ReckoningContinueButton");

            Assert.IsFalse(continueButton.activeInHierarchy,
                "the choice phase has grown a way to leave without choosing");
        }

        [UnityTest]
        public IEnumerator TakingOneSweepsThroughToTheSummary()
        {
            yield return OpenIt(Offers());

            yield return TakeAndMoveOn();

            yield return WaitForTheSweepToLand();

            Assert.IsFalse(Named("ReckoningOfferPhase").activeSelf);
            Assert.IsTrue(Named("ReckoningSummaryPhase").activeSelf);
            Assert.IsTrue(Named("ReckoningContinueButton").activeInHierarchy,
                "the summary needs its way out");
        }

        [UnityTest]
        public IEnumerator TheSummaryLandsAtRestRatherThanPartWayThroughTheSweep()
        {
            yield return OpenIt(Offers());

            yield return TakeAndMoveOn();
            yield return WaitForTheSweepToLand();

            var summary = Named("ReckoningSummaryPhase").GetComponent<RectTransform>();

            Assert.AreEqual(0f, summary.anchoredPosition.x, 1f, "the sweep never finished");
        }

        // Which way it goes: the summary arrives from the left and the
        // choice leaves to the right, the handedness for going FORWARD
        // (taking a reward), not the one every interface uses for going
        // back. Both directions land the summary at rest in the centre, so
        // only the direction of travel tells them apart.
        [UnityTest]
        public IEnumerator TheSweepAdvancesRatherThanRetreating()
        {
            yield return OpenIt(Offers());

            var offer = Named("ReckoningOfferPhase").GetComponent<RectTransform>();
            yield return TakeAndMoveOn();
            yield return WaitForTheSweepToLand();

            Assert.Less(offer.anchoredPosition.x, 0f,
                "the choice left to the RIGHT, so the summary came in from the left - that is the " +
                "direction a cancelled screen arrives from, and taking a reward is going forward");
        }

        // The smear is part of the MOTION and must not outlive it. A phase left
        // stretched or half-faded reads as a rendering fault, and it is the
        // failure mode of driving localScale and alpha from an animation that
        // never lands exactly on its last frame.
        [UnityTest]
        public IEnumerator NothingIsLeftSmearedOnceTheSweepHasLanded()
        {
            yield return OpenIt(Offers());

            yield return TakeAndMoveOn();
            yield return WaitForTheSweepToLand();

            var summary = Named("ReckoningSummaryPhase");
            var rect = summary.GetComponent<RectTransform>();

            Assert.AreEqual(1f, rect.localScale.x, 0.001f,
                "the summary is still horizontally stretched by the sweep's motion blur");

            var group = summary.GetComponent<CanvasGroup>();
            if (group != null)
            {
                Assert.AreEqual(1f, group.alpha, 0.001f,
                    "the summary is still dimmed by the sweep's motion blur");
            }
        }

        [UnityTest]
        public IEnumerator TheRingsOnlyFillOnceTheSweepHasLanded()
        {
            // Filling a ring mid-sweep means watching it climb on something
            // still sliding, and neither gets read.
            yield return OpenIt(Offers());

            yield return TakeAndMoveOn();
            var gain = Named("ReckoningPortrait0RingGain").GetComponent<Image>();
            if (Named("ReckoningOfferPhase").activeSelf)
            {
                Assert.AreEqual(0f, gain.fillAmount, 0.001f, "the ring moved before the board arrived");
            }

            yield return WaitForTheSweepToLand();
            yield return WaitForTheBoardToSettle();

            Assert.AreEqual(0.79f, gain.fillAmount, 0.001f, "the ring never ran after the sweep");
        }

        [UnityTest]
        public IEnumerator TheFrameOpensAsAHorizontalWipeAndEndsFullWidth()
        {
            // OPTED OUT of [SetUp]'s SpeedMultiplier = 40 for the opening
            // frames below -- this test samples the SHAPE of the wipe one
            // frame in (still part-open), not just whether it has finished.
            // At 40x, a single real frame's unscaled delta already covers
            // more than the whole 0.34s wipe, so the panel would read as
            // fully open before the first assertion ever ran. Restored to
            // full speed once those shape checks are done, for the settle
            // wait below.
            ReckoningController.SpeedMultiplier = 1f;

            // Full height from the first frame, zero width, opening outward.
            // A scale-pop reads as a dialog; this reads as the panel being
            // drawn across the fight.
            yield return OpenIt(Offers());

            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            var frame = Named("ReckoningFrame").GetComponent<RectTransform>();

            // One frame in: part-open, and already full height. Sampled a
            // single frame after Show, so this only mis-reads if one frame took
            // longer than the whole 0.34s wipe.
            Assert.Less(wipe.rect.width, ReckoningScreen.PanelWidth,
                "the wipe was already fully open on its first frame");
            Assert.AreEqual(ReckoningScreen.PanelHeight, wipe.rect.height, 0.5f,
                "the wipe is opening vertically too");

            // The frame itself is never touched: localScale scales
            // CHILDREN, so driving frame.localScale.x would compress the
            // border ornaments, the three cards and every label with it.
            // Checked mid-animation as well as at rest, because a scale
            // that settles back to 1 leaves no trace at the end.
            Assert.AreEqual(Vector3.one, frame.localScale,
                "the frame is being scaled again - everything inside it squashes too");
            Assert.AreEqual(ReckoningScreen.PanelWidth, frame.rect.width, 0.5f,
                "the frame is being resized rather than revealed by the mask");

            ReckoningController.SpeedMultiplier = 40f;
            yield return WaitForThePlayInToLand();

            Assert.AreEqual(ReckoningScreen.PanelWidth, wipe.rect.width, 1f,
                "the wipe never finished opening");
            Assert.AreEqual(0f, wipe.anchoredPosition.y, 1f, "the lift never settled");
            Assert.AreEqual(Vector3.one, frame.localScale, "the frame ended up scaled");
        }

        [UnityTest]
        public IEnumerator TheWipeAndTheSweepAreBothActuallyMasked()
        {
            // The animations are only half the fix. Without the two
            // RectMask2Ds the open still deforms nothing but reveals nothing
            // either -- a zero-width unmasked rect shows its children in full
            // -- and the outgoing cards still sail over the battlefield.
            yield return OpenIt(Offers());

            Assert.IsNotNull(Named("ReckoningFrameWipe").GetComponent<RectMask2D>(),
                "the wipe has no mask, so opening its width hides nothing");

            var clip = Named("ReckoningPhaseClip");
            Assert.IsNotNull(clip.GetComponent<RectMask2D>(),
                "the phases are unbounded again - the sweep will leave the frame");

            // Inset to the painted border, not to the panel edge: a card has to
            // vanish BEHIND the gold, not at the outside of it.
            Assert.AreEqual(ReckoningScreen.ContentHalfWidth * 2f,
                clip.GetComponent<RectTransform>().rect.width, 1f,
                "the clip no longer matches the frame's painted interior");
        }

        [UnityTest]
        public IEnumerator TheOfferCardsCarryNoButtonPlate()
        {
            // Reported fixed twice and never was: BuildOffer dropped the
            // declared children, but UiEmitter applies the shared button sprite
            // to any Ui.Button that does not name one, so the gold plates came
            // straight back from the fallback.
            yield return OpenIt(Offers());

            var plate = Named("ReckoningOffer0").GetComponent<Image>();

            Assert.IsNull(plate.sprite, "the offer is wearing button chrome again");
            Assert.AreEqual(0f, plate.color.a, 0.001f, "the offer's plate is still drawn");

            // And it is still the click target. A transparent Image raycasts on
            // its rect rather than its alpha, which is the only reason removing
            // the chrome does not also remove the one decision this screen asks
            // for.
            Assert.IsTrue(plate.raycastTarget, "the offer can no longer be clicked");
        }

        // C8: Continue is the gold forward-arrow plate, no word, and the ember
        // glow tuned to the old arrow's taper is gone.
        [UnityTest]
        public IEnumerator ContinueIsTheGoldForwardArrow_WithNoEmberGlow()
        {
            yield return OpenIt(Offers());
            yield return TakeAndMoveOn();
            yield return WaitForTheSweepToLand();

            Assert.IsNull(Named("ReckoningContinueGlow"), "the stale ember glow is back");
            var label = Named("ReckoningContinueButton").GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.AreEqual(">>", label.text);
        }

        [UnityTest]
        public IEnumerator TheDropGlowFadesUpBehindTheFrame()
        {
            yield return OpenIt(Offers());
            yield return WaitForThePlayInToLand();

            var glow = Named("ReckoningFrameGlow").GetComponent<Image>();

            Assert.Greater(glow.color.a, 0.05f, "the drop never arrived");
            Assert.Less(glow.color.a, 0.75f, "a glow you can point at has stopped being one");
        }
    }
}
