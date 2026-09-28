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
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

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

        // POLLED ON THE GAIN LABEL ITSELF. FillBar has no exposed
        // "still running" flag any more than ReckoningTests' bar sequence
        // does (see its WaitForTheBarSequence), but unlike that one this
        // fixture's Reward() never levels up (LevelAfter == LevelBefore), so
        // CountGain only ever climbs toward ExpGained and never plateaus or
        // resets mid-sequence -- safe to poll for the final value landing
        // rather than guess a duration for it. BarSeconds is 0.55s, and row
        // 0 has no stagger delay, so that is the whole budget.
        private IEnumerator WaitForTheGainToLand(TMPro.TMP_Text gain, string expected)
        {
            int frames = 0, maxFrames = MaxFrames(0.55f);
            while (!gain.text.Contains(expected) && frames < maxFrames)
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

        private static List<ItemOffer> Offers()
        {
            var ids = Content.ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable)
                .Take(3)
                .Select(i => i.id)
                .ToList();

            return ids.Select(id => new ItemOffer(id, 1, 0)).ToList();
        }

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

        private IEnumerator OpenIt(List<ItemOffer> offers)
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

            Click("ReckoningOffer0");

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

            Click("ReckoningOffer0");
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
            Click("ReckoningOffer0");
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

            Click("ReckoningOffer0");
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
        public IEnumerator TheBarsOnlyStartOnceTheSweepHasLanded()
        {
            // Filling a bar mid-sweep means watching a number climb on
            // something still sliding, and neither gets read.
            yield return OpenIt(Offers());

            Click("ReckoningOffer0");

            // MUST land the sweep first. Paint() writes each row's FINAL
            // gain text the moment Show() runs -- a preview, sitting there
            // before the sweep or FillBar have moved at all -- and FillBar
            // only overwrites it to "+0 EXP" and starts climbing once
            // StartBars() fires at the very end of SweepToSummary. Polling
            // the gain text immediately after Click could catch that
            // Paint() preview on its very first, same-frame check and
            // return early having watched nothing animate, independent of
            // whether the second wait below is bounded by seconds or by
            // frames.
            yield return WaitForTheSweepToLand();

            var gain = Named("ReckoningRow0Gain").GetComponent<TMPro.TMP_Text>();
            yield return WaitForTheGainToLand(gain, "79");

            StringAssert.Contains("79", gain.text, "the bar never ran after the sweep");
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

        [UnityTest]
        public IEnumerator TheContinueGlowIsAliveRatherThanADecal()
        {
            // A baked sprite holding perfectly still reads as a decal stuck on
            // the panel however well it is drawn. Nothing else on this screen
            // would fail if the flare stopped ticking -- the tree would still
            // audit, the sprite would still be there, and the screen would
            // simply go dead.
            yield return OpenIt(Offers());
            Click("ReckoningOffer0");
            yield return WaitForTheSweepToLand();

            var glow = Named("ReckoningContinueGlow").GetComponent<RectTransform>();
            var image = glow.GetComponent<Image>();

            float xMin = glow.anchoredPosition.x, xMax = xMin;
            float sxMin = glow.localScale.x, sxMax = sxMin;
            float syMin = glow.localScale.y, syMax = syMin;
            float aMin = image.color.a, aMax = aMin;

            // Long enough for the slowest term in FlickerCurve.Ember (1.31
            // rad/s) to travel, and for the drift, which is slower still.
            for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                xMin = Mathf.Min(xMin, glow.anchoredPosition.x);
                xMax = Mathf.Max(xMax, glow.anchoredPosition.x);
                sxMin = Mathf.Min(sxMin, glow.localScale.x);
                sxMax = Mathf.Max(sxMax, glow.localScale.x);
                syMin = Mathf.Min(syMin, glow.localScale.y);
                syMax = Mathf.Max(syMax, glow.localScale.y);
                aMin = Mathf.Min(aMin, image.color.a);
                aMax = Mathf.Max(aMax, image.color.a);
                yield return null;
            }

            Assert.Greater(xMax - xMin, 2f, "the glow never drifted");
            Assert.Greater(sxMax - sxMin, 0.02f, "the glow never flared");
            Assert.Greater(aMax - aMin, 0.02f, "the glow never changed brightness");

            // WIDER than it is tall, which is the difference between flaring
            // out along the arrow and simply pulsing. It is also the constraint
            // the layout imposes: the box is 88 tall against 90 of headroom
            // before the frame's painted bottom ornament.
            Assert.Greater(sxMax - sxMin, syMax - syMin,
                "the flare is pulsing evenly rather than reaching along the arrow");

            // AND THE CONFIGURED SWING, which is the assertion that was missing
            // and the reason this test was worthless the first time.
            //
            // Sampling alone only ever proves the numbers are not constant. The
            // first version of this effect swung x by 0.11 on a curve that
            // travels two thirds of its range, so a 1.6s window saw about a
            // hundredth of a scale unit change -- comfortably over the old
            // 0.01 floor, and completely invisible in the running game. A test
            // that passes while a player says "it doesn't move" is measuring
            // the wrong thing.
            //
            // This one is phase-independent: it asks what the effect was
            // CONFIGURED to do, not what a short sample happened to catch.
            var flare = glow.GetComponent<EmberFlare>();
            Assert.IsNotNull(flare, "the glow has no flare component at all");
            Assert.GreaterOrEqual(flare.MaxScaleX - flare.MinScaleX, 0.25f,
                "the flare is configured too tightly to be seen, whatever a sample says");
            Assert.GreaterOrEqual(flare.MaxAlpha - flare.MinAlpha, 0.35f,
                "brightness is what reads as fire; this is barely a change");

            // Still bounded. Past about a third it stops reading as light on
            // the arrow and starts reading as the arrow being inflated.
            Assert.Less(flare.MaxScaleX - flare.MinScaleX, 0.45f,
                "the flare has grown into a pulse you cannot ignore");
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
