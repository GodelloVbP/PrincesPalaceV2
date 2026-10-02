using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Stats;

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

        // The harness's own handle on the Image the flash writes. StageHitFlash
        // .image is `internal` and InternalsVisibleTo is granted to the Editor
        // assembly ONLY (.claude/rules/ui.md "Wiring"), so a PlayMode test reads
        // the colour off the Image it built rather than reaching into the
        // component -- which is the rule working, not a workaround for it.
        private Image _image;

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

        private void BuildCanvas(Material material) => BuildCanvas(material, flash => flash.Flash());

        // The flash is fired by the CALLER, so a test can ask for the white
        // one or for a typed one without a second copy of the harness.
        private void BuildCanvas(Material material, System.Action<StageHitFlash> fire)
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
            _image = image;

            var flash = imageGo.AddComponent<StageHitFlash>();
            flash.Attach(image);
            flash.SetSprite(image.sprite);
            fire(flash);
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

        // ---- the typed flash (owner 2026-09-19) ------------------------------
        //
        // "Flash like the white but THEN for example green poison, red for
        // fire." Two pulses, not one tinted one: the white impact exactly as
        // it always was, and then the element behind it at a little over half
        // strength. The first attempt tinted the single pulse and the tail
        // frame showed three monsters repainted green, which is the thing
        // "but THEN" rules out.
        //
        // SO EVERY TEST DOWN HERE HAS TO SAY WHICH PULSE IT IS LOOKING AT,
        // and wait for it. Sampled one frame in, a poison flash is white and
        // correct.

        // Past the end of the hit pulse at the speed FreezeTime pins. Derived
        // rather than guessed: at 0.05x the pulse alone is seconds long, and a
        // frame count would be a bet on the machine.
        private static float PastThePulse()
        {
            return FightBeatPlayer.Scaled(StageHitFlash.HitHoldSeconds + StageHitFlash.HitFadeSeconds)
                   + FightBeatPlayer.Scaled(StageHitFlash.HitHoldSeconds) * 0.5f;
        }

        [UnityTest]
        public IEnumerator APoisonFlashIsGreenWhereTheSpriteIsOpaque()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            var material = LoadFlashMaterial();
            Assert.IsNotNull(material, "PrincesPalace/UIHitFlash did not survive the URP move.");

            BuildCanvas(material, flash => flash.Flash(DamageType.Poison));

            // At the peak, inside the hold.
            yield return null;

            string path = Path.Combine(Path.GetTempPath(), "pp-hitflash-poison-" +
                                       System.Guid.NewGuid().ToString("N") + ".png");
            CanvasCapture.RenderToFile(_canvas, path, Size, Size);

            var bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var captured = new Texture2D(2, 2);
            Assert.IsTrue(captured.LoadImage(bytes), "the capture produced no readable PNG");

            var opaqueSide = captured.GetPixel(Size / 4, Size / 2);
            var clearSide = captured.GetPixel(Size * 3 / 4, Size / 2);

            // FightHudPalette.DamageTypePoison is #A8E63C: green over red over
            // blue, with real daylight between green and blue. Asserted as an
            // ORDERING rather than against the three channel values, so
            // retuning the acid green does not break a test about the flash.
            Assert.Greater(opaqueSide.g, opaqueSide.r,
                $"a poison flash must read green, not white or red (got {opaqueSide})");
            Assert.Greater(opaqueSide.g, opaqueSide.b + 0.2f,
                $"a poison flash must read green, not white or red (got {opaqueSide})");

            Assert.Less(Luminance(clearSide), Luminance(opaqueSide) - 0.3f,
                $"the transparent half must stay clear whatever the tint (got {clearSide})");
        }

        [UnityTest]
        public IEnumerator APhysicalFlashPeaksInALightRed()
        {
            // Read off the Image rather than the framebuffer so this holds
            // headless too. #ED423D lerped halfway to white.
            BuildCanvas(LoadFlashMaterial(), f => f.Flash(DamageType.Physical));
            yield return null;

            Assert.AreEqual(0.9647f, _image.color.r, 0.002f);
            Assert.AreEqual(0.6294f, _image.color.g, 0.002f);
            Assert.AreEqual(0.6196f, _image.color.b, 0.002f);
            Assert.AreEqual(1f, _image.color.a, 0.001f);
        }

        [UnityTest]
        public IEnumerator AFireFlashPeaksInALightOrangeAndIsOffAfterOnePulse()
        {
            BuildCanvas(LoadFlashMaterial(), f => f.Flash(DamageType.Fire));
            yield return null;

            // #FF6A2A lerped halfway to white.
            Assert.AreEqual(1f, _image.color.r, 0.002f);
            Assert.AreEqual(0.7078f, _image.color.g, 0.002f);
            Assert.AreEqual(0.5824f, _image.color.b, 0.002f);
            Assert.AreEqual(1f, _image.color.a, 0.001f);

            yield return new WaitForSeconds(PastThePulse());
            yield return null;

            Assert.IsFalse(_image.enabled, "a hit flashes once: no second pulse after the first");
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

        // ---- which colour a type flashes in ---------------------------------

        [Test]
        public void PhysicalWearsItsPaletteTokenLightened()
        {
            var physical = StageHitFlash.TintFor(DamageType.Physical);

            Assert.AreEqual(0.9647f, physical.r, 0.002f);
            Assert.AreEqual(0.6294f, physical.g, 0.002f);
            Assert.AreEqual(0.6196f, physical.b, 0.002f);
        }

        [Test]
        public void AnElementalFlashWearsTheSameColourAsItsDamageNumber()
        {
            // The literal, not a second call into ForDamageType: the flash is
            // the figure's colour pulled halfway to white (#A8E63C poison).
            var poison = StageHitFlash.TintFor(DamageType.Poison);

            Assert.AreEqual((1f + 0xA8 / 255f) / 2f, poison.r, 0.005f);
            Assert.AreEqual((1f + 0xE6 / 255f) / 2f, poison.g, 0.005f);
            Assert.AreEqual((1f + 0x3C / 255f) / 2f, poison.b, 0.005f);
            Assert.AreEqual(1f, poison.a, 0.005f);
        }

        // ---- how many pulses, and how hard --------------------------------

        [Test]
        public void TheHitConstantsArePinned()
        {
            Assert.AreEqual(3, StageHitFlash.HitPeakFrames);
            Assert.AreEqual(0.05f, StageHitFlash.HitHoldSeconds, 0.0001f);
            Assert.AreEqual(0.10f, StageHitFlash.HitFadeSeconds, 0.0001f);
            Assert.AreEqual(0.5f, StageHitFlash.HitTintAmount, 0.0001f);
            Assert.AreEqual(1f, StageHitFlash.HitPeakAlpha, 0.0001f);
        }

        [Test]
        public void EveryTypeIsOnePulseOfItsLightenedColour()
        {
            foreach (var type in System.Enum.GetValues(typeof(DamageType)).Cast<DamageType>())
            {
                var pulses = StageHitFlash.PulsesFor(type);

                Assert.AreEqual(1, pulses.Length, $"{type} flashes in {pulses.Length} pulses");
                Assert.AreEqual(StageHitFlash.TintFor(type), pulses[0].Tint);
                Assert.AreEqual(0.05f, pulses[0].Hold, 0.0001f);
                Assert.AreEqual(0.10f, pulses[0].Fade, 0.0001f);
                Assert.AreEqual(1f, pulses[0].Peak, 0.0001f);
            }
        }

        // A MULTI-PACKET CAST FLASHES ONE PULSE PER PACKET: Fire's light for
        // 3 frames (the stagger), then Ice's for the ordinary hit hold and fade.
        [Test]
        public void ATwoPacketCastFlashesEachElementStaggeredThreeFrames()
        {
            var pulses = StageHitFlash.PacketPulses(new[]
            {
                new PrincesPalace.Domain.Combat.Session.BeatPacket(DamageType.Fire, 6),
                new PrincesPalace.Domain.Combat.Session.BeatPacket(DamageType.Ice, 4),
            });

            Assert.AreEqual(2, pulses.Length);
            Assert.AreEqual(3, StageHitFlash.StaggerFrames);
            Assert.AreEqual(0.05f, pulses[0].Hold, 0.0001f);
            Assert.AreEqual(0f, pulses[0].Fade);
            Assert.AreEqual(StageHitFlash.TintFor(DamageType.Fire), pulses[0].Tint);
            Assert.AreEqual(StageHitFlash.TintFor(DamageType.Ice), pulses[1].Tint);
            Assert.AreNotEqual(pulses[0].Tint, pulses[1].Tint);
            Assert.AreEqual(0.05f, pulses[1].Hold, 0.0001f);
            Assert.AreEqual(0.10f, pulses[1].Fade, 0.0001f);
        }

        [Test]
        public void AFireHitPeaksInALightOrange()
        {
            var tint = StageHitFlash.PulsesFor(DamageType.Fire)[0].Tint;

            Assert.AreEqual(1f, tint.r, 0.002f);
            Assert.AreEqual(0.7078f, tint.g, 0.002f);
            Assert.AreEqual(0.5824f, tint.b, 0.002f);
        }

        [Test]
        public void EveryDamageTypeHasATintAndNoneOfThemIsInvisible()
        {
            var all = System.Enum.GetValues(typeof(DamageType)).Cast<DamageType>().ToList();
            Assert.GreaterOrEqual(all.Count, 11, "DamageType shrank; this test stopped covering it");

            foreach (var type in all)
            {
                var tint = StageHitFlash.TintFor(type);

                // A silhouette flashed in near-black is a figure briefly
                // blanking out, which reads as a rendering fault rather than
                // as a hit. The palette has no such token today and this is
                // what says so when one is added.
                Assert.Greater(tint.r + tint.g + tint.b, 0.45f, $"{type} flashes almost black");
                Assert.AreEqual(1f, tint.a, 0.001f, $"{type} flashes at less than full opacity");
            }
        }
    }
}
