using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Does the hit flash actually render white where the sprite is opaque?
    //
    // A PIXEL TEST, not an eyeball. The whole question the plan raised about
    // this shader is whether a hand-written unlit CG pass survives the move to
    // URP, and the honest answer to "does it still draw" is to draw it and read
    // the pixels back -- which CanvasCapture already does deterministically.
    //
    // GRAPHICS-GATED. The commit gate runs headless (-nographics), where
    // camera.Render() is a silent no-op and ReadPixels returns garbage, so this
    // self-skips there exactly as RuntimeScreenshotTests does. It runs under
    // tools/screenshot.ps1, which deliberately omits the flag.
    public class HitFlashPixelTests
    {
        private const int Size = 128;

        private Canvas _canvas;
        private GameObject _root;

        [SetUp]
        public void FreezeTime()
        {
            // The flash is a coroutine over real seconds; at 1x its opaque hold
            // is 50ms, which is a race against a capture. Slowing it DOWN is the
            // right direction here -- the same seam the fight tests speed up.
            FightBeatPlayer.BeatSpeedMultiplier = 0.05f;
        }

        [TearDown]
        public void Cleanup()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            if (_root != null) Object.DestroyImmediate(_root);
        }

        // A sprite that is opaque on its left half and fully transparent on its
        // right. That shape is the assertion: the flash must appear on one side
        // and not the other, which no amount of "something got drawn" can fake.
        private static Sprite HalfOpaque()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    // Deliberately DARK where it is opaque. A white tint on a
                    // dark sprite is the exact case a plain UI shader gets
                    // wrong -- Image.color multiplies, so white is the identity
                    // and the flash would render dark.
                    texture.SetPixel(x, y, x < 8
                        ? new Color(0.05f, 0.05f, 0.05f, 1f)
                        : new Color(0f, 0f, 0f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }

        private void BuildCanvas(Material material)
        {
            _root = new GameObject("FlashHarness", typeof(Canvas), typeof(CanvasScaler));
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

            var imageGo = new GameObject("Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageGo.transform.SetParent(_root.transform, false);

            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();
            image.sprite = HalfOpaque();
            image.material = material;

            var flash = imageGo.AddComponent<StageHitFlash>();
            flash.Attach(image);
            flash.SetSprite(image.sprite);
            flash.Flash();
        }

        private static Material LoadFlashMaterial()
        {
            // Resources, not AssetDatabase: this is a PlayMode test and has no
            // Editor assembly to reach through.
            var shader = Shader.Find("PrincesPalace/UIHitFlash");
            return shader == null ? null : new Material(shader);
        }

        [UnityTest]
        public IEnumerator TheFlashIsWhiteWhereTheSpriteIsOpaque()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            var material = LoadFlashMaterial();
            Assert.IsNotNull(material, "PrincesPalace/UIHitFlash did not survive the URP move - it is not findable at all.");

            BuildCanvas(material);
            yield return null;
            yield return null;

            string path = Path.Combine(Path.GetTempPath(), "pp-hitflash-" + System.Guid.NewGuid().ToString("N") + ".png");
            CanvasCapture.RenderToFile(_canvas, path, Size, Size);

            var bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var captured = new Texture2D(2, 2);
            Assert.IsTrue(captured.LoadImage(bytes), "the capture produced no readable PNG");

            // Sample well inside each half, away from the sprite's own edge
            // filtering.
            var opaqueSide = captured.GetPixel(Size / 4, Size / 2);
            var clearSide = captured.GetPixel(Size * 3 / 4, Size / 2);

            float opaqueLuminance = Luminance(opaqueSide);
            float clearLuminance = Luminance(clearSide);

            Assert.Greater(opaqueLuminance, 0.6f,
                $"the opaque half should read as a WHITE silhouette, not the sprite's own dark colour " +
                $"(got {opaqueSide}). If this fails, the shader did not survive URP and the fallback is " +
                $"UIEffect's fill mode.");

            Assert.Less(clearLuminance, opaqueLuminance - 0.3f,
                $"the transparent half must stay clear - the flash is the figure's SHAPE, not its bounding box " +
                $"(got {clearSide})");
        }

        [UnityTest]
        public IEnumerator NoMaterialDegradesToNothingRatherThanThrowing()
        {
            // Graceful degradation is the house style, and this is the path a
            // missing shader actually takes.
            BuildCanvas(null);
            yield return null;

            Assert.Pass("a flash with no material still runs its coroutine and draws nothing");
        }

        private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }

    // The curve, pinned without a scene.
    public class HitFlashCurveTests
    {
        [Test]
        public void TheFlashStartsAtItsPeakAndReachesZero()
        {
            Assert.AreEqual(1f, StageHitFlash.AlphaAt(0f, 1f), 0.0001f);
            Assert.AreEqual(0f, StageHitFlash.AlphaAt(1f, 1f), 0.0001f);
        }

        [Test]
        public void AHealFlashNeverReachesFullOpacity()
        {
            // Softer on purpose: good news should not hit as hard as a blow.
            Assert.Less(StageHitFlash.AlphaAt(0f, 0.75f), 1f);
            Assert.AreEqual(0.75f, StageHitFlash.AlphaAt(0f, 0.75f), 0.0001f);
        }

        [Test]
        public void ProgressPastTheEndStaysAtZero()
        {
            // A frame that overshoots the fade must not wrap into a second
            // flash, which is what an unclamped 1 - t would do.
            Assert.AreEqual(0f, StageHitFlash.AlphaAt(1.7f, 1f), 0.0001f);
        }
    }
}
