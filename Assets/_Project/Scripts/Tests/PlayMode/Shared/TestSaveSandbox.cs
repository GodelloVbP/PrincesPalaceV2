using System.IO;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // NO PLAYMODE TEST STARTS POINTED AT THE RUNNER'S REAL SAVE FOLDER.
    //
    // SaveSystem.RootOverride == null means Application.persistentDataPath,
    // which for the gate is the TestRunner2 copy's own folder under LocalLow.
    // It outlives the process, so a save written there leaks into the NEXT
    // run as well as the next test, and nothing in TestGlobals.ResetAll puts
    // it back: ResetAll nulls the override, which re-aims later tests at that
    // folder instead of clearing it.
    //
    // THE INCIDENT (2026-09-24). SceneLoadBenchmarkTests' reset loop called
    // ResetAll and then RunManager.StartRun before putting its own root back,
    // so the run it started was saved to the runner's real save_slot_0.json.
    // Every later bootstrap fight in that runner, in any run, loaded it:
    // FightBootstrap saw RunManager.HasRun and built the run's three-member
    // party instead of its one-member placeholder, so Odette swung where
    // FightPlayableTests watched Shawn, and StageFormationTests' ogre never
    // swung. Both failed alone, at HEAD too, because the cause was a file on
    // disk and not anything in either test.
    //
    // WHAT THIS DOES, from PlayModeTestProfiler.TestStarted:
    //   - before every TEST, a null override becomes a freshly emptied
    //     sandbox. A fixture that sets its own root in [SetUp] replaces it,
    //     as it always did; one that sets none (DossierXpBarTests, the
    //     FightBootstrap fixtures) now reads and writes an empty folder,
    //     which is what the real one was on a clean machine;
    //   - before every FIXTURE, a sandbox the last fixture left standing is
    //     emptied too, so the sandbox cannot become the next cross-fixture
    //     channel. Within one fixture it persists between tests, as the
    //     real folder did, because a shared scene's tests may lean on that.
    //
    // AND THE CACHE GOES WITH THE FOLDER. SaveSlotManager caches the loaded
    // save per SLOT, not per root, and PlaytimeTracker (booted
    // BeforeSceneLoad, DontDestroyOnLoad) reads CurrentSave every frame --
    // so the real folder's save is cached before the first test starts, and
    // re-aiming the root alone changes nothing a reader sees. Forget() runs
    // whenever this changes or empties the folder the cache was read from.
    //
    // WHAT IT DOES NOT COVER: code that nulls the override partway through a
    // test and then saves before anything re-aims it -- a teardown's
    // `RootOverride = null` is harmless (nothing saves after it before the
    // next TestStarted), a mid-test one is not. That is the benchmark's bug,
    // fixed at its own call site.
    //
    // RUNS ONLY BECAUSE PlayModeTestProfiler CALLS IT, like
    // UnityEventRegistryPrune and for the same reason: ResetAll runs only for
    // fixtures that call it; the profiler sees every test.
    internal static class TestSaveSandbox
    {
        private const string FolderName = "pp-playmode-save-sandbox";

        private static string s_root;

        // Under temporaryCachePath, never persistentDataPath: the one folder
        // this ever deletes must not be able to be the real one.
        public static string Root =>
            s_root ??= Path.Combine(Application.temporaryCachePath, FolderName);

        // Before the test's own [SetUp]. Only a NULL root is re-aimed; a
        // root some fixture chose is left alone, and so is this sandbox
        // between two tests of one fixture.
        public static void BeforeTest()
        {
            if (SaveSystem.RootOverride == null) Empty();
        }

        // Before the fixture's [OneTimeSetUp]. Null or this sandbox; never
        // a root a fixture chose.
        public static void BeforeFixture()
        {
            var current = SaveSystem.RootOverride;
            if (current == null || current == Root) Empty();
        }

        private static void Empty()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
            Directory.CreateDirectory(Root);
            SaveSystem.RootOverride = Root;
            SaveSlotManager.Forget();
        }
    }
}
