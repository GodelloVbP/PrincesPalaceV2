using System;
using PrincesPalace.Domain.UiKit;
using UnityEditor;
using UnityEngine;

// CAPTURE RUNS PLAY AT THE REFERENCE FRAME, not at batchmode's default screen.
//
// A batchmode Editor's play-mode view is 640x480 (MEASURED 2026-09-26: a
// probe in a -batchmode run without -nographics read Screen 640x480, and
// PlayModeWindow reported a GameView). The canvases scale with the screen,
// so every capture run played on a 1920x1440 (4:3) canvas: edge-anchored UI
// sat where it would on a 4:3 monitor, and CanvasCapture's 1920x1080 render
// target then showed that layout, not the reference one -- the combat log's
// lower edge came out at y=585 instead of 405. A fix that was right at the
// reference frame could not be shown in any capture.
//
// tools/graphics_tests.ps1 (and so preview.ps1 and static_pilot_qa.ps1) and
// tools/screenshot.ps1 -Runtime pass -ppReferenceScreen; with it, the
// play-mode view is set to UiFrames.Reference before Play starts, so
// Screen is 1920x1080 from the first frame. MEASURED in the same probe:
// PlayModeWindow.SetCustomRenderingResolution takes effect in batchmode and
// Screen follows it.
//
// OPT-IN BY FLAG, not every batchmode run: the commit gate runs -nographics,
// renders nothing, and has always played at 640x480; moving every gate test
// to a new screen size is a separate change with its own risk. A test that
// sets its own aspect (StageCaptureRig.FullFrame, CanvasCapture with an
// explicit size) and UiAudit's build-time four-aspect sweep do not read the
// play-mode view and are unaffected.
[InitializeOnLoad]
internal static class CaptureReferenceScreen
{
    public const string Flag = "-ppReferenceScreen";

    static CaptureReferenceScreen()
    {
        if (!Application.isBatchMode) return;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;

        // At load (before the test runner enters Play) and again on the way
        // into Play, in case entering Play rebuilt the view.
        Apply("editor load");
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                Apply(state.ToString());
        };
    }

    private static void Apply(string when)
    {
        uint width = (uint)UiFrames.Reference.X;
        uint height = (uint)UiFrames.Reference.Y;
        PlayModeWindow.SetCustomRenderingResolution(width, height, "PP reference");
        PlayModeWindow.GetRenderingResolution(out uint gotWidth, out uint gotHeight);
        Debug.Log($"[CaptureReferenceScreen] {when}: play-mode view {gotWidth}x{gotHeight} (asked {width}x{height})");
    }
}
