using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Renders a Canvas to a PNG. Lives in Core rather than Editor for one
    // reason: `ScreenshotTool` (Edit Mode) and the PlayMode capture fixture
    // must produce BYTE-COMPARABLE images, and the only way to guarantee
    // that is one implementation. Tests/PlayMode cannot reference Editor —
    // see docs/CODE_STANDARDS.md §1 — so the shared code has to sit here.
    //
    // The Edit Mode tool answers "does the art and layout look right." This
    // being callable from a PlayMode test answers the question the project
    // has never been able to ask: "does it look right WHILE IT IS MOVING."
    // Edit Mode never ticks Update(), so every ambient animator in this
    // project (LanternFlicker, MoteDrift, KenBurnsDrift, StarTwinkle...) has
    // until now been verified by arithmetic alone.
    public static class CanvasCapture
    {
        // This project's CanvasScaler.referenceResolution (see
        // SceneBuilder.CreateCanvas), so a capture matches what every
        // coordinate in SceneBuilder was authored against.
        // The reference stage, so a capture is what the layout was authored
        // and audited at rather than a second opinion about the size.
        public static readonly int DefaultWidth = (int)UiFrames.Reference.X;
        public static readonly int DefaultHeight = (int)UiFrames.Reference.Y;

        // A headless runner launched with -nographics has no graphics device
        // and cannot render anything — camera.Render() is a silent no-op and
        // ReadPixels returns garbage. `tools/run_tests_parallel.ps1` and
        // `tools/test.ps1` BOTH pass -nographics, so the normal commit gate
        // can never capture; only `tools/screenshot.ps1`, which deliberately
        // omits it, can. Callers must check this and skip rather than assert
        // against a black rectangle.
        public static bool IsSupported => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        // Repoints the canvas at a dedicated orthographic camera + RenderTexture,
        // renders one frame, reads the pixels back, and puts the canvas back
        // exactly as found.
        //
        // This used to say "every canvas in this project is ScreenSpaceOverlay",
        // which was true of v1 and is not true here: SceneBuilder.CreateCanvas
        // builds ScreenSpaceCamera canvases so URP post-processing can reach the
        // UI at all. The swap below is therefore no longer about rescuing an
        // Overlay canvas that has no camera -- it is about pointing whatever
        // camera the canvas already uses at a texture we can read. Doing it
        // unconditionally is deliberate: it costs nothing on a camera canvas and
        // still works if one is ever switched back.
        // ZERO MEANS "THE REFERENCE STAGE", because a default parameter has to
        // be a compile-time constant and the stage is now read from UiFrames --
        // which is the right trade: one statement of the frame, resolved here,
        // rather than a second literal kept in step by hand.
        public static void RenderToFile(Canvas canvas, string outputPath, int width = 0, int height = 0)
        {
            if (width <= 0) width = DefaultWidth;
            if (height <= 0) height = DefaultHeight;

            var previousRenderMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousPlaneDistance = canvas.planeDistance;

            // The real bug behind the "background is cut off" reports: the
            // CanvasScaler's ScaleWithScreenSize mode (see CreateCanvas) scales
            // against Screen.width/Screen.height -- the ACTUAL window's
            // current pixel size -- not against this method's RenderTexture.
            // Rendering into an offscreen 1920x1080 target does not change what
            // Screen.width/height report, so if the window wasn't
            // exactly 1920x1080 at capture time, the canvas silently scaled
            // itself to fit THAT size instead, leaving the fixed-size 1920-wide
            // panel short of the render target's actual right/bottom edge.
            // Forcing ConstantPixelSize for the capture makes 1 canvas unit =
            // 1 texture pixel unconditionally, matching what every position in
            // SceneBuilder was authored against, then restores whatever mode
            // the canvas normally runs in once the capture is done.
            var scaler = canvas.GetComponent<CanvasScaler>();
            var previousScaleMode = scaler != null ? scaler.uiScaleMode : CanvasScaler.ScaleMode.ConstantPixelSize;
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }

            var cameraGO = new GameObject("ScreenshotCamera");
            var camera = cameraGO.AddComponent<Camera>();
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                // Camera.aspect is documented to auto-sync to targetTexture,
                // but that sync is lazy -- a camera created and rendered within
                // one synchronous call can still carry over whatever
                // aspect the window happened to have, narrowing
                // the horizontal frame and leaving a solid-color gap on one
                // edge. Setting it explicitly is the only way to guarantee this
                // capture is the full 1920x1080 frame regardless of what
                // window state the process was in when this ran.
                camera.aspect = (float)width / height;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f); // distinguishable from any real UI color
                camera.targetTexture = renderTexture;
                cameraGO.transform.position = new Vector3(0f, 0f, -1f);

                // Without this a capture would silently show the game WITHOUT
                // bloom, vignette or grading -- i.e. not what ships. A
                // screenshot tool that quietly disagrees with the build is
                // worse than no screenshot tool.
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;

                Canvas.ForceUpdateCanvases();
                camera.Render();

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previousActive;

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlaneDistance;
                if (scaler != null)
                {
                    scaler.uiScaleMode = previousScaleMode;
                }

                camera.targetTexture = null;
                // DestroyImmediate deliberately, at runtime as well as in the
                // Editor. Object.Destroy defers to end of frame, which would
                // leave this camera alive — with targetTexture already cleared
                // above — long enough to render the scene a second time,
                // straight to the screen.
                Object.DestroyImmediate(cameraGO);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
