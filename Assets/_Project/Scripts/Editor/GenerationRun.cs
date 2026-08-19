using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Runs every generator the test harness needs in ONE Unity process.
//
// It used to launch one batchmode Unity per generator per runner copy: five
// methods times two copies is ten cold starts, and a cold start is ~15-20s
// before a single line of the actual work runs. The generation phase cost
// around three minutes, which is long enough that the author was killing the
// run rather than waiting for it -- so the harness was, in practice, not being
// used.
//
// Two things were wasted, and they are separate:
//
//  1. PER-METHOD PROCESSES. Nothing forced them. Each builder is a static
//     method that leaves its results on disk; calling four of them in sequence
//     in one process is the same work minus three Unity boots. The one real
//     constraint is TmpBootstrap, which genuinely cannot share a process with
//     anything that emits a label -- see its own header. So it stays a
//     separate boot and is deliberately NOT in this driver.
//
//  2. PER-COPY GENERATION. The secondary runner built everything and then had
//     it all overwritten: run_tests_parallel.ps1 re-mirrors main over every
//     secondary copy immediately after the sync-back, precisely so all copies
//     agree on asset GUIDs. The secondary's own output never survived to be
//     tested. Generating there was pure cost.
//
// Which generators run is passed as -ppSteps, because -executeMethod cannot
// take arguments and the harness needs -BuildContent and -BuildScenes to stay
// independent switches.
public static class GenerationRun
{
    // Each builder logs its own "BUILD-COMPLETE: <name>" sentinel, and the
    // harness gates on the sentinel rather than the exit code (Unity exits
    // non-zero on an untidy shutdown even when the work succeeded). Sharing a
    // process changes nothing about that: all the sentinels simply land in one
    // log, and the harness looks for all of them.
    public static void RunAll()
    {
        var steps = ReadSteps();
        Debug.Log($"[GenerationRun] steps: {string.Join(", ", steps)}");

        // Per-step timings, permanently. "The build is slow" was unactionable
        // until the harness started stamping its phases, and the answer turned
        // out to be inside this method rather than in any of the process
        // overhead everyone suspected. Leaving the stamps in means the next
        // regression is read off the log rather than bisected for.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var last = System.TimeSpan.Zero;
        void Mark(string what)
        {
            var now = watch.Elapsed;
            Debug.Log($"[GenerationRun] {what}: {(now - last).TotalSeconds:N1}s (total {now.TotalSeconds:N1}s)");
            last = now;
        }

        // Order matters and is the same order the separate processes ran in.
        // The sprite baker and the pipeline write ASSETS that the scene build
        // then references by GUID, so both have to be on disk and imported
        // before SceneBuilder looks for them.
        ProceduralSpriteBaker.BakeAll();
        Mark("ProceduralSpriteBaker");
        PipelineBuilder.BuildRenderPipeline();
        Mark("PipelineBuilder");

        // Between generators, not just at the end. In separate processes each
        // builder got a full import cycle for free on the next process's
        // startup; in one process nothing imports unless it is asked to, and a
        // scene build that cannot resolve a just-written material fails in a
        // way that names the material and not the missing refresh.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Mark("refresh after pipeline");

        if (steps.Contains("content"))
        {
            ContentBuilder.BuildDefaultContent();
            Mark("ContentBuilder");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Mark("refresh after content");
        }

        if (steps.Contains("scenes"))
        {
            SceneBuilder.BuildAllScenes();
            Mark("SceneBuilder");
        }

        AssetDatabase.SaveAssets();
        Mark("final SaveAssets");
        Debug.Log("BUILD-COMPLETE: GenerationRun");
    }

    private static string[] ReadSteps()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-ppSteps")
            {
                return args[i + 1].Split(',')
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Where(s => s.Length > 0)
                    .ToArray();
            }
        }

        // No steps named means the two unconditional generators only. Silently
        // building content and scenes here would make -BuildContent meaningless.
        return Array.Empty<string>();
    }
}
