using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrincesPalace.PlayModeTests
{
    // ONE SCENE LOAD PER FIXTURE instead of one per test, for fixtures whose
    // tests only read the scene or can put it back cheaply themselves.
    //
    // A Single-mode load is ~0.4-0.6s, most of it the full GC Unity runs on
    // every one (SceneLoadBenchmarkTests), and the PlayMode gate paid 809 of
    // them. A fixture that rebinds a fight or reopens a panel per test does
    // not need a fresh copy of the scene to do it.
    //
    // THE CONTRACT. Ensure(scene) loads only when
    //   - the active scene is not the one THIS SEAM loaded last (a different
    //     scene, or one a test loaded itself, whose state nobody vouches for);
    //   - the running test belongs to a different fixture than the one that
    //     scene was loaded for -- never shared across fixtures;
    //   - the scene was marked dirty: by MarkDirty(reason), or by AfterTest()
    //     seeing the previous test end in anything but Passed; or
    //   - forced reload is on (below).
    // Within ONE test, a second Ensure of the same scene reuses it: a test that
    // "opens the next fight" gets a rebind over the same slots, which is what
    // the game does too.
    //
    // WHAT IT DOES NOT DO: reset anything inside the scene. Putting a reused
    // scene back (closing a panel, stopping a playback) is each fixture's job
    // in its own setup/teardown, because only the fixture knows what it
    // touched. A test that changes the scene in a way its fixture cannot undo
    // calls MarkDirty with a one-line reason, and the next test reloads.
    //
    // FORCED RELOAD, the isolation check: set the environment variable
    //     PP_SHARED_SCENE_RELOAD=1
    // before launching Unity (it is inherited through tools/test.ps1) and
    // every Ensure reloads, which is exactly the behaviour the fixtures had
    // before this seam. A converted fixture must pass both ways; one that only
    // passes shared is passing on another test's leftovers. Off by default,
    // and the gate does not set it. The mode is logged once per run.
    //
    // CALLING IT. From the fixture's scene-opening helper (or [UnitySetUp]):
    //     yield return SharedScene.Ensure("Hub");
    //     yield return SharedScene.EnsureFight();   // pins the battle speed
    // and from its [TearDown]:
    //     SharedScene.AfterTest();
    //
    // HALF OF THIS RUNS FROM PlayModeTestProfiler, NOT FROM HERE. The
    // cross-fixture and failed-final-result dirty rules (AfterTestFinished)
    // are called by the assembly's one TestRunCallback, which is the
    // profiler. Deleting or replacing the profiler silently turns them off --
    // so Ensure refuses to run (CheckCallbackWired) unless that callback has
    // been seen starting this test and finishing the one before it.
    internal static class SharedScene
    {
        public const string ForceReloadVariable = "PP_SHARED_SCENE_RELOAD";

        private static readonly bool s_forceReload =
            System.Environment.GetEnvironmentVariable(ForceReloadVariable) == "1";

        private static bool s_modeLogged;

        // The scene this seam last loaded, compared as a Scene (by handle) --
        // a name match is not enough, because a test that loads "Fight"
        // itself hands the next test a scene nobody here has vouched for.
        private static Scene s_scene;
        private static string s_fixture;
        private static string s_dirtyReason = "never loaded";

        public static bool ForceReload => s_forceReload;

        // THE GUARD on the profiler wiring. Counted by the callback, checked
        // by every Ensure: a shared scene with nobody applying the dirty rules
        // would hand a fixture another fixture's leftovers without a word.
        private static int s_callbackTestsStarted;
        private static bool s_callbackFinishedSeen;

        // From PlayModeTestProfiler.TestStarted, once per test of this assembly.
        public static void CallbackSawTestStart() => s_callbackTestsStarted++;

        private static void CheckCallbackWired()
        {
            string missing = s_callbackTestsStarted == 0
                ? "never reported a test starting"
                : s_callbackTestsStarted > 1 && !s_callbackFinishedSeen
                    ? "never reported a test finishing"
                    : null;
            if (missing == null) return;
            throw new System.InvalidOperationException(
                "SharedScene: the assembly's TestRunCallback (PlayModeTestProfiler) " + missing +
                ". It is what runs SharedScene.AfterTestFinished and UnityEventRegistryPrune; " +
                "without it scenes would be shared across fixtures and after failures. " +
                "Restore the calls in PlayModeTestProfiler.TestStarted/TestFinished.");
        }

        public static IEnumerator Ensure(string scene) => EnsureWith(scene, () => LoadPlain(scene), null);

        // FightSceneFixture's pinning, both halves: the pin-load-settle-repin
        // on a fresh load, and the re-pin alone on reuse, since the previous
        // test's teardown is free to have moved PlayerSpeedSource.
        public static IEnumerator EnsureFight() =>
            EnsureWith("Fight", FightSceneFixture.LoadFight, FightSceneFixture.RepinSpeed);

        public static void MarkDirty(string reason)
        {
            if (s_dirtyReason == null) s_dirtyReason = reason;
        }

        // From the fixture's [TearDown]. NUnit has set the outcome by then; a
        // failure can have left the scene anywhere.
        public static void AfterTest()
        {
            var outcome = TestContext.CurrentContext.Result.Outcome.Status;
            if (outcome != TestStatus.Passed) MarkDirty("previous test ended " + outcome);
        }

        // THE SAME TWO RULES from outside the fixture, called by
        // PlayModeTestProfiler.TestFinished (the assembly's one
        // TestRunCallback): the end of a fixture node marks the scene dirty,
        // so nothing is shared across fixtures even when one forgets
        // AfterTest; and so does a test whose FINAL result is not Passed,
        // which covers any failure Unity attaches after [TearDown] has read
        // the outcome (an unexpected-log failure, say).
        public static void AfterTestFinished(ITestResult result)
        {
            s_callbackFinishedSeen = true;
            var test = result.Test;
            if (test is NUnit.Framework.Internal.TestFixture) MarkDirty("fixture ended");
            else if (!test.IsSuite && result.ResultState.Status != TestStatus.Passed)
                MarkDirty("previous test finished " + result.ResultState.Status);
        }

        private static IEnumerator EnsureWith(string scene, System.Func<IEnumerator> load, System.Action onReuse)
        {
            CheckCallbackWired();
            if (!s_modeLogged)
            {
                s_modeLogged = true;
                Debug.Log("SHARED-SCENE mode: " + (s_forceReload
                    ? "forced reload (" + ForceReloadVariable + "=1), every Ensure loads"
                    : "shared, one load per fixture"));
            }

            string fixture = TestContext.CurrentContext.Test.ClassName;
            var active = SceneManager.GetActiveScene();

            string reason = s_forceReload ? "forced reload"
                : s_dirtyReason ?? (fixture != s_fixture ? "new fixture"
                : active != s_scene || active.name != scene || !active.isLoaded ? "active scene is not the shared one"
                : null);

            if (reason == null)
            {
                onReuse?.Invoke();
                yield break;
            }

            yield return load();
            var loaded = SceneManager.GetActiveScene();
            s_scene = loaded.name == scene ? loaded : default;
            s_fixture = fixture;
            s_dirtyReason = null;
        }

        // Two settle frames: Start() runs one frame after a fresh scene's
        // objects activate (.claude/rules/tests.md).
        private static IEnumerator LoadPlain(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }
    }
}
