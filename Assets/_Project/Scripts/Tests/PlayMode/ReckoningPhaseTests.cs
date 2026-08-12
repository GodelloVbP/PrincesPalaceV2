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

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

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

            // The sweep is 0.3s; give it room to land.
            yield return new WaitForSecondsRealtime(0.7f);

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
            yield return new WaitForSecondsRealtime(0.7f);

            var summary = Named("ReckoningSummaryPhase").GetComponent<RectTransform>();

            Assert.AreEqual(0f, summary.anchoredPosition.x, 1f, "the sweep never finished");
        }

        [UnityTest]
        public IEnumerator TheBarsOnlyStartOnceTheSweepHasLanded()
        {
            // Filling a bar mid-sweep means watching a number climb on
            // something still sliding, and neither gets read.
            yield return OpenIt(Offers());

            Click("ReckoningOffer0");
            yield return new WaitForSecondsRealtime(1.6f);

            var gain = Named("ReckoningRow0Gain").GetComponent<TMPro.TMP_Text>();

            StringAssert.Contains("79", gain.text, "the bar never ran after the sweep");
        }

        [UnityTest]
        public IEnumerator TheFrameOpensAsAHorizontalWipeAndEndsFullWidth()
        {
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

            yield return new WaitForSecondsRealtime(0.8f);

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
        public IEnumerator TheDropGlowFadesUpBehindTheFrame()
        {
            yield return OpenIt(Offers());
            yield return new WaitForSecondsRealtime(0.8f);

            var glow = Named("ReckoningFrameGlow").GetComponent<Image>();

            Assert.Greater(glow.color.a, 0.05f, "the drop never arrived");
            Assert.Less(glow.color.a, 0.75f, "a glow you can point at has stopped being one");
        }
    }
}
