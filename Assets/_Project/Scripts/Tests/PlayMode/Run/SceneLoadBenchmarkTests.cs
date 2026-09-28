using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // WHAT ONE SCENE LOAD COSTS, per game scene, measured the way fixtures
    // pay for it: LoadSceneAsync in Single mode, then the two settle frames.
    //
    // A benchmark, not a check: it asserts nothing about the numbers. It runs
    // only when PP_BENCHMARK is set; otherwise SetUp ignores every case.
    // [Explicit] alone is not enough: tools/test.ps1 filters with
    // ".*\.(Class)\..*", which matches each method's full name, and NUnit runs
    // an explicit node the filter matched. Area expansion
    // (tools/test_areas.ps1's Get-AreaClassNames / $AreaExpansionExcludedClasses)
    // leaves this class out of an AREA run's sweep, and -Changed inherits the
    // same exclusion since it resolves through the same helper. [Explicit]
    // still stays so the gate (no filter) does not even list them, and naming
    // this class explicitly still reaches it -- see the PP_BENCHMARK line
    // below.
    //
    //   $env:PP_BENCHMARK='1'; tools/test.ps1 SceneLoadBenchmarkTests
    //
    // PP_BENCHMARK=1 runs every case; any other value runs only the methods
    // whose name contains it (e.g. 'EveryGameScene'), since test.ps1 selects
    // by class and cannot name one method.
    //
    // Output: the log line "SCENE-BENCH" per scene, and
    // scene-load-benchmark[-high].{csv,txt} in the project root it ran in
    // (the PlayMode runner copy under test.ps1). The methods below it add
    // scene-load-async-vs-sync.*, scene-load-activation.txt and
    // scene-load-trend-Fight[-noreset|-reset-prune].*. Unity's own per-load breakdown
    // (Deserialize / Integration / UnloadTime / UnloadUnusedAssets) is in
    // that copy's test-run-PlayMode.log; profiler markers record nothing
    // under -batchmode -nographics, even with Profiler.enabled.
    //
    // SameScene100x_Trend EMPTIES UnityEventBase.s_UnityEvents by reflection
    // at the end of its reset variant (the causality probe). Anything run
    // after it in the same process sees a cleared registry.
    public class SceneLoadBenchmarkTests
    {
        // The build-settings list (ProjectSettings/EditorBuildSettings.asset).
        private static readonly string[] Scenes = { "MainMenu", "Hub", "Fight", "Map", "Talents" };
        private const int Repeats = 10;

        private string _root;
        private ThreadPriority _priority;

        private const string BenchmarkVariable = "PP_BENCHMARK";

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            // Null until the gate passes, so Restore touches nothing global for
            // an ignored case (NUnit still runs TearDown after SetUp throws).
            _root = null;
            string wanted = System.Environment.GetEnvironmentVariable(BenchmarkVariable);
            if (string.IsNullOrEmpty(wanted))
                Assert.Ignore("benchmark; set " + BenchmarkVariable + "=1 to run it");
            if (wanted != "1" && !TestContext.CurrentContext.Test.MethodName.Contains(wanted))
                Assert.Ignore("benchmark; " + BenchmarkVariable + "='" + wanted + "' selects other methods");

            _root = Path.Combine(Path.GetTempPath(), "pp-scene-bench-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            // A scene whose Start navigates elsewhere must not leave the scene
            // under measurement; the request is swallowed.
            Navigation.LoadOverride = _ => { };
            // Scenes loaded outside their normal route may log errors; those
            // are not what is being measured.
            LogAssert.ignoreFailingMessages = true;
            _priority = Application.backgroundLoadingPriority;
        }

        [TearDown]
        public void Restore()
        {
            if (_root == null) return;
            LogAssert.ignoreFailingMessages = false;
            Application.backgroundLoadingPriority = _priority;
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Once at the priority the gate runs with, once at High: the first
        // profile showed a load spread over ~360 idle ~1 ms frames, which is
        // the shape of a load throttled by its per-frame integration budget.
        [UnityTest, Explicit("benchmark; needs PP_BENCHMARK, see the header")]
        public IEnumerator EveryGameScene_LoadPlusTwoSettleFrames(
            [Values(false, true)] bool highLoadingPriority)
        {
            RunManager.StartRun(4242);
            if (highLoadingPriority) Application.backgroundLoadingPriority = ThreadPriority.High;
            string tag = "priority=" + Application.backgroundLoadingPriority;

            var csv = new StringBuilder("scene,iteration,load_s,settle1_s,settle2_s,total_s,load_frames\n");
            var summary = new StringBuilder();

            // One throwaway round first so the first scene measured is not also
            // paying for first-use shader/asset import in this process.
            foreach (var scene in Scenes)
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;
            }

            // Also measured: an idle frame in whatever scene is up, to price
            // the frames tests spend waiting.
            var idle = new List<double>();

            foreach (var scene in Scenes)
            {
                var loads = new List<double>();
                var totals = new List<double>();
                for (int i = 0; i < Repeats; i++)
                {
                    var sw = Stopwatch.StartNew();
                    int f0 = Time.frameCount;
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    double load = sw.Elapsed.TotalSeconds;
                    int loadFrames = Time.frameCount - f0;
                    yield return null;
                    double s1 = sw.Elapsed.TotalSeconds - load;
                    yield return null;
                    double total = sw.Elapsed.TotalSeconds;
                    double s2 = total - load - s1;
                    loads.Add(load);
                    totals.Add(total);
                    csv.Append(scene).Append(',').Append(i).Append(',')
                       .Append(F(load)).Append(',').Append(F(s1)).Append(',').Append(F(s2)).Append(',')
                       .Append(F(total)).Append(',').Append(loadFrames).Append('\n');
                }

                for (int k = 0; k < 20; k++)
                {
                    var sw = Stopwatch.StartNew();
                    yield return null;
                    idle.Add(sw.Elapsed.TotalSeconds);
                }

                var line = $"SCENE-BENCH [{tag}] {scene}: median load {F(Median(loads))}s, median load+2 settle {F(Median(totals))}s, " +
                           $"min {F(totals.Min())}s, max {F(totals.Max())}s over {Repeats}";
                summary.AppendLine(line);
                UnityEngine.Debug.Log(line);
            }

            var idleLine = $"SCENE-BENCH [{tag}] idle frame median {F(Median(idle) * 1000)}ms over {idle.Count}";
            summary.AppendLine(idleLine);
            UnityEngine.Debug.Log(idleLine);

            var dir = Directory.GetParent(Application.dataPath).FullName;
            string suffix = highLoadingPriority ? "-high" : "";
            File.WriteAllText(Path.Combine(dir, "scene-load-benchmark" + suffix + ".csv"), csv.ToString());
            File.WriteAllText(Path.Combine(dir, "scene-load-benchmark" + suffix + ".txt"), summary.ToString());
        }

        // ---------------------------------------------------------------
        // ASYNC VS SYNC, and where the async wait goes.
        //
        // Per scene, Repeats loads each way, interleaved (async, sync, async,
        // ...) so the run-long slowdown lands on both columns equally.
        //   async: LoadSceneAsync(Single), polled every frame for progress.
        //   sync:  LoadScene(Single), then yield until sceneLoaded fired for
        //          the new handle AND it is the active scene.
        // Both then take the two settle frames fixtures take.
        //
        // Start ordering is checked with a probe MonoBehaviour added to the
        // new scene from the sceneLoaded callback: its Start runs in the same
        // pending-Start pass as the scene's own components (both were created
        // before that pass), so "probe Start frame vs the frame the test
        // resumes" is the evidence for when scene Start() has run.
        //
        // Marker sums: every TimeNanoseconds profiler marker whose name looks
        // loading/awake/wait related, recorded from ALL threads over the load
        // window. Writes scene-load-async-vs-sync.{csv,txt}.
        [UnityTest, Explicit("benchmark; needs PP_BENCHMARK, see the header")]
        public IEnumerator AsyncVsSync_PerScene()
        {
            RunManager.StartRun(4242);
            foreach (var scene in Scenes)
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;
            }

            using var markers = MarkerSet.Start();
            var csv = new StringBuilder("scene,mode,iteration,to_loaded_cb_s,to_resume_s,total_s,frames_to_resume,frames_below_09,frames_at_09,t_reach_09_s," +
                                        "busy_frames_gt5ms,busy_ms,idle_frames,idle_ms,loaded_cb_frame_rel,probe_start_frame_rel,probe_start_before_resume\n");
            var summary = new StringBuilder();
            var markerCsv = new StringBuilder("scene,mode,marker,median_ms\n");

            foreach (var scene in Scenes)
            {
                var rows = new Dictionary<string, List<LoadRow>> { ["async"] = new List<LoadRow>(), ["sync"] = new List<LoadRow>() };
                var markerRuns = new Dictionary<string, List<Dictionary<string, double>>>
                    { ["async"] = new List<Dictionary<string, double>>(), ["sync"] = new List<Dictionary<string, double>>() };
                for (int i = 0; i < Repeats; i++)
                {
                    foreach (var mode in new[] { "async", "sync" })
                    {
                        var row = new LoadRow();
                        markers.Reset();
                        yield return MeasureOne(scene, mode == "async", row);
                        markerRuns[mode].Add(markers.Sums());
                        rows[mode].Add(row);
                        csv.Append(scene).Append(',').Append(mode).Append(',').Append(i).Append(',')
                           .Append(F(row.ToLoadedCb)).Append(',').Append(F(row.ToResume)).Append(',').Append(F(row.Total)).Append(',')
                           .Append(row.FramesToResume).Append(',').Append(row.FramesBelow09).Append(',').Append(row.FramesAt09).Append(',')
                           .Append(F(row.TReach09)).Append(',').Append(row.BusyFrames).Append(',').Append(F(row.BusyMs)).Append(',')
                           .Append(row.IdleFrames).Append(',').Append(F(row.IdleMs)).Append(',')
                           .Append(row.LoadedCbFrameRel).Append(',').Append(row.ProbeStartFrameRel).Append(',')
                           .Append(row.ProbeStartedBeforeResume ? 1 : 0).Append('\n');
                    }
                }

                foreach (var mode in new[] { "async", "sync" })
                {
                    var r = rows[mode];
                    var line = $"SCENE-BENCH2 {scene} {mode}: median to-loaded-cb {F(Median(r.Select(x => x.ToLoadedCb).ToList()))}s, " +
                               $"to-resume {F(Median(r.Select(x => x.ToResume).ToList()))}s, total+2settle {F(Median(r.Select(x => x.Total).ToList()))}s, " +
                               $"frames {Median(r.Select(x => (double)x.FramesToResume).ToList())} (below0.9 {Median(r.Select(x => (double)x.FramesBelow09).ToList())}, at0.9 {Median(r.Select(x => (double)x.FramesAt09).ToList())}), " +
                               $"t0.9 {F(Median(r.Select(x => x.TReach09).ToList()))}s, busy {Median(r.Select(x => (double)x.BusyFrames).ToList())}f/{F(Median(r.Select(x => x.BusyMs).ToList()))}ms, " +
                               $"idle {Median(r.Select(x => (double)x.IdleFrames).ToList())}f/{F(Median(r.Select(x => x.IdleMs).ToList()))}ms, " +
                               $"loadedCbFrameRel {Median(r.Select(x => (double)x.LoadedCbFrameRel).ToList())}, probeStartFrameRel {Median(r.Select(x => (double)x.ProbeStartFrameRel).ToList())}, " +
                               $"probeStartedBeforeResume {r.Count(x => x.ProbeStartedBeforeResume)}/{r.Count}";
                    summary.AppendLine(line);
                    UnityEngine.Debug.Log(line);

                    var names = markerRuns[mode].SelectMany(d => d.Keys).Distinct();
                    var med = names.Select(n => (n, Median(markerRuns[mode].Select(d => d.TryGetValue(n, out var v) ? v : 0).ToList())))
                                   .Where(t => t.Item2 > 0.05).OrderByDescending(t => t.Item2).ToList();
                    foreach (var (n, v) in med) markerCsv.Append(scene).Append(',').Append(mode).Append(",\"").Append(n).Append("\",").Append(F(v)).Append('\n');
                    var top = string.Join("; ", med.Take(25).Select(t => $"{t.n}={F(t.Item2)}ms"));
                    summary.AppendLine($"  markers[{scene} {mode}] {top}");
                }
            }
            summary.AppendLine("markers recorded: " + markers.Count + (markers.Count == 0 ? " (none available)" : ""));

            var dir = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(Path.Combine(dir, "scene-load-async-vs-sync.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(dir, "scene-load-async-vs-sync-markers.csv"), markerCsv.ToString());
            File.WriteAllText(Path.Combine(dir, "scene-load-async-vs-sync.txt"), summary.ToString());
        }

        private sealed class LoadRow
        {
            public double ToLoadedCb, ToResume, Total, TReach09 = -1, BusyMs, IdleMs;
            public int FramesToResume, FramesBelow09, FramesAt09, BusyFrames, IdleFrames, LoadedCbFrameRel, ProbeStartFrameRel;
            public bool ProbeStartedBeforeResume;
        }

        private static IEnumerator MeasureOne(string scene, bool async, LoadRow row)
        {
            var sw = Stopwatch.StartNew();
            int f0 = Time.frameCount;
            bool loadedSeen = false;
            Scene loadedScene = default;
            double loadedT = -1;
            int loadedFrame = -1;
            LoadProbe.Reset();
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (s, m) =>
            {
                if (s.name != scene || loadedSeen) return;
                loadedSeen = true;
                loadedScene = s;
                loadedT = sw.Elapsed.TotalSeconds;
                loadedFrame = Time.frameCount;
                var go = new GameObject("__LoadProbe");
                SceneManager.MoveGameObjectToScene(go, s);
                go.AddComponent<LoadProbe>();
            };
            SceneManager.sceneLoaded += onLoaded;
            double last = 0;
            void Tick()
            {
                double now = sw.Elapsed.TotalSeconds;
                double dt = (now - last) * 1000;
                last = now;
                if (dt > 5) { row.BusyFrames++; row.BusyMs += dt; }
                else { row.IdleFrames++; row.IdleMs += dt; }
            }
            try
            {
                if (async)
                {
                    var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    while (!op.isDone)
                    {
                        if (op.progress < 0.9f) row.FramesBelow09++;
                        else
                        {
                            row.FramesAt09++;
                            if (row.TReach09 < 0) row.TReach09 = sw.Elapsed.TotalSeconds;
                        }
                        yield return null;
                        Tick();
                    }
                }
                else
                {
                    SceneManager.LoadScene(scene, LoadSceneMode.Single);
                    while (!loadedSeen || SceneManager.GetActiveScene() != loadedScene)
                    {
                        yield return null;
                        Tick();
                    }
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= onLoaded;
            }
            row.ToResume = sw.Elapsed.TotalSeconds;
            row.ToLoadedCb = loadedT;
            int resumeFrame = Time.frameCount;
            row.FramesToResume = resumeFrame - f0;
            row.LoadedCbFrameRel = loadedFrame - resumeFrame;
            row.ProbeStartedBeforeResume = LoadProbe.StartFrame != -1;
            yield return null;
            yield return null;
            row.Total = sw.Elapsed.TotalSeconds;
            row.ProbeStartFrameRel = LoadProbe.StartFrame == -1 ? 99 : LoadProbe.StartFrame - resumeFrame;
        }

        private sealed class LoadProbe : MonoBehaviour
        {
            public static int StartFrame = -1;
            public static void Reset() => StartFrame = -1;
            private void Start() => StartFrame = Time.frameCount;
        }

        // Every time-unit profiler marker whose name suggests loading, object
        // activation, script callbacks, GC or waiting. All threads.
        private sealed class MarkerSet : System.IDisposable
        {
            private static readonly string[] Keys =
            {
                "Load", "Integrate", "Awake", "Unload", "GC.", "Activate", "Preload", "Deserial", "Enable",
                "Start", "Sleep", "Wait", "Idle", "Instantiate", "Resources", "Scene", "PlayerLoop", "EditorLoop",
                "Canvas", "Layout", "Text", "Font", "Shader", "Texture", "Sprite", "Persistent", "Garbage",
                "Destroy", "Cleanup", "Behaviour", "Coroutine", "Update", "Script", "Serializ", "Read", "Asset"
            };
            private readonly List<(string name, Unity.Profiling.ProfilerRecorder rec)> _recs = new List<(string, Unity.Profiling.ProfilerRecorder)>();
            public int Count => _recs.Count;

            public static MarkerSet Start()
            {
                var set = new MarkerSet();
                var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var h in handles)
                {
                    var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                    if (d.UnitType != Unity.Profiling.ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                    if (!Keys.Any(k => d.Name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if (set._recs.Count >= 400) break;
                    var r = new Unity.Profiling.ProfilerRecorder(h, 8192);
                    if (!r.IsRunning) r.Start();
                    set._recs.Add((d.Name, r));
                }
                return set;
            }

            public void Reset()
            {
                foreach (var (_, r) in _recs) r.Reset();
            }

            public Dictionary<string, double> Sums()
            {
                var d = new Dictionary<string, double>();
                foreach (var (n, r) in _recs)
                {
                    double ns = 0;
                    int c = r.Count;
                    for (int i = 0; i < c; i++) ns += r.GetSample(i).Value;
                    if (ns > 0) d[n] = (d.TryGetValue(n, out var prev) ? prev : 0) + ns / 1e6;
                }
                return d;
            }

            public void Dispose()
            {
                foreach (var (_, r) in _recs) r.Dispose();
            }
        }

        // ---------------------------------------------------------------
        // THE SLOWDOWN OVER A RUN: the same scene 100x, async + 2 settle
        // frames as fixtures do, with TestGlobals.ResetAll between loads
        // like a fixture TearDown. Per load: time, live UnityEngine.Object
        // count, DontDestroyOnLoad root count, managed heap. Plus a snapshot
        // of every static collection/delegate in the project assemblies and
        // SceneManager's own event fields at load 1 and load 100, and the
        // per-type object count at both, so whatever grows is named.
        // Writes scene-load-trend-<scene>[-<mode>].{csv,txt}.
        //
        // THREE MODES: "noreset" (loads back to back), "reset" (ResetAll
        // between loads, as a fixture TearDown), and "reset+prune" (that plus
        // UnityEventRegistryPrune.Prune, as its per-test callback does in a
        // real run). "reset" against "reset+prune" is the prune's evidence;
        // the unity_events column is the registry's size after each load.
        [UnityTest, Explicit("benchmark; needs PP_BENCHMARK, see the header")]
        public IEnumerator SameScene100x_Trend([Values("Fight")] string scene,
                                               [Values("noreset", "reset", "reset+prune")] string between)
        {
            const int N = 100;
            bool resetBetween = between != "noreset";
            bool pruneBetween = between == "reset+prune";
            string suffix = between == "reset" ? "" : "-" + between.Replace('+', '-');
            RunManager.StartRun(4242);
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var ddolProbe = new GameObject("__DdolProbe");
            Object.DontDestroyOnLoad(ddolProbe);

            var csv = new StringBuilder("iteration,load_s,total_s,objects,ddol_roots,gc_total_bytes,mono_used_bytes,unity_alloc_bytes,retained_after_full_gc_bytes,full_gc_s,unity_events\n");
            Dictionary<string, int> staticsFirst = null, typesFirst = null;
            var totals = new List<double>();
            for (int i = 0; i < N; i++)
            {
                var sw = Stopwatch.StartNew();
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                double load = sw.Elapsed.TotalSeconds;
                yield return null;
                yield return null;
                double total = sw.Elapsed.TotalSeconds;
                totals.Add(total);

                var all = Resources.FindObjectsOfTypeAll<Object>();
                int ddolRoots = ddolProbe.scene.GetRootGameObjects().Length - 1;
                csv.Append(i).Append(',').Append(F(load)).Append(',').Append(F(total)).Append(',').Append(all.Length).Append(',')
                   .Append(ddolRoots).Append(',').Append(System.GC.GetTotalMemory(false)).Append(',')
                   .Append(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()).Append(',')
                   .Append(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()).Append(',')
                   .Append(i % 10 == 9 || i == 0 ? System.GC.GetTotalMemory(true) : -1).Append(',')
                   .Append(i % 10 == 9 || i == 0 ? F(TimeFullGc()) : "-1").Append(',')
                   .Append(UnityEventRegistryPrune.Count).Append('\n');

                if (i == 0 || i == N - 1)
                {
                    var types = all.GroupBy(o => o.GetType().FullName).ToDictionary(g => g.Key, g => g.Count());
                    var statics = SnapshotStatics();
                    if (i == 0) { typesFirst = types; staticsFirst = statics; }
                    else
                    {
                        var summary = new StringBuilder();
                        summary.AppendLine($"TREND {scene}: first10 median {F(Median(totals.Take(10).ToList()))}s, last10 median {F(Median(totals.Skip(N - 10).ToList()))}s");
                        summary.AppendLine("object types that grew (first -> last):");
                        foreach (var kv in types.Select(kv => (kv.Key, first: typesFirst.TryGetValue(kv.Key, out var f) ? f : 0, last: kv.Value))
                                                .Where(t => t.last != t.first).OrderByDescending(t => t.last - t.first).Take(40))
                            summary.AppendLine($"  {kv.Key}: {kv.first} -> {kv.last}");
                        summary.AppendLine("statics that changed (first -> last):");
                        foreach (var kv in statics.Select(kv => (kv.Key, first: staticsFirst.TryGetValue(kv.Key, out var f) ? f : 0, last: kv.Value))
                                                  .Where(t => t.last != t.first).OrderByDescending(t => t.last - t.first).Take(60))
                            summary.AppendLine($"  {kv.Key}: {kv.first} -> {kv.last}");
                        summary.AppendLine(DescribeUnityEventRegistry());
                        summary.AppendLine("largest statics at last load:");
                        foreach (var kv in statics.OrderByDescending(kv => kv.Value).Take(25))
                            summary.AppendLine($"  {kv.Key}: {kv.Value}");
                        var dir = Directory.GetParent(Application.dataPath).FullName;
                        File.WriteAllText(Path.Combine(dir, "scene-load-trend-" + scene + suffix + ".txt"), summary.ToString());
                        UnityEngine.Debug.Log(summary.ToString());
                    }
                }
                if (!resetBetween) continue;
                TestGlobals.ResetAll();
                // THE ROOT GOES BACK FIRST. ResetAll nulls it, and StartRun
                // saves: in the other order the run was written to the
                // runner's real save_slot_0.json, where every later bootstrap
                // fight in that runner loaded it (TestSaveSandbox's header).
                SaveSystem.RootOverride = _root;
                Navigation.LoadOverride = _ => { };
                if (pruneBetween) UnityEventRegistryPrune.Prune(out _, out _);
                RunManager.StartRun(4242);
            }
            // CAUSALITY: empty the UnityEvent registry (a probe, not a fix to
            // ship) and measure retained heap, a full GC and 10 more loads.
            if (between == "reset")
            {
                var before = new List<double>();
                for (int i = 0; i < 10; i++)
                {
                    var sw = Stopwatch.StartNew();
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    before.Add(sw.Elapsed.TotalSeconds);
                    yield return null;
                }
                long heapBefore = System.GC.GetTotalMemory(true);
                double gcBefore = TimeFullGc();
                var f = typeof(UnityEngine.Events.UnityEventBase).GetField("s_UnityEvents",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var reg = f?.GetValue(null);
                var clear = reg?.GetType().GetMethod("Clear", System.Type.EmptyTypes);
                string cleared = clear != null ? "cleared" : "NO Clear() on " + (reg?.GetType().FullName ?? "null");
                clear?.Invoke(reg, null);
                long heapAfter = System.GC.GetTotalMemory(true);
                double gcAfter = TimeFullGc();
                var after = new List<double>();
                for (int i = 0; i < 10; i++)
                {
                    var sw = Stopwatch.StartNew();
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    after.Add(sw.Elapsed.TotalSeconds);
                    yield return null;
                }
                var line = $"REGISTRY-PROBE {scene}: {cleared}; retained heap {heapBefore / 1048576} -> {heapAfter / 1048576} MB; " +
                           $"full GC {F(gcBefore)} -> {F(gcAfter)}s; load median {F(Median(before))} -> {F(Median(after))}s";
                UnityEngine.Debug.Log(line);
                var dirp = Directory.GetParent(Application.dataPath).FullName;
                File.AppendAllText(Path.Combine(dirp, "scene-load-trend-" + scene + ".txt"), line + "\n");
            }
            Object.Destroy(ddolProbe);
            var d2 = Directory.GetParent(Application.dataPath).FullName;
            UnityEngine.Debug.Log($"TREND-{between} {scene}: first10 median {F(Median(totals.Take(10).ToList()))}s, " +
                                  $"last10 median {F(Median(totals.Skip(N - 10).ToList()))}s, " +
                                  $"UnityEvent registry now {UnityEventRegistryPrune.Count}");
            File.WriteAllText(Path.Combine(d2, "scene-load-trend-" + scene + suffix + ".csv"), csv.ToString());
        }

        // ---------------------------------------------------------------
        // WHAT THE ACTIVATION FRAME IS. The async trace shows every load is
        // (idle background frames) + ONE busy frame of ~180-230 ms, and that
        // busy frame is ~180 ms even for MainMenu (1.1k objects). A size-
        // independent cost points at the UnloadUnusedAssets + full GC that a
        // Single-mode load runs. Decomposed here, per scene, Repeats each:
        //   single:   LoadSceneAsync(Single) (baseline), with GC counts across it
        //   gc:       GC.Collect() + WaitForPendingFinalizers alone
        //   uua:      Resources.UnloadUnusedAssets() alone
        //   additive: LoadSceneAsync(Additive) + UnloadSceneAsync(old), no UUA
        //   single+heap: baseline again while ~Inflate small managed objects
        //             are held live (does the cost scale with the heap?)
        // Writes scene-load-activation.txt.
        [UnityTest, Explicit("benchmark; needs PP_BENCHMARK, see the header")]
        public IEnumerator ActivationFrame_Decomposed()
        {
            const int Inflate = 4_000_000;
            RunManager.StartRun(4242);
            foreach (var scene in Scenes)
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;
            }

            UnityEngine.Profiling.Profiler.enabled = true;
            using var markers = MarkerSet.Start();
            var summary = new StringBuilder();
            summary.AppendLine($"managed heap (GetTotalMemory(true)) at start: {System.GC.GetTotalMemory(true) / 1048576} MB; " +
                               $"incremental GC {UnityEngine.Scripting.GarbageCollector.isIncremental}; batchmode {Application.isBatchMode}");

            foreach (var scene in new[] { "MainMenu", "Fight" })
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                var single = new List<double>(); var gen0 = new List<double>(); var gen2 = new List<double>();
                var gc = new List<double>(); var uua = new List<double>();
                var addLoad = new List<double>(); var addUnload = new List<double>();
                var singleHeap = new List<double>();
                Dictionary<string, double> singleMarkers = null;
                for (int i = 0; i < Repeats; i++)
                {
                    int c0 = System.GC.CollectionCount(0), c2 = System.GC.CollectionCount(2);
                    markers.Reset();
                    var sw = Stopwatch.StartNew();
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    single.Add(sw.Elapsed.TotalSeconds);
                    gen0.Add(System.GC.CollectionCount(0) - c0);
                    gen2.Add(System.GC.CollectionCount(2) - c2);
                    if (i == Repeats - 1) singleMarkers = markers.Sums();
                    yield return null;

                    sw.Restart();
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();
                    gc.Add(sw.Elapsed.TotalSeconds);
                    yield return null;

                    sw.Restart();
                    yield return Resources.UnloadUnusedAssets();
                    uua.Add(sw.Elapsed.TotalSeconds);
                    yield return null;

                    var old = SceneManager.GetActiveScene();
                    sw.Restart();
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
                    addLoad.Add(sw.Elapsed.TotalSeconds);
                    var fresh = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                    SceneManager.SetActiveScene(fresh);
                    sw.Restart();
                    yield return SceneManager.UnloadSceneAsync(old);
                    addUnload.Add(sw.Elapsed.TotalSeconds);
                    yield return null;
                }

                var ballast = new object[Inflate];
                for (int k = 0; k < Inflate; k++) ballast[k] = new object[1];
                long inflated = System.GC.GetTotalMemory(true) / 1048576;
                for (int i = 0; i < Repeats; i++)
                {
                    var sw = Stopwatch.StartNew();
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    singleHeap.Add(sw.Elapsed.TotalSeconds);
                    yield return null;
                }
                var gcHeap = Stopwatch.StartNew();
                System.GC.Collect();
                double gcHeapS = gcHeap.Elapsed.TotalSeconds;
                System.GC.KeepAlive(ballast);
                ballast = null;
                System.GC.Collect();

                var line = $"ACTIVATION {scene}: single {F(Median(single))}s (gen0 GCs {Median(gen0)}, gen2 GCs {Median(gen2)}), " +
                           $"GC.Collect alone {F(Median(gc))}s, UnloadUnusedAssets alone {F(Median(uua))}s, " +
                           $"additive load {F(Median(addLoad))}s + unload old {F(Median(addUnload))}s = {F(Median(addLoad) + Median(addUnload))}s, " +
                           $"single with +{Inflate / 1000000}M live objects (heap {inflated} MB) {F(Median(singleHeap))}s, GC.Collect then {F(gcHeapS)}s";
                summary.AppendLine(line);
                UnityEngine.Debug.Log(line);
                if (singleMarkers != null)
                    summary.AppendLine("  markers(single, last iter): " + string.Join("; ",
                        singleMarkers.Where(kv => kv.Value > 1).OrderByDescending(kv => kv.Value).Take(30).Select(kv => $"{kv.Key}={F(kv.Value)}ms")));
            }
            UnityEngine.Profiling.Profiler.enabled = false;
            var dir = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(Path.Combine(dir, "scene-load-activation.txt"), summary.ToString());
        }

        // Count of every static ICollection / delegate invocation list in the
        // project assemblies plus UnityEngine's SceneManager / Application /
        // Canvas event fields. Key: Assembly:Type.field.
        private static Dictionary<string, int> SnapshotStatics()
        {
            var result = new Dictionary<string, int>();
            const System.Reflection.BindingFlags B = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            var types = new List<System.Type>();
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var n = asm.GetName().Name;
                if ((n.StartsWith("PrincesPalace") && !n.Contains("Tests")) || n.StartsWith("UnityEngine") || n.StartsWith("Unity.") || n.StartsWith("UnityEditor.UI"))
                {
                    try { types.AddRange(asm.GetTypes()); } catch (System.Reflection.ReflectionTypeLoadException e) { types.AddRange(e.Types.Where(t => t != null)); }
                }
            }
            types.Add(typeof(SceneManager));
            types.Add(typeof(Application));
            types.Add(typeof(Canvas));
            types.Add(typeof(Camera));
            types.Add(typeof(UnityEngine.U2D.SpriteAtlasManager));
            var tmp = System.Type.GetType("TMPro.TMPro_EventManager, Unity.TextMeshPro");
            if (tmp != null) types.Add(tmp);
            foreach (var t in types)
            {
                if (t.ContainsGenericParameters) continue;
                foreach (var f in t.GetFields(B))
                {
                    if (f.IsLiteral) continue;
                    object v;
                    try { v = f.GetValue(null); } catch { continue; }
                    int c;
                    if (v is System.Delegate del) c = del.GetInvocationList().Length;
                    else if (v is System.Collections.ICollection col)
                    {
                        c = col.Count;
                        // One level deeper: a dictionary of lists keeps a
                        // constant key count while its lists grow.
                        int inner = 0;
                        try
                        {
                            foreach (var e in col)
                            {
                                object x = e;
                                var vp = e?.GetType().GetProperty("Value");
                                if (vp != null && e.GetType().IsGenericType && e.GetType().Name.StartsWith("KeyValuePair")) x = vp.GetValue(e);
                                if (x is System.Collections.ICollection ic) inner += ic.Count;
                            }
                        }
                        catch { inner = 0; }
                        if (inner > 0) result[t.Assembly.GetName().Name + ":" + t.FullName + "." + f.Name + "(inner)"] = inner;
                    }
                    else if (v != null && v.GetType().GetProperty("Count") is var p && p != null && p.PropertyType == typeof(int) && !(v is string))
                    {
                        try { c = (int)p.GetValue(v); } catch { continue; }
                    }
                    else continue;
                    result[t.Assembly.GetName().Name + ":" + t.FullName + "." + f.Name] = c;
                }
            }
            return result;
        }

        // What UnityEventBase.s_UnityEvents is: its type, what its elements
        // are, whether they are weak and still alive, and which UnityEventBase
        // members mention it by name shape (register/unregister).
        private static string DescribeUnityEventRegistry()
        {
            var sb = new StringBuilder("UnityEventBase.s_UnityEvents: ");
            try
            {
                var t = typeof(UnityEngine.Events.UnityEventBase);
                const System.Reflection.BindingFlags B = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
                var f = t.GetField("s_UnityEvents", B);
                if (f == null) return sb.Append("field not found").ToString();
                var v = f.GetValue(null);
                sb.Append("declared ").Append(f.FieldType.FullName).Append(", runtime ").Append(v?.GetType().FullName ?? "null");
                if (v is System.Collections.IEnumerable en)
                {
                    int n = 0, weak = 0, alive = 0, destroyedOwner = 0;
                    var elemTypes = new Dictionary<string, int>();
                    foreach (var e in en)
                    {
                        n++;
                        object x = e;
                        if (e is System.WeakReference wr) { weak++; x = wr.Target; if (x != null) alive++; }
                        else if (e != null && e.GetType().IsGenericType && e.GetType().Name.StartsWith("WeakReference"))
                        {
                            weak++;
                            var args = new object[] { null };
                            var ok = (bool)e.GetType().GetMethod("TryGetTarget").Invoke(e, args);
                            x = ok ? args[0] : null;
                            if (ok) alive++;
                        }
                        if (n <= 200000)
                        {
                            var k = x?.GetType().FullName ?? e?.GetType().FullName ?? "null";
                            elemTypes[k] = (elemTypes.TryGetValue(k, out var c) ? c : 0) + 1;
                        }
                    }
                    sb.Append($", count {n}, weak {weak}, alive {alive}; element types (first 200k): ")
                      .Append(string.Join(", ", elemTypes.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}={kv.Value}")));
                }
                sb.Append("; members: ").Append(string.Join(", ", t.GetMembers(B).Select(m => m.Name).Where(m => m.IndexOf("Event", System.StringComparison.OrdinalIgnoreCase) >= 0 || m.StartsWith("s_") || m.Contains("Register") || m.Contains("Finalize")).Distinct()));
            }
            catch (System.Exception ex) { sb.Append("describe failed: ").Append(ex.GetType().Name).Append(' ').Append(ex.Message); }
            return sb.ToString();
        }

        private static double TimeFullGc()
        {
            var sw = Stopwatch.StartNew();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            return sw.Elapsed.TotalSeconds;
        }

        private static double Median(List<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
        }

        private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
