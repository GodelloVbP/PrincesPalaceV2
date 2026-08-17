using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PrincesPalace;

// Headless visual QA, driven entirely by ScreenRegistry.
//
// v1 kept a hand-maintained KnownPanels table here AND a third copy of the same
// list in screenshot.ps1's usage text, with a documented "nothing keeps these in
// sync" hazard. There is one list now -- the same one that builds the scenes --
// so a screen cannot exist in the game and be invisible to its own tooling.
//
// A UiKitLintTests rule allows `new GameObject(` in this file by name; the
// capture camera is not a UI widget.
public static class ScreenshotTool
{
    public static string DefaultOutputDir()
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, "tools", "screenshots"));
    }

    public static IEnumerable<string> KnownPanelNames => ScreenRegistry.All.Select(s => s.PanelName);

    [MenuItem("Prince's Palace/Capture All Panels")]
    public static void CaptureAll()
    {
        int written = CaptureAllTo(DefaultOutputDir());
        Debug.Log($"[ScreenshotTool] wrote {written} screenshot(s) to {DefaultOutputDir()}");
    }

    // -executeMethod entry:
    //   -executeMethod ScreenshotTool.CaptureFromArgs -panel MainMenuPanel -out C:\shots
    public static void CaptureFromArgs()
    {
        var args = ParseArgs();
        string outDir = args.TryGetValue("out", out var o) ? o : DefaultOutputDir();

        if (args.TryGetValue("panel", out var panel))
        {
            var def = ScreenRegistry.All.FirstOrDefault(s => s.PanelName == panel);
            if (def == null)
            {
                Debug.LogError($"[ScreenshotTool] no screen named '{panel}'. Known: {string.Join(", ", KnownPanelNames)}");
                EditorApplication.Exit(1);
                return;
            }

            if (!Capture(def, Path.Combine(outDir, panel + ".png")))
            {
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[ScreenshotTool] wrote 1 screenshot to {outDir}");
        }
        else
        {
            int written = CaptureAllTo(outDir);
            int expected = ScreenRegistry.All.Count;
            Debug.Log($"[ScreenshotTool] wrote {written} of {expected} screenshot(s) to {outDir}");

            // An unknown panel already exits 1 above; a screen that failed to
            // render is no less a failure, and saying so here is what makes the
            // exit code mean one thing rather than two.
            if (written < expected)
            {
                Debug.LogError($"[ScreenshotTool] {expected - written} screen(s) did not render.");
                EditorApplication.Exit(1);
                return;
            }
        }

        EditorApplication.Exit(0);
    }

    // Counts what was WRITTEN, not what was attempted.
    //
    // This used to increment once per registered screen regardless, so a
    // Capture that bailed early -- no graphics device, a scene with no Canvas --
    // still reported "wrote 5 screenshot(s)" having written four. The wrapper
    // now checks for each expected file by name and would catch it anyway, but
    // a success count that is known to overstate is worse than no count: it is
    // the same class of bug as the one screenshot.ps1 just had, one layer down.
    private static int CaptureAllTo(string outDir)
    {
        int written = 0;
        foreach (var def in ScreenRegistry.All)
        {
            if (Capture(def, Path.Combine(outDir, def.PanelName + ".png"))) written++;
        }
        return written;
    }

    private static bool Capture(ScreenDef def, string outputPath)
    {
        if (!CanvasCapture.IsSupported)
        {
            Debug.LogError("[ScreenshotTool] no graphics device - run without -nographics.");
            return false;
        }

        EditorSceneManager.OpenScene(def.ScenePath, OpenSceneMode.Single);

        // THE ROOT canvas, not the first one and not any one.
        //
        // FindFirstObjectByType is deprecated in this Unity -- it ordered by
        // instance ID, which was never a real ordering -- and the obvious
        // swap to FindAnyObjectByType would have been a latent bug rather
        // than a fix. UiEmitter.EmitNestedCanvas gives a node its own Canvas
        // for sort-order control, so a scene may hold several; "any" of them
        // would eventually be a bark banner, and the screenshot would come
        // back as one panel on a black field with nothing to explain it.
        //
        // Today every built scene has exactly one, which is precisely why
        // this needed deciding now instead of being found later.
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
            .FirstOrDefault(c => c.isRootCanvas);
        if (canvas == null)
        {
            Debug.LogError($"[ScreenshotTool] {def.ScenePath} has no Canvas.");
            return false;
        }

        // Shows the scene exactly as SceneBuilder authored it: Edit Mode never
        // ticks Update(), so nothing a MonoBehaviour populates at runtime is
        // here. For anything that MOVES, use screenshot.ps1 -Runtime, which
        // drives a PlayMode test instead.
        CanvasCapture.RenderToFile(canvas, outputPath);

        // The file, not the call. RenderToFile returning without throwing is not
        // evidence that a png landed, and this counter's whole job is now to be
        // trusted about that.
        if (File.Exists(outputPath)) return true;

        Debug.LogError($"[ScreenshotTool] {def.PanelName} rendered but no file appeared at {outputPath}.");
        return false;
    }

    private static Dictionary<string, string> ParseArgs()
    {
        var result = new Dictionary<string, string>();
        var argv = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length - 1; i++)
        {
            if (argv[i].StartsWith("-") && !argv[i + 1].StartsWith("-"))
            {
                result[argv[i].TrimStart('-')] = argv[i + 1];
            }
        }
        return result;
    }
}
