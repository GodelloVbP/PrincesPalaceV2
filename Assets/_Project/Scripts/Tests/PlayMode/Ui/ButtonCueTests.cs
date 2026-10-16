using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // A button's press cue belongs to the button, not to the device that
    // pressed it. Pointer, Submit (pad A, Enter) and Cancel all have to sound
    // the way the mouse always did, exactly once.
    //
    // The rig half drives the EventSystem's own submit and click events at a
    // bare button, which is what the dispatcher ends in; the scene half goes
    // through the real NavigationInputModule with scripted input.
    public class ButtonCueTests
    {
        private readonly List<Sound> _played = new List<Sound>();
        private GameObject _rig;
        private string _root;
        private ScriptedBaseInput _input;

        [SetUp]
        public void Listen()
        {
            _played.Clear();
            SoundController.Observer = s => _played.Add(s);
        }

        [TearDown]
        public void Restore()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
            if (_input != null) NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            if (_root != null)
            {
                SaveSystem.RootOverride = null;
                if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            }
        }

        private GameObject Button(bool themed, Sound cue, bool interactable = true)
        {
            if (_rig == null)
            {
                _rig = new GameObject("CueRig", typeof(Canvas), typeof(EventSystem));
                _rig.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var go = new GameObject("CueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_rig.transform, worldPositionStays: false);
            go.GetComponent<Button>().interactable = interactable;
            if (themed) go.AddComponent<ThemedButtonState>().clickSound = cue;
            else go.AddComponent<ButtonPressAnimator>().clickSound = cue;
            return go;
        }

        private void Submit(GameObject go) =>
            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        private static void Click(GameObject go) =>
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        private static readonly bool[] BothKinds = { false, true };

        [UnityTest]
        public IEnumerator Submit_SoundsTheButtonsCue_ExactlyOnce([ValueSource(nameof(BothKinds))] bool themed)
        {
            var go = Button(themed, Sound.ButtonClick);
            yield return null;

            Submit(go);

            CollectionAssert.AreEqual(new[] { Sound.ButtonClick }, _played);
        }

        [UnityTest]
        public IEnumerator Submit_AndPointerClick_PlayTheSameCue([ValueSource(nameof(BothKinds))] bool themed)
        {
            var go = Button(themed, Sound.MenuClose);
            yield return null;

            Click(go);
            Submit(go);

            CollectionAssert.AreEqual(new[] { Sound.MenuClose, Sound.MenuClose }, _played);
        }

        [UnityTest]
        public IEnumerator Submit_OnANonInteractableButton_IsClickFail([ValueSource(nameof(BothKinds))] bool themed)
        {
            var go = Button(themed, Sound.ButtonClick, interactable: false);
            yield return null;

            Submit(go);

            CollectionAssert.AreEqual(new[] { Sound.ClickFail }, _played);
        }

        [UnityTest]
        public IEnumerator Submit_OnAQuietButton_IsSilent_EvenWhenRefused([ValueSource(nameof(BothKinds))] bool themed)
        {
            var quiet = Button(themed, Sound.None);
            var refused = Button(themed, Sound.None, interactable: false);
            yield return null;

            Submit(quiet);
            Submit(refused);

            CollectionAssert.IsEmpty(_played);
        }

        // Nothing builds both on one button, but the seam promises a press
        // never doubles if something does.
        [UnityTest]
        public IEnumerator BothComponentsOnOneButton_PlayOnce()
        {
            var go = Button(themed: false, Sound.ButtonClick);
            go.AddComponent<ThemedButtonState>().clickSound = Sound.ButtonClick;
            yield return null;

            Submit(go);

            CollectionAssert.AreEqual(new[] { Sound.ButtonClick }, _played);
        }

        // ---- Cancel -------------------------------------------------------------

        [Test]
        public void Cancel_WithALiveHandler_PlaysMenuCloseOnce()
        {
            int closed = 0;
            var context = new NavContext(null, null, cancel: () => closed++);

            NavigationInputModule.RaiseCancelWithCue(context);

            Assert.AreEqual(1, closed);
            CollectionAssert.AreEqual(new[] { Sound.MenuClose }, _played);
        }

        [Test]
        public void Cancel_OnARoot_IsSilent()
        {
            NavigationInputModule.RaiseCancelWithCue(new NavContext(null, null, cancel: null));

            CollectionAssert.IsEmpty(_played);
        }

        [Test]
        public void Cancel_ThatTheContextReportsAsNotLive_IsSilent()
        {
            int ran = 0;
            var context = new NavContext(null, null, cancel: () => ran++, cancelLive: () => false);

            NavigationInputModule.RaiseCancelWithCue(context);

            Assert.AreEqual(1, ran, "the handler still runs; only the cue is withheld");
            CollectionAssert.IsEmpty(_played);
        }

        private sealed class Claim : INavCancelClaim
        {
            public bool Spend;
            public bool ClaimCancel() => Spend;
        }

        [Test]
        public void Cancel_ClaimedByAPane_PlaysMenuCloseOnce_AndDeclinedFallsThrough()
        {
            var claim = new Claim { Spend = true };
            int closed = 0;
            var context = new NavContext(null, null, cancel: () => closed++, claimant: () => claim);

            NavigationInputModule.RaiseCancelWithCue(context);
            Assert.AreEqual(0, closed, "a claimed press never reaches the context's own Cancel");
            CollectionAssert.AreEqual(new[] { Sound.MenuClose }, _played);

            claim.Spend = false;
            NavigationInputModule.RaiseCancelWithCue(context);
            Assert.AreEqual(1, closed);
            CollectionAssert.AreEqual(new[] { Sound.MenuClose, Sound.MenuClose }, _played);
        }

        // ---- through the real dispatcher ------------------------------------------

        private IEnumerator OpenDefeat()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cue-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };

            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();

            var defeat = Object.FindAnyObjectByType<DefeatController>(FindObjectsInactive.Include);
            Assert.IsNotNull(defeat);
            if (defeat.gameObject.activeSelf) defeat.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();
            defeat.Show(new RunSettlement.Result { GoldLost = 10, RoomsCleared = 1, DeepestStep = 1, ExpEarned = 5 });
            defeat.Dismissed = () => { };
            yield return null;
            _played.Clear();
        }

        [UnityTest]
        public IEnumerator Dispatcher_PadSubmit_SoundsTheFocusedButtonsOwnCue_Once()
        {
            yield return OpenDefeat();

            var button = Resources.FindObjectsOfTypeAll<Button>()
                .First(b => b.name == "DefeatReturnButton" && b.gameObject.scene.IsValid());
            var press = button.GetComponent<ButtonPressAnimator>();
            var expected = press != null ? press.clickSound : button.GetComponent<ThemedButtonState>().clickSound;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            _played.Clear();

            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();

            CollectionAssert.AreEqual(expected == Sound.None ? new Sound[0] : new[] { expected }, _played);
        }

        // ---- the Reckoning ---------------------------------------------------------

        private IEnumerator OpenReckoning()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cue-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };

            yield return SharedScene.Ensure("Fight");
            _input = NavSceneReuse.TakeOverInput();

            var pages = ReckoningPageFixtures.OneGearPage();
            var reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning);
            if (reckoning.gameObject.activeSelf) reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();
            reckoning.Show(new CombatReward { GoldGained = 10 }, pages);
            reckoning.Dismissed = () => { };
            yield return null;
            yield return null;
            yield return ReckoningRevealWait.UntilChoosable(reckoning);
            _played.Clear();
        }

        // Sound belongs to the Reckoning's own named cues (docs/PLAN_REWARD_SCREENS.md
        // C11): none of its controls clicks, and a refused one does not fail.
        [UnityTest]
        public IEnumerator Reckoning_EveryControlIsQuiet()
        {
            yield return OpenReckoning();

            var buttons = Resources.FindObjectsOfTypeAll<Button>()
                .Where(b => b.gameObject.scene.IsValid() && b.name.StartsWith("Reckoning")).ToList();
            Assert.GreaterOrEqual(buttons.Count, 10, "the cards, whetstones, door, tabs and Continue");

            foreach (var button in buttons)
            {
                var press = button.GetComponent<ButtonPressAnimator>();
                var themed = button.GetComponent<ThemedButtonState>();
                Assert.IsTrue(press != null || themed != null, $"{button.name} has no press cue component");
                Assert.AreEqual(Sound.None, press != null ? press.clickSound : themed.clickSound,
                    $"{button.name} would click");
            }
        }

        [UnityTest]
        public IEnumerator Reckoning_PadSubmitOnTheDoorPlaysNoClick()
        {
            yield return OpenReckoning();

            var door = Resources.FindObjectsOfTypeAll<Button>()
                .First(b => b.name == "ReckoningLeaveButton" && b.gameObject.scene.IsValid());
            EventSystem.current.SetSelectedGameObject(door.gameObject);
            yield return null;
            _played.Clear();

            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();

            CollectionAssert.DoesNotContain(_played, Sound.ButtonClick);
            CollectionAssert.DoesNotContain(_played, Sound.ClickFail);
        }

        [UnityTest]
        public IEnumerator Dispatcher_PadCancel_PlaysMenuCloseOnce()
        {
            yield return OpenDefeat();

            _input.CancelDown = true;
            yield return null;
            _input.ClearOneFrameFlags();

            CollectionAssert.AreEqual(new[] { Sound.MenuClose }, _played);
        }
    }
}
