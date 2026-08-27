using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Core.Rig;

namespace PrincesPalace.PlayModeTests
{
    // The SpriteRenderer twin of HitFlashPixelTests: does RigHitFlash.shader
    // actually render a white silhouette under URP, or only look right on
    // paper? Same technique -- a half-opaque, deliberately DARK sprite, so a
    // shader that merely multiplies (the exact bug this shader exists to
    // avoid -- SpriteRenderer.color multiplies the texture exactly like
    // Image.color does) would render dark rather than white and get caught.
    //
    // GRAPHICS-GATED, self-skips under -nographics exactly like
    // HitFlashPixelTests. Run via tools/graphics_tests.ps1.
    public class RigHitFlashPixelTests
    {
        private const int Size = 128;

        private GameObject _root;
        private Camera _camera;
        private RenderTexture _rt;

        [SetUp]
        public void FreezeTime() => FightBeatPlayer.BeatSpeedMultiplier = 0.05f;

        [TearDown]
        public void Cleanup()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
            if (_rt != null) _rt.Release();
        }

        private static Sprite HalfOpaque()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    texture.SetPixel(x, y, x < 8
                        ? new Color(0.05f, 0.05f, 0.05f, 1f)
                        : new Color(0f, 0f, 0f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        [UnityTest]
        public IEnumerator TheFlashIsWhiteWhereThePartIsOpaque()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            _root = new GameObject("RigFlashHarness", typeof(SpriteRenderer));
            _root.GetComponent<SpriteRenderer>().sprite = HalfOpaque();

            var flash = _root.AddComponent<RigHitFlash>();
            flash.Flash();

            var cameraGo = new GameObject("RigFlashCamera");
            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 1f;
            _camera.aspect = 1f;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 10f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            cameraGo.transform.position = new Vector3(0f, 0f, -5f);

            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _rt;

            // A frame for the flash coroutine to actually set the peak tint
            // before this renders.
            yield return null;
            yield return null;

            _camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var captured = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            captured.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            captured.Apply();
            RenderTexture.active = previousActive;

            var opaqueSide = captured.GetPixel(Size / 4, Size / 2);
            var clearSide = captured.GetPixel(Size * 3 / 4, Size / 2);
            Object.DestroyImmediate(captured);

            float opaqueLuminance = Luminance(opaqueSide);
            float clearLuminance = Luminance(clearSide);

            Assert.Greater(opaqueLuminance, 0.6f,
                $"the opaque half should read as a WHITE silhouette, not the sprite's own dark colour (got {opaqueSide}) " +
                "- PrincesPalace/RigHitFlash did not survive, or RigHitFlash.mat is not resolving it");

            Assert.Less(clearLuminance, opaqueLuminance - 0.3f,
                $"the transparent half must stay clear - the flash is the part's SHAPE, not its bounding box (got {clearSide})");
        }

        private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
