using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

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
