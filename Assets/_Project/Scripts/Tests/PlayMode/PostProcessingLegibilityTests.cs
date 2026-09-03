using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Does URP's post-processing hurt the fight HUD's legibility?
    //
    // The plan named this as a question to answer BY MEASUREMENT rather than by
    // prediction, because the concern is real and the mitigation is expensive: a
    // second root-level Overlay canvas for text-critical HUD, which composites
    // after post and therefore sits outside it. Worth doing if the numbers say
    // so; a lot of structural risk if they do not.
    //
    // THE MEASUREMENT HAS TO BE VOLUME-ON AGAINST VOLUME-OFF, on the same
    // pixels. An earlier pass of mine compared the party plate's corner against
    // mid-screen and reported the plate was darker -- which is true, and says
    // nothing whatsoever about the vignette, because those are different
    // CONTENT. A dark plate on a bright forest is dark with or without any
    // post-processing at all. The only honest comparison is the same region
    // twice with one variable changed, which is what this does.
    //
    // GRAPHICS-GATED: headless, camera.Render() is a no-op and ReadPixels
    // returns garbage, so this self-skips exactly as the other pixel tests do.
    // Run it with tools/graphics_tests.ps1.
    public class PostProcessingLegibilityTests
    {
        // The reference resolution every coordinate in FightScreen is authored
        // against. Captures are taken at exactly this size so canvas units and
        // pixels are the same thing and the crops below need no scaling.
        private const int Width = 1920;
        private const int Height = 1080;

        // FightScreen.BuildPartyPlate: the plate sits at (-694, -385.858),
        // 452x228.283 (a Blue 2:1 container now, was a flat 452x216
        // panel_crimson sprite -- the bottom edge stayed at -500, so only the
        // centre and height moved), and PartyHpValue sits at plate-local
        // (194, 4) with a right pivot, 70x20 -- content coordinates are
        // unchanged by the container move, only the plate's own world centre
        // is. Stated here rather than read from the live rect on purpose --
        // if the tree moves the plate, this test should fail and be re-aimed,
        // not silently follow it somewhere the vignette is weaker.
        // CENTRE and size, matching Place.At's own convention -- Place.At is
        // centre-anchored, so treating these as corners would aim every crop
        // half a plate up and to the left, which is the kind of mistake that
        // still produces plausible numbers.
        private static readonly Rect PlateRect = CentredAt(-694f, -385.858f, 452f, 228.283f);
        private static readonly Rect HpValueRect = CentredAt(-694f + 194f - 35f, -385.858f + 4f, 70f, 20f);

        private static Rect CentredAt(float centreX, float centreY, float width, float height) =>
            new Rect(centreX - width * 0.5f, centreY - height * 0.5f, width, height);

        private Canvas _canvas;
        private Volume _volume;

        [TearDown]
        public void RestoreVolume()
        {
            if (_volume != null) _volume.enabled = true;
        }

        private IEnumerator LoadFightWithRealNumbers()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            // A bound session, not the resting scene: the plate has to be
            // showing real text for a text-contrast measurement to mean
            // anything.
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            hero.Signature = new SignatureResource("wool", "Wool", 16, 0, 2, 0);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(3));
            session.Begin();
            fight.Bind(session, EncounterClass.Normal);

            yield return null;

            _canvas = fight.GetComponentInParent<Canvas>();
            Assert.IsNotNull(_canvas, "the fight panel is not under a Canvas");

            _volume = Object.FindAnyObjectByType<Volume>();
            Assert.IsNotNull(_volume, "the Fight scene has no GlobalVolume - SceneBuilder.CreateGlobalVolume did not run");
        }

        private Texture2D Capture()
        {
            string path = Path.Combine(Path.GetTempPath(), "pp-legibility-" + System.Guid.NewGuid().ToString("N") + ".png");
            CanvasCapture.RenderToFile(_canvas, path, Width, Height);

            var bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(bytes), "the capture produced no readable PNG");
            return texture;
        }

        // Canvas space (origin centre, +y up) to texture space (origin bottom
        // left, +y up). Only y needs the flip that is NOT here: Texture2D's
        // origin is already bottom-left, so both agree on +y.
        private static IEnumerable<Color> Pixels(Texture2D texture, Rect canvasRect)
        {
            int left = Mathf.RoundToInt(canvasRect.x + Width * 0.5f);
            int bottom = Mathf.RoundToInt(canvasRect.y + Height * 0.5f);

            for (int y = bottom; y < bottom + canvasRect.height; y++)
            {
                for (int x = left; x < left + canvasRect.width; x++)
                {
                    yield return texture.GetPixel(x, y);
                }
            }
        }

        // Relative luminance, sRGB weights. The same quantity WCAG's contrast
        // ratio is built on, so the threshold below is a real accessibility
        // number rather than one invented for this test.
        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        private static float MeanLuminance(IEnumerable<Color> pixels)
        {
            float total = 0f;
            int count = 0;
            foreach (var pixel in pixels) { total += Luminance(pixel); count++; }
            return count == 0 ? 0f : total / count;
        }

        // Contrast between the brightest thing in the patch (the glyph strokes)
        // and the darkest (the plate behind them). Sampling extremes rather than
        // means is the point: a mean would average the text away and report the
        // background's contrast with itself.
        private static float ContrastRatio(IEnumerable<Color> pixels)
        {
            float brightest = 0f;
            float darkest = 1f;
            foreach (var pixel in pixels)
            {
                float luminance = Luminance(pixel);
                if (luminance > brightest) brightest = luminance;
                if (luminance < darkest) darkest = luminance;
            }

            return (brightest + 0.05f) / (darkest + 0.05f);
        }

        [UnityTest]
        public IEnumerator PostProcessingActuallyReachesTheCapture()
        {
            // THE SANITY CHECK, and it has to come first. If the volume makes no
            // measurable difference, every legibility number below is a
            // measurement of nothing and would pass no matter how bad the
            // grading got. This test failing means the harness is broken, not
            // that the grading is fine.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            yield return LoadFightWithRealNumbers();

            _volume.enabled = true;
            yield return null;
            float graded = MeanLuminance(Pixels(Capture(), PlateRect));

            _volume.enabled = false;
            yield return null;
            float raw = MeanLuminance(Pixels(Capture(), PlateRect));

            Assert.AreNotEqual(raw, graded,
                $"the volume changed nothing at all (both {graded:F4}) - post-processing is not reaching " +
                $"the capture, so no legibility measurement here means anything");

            Debug.Log($"[legibility] party plate mean luminance: graded {graded:F4}, ungraded {raw:F4} " +
                      $"({(graded - raw) / raw * 100f:+0.0;-0.0}%)");
        }

        [UnityTest]
        public IEnumerator ThePartyPlatesNumbersStayReadableUnderTheVignette()
        {
            // The actual question. The vignette is centred, so the party plate's
            // bottom-left corner is about as far into it as anything on this
            // screen gets -- if legibility survives here it survives everywhere.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            yield return LoadFightWithRealNumbers();

            _volume.enabled = true;
            yield return null;
            float graded = ContrastRatio(Pixels(Capture(), HpValueRect));

            _volume.enabled = false;
            yield return null;
            float raw = ContrastRatio(Pixels(Capture(), HpValueRect));

            Debug.Log($"[legibility] HP value contrast ratio: graded {graded:F2}:1, ungraded {raw:F2}:1");

            // 4.5:1 is WCAG AA for body text. A HUD readout at 12pt is body
            // text by any reasonable reading, and this is the number to argue
            // with if the grading ever changes -- not a feeling about a
            // screenshot.
            Assert.Greater(graded, 4.5f,
                $"the HP readout falls below WCAG AA under the current grading ({graded:F2}:1). " +
                $"THIS is the failure that justifies a second root-level Overlay canvas for text-critical " +
                $"HUD; do not reach for that mitigation while this passes.");
        }

        [UnityTest]
        public IEnumerator TheVignetteCostsLessThanAQuarterOfThePlatesBrightness()
        {
            // A guard on the direction of travel rather than on a single value.
            // Vignette intensity is one number in PipelineBuilder, and nothing
            // else in the project would notice it being raised until the screen
            // was already too dark to read.
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op and ReadPixels returns garbage.");
                yield break;
            }

            yield return LoadFightWithRealNumbers();

            _volume.enabled = true;
            yield return null;
            float graded = MeanLuminance(Pixels(Capture(), PlateRect));

            _volume.enabled = false;
            yield return null;
            float raw = MeanLuminance(Pixels(Capture(), PlateRect));

            Assert.Greater(raw, 0f, "the ungraded plate is pure black, which means the crop is aimed at nothing");

            float retained = graded / raw;
            Debug.Log($"[legibility] the plate keeps {retained * 100f:F1}% of its ungraded brightness");

            Assert.Greater(retained, 0.75f,
                $"post-processing is taking more than a quarter of the party plate's brightness " +
                $"({retained * 100f:F1}% retained). Raising the vignette further needs the Overlay-canvas " +
                $"mitigation, not just a nudge back.");
        }
    }
}
