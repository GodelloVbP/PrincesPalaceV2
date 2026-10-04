using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat.Session;
using Debug = UnityEngine.Debug;

namespace PrincesPalace.PlayModeTests
{
    // THE COLD BATCH tools/gif.ps1 drives: every scenario of one request file in
    // ONE Unity boot (docs/PLAN_GIF_CAPTURE.md 2.2 and 2.5).
    //
    // NOT A GATE. It needs a graphics device and a request file, so it is
    // [Explicit] and the commit gate never selects it; tools/gif.ps1 names it
    // with -testFilter. Frames and per-scenario timing land in the run directory
    // the request names, written by the shared CaptureScenarioRunner, so a GIF
    // is a recording of the same fight SpellRuntimeCaptureTests records.
    //
    // THE LAUNCHER'S CONTRACT, in files under the request's outDir:
    //   ready.json            written once PlayMode is entered and the request
    //                         parsed: the end of the startup stage.
    //   state.json            rewritten at the start of every scenario (index and
    //                         key), which is how the launcher knows which scenario
    //                         a 60 s deadline is running against.
    //   <key>/result.json     one scenario's verdict, written when it ends.
    //   result.json           the whole request's verdict, last, echoing the
    //                         requestId. Every file is written to a temp name and
    //                         renamed, so a reader never sees half of one.
    //
    // A scenario that throws (a refused id, a cast that never drew) is recorded
    // and the batch goes on: one bad id must not cost the rest their boot.
    public class GifBatchCaptureTests
    {
        private const string RequestVariable = "PP_GIF_REQUEST";

        [Serializable]
        private sealed class ScenarioResult
        {
            public string key = "";
            public string kind = "";
            public string id = "";
            public bool ok;
            public string error = "";
            public float seconds;
            public int frames;
            public int litFrames;
            public bool presentationOnly;
            public string[] logErrors = Array.Empty<string>();
        }

        [Serializable]
        private sealed class BatchResult
        {
            public string requestId = "";
            public bool ok;
            public string error = "";
            public float seconds;
            public ScenarioResult[] scenarios = Array.Empty<ScenarioResult>();
        }

        [Serializable]
        private sealed class State
        {
            public string requestId = "";
            public int index;
            public int total;
            public string key = "";
        }

        [Serializable]
        private sealed class Ready
        {
            public string requestId = "";
            public int scenarios;
        }

        // The Test Framework's default per-test limit is three minutes; a batch
        // is as long as its request.
        [UnityTest, Explicit("Driven by tools/gif.ps1, which sets " + RequestVariable + "."), Timeout(14400000)]
        public IEnumerator RunsEveryScenarioOfTheRequestFile()
        {
            string requestPath = Environment.GetEnvironmentVariable(RequestVariable);
            // The commit gate selects classes by name, which runs an [Explicit]
            // test too, so the missing request is an ignore here, the way the
            // other capture classes ignore without a graphics device.
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                Assert.Ignore("No request file (" + RequestVariable + " unset). Run: tools/gif.ps1");
            }

            Assert.IsTrue(File.Exists(requestPath), "no request file at " + requestPath);

            var request = JsonUtility.FromJson<CaptureRequest>(File.ReadAllText(requestPath));
            Assert.IsNotNull(request, "the request file did not parse");
            Directory.CreateDirectory(request.outDir);

            if (!CanvasCapture.IsSupported)
            {
                WriteAtomic(Path.Combine(request.outDir, "result.json"), JsonUtility.ToJson(new BatchResult
                {
                    requestId = request.requestId,
                    error = "No graphics device. tools/gif.ps1 launches without -nographics on the hidden " +
                            "desktop; PP_GRAPHICS_DESKTOP=visible is the opt-out.",
                }));
                Assert.Ignore("No graphics device (-nographics). Run: tools/gif.ps1");
            }

            WriteAtomic(Path.Combine(request.outDir, "ready.json"), JsonUtility.ToJson(new Ready
            {
                requestId = request.requestId,
                scenarios = request.scenarios.Length,
            }));

            var batchClock = Stopwatch.StartNew();
            var results = new List<ScenarioResult>();
            var logErrors = new List<string>();
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                    logErrors.Count < 20)
                {
                    logErrors.Add(message);
                }
            };

            Application.logMessageReceived += onLog;
            try
            {
                for (int i = 0; i < request.scenarios.Length; i++)
                {
                    var scenario = request.scenarios[i];
                    WriteAtomic(Path.Combine(request.outDir, "state.json"), JsonUtility.ToJson(new State
                    {
                        requestId = request.requestId,
                        index = i,
                        total = request.scenarios.Length,
                        key = scenario.key,
                    }));

                    var result = new ScenarioResult { key = scenario.key, kind = scenario.kind, id = scenario.id };
                    logErrors.Clear();
                    var clock = Stopwatch.StartNew();

                    Exception failure = null;
                    yield return Guard(RunOne(scenario, request.outDir, result), e => failure = e);

                    result.seconds = (float)clock.Elapsed.TotalSeconds;
                    result.ok = failure == null;
                    result.error = failure == null ? "" : failure.GetType().Name + ": " + failure.Message;
                    result.logErrors = logErrors.ToArray();
                    results.Add(result);

                    string scenarioDir = Path.Combine(request.outDir, scenario.key);
                    Directory.CreateDirectory(scenarioDir);
                    WriteAtomic(Path.Combine(scenarioDir, "result.json"), JsonUtility.ToJson(result));

                    CaptureScenarioRunner.Restore();
                    yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                CaptureScenarioRunner.Restore();
            }

            var batch = new BatchResult
            {
                requestId = request.requestId,
                ok = results.TrueForAll(r => r.ok),
                seconds = (float)batchClock.Elapsed.TotalSeconds,
                scenarios = results.ToArray(),
            };
            WriteAtomic(Path.Combine(request.outDir, "result.json"), JsonUtility.ToJson(batch));
            Debug.Log("[GifBatch] " + results.Count + " scenario(s), ok=" + batch.ok);
        }

        private static IEnumerator RunOne(CaptureScenario s, string outDir, ScenarioResult result)
        {
            string dir = Path.Combine(outDir, s.key);
            string frames = Path.Combine(dir, "frames");
            Directory.CreateDirectory(frames);

            var runner = new CaptureScenarioRunner
            {
                Width = s.width,
                Height = s.height,
                Darkness = s.darkness,
            };
            var sink = new CaptureSink
            {
                FramePath = frame => Path.Combine(frames, string.Format("f{0:0000}.png", frame)),
                TimingPath = Path.Combine(dir, "timing.json"),
            };
            var outcome = new CaptureOutcome();
            bool full = s.formation == PreviewFightFormationFull;
            int copies = full ? CaptureScenarioRunner.FullFormation : 1;

            switch (s.kind)
            {
                case "spell":
                    yield return runner.StandTheFightUp(new CaptureStandRequest
                    {
                        SpellId = s.id,
                        Versus = s.vs,
                        Speed = s.speed,
                        PreStatus = s.preStatus,
                        Enemies = full ? CaptureScenarioRunner.FullFormation : (int?)null,
                        VersusFillsFormation = full,
                        Party = s.party.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                        Target = s.target,
                    });
                    if (s.repeat > 1) yield return runner.RecordRepeatedCast(s.id, s.repeat, s.speed, sink, outcome);
                    else yield return runner.RecordCast(s.id, relic: false, s.speed, sink, outcome);
                    if (s.seconds > 0f) yield return runner.PlayOn(sink, s.seconds, outcome);
                    break;

                case "relic":
                    yield return runner.StandTheFightUp(s.vs, copies, s.speed, new[] { s.id }, null, s.preStatus);
                    yield return runner.RecordCast(s.id, relic: true, s.speed, sink, outcome);
                    break;

                case "attack":
                    yield return runner.StandTheFightUp(s.id, copies, s.speed, null,
                        EnemyShowcase.Forcing(s.skill, Debug.Log), s.preStatus);
                    yield return runner.RecordAttackRound(sink, s.speed, s.id, s.skill, outcome);
                    break;

                case "status":
                    yield return runner.StandTheFightUp(s.vs, copies, s.speed, null, null, s.id);
                    yield return runner.RecordSeconds(sink, s.speed, "status", s.id, s.seconds, outcome);
                    break;

                default:
                    Assert.Fail("scenario kind '" + s.kind + "' is not a PlayMode kind");
                    break;
            }

            result.frames = outcome.Frames;
            result.litFrames = outcome.LitFrames;
            result.presentationOnly = outcome.PresentationOnly;
        }

        private const string PreviewFightFormationFull = PrincesPalace.PreviewFight.FormationFull;

        // Runs `inner` to its end, flattening nested enumerators itself because
        // the Test Framework would otherwise own them and an exception thrown in
        // one would end the whole test instead of reaching `failed`. C# forbids
        // `yield return` inside a try block with a catch, hence the manual stack.
        private static IEnumerator Guard(IEnumerator inner, Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(inner);

            while (stack.Count > 0)
            {
                object current;
                try
                {
                    if (!stack.Peek().MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }

                    current = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    failed(e);
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current;
            }
        }

        private static void WriteAtomic(string path, string text)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
