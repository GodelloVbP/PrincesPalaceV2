using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Shared mechanism for docs/GAMEPAD_NAVIGATION_PLAN.md phase 4 item 2 --
    // the controller-only journey (section 13's own play-test checklist),
    // driven end to end through the REAL production dispatcher. Carries no
    // test of its own, which is what lets it live in Shared/ rather than in
    // whichever area folder holds the first segment class (CLAUDE.md/
    // tools/test_areas.ps1's own rule: nothing here may carry a [Test]/
    // [UnityTest]).
    //
    // ONE RULE EVERY SEGMENT CLASS FOLLOWS: no step drives UI except through
    // ScriptedBaseInput. Nothing here calls a controller method or a
    // Button's onClick directly -- every press below reaches production
    // through NavigationInputModule.Process(), the same seam a real pad
    // reaches it through (docs/GAMEPAD_NAVIGATION_PLAN.md section 2's own
    // verification that every value the dispatcher reads goes through this
    // exact door). A segment MAY set up state directly through the
    // orchestrator/save layer before its own interaction starts (seeding a
    // run, levelling a squad) -- that is preparing the world, not driving
    // the screen.
    public abstract class JourneyFixture
    {
        protected ScriptedBaseInput Input;

        // Re-homes the scripted input onto whichever scene just (re)loaded.
        // Every scene this project builds carries its own EventSystem/
        // NavigationInputModule, and a previously loaded scene's own
        // EventSystem can still be EventSystem.current for at least one
        // frame after a fresh load (CancelOpensSystemMenuTests' own hazard
        // note, repeated by every file in this family) -- so this is called
        // again after EVERY real scene transition the journey crosses, never
        // once at the top of a test.
        protected NavigationInputModule TakeOverInput()
        {
            var module = UnityEngine.Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the freshly loaded scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            Input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = Input;
            return module;
        }

        // One engine frame: EventSystem.Update() calls Process() exactly
        // once during it (docs/GAMEPAD_NAVIGATION_PLAN.md section 2) --
        // every press helper below yields exactly one of these and never
        // calls Process() by hand.
        protected IEnumerator DriveFrame()
        {
            yield return null;
            Input.ClearOneFrameFlags();
        }

        protected IEnumerator PressSubmit()
        {
            Input.SubmitDown = true;
            yield return DriveFrame();
        }

        protected IEnumerator PressCancel()
        {
            Input.CancelDown = true;
            yield return DriveFrame();
        }

        protected IEnumerator PressTabNext()
        {
            Input.TabNextDown = true;
            yield return DriveFrame();
        }

        protected IEnumerator PressTabPrev()
        {
            Input.TabPrevDown = true;
            yield return DriveFrame();
        }

        // Horizontal/Vertical are LEVELS (ScriptedBaseInput's own header),
        // so this sets both, drives the one frame the dispatcher reads them
        // on, then returns them to rest before returning.
        //
        // THE REAL-TIME SETTLE AFTER RELEASE IS NOT COSMETIC. Confirmed
        // empirically against this exact suite (a diagnostic
        // WaitForSecondsRealtime inserted, then removed once the cause was
        // named): StandaloneInputModule's own move gate,
        // AllowMoveEventProcessing, ORs a fresh nonzero axis read against
        // `time > m_PrevActionTime + moveRepeatDelay` (Time.unscaledTime, no
        // BaseInput seam -- docs/GAMEPAD_NAVIGATION_PLAN.md section 10's own
        // "repeat-cadence testing... has to wait real frames" note is this
        // same gate from the other side). A single isolated press is always
        // let through by the axis-nonzero half of that OR; what silently
        // drops is the NEXT chained move in an ordinary (non-Fight) context
        // when it arrives sooner than moveRepeatDelay (Unity's own default,
        // 0.5s) after the last one -- which every SINGLE-PRESS-PER-TEST file
        // in this project's existing gamepad-nav suite (SystemMenu/Map/Shop/
        // RelicDraft/Reckoning's own headers all say so) sidesteps by never
        // chaining two dispatcher moves in the same test at all. A journey
        // is nothing BUT chained moves, so this fixture pays the real-time
        // cost once, here, instead of every segment rediscovering the same
        // silent drop. Harmless for Fight's own branch (ProcessFight's
        // MoveFocus path never goes through base.Process()'s move dispatch,
        // so it never reads this gate) and for the first move after a
        // Submit/Cancel/TabNext/scene-load, which already have their own
        // real-time cost baked in.
        private const float SettleSeconds = 0.6f;

        protected IEnumerator Move(float horizontal, float vertical)
        {
            Input.Horizontal = horizontal;
            Input.Vertical = vertical;
            yield return DriveFrame();
            Input.Horizontal = 0f;
            Input.Vertical = 0f;
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return DriveFrame();
        }

        protected IEnumerator MoveUp() => Move(0f, 1f);
        protected IEnumerator MoveDown() => Move(0f, -1f);
        protected IEnumerator MoveLeft() => Move(-1f, 0f);
        protected IEnumerator MoveRight() => Move(1f, 0f);

        // ---- pointer helpers (phase 4 item 3 -- the mouse-only regression) -----
        //
        // The scene's own root Canvas, freshly looked up rather than cached:
        // TakeOverInput is called again after every real scene load (this
        // file's own header on why), and a cached Canvas reference from the
        // PREVIOUS scene would be Unity's fake-null the moment that scene
        // unloads -- the same MissingReferenceException trap WaitForScene's
        // own comment warns about for a controller reference.
        //
        // FILTERED TO THE ACTIVE SCENE explicitly, not just `isRootCanvas` --
        // found flaky under the full run_tests_parallel.ps1 gate (never
        // under a single-class or single-area slice): several PlayMode
        // fixtures elsewhere in this project build their OWN throwaway
        // Canvas by hand (NavigationDispatcherTests' own SetUp, for one) and
        // free it with a plain `Object.Destroy`, which Unity defers to the
        // END of the frame rather than performing immediately -- a bare
        // `FindObjectsByType<Canvas>` run early in the very next test can
        // still see that pending-destroy instance for one frame, and
        // `.isRootCanvas` is true for a bare constructed Canvas exactly the
        // same as for a scene's real one, so `FirstOrDefault` had no way to
        // tell them apart. Scoping to `SceneManager.GetActiveScene()` does.
        private static Canvas RootCanvas()
        {
            var active = SceneManager.GetActiveScene();
            return UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.gameObject.scene == active);
        }

        // Screen-space position over `node`'s own world rect centre, through
        // the SAME conversion PartyGamepadVisualCaptureTests' own HoverCard
        // already uses against a real production canvas: every scene this
        // project builds is ScreenSpaceCamera (SceneBuilder.cs), never
        // Overlay, so a RectTransform's world position has to be projected
        // through the canvas's own worldCamera to land where
        // GraphicRaycaster's ray would actually hit it -- a bare
        // `(Vector2)rect.position` is only correct for Overlay, which is
        // NavigationDispatcherTests' own synthetic fixture canvas, not any
        // real scene this suite drives.
        protected IEnumerator MoveMouseTo(GameObject node)
        {
            Assert.IsNotNull(node, "MoveMouseTo given a null node -- the target was never found in the scene");
            yield return MoveMouseToWorldPoint(((RectTransform)node.transform).position);
        }

        // TWO FRAMES, not one -- PartyGamepadVisualCaptureTests' own
        // HoverCard is the precedent this file found the hard way it had
        // dropped: "one for the module to raycast the new position, one for
        // whatever the hover changed to have been laid out and drawn." A
        // single frame is enough for a CLICK (Click's own Down/Up pair
        // already spans two calls), but a hover that drives a downstream
        // repaint -- RewardTrackController's own ScrollTo-on-hover,
        // section 7's "the SAME call a mouse hover already triggers" -- can
        // still be mid-layout on the very frame OnPointerEnter fires.
        protected IEnumerator SettleMouseAt(GameObject node)
        {
            yield return MoveMouseTo(node);
            yield return DriveFrame();
        }

        // The general form MoveMouseTo(node) is built on -- an explicit world
        // point rather than a node's own rect centre, for the one caller that
        // needs a PRECISE fraction along a track (BarSlider's own click-sets-
        // value contract, item 4 rule (e)/segment 3's slider adjustment) rather
        // than whatever a whole node's centre happens to land on.
        protected IEnumerator MoveMouseToWorldPoint(Vector3 worldPoint)
        {
            var canvas = RootCanvas();
            Assert.IsNotNull(canvas, "the scene has no root Canvas to project the pointer through");
            Input.MousePosition = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPoint);
            yield return DriveFrame();
        }

        // A world point at horizontal fraction `t` (0 = left edge, 1 = right
        // edge) along `rect`'s own width, at its vertical centre -- BarSlider.
        // Set reads the click's LOCAL x back out through this exact same
        // ScreenPointToLocalPointInRectangle/rect.width arithmetic, so this is
        // its own inverse, not a separate approximation of it.
        protected static Vector3 WorldPointAtFraction(RectTransform rect, float t)
        {
            var local = new Vector3(rect.rect.xMin + Mathf.Clamp01(t) * rect.rect.width, 0f, 0f);
            return rect.TransformPoint(local);
        }

        // One frame down, one frame up -- real mouse timing, not both edges
        // in the same Process() call (unlike NavigationDispatcherTests' own
        // synthetic-fixture shortcut): a Button's OnPointerClick only fires
        // once GetMouseButtonUp lands on the same target GetMouseButtonDown
        // pressed, and nothing in this suite needs the click to resolve
        // before the up-frame anyway.
        protected IEnumerator Click(GameObject node)
        {
            yield return MoveMouseTo(node);
            Input.MouseButton0Down = true;
            yield return DriveFrame();
            Input.MouseButton0Up = true;
            yield return DriveFrame();
        }

        // Click at an explicit world point rather than a node's own centre --
        // BarSlider's own click-sets-value contract (see WorldPointAtFraction).
        protected IEnumerator ClickWorldPoint(Vector3 worldPoint)
        {
            yield return MoveMouseToWorldPoint(worldPoint);
            Input.MouseButton0Down = true;
            yield return DriveFrame();
            Input.MouseButton0Up = true;
            yield return DriveFrame();
        }

        // A background click -- nothing this project draws puts a raycast
        // target this close to the corner (every decorative full-screen
        // Image is AsDecor(), raycastTarget false; docs/GAMEPAD_NAVIGATION_
        // PLAN.md section 2's own modal-dimmer note is the one exception,
        // and no modal is ever placed here). Used by item 4's rule (a).
        protected IEnumerator ClickBackground()
        {
            Input.MousePosition = new Vector2(5f, 5f);
            Input.MouseButton0Down = true;
            yield return DriveFrame();
            Input.MouseButton0Up = true;
            yield return DriveFrame();
        }

        // Polls rather than counting frames -- several transitions this
        // journey crosses (HubController's descent zoom/fade, a Map walk's
        // eased pan) run off real time even with their own SpeedMultiplier
        // cranked (RelicDraftGamepadNavigationTests' own comment: stepping
        // between coroutine phases still costs a real engine frame apiece).
        // The deadline is a hard, named failure -- never a silent timeout
        // that leaves the next line to fail with no context.
        protected static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string because)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), because);
        }

        // A REAL scene transition (Navigation.Go's own unstubbed
        // SceneManager.LoadScene) rather than a fixed frame count. Never
        // touches the outgoing scene's own controller reference to decide
        // when to stop polling -- a MonoBehaviour on the scene that just
        // unloaded reads as Unity's fake-null on ==, but a MEMBER CALL on it
        // throws MissingReferenceException, which is exactly the trap a
        // "while (_map.IsWalking)" loop falls into once the walk's own
        // Arrive() has already swapped the active scene out from under it.
        protected static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string because)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, because);
        }

        // ---- assertion helpers -------------------------------------------

        protected static GameObject Node(string name) => GameObject.Find(name);

        protected static void AssertSelectedName(string name, string because)
        {
            var current = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(current, because + " (nothing was selected at all)");
            Assert.AreEqual(name, current.name, because);
        }

        // The Fight branch's own invariant (docs/GAMEPAD_NAVIGATION_PLAN.md
        // section 3/6): selection is null BECAUSE the dispatcher asserts it
        // every frame while Fight is top, not an absence to recover from.
        protected static void AssertFightIsTopWithNoSelection(string because)
        {
            var top = NavigationInputModule.Contexts?.Top;
            Assert.IsNotNull(top, because + " (the context stack is empty)");
            Assert.IsTrue(top.IsNonSelecting, because + " (the top of the stack is not Fight's own non-selecting context)");
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                because + " (Fight is top, so EventSystem selection must be null every frame)");
        }

        protected static void AssertTopIsNotFight(string because)
        {
            var top = NavigationInputModule.Contexts?.Top;
            Assert.IsTrue(top == null || !top.IsNonSelecting, because);
        }
    }
}
