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

            Capture(def, Path.Combine(outDir, panel + ".png"));
            Debug.Log($"[ScreenshotTool] wrote 1 screenshot to {outDir}");
        }
        else
        {
            int written = CaptureAllTo(outDir);
            Debug.Log($"[ScreenshotTool] wrote {written} screenshot(s) to {outDir}");
        }

        EditorApplication.Exit(0);
    }

    private static int CaptureAllTo(string outDir)
    {
        int written = 0;
        foreach (var def in ScreenRegistry.All)
        {
            Capture(def, Path.Combine(outDir, def.PanelName + ".png"));
            written++;
        }
        return written;
    }

    private static void Capture(ScreenDef def, string outputPath)
    {
        if (!CanvasCapture.IsSupported)
        {
            Debug.LogError("[ScreenshotTool] no graphics device - run without -nographics.");
            return;
        }

        EditorSceneManager.OpenScene(def.ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"[ScreenshotTool] {def.ScenePath} has no Canvas.");
            return;
        }

        // Shows the scene exactly as SceneBuilder authored it: Edit Mode never
        // ticks Update(), so nothing a MonoBehaviour populates at runtime is
        // here. For anything that MOVES, use screenshot.ps1 -Runtime, which
        // drives a PlayMode test instead.
        CanvasCapture.RenderToFile(canvas, outputPath);
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
