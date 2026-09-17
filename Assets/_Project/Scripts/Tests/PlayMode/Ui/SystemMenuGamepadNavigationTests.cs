using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Step B's behavioural gate (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2):
    // SystemMenu as a nested modal context, driven through the REAL
    // production dispatcher on the REAL Hub scene -- a scripted BaseInput
    // via inputOverride, yield return null, assert resulting state. Never a
    // direct HandleEscape/Open call standing in for a press: Navigation
    // DispatcherTests' own header draws exactly this distinction, and this
    // file is that same proof against production wiring instead of a
    // fixture.
    public class SystemMenuGamepadNavigationTests
    {
        private HubController _hub;
        private SystemMenuController _menu;
        private ScriptedBaseInput _input;

        private IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            // Start() runs one frame after activation.
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_hub, "the hub scene has no HubController");
            Assert.IsNotNull(_menu, "the hub scene has no SystemMenuController");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
        }

        // One engine frame: EventSystem.Update() calls Process() exactly
        // once during it (plan section 2), matching NavigationDispatcherTests'
        // own DriveFrame.
        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        [UnityTest]
        public IEnumerator CancelOpensTheMenuFromTheHub_CancelAgainClosesItAndClearsSelection()
        {
            yield return LoadHub();

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed");
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "nothing should be selected on the hub before anything opens -- the hub declares no " +
                "selectable set of its own yet (phase 3's rollout)");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_menu.IsOpen,
                "Cancel with nothing else up should open the system menu -- HubController.HandleEscape, " +
                "now the hub's own NavContext.Cancel handler, doing the job SystemMenuController's own " +
                "deleted poll used to do");
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "opening the menu should select its own tab bar");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen, "a second Cancel should close the menu it just opened");

            // "Hub's remembered node is restored": the hub has no
            // selectable set of its own to remember a node IN this phase
            // (its own buildings are phase 3's rollout), so the real claim
            // provable today is the one that matters for correctness --
            // closing the menu must not leave its own last-selected tab as
            // the active EventSystem selection, which is exactly what
            // SystemMenuController.PopNavContext leaving a stale selection
            // behind would look like.
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "the menu's last-selected tab leaked through as the hub's own selection after closing");
        }

        [UnityTest]
        public IEnumerator CancelInsideOptions_PopsExactlyOneLayer()
        {
            yield return LoadHub();

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;
            Assert.IsTrue(_menu.IsOpen);

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen,
                "Cancel from inside the Options pane should close the whole menu -- Options has no nested " +
                "context of its own, so there is only ever one layer to pop");

            // Exactly ONE layer, proven rather than assumed: if Cancel had
            // somehow popped the hub's own base context too, this second
            // press would find an empty stack and do nothing.
            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_menu.IsOpen,
                "the hub's own context should still be on top and functioning after the menu's one pop -- " +
                "a second Cancel re-opening the menu is what proves that, rather than the stack having lost " +
                "its own base context along with the menu's");
        }

        // Pinned to literal expected values (docs/CODE_STANDARDS.md section
        // 8) -- never recomputed from the production formula under test.
        //
        // ONE PRESS PER TEST, each from a freshly loaded scene, rather than
        // Right-then-Left in one test. StandaloneInputModule's own move
        // debounce (SendMoveEventToSelectedObject) tracks a real-time
        // timestamp per module instance across consecutive moves, which the
        // plan's own section 10 already flags as outside the seam a scripted
        // BaseInput can control ("a repeat-cadence test... has to wait real
        // frames... or stay hardware-acceptance-only") -- a second press
        // moments after the first, by test-clock time, can be silently
        // treated as a repeat still under the framework's own repeat delay.
        // A fresh scene load gives each press its own fresh module instance
        // instead, sidestepping that timing entirely for what this test
        // actually needs to prove: one press, one step.
        [UnityTest]
        public IEnumerator SliderRow_RightPress_RaisesItByExactlyOneStep()
        {
            yield return LoadHub();
            GameSettings.SetSoundVolume(0.5f);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var soundRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowsound");
            EventSystem.current.SetSelectedGameObject(soundRow.gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();

            Assert.AreEqual(0.55f, GameSettings.SoundVolume, 0.0001f,
                "one Right press on a slider row should raise it by exactly OptionsController.SliderStep (0.05)");
        }

        [UnityTest]
        public IEnumerator SliderRow_LeftPress_LowersItByExactlyOneStep()
        {
            yield return LoadHub();
            GameSettings.SetSoundVolume(0.5f);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var soundRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowsound");
            EventSystem.current.SetSelectedGameObject(soundRow.gameObject);
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();

            Assert.AreEqual(0.45f, GameSettings.SoundVolume, 0.0001f,
                "one Left press on a slider row should lower it by exactly OptionsController.SliderStep (0.05)");
        }

        [UnityTest]
        public IEnumerator StepperRow_RightPress_StepsItUpByExactlyOneIndex()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(0);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var resolutionRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowresolution");
            EventSystem.current.SetSelectedGameObject(resolutionRow.gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();

            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "one Right press on a stepper row should step its index up by exactly one");
        }

        [UnityTest]
        public IEnumerator StepperRow_LeftPress_StepsItDownByExactlyOneIndex()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(1);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var resolutionRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowresolution");
            EventSystem.current.SetSelectedGameObject(resolutionRow.gameObject);
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();

            Assert.AreEqual(0, GameSettings.ResolutionIndex,
                "one Left press on a stepper row should step its index down by exactly one");
        }

        // Structural half of plan section 7's stepper claim: a stepper
        // button must never be a navigation target, and clicking it must
        // still fire its own onClick. NavigationDispatcherTests' fixture
        // (a hand-built ScreenSpaceOverlay canvas) is where the full
        // click-vs-selection interaction through a real scripted raycast is
        // proven -- this scene's canvas is ScreenSpaceCamera
        // (SceneBuilder.CreateCanvas), where a RectTransform's world
        // position is not simply a screen coordinate, so a click simulated
        // here would be testing arithmetic this file has no business
        // getting wrong rather than the row's own behaviour.
        [UnityTest]
        public IEnumerator StepperButtons_AreNeverANavigationTarget_AndStillClick()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(0);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var prevButton = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "OptionsRowresolutionPrev");
            var nextButton = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "OptionsRowresolutionNext");

            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, prevButton.navigation.mode,
                "the stepper's Prev button must never be a navigation target");
            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, nextButton.navigation.mode,
                "the stepper's Next button must never be a navigation target");

            nextButton.onClick.Invoke();
            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "Navigation.Mode.None must not also suppress the button's own click");
        }
    }
}
