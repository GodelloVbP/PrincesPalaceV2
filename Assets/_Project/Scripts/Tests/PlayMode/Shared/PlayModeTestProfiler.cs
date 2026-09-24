using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(PrincesPalace.PlayModeTests.PlayModeTestProfiler))]

namespace PrincesPalace.PlayModeTests
{
    // =====================================================================
    // DO NOT DELETE, RENAME OR REPLACE THIS CLASS WITHOUT MOVING ITS HOOKS.
    // It is NOT only a profiler. It is the PlayMode assembly's one
    // TestRunCallback, and four pieces of test infrastructure run from it:
    //   - TestGlobals.ResetEngineClock, before every test and fixture --
    //     without it a clock another fixture (or a system menu it left
    //     open) stopped freezes every scaled wait in the next one;
    //   - UnityEventRegistryPrune.BeforeTest, before every test -- without
    //     it every Single scene load slows as Unity's event registry grows;
    //   - TestSaveSandbox, before every test and fixture -- without it a
    //     test with no save root of its own reads and writes the runner's
    //     real save_slot_0.json, which outlives the run (silent: the
    //     symptom is a bootstrap fight built from a stale run, elsewhere);
    //   - SharedScene.AfterTestFinished, after every test and fixture --
    //     without it a shared scene outlives its fixture and survives a
    //     failure Unity reports after [TearDown].
    // Losing the prune is silent (only slower). Losing the dirty rules is
    // caught: SharedScene.Ensure throws unless CallbackSawTestStart and
    // AfterTestFinished have been called (SharedScene.CheckCallbackWired).
    // =====================================================================
    //
    // WHERE THE PLAYMODE HALF OF THE GATE SPENDS ITS TIME, one CSV row per
    // test and per fixture. Measurement only: it asserts nothing, logs nothing
    // while a test runs, and swallows every exception of its own -- the Unity
    // listener that calls it rethrows, and a profiler that could fail the run
    // it measures would be worse than no profiler.
    //
    // OUTPUT, beside the results XML in the project root it runs in (the
    // runner copy under the gate):
    //   test-profile-PlayMode.csv         -- one row per test, fixture, the
    //                                        assembly, and three markers
    //                                        (process start, run start, first
    //                                        test) that price the boot.
    //   test-profile-PlayMode-frames.csv  -- a histogram of every frame's wall
    //                                        cost, 0.1 ms bins, for the median
    //                                        frame cost the analysis needs.
    // Rows are appended as each node finishes, so a run that dies partway
    // still leaves everything measured before it died.
    //
    // HOW A FRAME IS CLASSIFIED. A PlayerLoop hook at the top of
    // Initialization timestamps every frame; the interval between two hooks is
    // one whole frame. An interval is a LOAD frame when a scene load was
    // pending at either end of it (SceneManager.sceneCount counts a scene
    // still loading, loadedSceneCount does not) or a sceneLoaded/sceneUnloaded
    // fired inside it. The two intervals after the one a load completed in are
    // SETTLE frames -- the convention every fixture follows, and where Start()
    // runs. Everything else lands in a cost bucket. There is no way to tell a
    // WaitForSecondsRealtime frame from a scaled WaitForSeconds frame at this
    // level (both are idle frames at timeScale 1), so the rows carry the
    // scaled-seconds delta and the paused seconds instead of pretending to.
    //
    // ALSO THE ASSEMBLY'S ONLY PER-TEST HOOK. TestRunCallback allows one per
    // assembly (a second is CS0579), so the test infrastructure that must
    // see every test rides on this one, each piece in its own try so none
    // can disturb the measurement: UnityEventRegistryPrune and
    // TestSaveSandbox before each test (and fixture), SharedScene's dirty rules after each
    // test and fixture. The
    // prune runs before the start snapshot, so its cost is not billed to the
    // test; it is microseconds once the registry is kept short.
    //
    // WHY BOTH PLATFORMS CALL IT. Unity constructs every TestRunCallback it
    // finds in any loaded assembly, and the Editor loads this assembly during
    // an EditMode run too. Only nodes whose type lives in THIS assembly, while
    // playing, are recorded; the EditMode run writes nothing.
    public class PlayModeTestProfiler : ITestRunCallback
    {
        private const string CsvName = "test-profile-PlayMode.csv";
        private const string FramesName = "test-profile-PlayMode-frames.csv";

        private const string Header =
            "kind,fixture,name,result,start_utc,since_process_s,wall_s,frames,scaled_s,unscaled_s," +
            "scene_loads,scene_unloads,scenes,load_frames,load_s,settle_frames,settle_s,paused_s," +
            "b0_n,b0_s,b1_n,b1_s,b2_n,b2_s,b3_n,b3_s,max_frame_s";

        // Cost buckets for frames that are neither load nor settle frames.
        private static readonly double[] BucketUpper = { 0.002, 0.010, 0.050, double.MaxValue };

        private struct Totals
        {
            public long Frames;
            public int Loads, Unloads, SceneIndex;
            public long LoadFrames, SettleFrames;
            public double LoadS, SettleS, PausedS;
            public long B0N, B1N, B2N, B3N;
            public double B0S, B1S, B2S, B3S;
        }

        // Cumulative since the hook was installed; a node's figures are the
        // difference between its start and finish snapshots.
        private static Totals s_totals;
        private static readonly List<string> s_sceneNames = new List<string>();
        private static readonly int[] s_histogram = new int[1001]; // 0.1 ms bins, last = overflow
        private static double s_maxFrameSinceMark;

        private static bool s_hookInstalled;
        private static bool s_sceneEventThisFrame;
        private static bool s_pendingAtLastHook;
        private static int s_settleFramesLeft;
        private static bool s_loadCompletedThisFrame;
        private static long s_lastHookTs;
        private static int s_lastHookFrame = -1;
        private static bool s_firstTestMarked;

        private struct Start
        {
            public long Ts;
            public DateTime Utc;
            public int FrameCount;
            public double Scaled, Unscaled;
            public Totals Totals;
        }

        private readonly Dictionary<string, Start> _starts = new Dictionary<string, Start>();

        private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;
        private static readonly Assembly ThisAssembly = typeof(PlayModeTestProfiler).Assembly;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        // ---- ITestRunCallback ------------------------------------------------

        public void RunStarted(ITest testsToRun)
        {
            try
            {
                if (!TreeHasThisAssembly(testsToRun)) return;
                // A fresh file per run. RunStarted fires in the Editor before
                // play mode is entered, which is also the moment boot and
                // compile are over -- the second boot marker.
                var path = Path.Combine(ProjectRoot, CsvName);
                if (File.Exists(path)) File.Delete(path);
                var frames = Path.Combine(ProjectRoot, FramesName);
                if (File.Exists(frames)) File.Delete(frames);
                AppendMarker("process_start", ProcessStartUtc());
                AppendMarker("run_started", DateTime.UtcNow);
            }
            catch (Exception) { }
        }

        public void RunFinished(ITestResult testResults) { }

        public void TestStarted(ITest test)
        {
            try
            {
                if (!Application.isPlaying) return;
                var kind = KindOf(test);
                if (kind == null) return;

                if (kind == "test")
                {
                    try { SharedScene.CallbackSawTestStart(); } catch (Exception) { }
                    try { UnityEventRegistryPrune.BeforeTest(); } catch (Exception) { }
                    try { TestSaveSandbox.BeforeTest(); } catch (Exception) { }
                    try { TestGlobals.ResetEngineClock(); } catch (Exception) { }
                }
                else if (kind == "fixture")
                {
                    try { TestSaveSandbox.BeforeFixture(); } catch (Exception) { }
                    try { TestGlobals.ResetEngineClock(); } catch (Exception) { }
                }

                EnsureHook();
                if (!s_firstTestMarked && !test.IsSuite)
                {
                    s_firstTestMarked = true;
                    AppendMarker("first_test_started", DateTime.UtcNow);
                }
                if (!test.IsSuite) s_maxFrameSinceMark = 0;

                var id = test.Id;
                _starts[id] = new Start
                {
                    Ts = Stopwatch.GetTimestamp(),
                    Utc = DateTime.UtcNow,
                    FrameCount = Time.frameCount,
                    Scaled = Time.timeAsDouble,
                    Unscaled = Time.unscaledTimeAsDouble,
                    Totals = s_totals,
                };
            }
            catch (Exception) { }
        }

        public void TestFinished(ITestResult result)
        {
            try
            {
                var test = result.Test;
                if (Application.isPlaying && KindOf(test) is string infraKind)
                {
                    try
                    {
                        if (infraKind != "assembly") SharedScene.AfterTestFinished(result);
                        else UnityEventRegistryPrune.LogSummary();
                    }
                    catch (Exception) { }
                }
                if (!_starts.TryGetValue(test.Id, out var start)) return;
                _starts.Remove(test.Id);
                var kind = KindOf(test);

                long now = Stopwatch.GetTimestamp();
                var t = s_totals;
                var s = start.Totals;
                var scenes = new StringBuilder();
                for (int i = s.SceneIndex; i < t.SceneIndex && i < s_sceneNames.Count; i++)
                {
                    if (scenes.Length > 0) scenes.Append('|');
                    scenes.Append(s_sceneNames[i]);
                }

                string fixture = kind == "test" ? test.TypeInfo.Name : test.Name;
                var row = string.Join(",", new[]
                {
                    kind,
                    Csv(fixture),
                    Csv(test.Name),
                    Csv(result.ResultState.Status.ToString()),
                    start.Utc.ToString("o", CultureInfo.InvariantCulture),
                    F((start.Utc - ProcessStartUtc()).TotalSeconds),
                    F((now - start.Ts) * TicksToSeconds),
                    (Time.frameCount - start.FrameCount).ToString(CultureInfo.InvariantCulture),
                    F(Time.timeAsDouble - start.Scaled),
                    F(Time.unscaledTimeAsDouble - start.Unscaled),
                    (t.Loads - s.Loads).ToString(CultureInfo.InvariantCulture),
                    (t.Unloads - s.Unloads).ToString(CultureInfo.InvariantCulture),
                    Csv(scenes.ToString()),
                    (t.LoadFrames - s.LoadFrames).ToString(CultureInfo.InvariantCulture),
                    F(t.LoadS - s.LoadS),
                    (t.SettleFrames - s.SettleFrames).ToString(CultureInfo.InvariantCulture),
                    F(t.SettleS - s.SettleS),
                    F(t.PausedS - s.PausedS),
                    (t.B0N - s.B0N).ToString(CultureInfo.InvariantCulture), F(t.B0S - s.B0S),
                    (t.B1N - s.B1N).ToString(CultureInfo.InvariantCulture), F(t.B1S - s.B1S),
                    (t.B2N - s.B2N).ToString(CultureInfo.InvariantCulture), F(t.B2S - s.B2S),
                    (t.B3N - s.B3N).ToString(CultureInfo.InvariantCulture), F(t.B3S - s.B3S),
                    kind == "test" ? F(s_maxFrameSinceMark) : "",
                });
                Append(CsvName, row);

                if (kind == "assembly") WriteHistogram();
            }
            catch (Exception) { }
        }

        // ---- the frame hook --------------------------------------------------

        private struct ProfilerFrameHook { }

        private static void EnsureHook()
        {
            // Installed once per domain; reinstalled if something replaced the
            // player loop since (the hook stops advancing its frame stamp).
            bool stale = s_hookInstalled && s_lastHookFrame >= 0 && Time.frameCount - s_lastHookFrame > 2;
            if (s_hookInstalled && !stale) return;

            var root = PlayerLoop.GetCurrentPlayerLoop();
            var systems = root.subSystemList;
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i].type != typeof(UnityEngine.PlayerLoop.Initialization)) continue;
                var init = systems[i];
                var subs = new List<PlayerLoopSystem>(init.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                subs.RemoveAll(x => x.type == typeof(ProfilerFrameHook));
                subs.Insert(0, new PlayerLoopSystem { type = typeof(ProfilerFrameHook), updateDelegate = OnFrame });
                init.subSystemList = subs.ToArray();
                systems[i] = init;
                root.subSystemList = systems;
                PlayerLoop.SetPlayerLoop(root);
                break;
            }

            if (!s_hookInstalled)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
            s_hookInstalled = true;
            s_lastHookTs = 0;
            s_pendingAtLastHook = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            s_totals.Loads++;
            s_totals.SceneIndex++;
            s_sceneNames.Add(scene.name);
            s_sceneEventThisFrame = true;
            s_loadCompletedThisFrame = true;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            s_totals.Unloads++;
            s_sceneEventThisFrame = true;
        }

        private static void OnFrame()
        {
            if (!Application.isPlaying) return;
            long now = Stopwatch.GetTimestamp();
            bool pendingNow = SceneManager.sceneCount != SceneManager.loadedSceneCount;
            s_lastHookFrame = Time.frameCount;

            if (s_lastHookTs != 0)
            {
                double dt = (now - s_lastHookTs) * TicksToSeconds;
                s_totals.Frames++;
                if (dt > s_maxFrameSinceMark) s_maxFrameSinceMark = dt;
                int bin = (int)(dt * 10000.0);
                s_histogram[bin < 0 ? 0 : bin > 1000 ? 1000 : bin]++;
                if (Time.timeScale <= 0f) s_totals.PausedS += dt;

                if (pendingNow || s_pendingAtLastHook || s_sceneEventThisFrame)
                {
                    s_totals.LoadFrames++;
                    s_totals.LoadS += dt;
                }
                else if (s_settleFramesLeft > 0)
                {
                    s_totals.SettleFrames++;
                    s_totals.SettleS += dt;
                    s_settleFramesLeft--;
                }
                else if (dt < BucketUpper[0]) { s_totals.B0N++; s_totals.B0S += dt; }
                else if (dt < BucketUpper[1]) { s_totals.B1N++; s_totals.B1S += dt; }
                else if (dt < BucketUpper[2]) { s_totals.B2N++; s_totals.B2S += dt; }
                else { s_totals.B3N++; s_totals.B3S += dt; }

                if (s_loadCompletedThisFrame) s_settleFramesLeft = 2;
            }

            s_sceneEventThisFrame = false;
            s_loadCompletedThisFrame = false;
            s_pendingAtLastHook = pendingNow;
            s_lastHookTs = now;
        }

        // ---- helpers ---------------------------------------------------------

        private static string KindOf(ITest test)
        {
            if (test.TypeInfo == null)
            {
                // The assembly node carries no type; recognise it by name.
                return test.IsSuite && test.Name.StartsWith(ThisAssembly.GetName().Name, StringComparison.Ordinal)
                    ? "assembly" : null;
            }
            if (test.TypeInfo.Assembly != ThisAssembly) return null;
            if (!test.IsSuite) return "test";
            // A parameterized method is a suite too; only the fixture itself
            // is recorded, or its cases would be counted twice.
            return test is NUnit.Framework.Internal.TestFixture ? "fixture" : null;
        }

        private static bool TreeHasThisAssembly(ITest node)
        {
            if (node == null) return false;
            if (node.TypeInfo != null) return node.TypeInfo.Assembly == ThisAssembly;
            if (node.Name.StartsWith(ThisAssembly.GetName().Name, StringComparison.Ordinal)) return true;
            if (!node.HasChildren) return false;
            foreach (var child in node.Tests)
                if (TreeHasThisAssembly(child)) return true;
            return false;
        }

        private static DateTime ProcessStartUtc()
        {
            using (var p = Process.GetCurrentProcess()) return p.StartTime.ToUniversalTime();
        }

        private static void AppendMarker(string name, DateTime utc)
        {
            var row = "marker,," + name + ",," + utc.ToString("o", CultureInfo.InvariantCulture) + "," +
                      F((utc - ProcessStartUtc()).TotalSeconds) + new string(',', 21);
            Append(CsvName, row);
        }

        private static void Append(string file, string row)
        {
            var path = Path.Combine(ProjectRoot, file);
            if (!File.Exists(path)) File.WriteAllText(path, Header + "\n");
            File.AppendAllText(path, row + "\n");
        }

        private static void WriteHistogram()
        {
            var sb = new StringBuilder("bin_ms,count\n");
            for (int i = 0; i < s_histogram.Length; i++)
            {
                if (s_histogram[i] == 0) continue;
                sb.Append((i / 10.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                  .Append(s_histogram[i].ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            File.WriteAllText(Path.Combine(ProjectRoot, FramesName), sb.ToString());
        }

        private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Csv(string s) =>
            s.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
