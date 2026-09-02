using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        // POLLED ON THE OFFER PHASE ITSELF. SweepToSummary sets it inactive
        // as its very last step -- after StartBars() has already been kicked
        // off, so this also guarantees the bars have started where that
        // matters below -- and it is a one-way flag rather than a value that
        // plateaus, so it is safe to poll directly. [SetUp]'s
        // SpeedMultiplier = 40 runs the whole 0.18s SweepSeconds journey in
        // low single-digit milliseconds of real time; 0.3s real is two
        // orders of magnitude past that while still catching a real stall
        // (the offer phase never leaving).
        private IEnumerator WaitForTheSweepToLand()
        {
            float deadline = Time.realtimeSinceStartup + 0.3f;
            while (Named("ReckoningOfferPhase").activeSelf && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        // POLLED ON THE GAIN LABEL ITSELF. FillBar has no exposed
        // "still running" flag any more than ReckoningTests' bar sequence
        // does (see its WaitForTheBarSequence), but unlike that one this
        // fixture's Reward() never levels up (LevelAfter == LevelBefore), so
        // CountGain only ever climbs toward ExpGained and never plateaus or
        // resets mid-sequence -- safe to poll for the final value landing
        // rather than guess a duration for it.
        //
        // 2s ceiling, not the tighter 0.5s this started at: the animation
        // itself needs only a handful of scaled frames, but the deadline is
        // WALL time against a coroutine that only gets to advance once per
        // Update. One slow frame right after Show() -- a GC pause, JIT on a
        // scene freshly loaded -- can burn the whole budget before FillBar
        // gets its second tick, which read here as the bar never having
        // started at all. Seen flaky at 0.5s in a full multi-fixture run
        // (never in isolation); 2s, matching WaitForThePlayInToLand's own
        // margin, gives one bad frame room without hiding an actual stall.
        private IEnumerator WaitForTheGainToLand(TMPro.TMP_Text gain, string expected)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!gain.text.Contains(expected) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        // BOUNDED ON THE WIPE'S OWN WIDTH, same shape as ReckoningTests'
        // equivalent poll -- PlayIn is a single monotonic ease with no
        // plateau, so it is safe to poll directly: it either reaches full
        // width or the ceiling fires and callers see the truth (a still-
        // narrow panel, a glow that never faded up) instead of a timing
        // guess.
        private IEnumerator WaitForThePlayInToLand()
        {
            var wipe = Named("ReckoningFrameWipe").GetComponent<RectTransform>();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (wipe.rect.width < ReckoningScreen.PanelWidth - 0.5f && Time.realtimeSinceStartup < deadline)
            {
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

        private IEnumerator OpenIt(List<ItemOffer> offers)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

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

        // WHICH WAY IT GOES, which nothing checked and which was backwards.
        //
        // The summary used to arrive from the LEFT and the choice leave to the
        // right, which is the handedness every interface uses for going BACK.
        // Taking a reward is going forward. Both directions land the summary at
        // rest in the centre, so the test above passed either way and the only
        // thing that could tell them apart was somebody watching it.
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

            // AND THE FRAME ITSELF IS NEVER TOUCHED. This is the pair of
            // assertions that would have caught the squash: the old animation
            // drove frame.localScale.x, and localScale scales CHILDREN, so the
            // border ornaments, the three cards and every label compressed with
            // it. Checked mid-animation as well as at rest, because a scale
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
