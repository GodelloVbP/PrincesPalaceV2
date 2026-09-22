using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Phase 1's gate (docs/GAMEPAD_NAVIGATION_PLAN.md sections 3, 10, 11):
    // the four acceptance tests, driven through the REAL dispatcher on a
    // fixture -- a scripted BaseInput via inputOverride, `yield return null`
    // per frame, never a direct MoveFocus/ConfirmFocus/OnBackPressed call.
    // That distinction is the whole point: a direct call proves the model
    // underneath (FightGamepadNavigationTests already does that); these
    // prove the ONE DISPATCH POINT that decides which frame's input reaches
    // which context at all.
    public class NavigationDispatcherTests
    {
        private GameObject _root;
        private ScriptedBaseInput _input;
        private Button _buttonA;
        private Button _buttonB;
        private Button _modalButton;
        private Image _modalDimmer;
        private FightStub _fight;

        private int _clicksA;
        private int _clicksB;
        private int _clicksModal;

        // The three call counters the plan's Fight branch is proven against
        // -- no MonoBehaviour, no scene, standing in for FightController the
        // same way the plan's own worked example describes (section 3's "a
        // future Fight verb that synchronously selects").
        private class FightStub : IFightNavigationTarget
        {
            public int MoveFocusCalls;
            public int ConfirmFocusCalls;
            public int OnBackPressedCalls;
            public int InspectMoveCalls;
            public int CharacterSelectCalls;
            public Action OnConfirm;

            public void MoveFocus(int delta) => MoveFocusCalls++;

            // The horizontal axis' own call (the owner's 2026-09-19 inspect
            // path). Counted rather than ignored for the same reason the
            // three above are: this fixture's claims are about WHICH member
            // one frame's input reaches, and a fourth member that could be
            // reached has to be visible to them.
            public void InspectMove(int delta) => InspectMoveCalls++;
            public void EnterCharacterSelect() => CharacterSelectCalls++;

            public void ConfirmFocus()
            {
                ConfirmFocusCalls++;
                OnConfirm?.Invoke();
            }

            public void OnBackPressed() => OnBackPressedCalls++;

            // The focus marker's own source in the Fight branch (hardware
            // round 1's visual pass). This stub has no scene and therefore no
            // element to point at -- null is the interface's own "nothing to
            // point at" answer, and it is what keeps this fixture's claims
            // about the three CALLS unaffected by the marker existing.
            public object FocusedElement => null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("NavigationDispatcherFixture");

            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(NavigationInputModule));
            esGO.transform.SetParent(_root.transform);
            var eventSystem = esGO.GetComponent<EventSystem>();
            var module = esGO.GetComponent<NavigationInputModule>();
            _input = esGO.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            // EventSystem.current is a list, newest-enabled appended at the
            // END (m_EventSystems.Add), so "current" (index 0) stays
            // whichever ran first -- and EventSystem.Update() no-ops its own
            // Process() call entirely when it isn't current. A stray
            // EventSystem left enabled by an earlier fixture in the same
            // suite run is exactly that: harmless alone (this file's tests
            // all pass in isolation), silently starving every assertion
            // below when the full gate runs them after other PlayMode
            // classes. Forcing it explicitly is the fix, not hoping nothing
            // else is still enabled.
            EventSystem.current = eventSystem;

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(_root.transform);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _buttonA = MakeButton(canvas.transform, "ButtonA", new Vector2(-150f, 0f));
            _buttonA.onClick.AddListener(() => _clicksA++);

            _buttonB = MakeButton(canvas.transform, "ButtonB", new Vector2(150f, 0f));
            _buttonB.onClick.AddListener(() => _clicksB++);

            SetExplicit(_buttonA, right: _buttonB);
            SetExplicit(_buttonB, left: _buttonA);

            // Two ordinary Buttons registered as one context with Explicit
            // links between them (plan section 10's fixture shape) -- the
            // modal's own button, on the same canvas.
            _modalButton = MakeButton(canvas.transform, "ModalButton", Vector2.zero);
            _modalButton.onClick.AddListener(() => _clicksModal++);

            // The full-screen raycast-blocking Image, matching Ui.Modal's own
            // dimmer (plan section 2: never marked AsDecor, so it keeps
            // Image's default raycastTarget = true) -- created AFTER
            // buttonA/B so it draws above them, and the modal button is then
            // moved above THAT, so the modal stays clickable while it blocks
            // the screen underneath.
            var dimmerGO = new GameObject("ModalDimmer", typeof(Image));
            dimmerGO.transform.SetParent(canvas.transform, false);
            _modalDimmer = dimmerGO.GetComponent<Image>();
            _modalDimmer.color = new Color(0f, 0f, 0f, 0.5f);
            var dimmerRect = (RectTransform)dimmerGO.transform;
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            _modalDimmer.raycastTarget = true;
            dimmerGO.SetActive(false);

            _modalButton.transform.SetAsLastSibling();

            _fight = new FightStub();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(_root);
            yield return null;
        }

        private static Button MakeButton(Transform parent, string name, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.anchoredPosition = anchoredPosition;
            return go.GetComponent<Button>();
        }

        private static void SetExplicit(Button button, Button left = null, Button right = null)
        {
            // Fully qualified -- PrincesPalace.Navigation (the scene-load
            // helper) is in scope through this file's own enclosing
            // namespace and shadows UnityEngine.UI.Navigation otherwise.
            var nav = button.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            if (left != null) nav.selectOnLeft = left;
            if (right != null) nav.selectOnRight = right;
            button.navigation = nav;
        }

        // One engine frame: EventSystem.Update() calls Process() exactly
        // once during it (plan section 2), so this is the only way any of
        // these tests drives the module -- never a direct Process() call.
        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // ---- test 1: modal-to-Fight -------------------------------------------

        [UnityTest]
        public IEnumerator ModalCancel_PopsTheModal_FightsBranchWaitsForTheNextCall()
        {
            var fightContext = NavContext.ForFight(_fight);
            NavigationInputModule.Contexts.Push(fightContext);

            bool modalClosed = false;
            var modalContext = new NavContext(
                _modalButton.gameObject,
                new Dictionary<string, object> { { "modal", _modalButton.gameObject } },
                cancel: () =>
                {
                    NavigationInputModule.Contexts.Pop();
                    _modalDimmer.gameObject.SetActive(false);
                    modalClosed = true;
                });
            NavigationInputModule.Contexts.Push(modalContext);
            _modalDimmer.gameObject.SetActive(true);

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsTrue(modalClosed, "the modal's own Cancel handler never ran");
            Assert.AreEqual(0, _fight.OnBackPressedCalls,
                "Fight's OnBackPressed fired in the SAME call the modal popped -- topAtStart must stay " +
                "the modal for this whole Process(), Fight's branch waits for the next call");
            Assert.IsTrue(NavigationInputModule.Contexts.IsTop(fightContext),
                "Fight should be top again once the modal's own Cancel handler popped it");
        }

        // ---- test 2: same-call ordering -----------------------------------

        [UnityTest]
        public IEnumerator FightConfirm_SynchronouslySelecting_DoesNotAlsoReceiveThisFramesSubmit()
        {
            var fightContext = NavContext.ForFight(_fight);
            NavigationInputModule.Contexts.Push(fightContext);

            int lateClicks = 0;
            _fight.OnConfirm = () =>
            {
                // Stands in for the not-yet-live case the plan's section 3
                // argues about: a Fight verb that opens a shared menu and
                // selects its first entry SYNCHRONOUSLY, inside ConfirmFocus
                // itself. _buttonB stands in for that menu's entry.
                _buttonB.onClick.AddListener(() => lateClicks++);
                EventSystem.current.SetSelectedGameObject(_buttonB.gameObject);
            };

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _fight.ConfirmFocusCalls);
            Assert.AreEqual(0, lateClicks,
                "the newly-selected Button received THIS call's Submit -- base.Process() must consume " +
                "the frame's Submit against null selection (step 3) BEFORE step 4 calls ConfirmFocus");
        }

        // ---- test 3: mouse-then-Submit -------------------------------------

        [UnityTest]
        public IEnumerator MouseClickThenSubmit_FiresOnClickOnceForEach_FightUntouched()
        {
            var context = new NavContext(
                _buttonA.gameObject,
                new Dictionary<string, object> { { "A", _buttonA.gameObject }, { "B", _buttonB.gameObject } },
                cancel: null);
            NavigationInputModule.Contexts.Push(context);

            _input.MousePosition = ((RectTransform)_buttonA.transform).position;
            _input.MouseButton0Down = true;
            _input.MouseButton0Up = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _clicksA, "the mouse click did not fire Button A's onClick exactly once");

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(2, _clicksA,
                "Submit on the now-selected Button A should fire onClick a second time, not ConfirmFocus " +
                "-- Draft 2's own Test 1 watched the wrong signal here (plan section 3)");
            Assert.AreEqual(0, _fight.MoveFocusCalls + _fight.ConfirmFocusCalls + _fight.OnBackPressedCalls,
                "an ordinary context is top -- the Fight stub must never be touched");
        }

        [UnityTest]
        public IEnumerator MouseClickBehindModal_DoesNotReachTheScreen_SubmitResolvesAgainstTheModal()
        {
            var modalContext = new NavContext(
                _modalButton.gameObject,
                new Dictionary<string, object> { { "modal", _modalButton.gameObject } },
                cancel: null);
            NavigationInputModule.Contexts.Push(modalContext);
            _modalDimmer.gameObject.SetActive(true);

            // One quiet frame lets the post-dispatch reselection rule select
            // the modal's own entry -- nothing was selected the instant it
            // was pushed (plan section 4: a modal self-registers its focus
            // on open; here that is the dispatcher's own rule doing it).
            yield return DriveFrame();

            _input.MousePosition = ((RectTransform)_buttonA.transform).position;
            _input.MouseButton0Down = true;
            _input.MouseButton0Up = true;
            yield return DriveFrame();

            Assert.AreEqual(0, _clicksA,
                "a click through the modal's dimmer must not reach the screen underneath it");

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _clicksModal, "Submit should resolve against the modal's own selected control");
        }

        // ---- test 4: Fight isolation ----------------------------------------

        [UnityTest]
        public IEnumerator FightTop_NullsAnyStraySelection_AndOnlyDrivesTheStub()
        {
            var fightContext = NavContext.ForFight(_fight);
            NavigationInputModule.Contexts.Push(fightContext);

            // The exact counter-example the null-assert exists to defeat
            // (plan section 3): a stray/programmatic selection landing while
            // Fight is top.
            EventSystem.current.SetSelectedGameObject(_buttonA.gameObject);
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "the programmatic select before the assertion needs to actually land, or this test proves nothing");

            yield return DriveFrame();

            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "Fight must null selection every frame it is top, unconditionally");

            _input.Vertical = 1f;
            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, _fight.MoveFocusCalls);
            Assert.AreEqual(1, _fight.ConfirmFocusCalls);
            Assert.AreEqual(0, _clicksA);
            Assert.AreEqual(0, _clicksB);
            Assert.AreEqual(0, _clicksModal);
        }
    }
}
