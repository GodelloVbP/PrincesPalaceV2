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

            // Lifts the stock uGUI rate limit on DIRECTION changes and
            // re-presses so that chained Moves land without any real-time
            // wait (see Move's own comment for the measured gate). Set here
            // because the module is fresh on every scene load, and this is
            // already called after every load. Nothing a journey asserts is
            // a cadence: NavigationInputModule's own armed edge (one press,
            // one Move) still applies unchanged, and
            // StickThresholdGamepadNavigationTests pins the production
            // timing on an untouched module.
            module.inputActionsPerSecond = UnthrottledActionsPerSecond;
            return module;
        }

        private const float UnthrottledActionsPerSecond = 1e6f;

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

        // START -- the button that opens and closes the overarching menu
        // since the owner's 2026-09-19 call. Distinct from PressCancel,
        // which now steps back a level and nothing else.
        protected IEnumerator PressSystemMenu()
        {
            Input.SystemMenuDown = true;
            yield return DriveFrame();
        }

        // RB/LB -- pages SECTIONS/CHARACTERS (dossier paging, StepCharacter
        // on the talent screen) since the owner's 2026-09-19 hardware-round
        // call moved TAB-stepping onto the triggers below. Keep this name
        // (rather than PressShoulderNext/Prev) only because callers that
        // actually want to page a section, not a tab, still read clearly
        // with it -- a caller that means "step the TAB" must use
        // PressTriggerRight/Left instead.
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

        // LT/RT -- steps TABS (system-menu tab strip; talent constellations/
        // paths) since the owner's 2026-09-19 hardware-round call. A level,
        // not an edge (ScriptedBaseInput's own header): armed-by-default
        // (NavigationInputModule's _triggerLeftArmed/_triggerRightArmed both
        // start true) means a single frame at 1f registers the press even
        // with no frame at 0f first, but the frame that drops it back to 0f
        // has to be DRIVEN too, not merely set, or TriggerPressed never
        // re-reads the axis to re-arm -- a second call landing on top of an
        // unread 0 would find `armed` still false from the first pull and
        // silently do nothing (caught by
        // FocusMemoryGamepadNavigationTests' two-pulls-to-the-third-tab
        // claim).
        protected IEnumerator PressTriggerRight()
        {
            Input.TriggerRight = 1f;
            yield return DriveFrame();
            Input.TriggerRight = 0f;
            yield return DriveFrame();
        }

        protected IEnumerator PressTriggerLeft()
        {
            Input.TriggerLeft = 1f;
            yield return DriveFrame();
            Input.TriggerLeft = 0f;
            yield return DriveFrame();
        }

        // Horizontal/Vertical are LEVELS (ScriptedBaseInput's own header),
        // so this sets both, drives the one frame the dispatcher reads them
        // on, then drives one frame at rest before returning.
        //
        // THE REST FRAME IS LOAD-BEARING; NO REAL-TIME WAIT IS. It re-arms
        // NavigationInputModule's own edge (UpdateMoveGate only re-arms on a
        // frame it reads below MoveThreshold) and it resets uGUI's
        // m_ConsecutiveMoveCount, so the next Move never takes the 0.5s
        // repeatDelay branch of StandaloneInputModule.
        // SendMoveEventToSelectedObject -- that branch is only for a stick
        // HELD past its first move. What does gate a re-press is the other
        // branch, `time <= m_PrevActionTime + 1f / inputActionsPerSecond`
        // (0.1s at the stock 10, Time.unscaledTime, no BaseInput seam).
        // Measured 2026-09-24 on the Main Menu: two chained Moves one rest
        // frame apart (~2ms) -- the second is dropped at the stock rate
        // (down,down and down,up alike), lands at 1000/s, and at stock
        // first lands exactly 0.100s after the first. TakeOverInput lifts
        // that rate, so this helper waits no wall clock at all. Fight's own
        // branch never reads this gate (ProcessFight's MoveFocus path skips
        // base.Process()'s move dispatch).
        protected IEnumerator Move(float horizontal, float vertical)
        {
            Input.Horizontal = horizontal;
            Input.Vertical = vertical;
            yield return DriveFrame();
            Input.Horizontal = 0f;
            Input.Vertical = 0f;
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
            yield return MoveMouseToWorldPoint(WorldCentreOf((RectTransform)node.transform));
        }

        // THE RECT'S CENTRE, NOT ITS TRANSFORM POSITION -- and the difference
        // is AUDIT.md #162's whole root cause, so this is not a tidy-up.
        //
        // `RectTransform.position` is the PIVOT, which is only the centre when
        // the pivot happens to be (0.5, 0.5). The Hub's own gate is pivoted
        // (0.5, 0) -- rect (x:-310, y:0, w:620, h:620), pivot flat on the
        // bottom edge, because the building is placed by the ground it stands
        // on -- so aiming at `position` put the pointer exactly on that rect's
        // yMin boundary, where `Rect.Contains` answers true only because yMin
        // is the inclusive edge. localPt read back (0.000, 0.000): a knife
        // edge, not a click.
        //
        // What tipped it over was the pointer's own arrival. `MoveMouseTo`'s
        // frame fires OnPointerEnter, ButtonPressAnimator starts lerping the
        // button up to HoverScale, and the sub-pixel shift that puts in the
        // pivot's screen position (measured: 78.320 -> 78.323 at a 0.333
        // canvas scale) moves the frozen pointer to localPt (0.009, -0.009) --
        // BELOW yMin -- on the very next frame, which is the frame that
        // carries MouseButton0Down. GraphicRaycaster then finds nothing at
        // all, the press lands on no target, and the release has no
        // pointerPress to match, so the Button's own onClick never fires and
        // the test sees a screen that simply did not react.
        //
        // Batch size decided which way the coin fell because the size of that
        // first hover step is `Time.deltaTime`-driven: a loaded run's longer
        // frame steps the lerp far enough to move the pivot a measurable
        // fraction of a pixel, while the same test alone steps it too little
        // to leave the edge. Hence "fails in every big batch, passes alone" --
        // never a race this suite could have waited out, and never anything
        // the production dispatcher did wrong.
        //
        // The centre is what the fixture's own contract already claimed to aim
        // at ("over `node`'s own world rect centre"), and it is what a mouse
        // player aims at. It is also robust by construction: the centre of a
        // rect is interior to it for any pivot, so no control's pivot
        // convention can put this pointer on a boundary again.
        private static Vector3 WorldCentreOf(RectTransform rect) => rect.TransformPoint(rect.rect.center);

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
            // y is rect.center.y, not 0, for the same reason MoveMouseTo aims
            // at the rect centre: local y == 0 is the PIVOT's row, which is
            // the rect's own edge on anything not pivoted at 0.5. Identical
            // for a vertically-centred pivot (rect.center.y is then 0), and
            // correct rather than lucky for anything else.
            var local = new Vector3(rect.rect.xMin + Mathf.Clamp01(t) * rect.rect.width, rect.rect.center.y, 0f);
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
