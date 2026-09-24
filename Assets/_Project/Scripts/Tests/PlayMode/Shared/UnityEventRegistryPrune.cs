using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace PrincesPalace.PlayModeTests
{
    // UNITY'S OWN LEAK, TRIMMED BETWEEN TESTS. Test infrastructure only; the
    // player never loads a scene often enough for this to matter.
    //
    // UnityEventBase.s_UnityEvents is a List<WeakReference<UnityEventBase>>
    // that the engine appends to for every UnityEvent it constructs (every uGUI
    // Graphic's m_OnCullStateChanged, ~3k per Fight load) and never prunes. The
    // targets die with their scene; the WeakReference wrappers do not. A full
    // PlayMode run grew it past 900k entries, and every Single-mode load runs a
    // full GC that has to walk all of them: emptying it cut a Fight load from
    // 0.499 to 0.394s (SceneLoadBenchmarkTests.SameScene100x_Trend).
    //
    // REMOVE THE DEAD, NEVER Clear(). A live entry belongs to a UnityEvent in
    // the scene still standing, and the engine may walk the list to reach it.
    // An entry only goes dead after a GC has collected its event, which every
    // Single load provides, so each prune removes the scene before last.
    //
    // RUNS ONLY BECAUSE PlayModeTestProfiler CALLS IT. Delete or replace the
    // profiler and this stops, with no error: the run just gets slower. See
    // the warning at the top of PlayModeTestProfiler.cs.
    //
    // CALLED FROM PlayModeTestProfiler.TestStarted, NOT TestGlobals.ResetAll.
    // ResetAll runs only for fixtures that call it from their teardown; the
    // profiler sees every test in this assembly, whatever its fixture does.
    // It is the profiler because TestRunCallback allows ONE per assembly
    // (AllowMultiple = false; a second one is CS0579), and that one is taken.
    //
    // SKIPPED WHILE A LOAD IS IN FLIGHT (sceneCount != loadedSceneCount), the
    // one moment the engine could be constructing events into the list while
    // this rewrites it.
    //
    // If a Unity upgrade renames the field or changes its type, this logs one
    // warning and does nothing -- the tests still run, only slower.
    public static class UnityEventRegistryPrune
    {
        private static bool s_resolved;
        private static bool s_warned;
        private static FieldInfo s_field;

        // For the one summary line at the end of the run.
        private static int s_prunes;
        private static long s_removed;
        private static int s_maxBefore;
        private static int s_lastAfter;

        // Before every test of this assembly, while playing.
        public static void BeforeTest()
        {
            if (SceneManager.sceneCount != SceneManager.loadedSceneCount) return;
            Prune(out _, out _);
        }

        // The one entry-count line, at the end of the assembly.
        public static void LogSummary()
        {
            if (s_prunes == 0) return;
            Debug.Log($"UNITY-EVENT-PRUNE: {s_prunes} prunes, {s_removed} dead entries removed, " +
                      $"largest list before a prune {s_maxBefore}, after the last prune {s_lastAfter}");
        }

        // Public so SceneLoadBenchmarkTests can prune between the loads of a
        // single test the way BeforeTest prunes between tests. Returns
        // false when the registry could not be reached.
        public static bool Prune(out int before, out int after)
        {
            before = after = 0;
            var list = Registry();
            if (list == null) return false;

            before = list.Count;
            list.RemoveAll(w => w == null || !w.TryGetTarget(out _));
            after = list.Count;

            s_prunes++;
            s_removed += before - after;
            if (before > s_maxBefore) s_maxBefore = before;
            s_lastAfter = after;
            return true;
        }

        public static int Count => Registry()?.Count ?? -1;

        // The field is re-read on every call rather than the list cached, so
        // an engine that ever swaps the instance is followed, not missed.
        private static List<WeakReference<UnityEventBase>> Registry()
        {
            if (!s_resolved)
            {
                s_resolved = true;
                s_field = typeof(UnityEventBase).GetField("s_UnityEvents",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            var list = s_field?.GetValue(null) as List<WeakReference<UnityEventBase>>;
            if (list == null && !s_warned)
            {
                s_warned = true;
                Debug.LogWarning("UnityEventRegistryPrune: UnityEventBase.s_UnityEvents is " +
                                 (s_field == null ? "missing" : "a " + s_field.FieldType.FullName) +
                                 ", not a List<WeakReference<UnityEventBase>>; not pruning. " +
                                 "Scene loads in the PlayMode run will slow as it grows.");
            }
            return list;
        }
    }
}
