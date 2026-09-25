using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Shared camera/crop machinery for a PlayMode fixture that photographs the
    // fight stage's own box rather than the whole screen.
    //
    // Extracted from StaticPilotStageCaptureTests, which was the first fixture
    // to need it, when PartyFormationCaptureTests needed the exact same rig for
    // a different capture (a party-formation frame series instead of one
    // monster's swing). What is duplicated here is the part that is
    // load-bearing for correctness -- the ConstantPixelSize scaler swap,
    // without which the canvas scales itself against the window's real size
    // rather than the render target, and the explicit camera.aspect, without
    // which the lazy targetTexture sync can leave a gap down one edge -- so a
    // second capture fixture reimplementing this from scratch would either
    // duplicate both subtleties correctly by luck or silently drop one of
    // them. Not folded into CanvasCapture itself: that method's contract is
    // one canvas, one file, byte-comparable with the Edit Mode screenshot
    // tool, and a frame series wants neither of the last two.
    internal sealed class StageCaptureRig
    {
        public readonly int CropX;
        public readonly int CropY;
        public readonly int CropWidth;
        public readonly int CropHeight;

        private readonly Canvas _canvas;
        private readonly CanvasScaler _scaler;
        private readonly RenderMode _mode;
        private readonly Camera _previousCamera;
        private readonly float _planeDistance;
        private readonly CanvasScaler.ScaleMode _scaleMode;

        private readonly GameObject _cameraGo;
        private readonly Camera _camera;
        private readonly RenderTexture _rt;

        // cropWidth/cropHeight in stage pixels -- callers pass
        // FightStageAnchors.StageSize so the crop follows that layout if it is
        // ever resized, rather than a value pinned here.
        public StageCaptureRig(Canvas canvas, int cropWidth, int cropHeight)
            : this(canvas, CanvasCapture.DefaultWidth, CanvasCapture.DefaultHeight, cropWidth, cropHeight)
        {
        }

        // THE WHOLE CANVAS AT ONE UiFrames ASPECT, held across frames: from
        // the next layout pass the canvas is solved at width x height, so
        // anything placed in LateUpdate against the canvas rect (FocusMarker)
        // is placed for THIS aspect -- which a one-shot
        // CanvasCapture.RenderToFile at a non-reference size cannot give it.
        // DialogueStageCaptureTests is the first caller.
        public static StageCaptureRig FullFrame(Canvas canvas, int width, int height) =>
            new StageCaptureRig(canvas, width, height, width, height);

        private StageCaptureRig(Canvas canvas, int width, int height, int cropWidth, int cropHeight)
        {
            CropWidth = cropWidth;
            CropHeight = cropHeight;
            CropX = (width - CropWidth) / 2;
            CropY = (height - CropHeight) / 2;

            _canvas = canvas;
            _mode = canvas.renderMode;
            _previousCamera = canvas.worldCamera;
            _planeDistance = canvas.planeDistance;
            _scaler = canvas.GetComponent<CanvasScaler>();
            _scaleMode = _scaler != null ? _scaler.uiScaleMode : CanvasScaler.ScaleMode.ConstantPixelSize;

            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.scaleFactor = 1f;
            }

            _cameraGo = new GameObject("StageCaptureRigCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

            _camera.orthographic = true;
            _camera.orthographicSize = height / 2f;
            _camera.aspect = (float)width / height;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 10f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
            _camera.targetTexture = _rt;
            _cameraGo.transform.position = new Vector3(0f, 0f, -1f);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
        }

        public Texture2D Grab()
        {
            Canvas.ForceUpdateCanvases();
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            var texture = new Texture2D(CropWidth, CropHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(CropX, CropY, CropWidth, CropHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        public void Restore()
        {
            _canvas.renderMode = _mode;
            _canvas.worldCamera = _previousCamera;
            _canvas.planeDistance = _planeDistance;
            if (_scaler != null) _scaler.uiScaleMode = _scaleMode;

            _camera.targetTexture = null;
            RenderTexture.active = null;

            // DestroyImmediate for the reason CanvasCapture gives: a deferred
            // Destroy leaves this camera alive with no target texture long
            // enough to render the scene straight to the screen once more.
            UnityEngine.Object.DestroyImmediate(_cameraGo);
            _rt.Release();
            UnityEngine.Object.DestroyImmediate(_rt);
        }
    }

    // tools/screenshots/runtime/<subsystem>/<PP_CAPTURE_LABEL or "unlabelled">/
    // -- the folder convention every frame-series capture writes into, so the
    // same environment variable names a "before" and an "after" run no matter
    // which fixture wrote them.
    internal static class CaptureOutput
    {
        public static string LabelDir(string subsystem) =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "tools", "screenshots", "runtime", subsystem,
                Environment.GetEnvironmentVariable("PP_CAPTURE_LABEL") ?? "unlabelled"));
    }
}
