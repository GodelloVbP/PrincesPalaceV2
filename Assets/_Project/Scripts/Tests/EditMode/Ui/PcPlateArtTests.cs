using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // MEASURED vs LITERAL, the same arrangement UiKitVisiblePadTests uses for
    // ContainerArt/ButtonPlateArt and for the same reason: every number in
    // PcPlateArt was pasted by hand out of `py tools/normalize_pc_plates.py`,
    // and a literal typed by hand is the thing that drifts. This scans the
    // committed PNGs off disk and re-derives them.
    //
    // IT IS EXPECTED TO GO RED the day the owner redelivers a plate. Whoever
    // does that re-runs the tool, pastes its C#-pasteable block into
    // PcPlateArt, and re-pins the literals here -- deliberately, in one
    // commit, rather than discovering months later that the head zone moved
    // and a name has been sitting under a muzzle.
    public class PcPlateArtTests
    {
        // The same alpha cutoff every other pad in this project is measured
        // at -- see tools/measure_ui_kit.py's header for why 32.
        private const int PinThreshold = 32;

        // One file per playable character, named by CHARACTER ID (what
        // characters.json's plateArt addresses), not by the owner's own
        // delivery names (shawn/bjorn/odette).
        private static readonly string[] Plates = { "pc_sheep", "pc_bear", "pc_owl" };

        private static string PlatesDir() =>
            Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources", "Plates");

        private static string PathFor(string stem) => Path.Combine(PlatesDir(), stem + ".png");

        // THE WHOLE POINT OF tools/normalize_pc_plates.py, asserted. The three
        // sources arrived at three different shapes (2020x360, 2091x277,
        // 2045x313 -- 5.61:1, 7.55:1, 6.53:1) and the HUD draws all three at
        // one size, so a plate that is not on the shared canvas would be
        // letterboxed or stretched inside its own rect with nothing failing.
        [Test]
        public void EveryPlateIsOnTheOneSharedCanvas()
        {
            foreach (string stem in Plates)
            {
                string path = PathFor(stem);
                Assert.IsTrue(File.Exists(path), $"{stem}.png is missing -- run `py tools/normalize_pc_plates.py`");

                var image = PngAlpha.Read(path);
                Assert.AreEqual(2048, image.Width, $"{stem}: width");
                Assert.AreEqual(365, image.Height, $"{stem}: height");
            }
        }

        // 5.6111 is Shawn's own delivered aspect, which is the SMALLEST of
        // the three and therefore the only one the tool can bring the other
        // two to (it removes field width; it never adds height). Pinned
        // against the canvas rather than restated: 2048/365 = 5.61096.
        [Test]
        public void TheAspectLiteralMatchesTheCanvasOnDisk()
        {
            var image = PngAlpha.Read(PathFor(Plates[0]));
            float measured = (float)image.Width / image.Height;

            Assert.AreEqual(measured, PcPlateArt.Aspect, 0.001f,
                "PcPlateArt.Aspect no longer describes the committed PNG -- re-run " +
                "`py tools/normalize_pc_plates.py` and paste its block");
        }

        // ZERO PAD ON EVERY EDGE, which is a property the layout leans on:
        // FightScreen places the bottom plate with Ui.CentreYForVisibleBottom
        // and PcPlateArt.VisiblePad.Bottom, so a plate that grew a
        // transparent halo would sit visibly above the line the verb column
        // ends on while its RECT still agreed exactly.
        //
        // 1px of band, not 0: the LANCZOS resample leaves a sub-threshold
        // ramp on one column of one plate (bear's left).
        [Test]
        public void NoPlateCarriesATransparentHalo()
        {
            var pad = PcPlateArt.VisiblePad;
            Assert.AreEqual(0f, pad.Left + pad.Right + pad.Top + pad.Bottom, 0.0001f,
                "PcPlateArt.VisiblePad claims a halo -- the plates are cropped to their alpha bbox");

            foreach (string stem in Plates)
            {
                var image = PngAlpha.Read(PathFor(stem));
                var (left, top, right, bottom) = EdgePads(image, PinThreshold);

                Assert.LessOrEqual(left, 1, $"{stem}: left pad");
                Assert.LessOrEqual(top, 1, $"{stem}: top pad");
                Assert.LessOrEqual(right, 1, $"{stem}: right pad");
                Assert.LessOrEqual(bottom, 1, $"{stem}: bottom pad");
            }
        }

        // THE HEAD ZONE IS A REAL MEASUREMENT, not a taste call, and this is
        // the assert that keeps it one. 0.1904 is the widest of the three
        // heads (sheep 0.1904, bear 0.1816, owl 0.1431) as the tool measures
        // them; the check here is the cheap, independent half -- the right
        // end of every plate must actually be occupied, and the reserved band
        // must not have shrunk below what the art needs.
        //
        // Measured by ALPHA-independent means, because the head is embossed:
        // it has no alpha of its own and barely any hue, so what separates it
        // from empty field is VARIANCE down a column.
        [Test]
        public void TheReservedHeadZoneCoversTheEmbossedHeadOnEveryPlate()
        {
            foreach (string stem in Plates)
            {
                var image = PngAlpha.Read(PathFor(stem), keepLuminance: true);
                int reserved = (int)(image.Width * PcPlateArt.HeadZoneFrac);
                int fieldStart = (int)(image.Width * 0.15f);
                int fieldEnd = (int)(image.Width * 0.60f);

                double field = MeanColumnVariance(image, fieldStart, fieldEnd);
                double head = MeanColumnVariance(image, image.Width - reserved, image.Width - 8);
                double justLeftOfIt = MeanColumnVariance(image, image.Width - reserved - 120,
                    image.Width - reserved - 20);

                Assert.Greater(head, field * 2.0,
                    $"{stem}: the reserved head zone is empty field -- either the head moved or " +
                    "PcPlateArt.HeadZoneFrac is reserving the wrong end");

                Assert.Less(justLeftOfIt, field * 2.0,
                    $"{stem}: the head reaches LEFT of the reserved zone -- text placed against " +
                    "PcPlateArt.HeadZoneFrac would run under it. Re-measure with " +
                    "`py tools/normalize_pc_plates.py`.");
            }
        }

        // THE ACTING DECAL, and the one thing about it the layout depends on:
        // it is authored at plate + GlowPadFrac on every side, and FightScreen
        // stretches it to exactly that. A decal at any other proportion draws
        // its halo squashed or cropped.
        [Test]
        public void TheGlowDecalIsAuthoredAtThePadTheScreenStretchesItTo()
        {
            string path = Path.Combine(PlatesDir(), "pc_plate_glow.png");
            Assert.IsTrue(File.Exists(path), "pc_plate_glow.png is missing -- run `py tools/normalize_pc_plates.py`");

            var plate = PngAlpha.Read(PathFor(Plates[0]));
            var glow = PngAlpha.Read(path);

            // CHECKED AS A FRACTION, WITHIN A PIXEL, not as an exact pixel
            // count. 365 * 0.10 lands on 36.5, and Python rounds a tie to
            // even (36) where a C# float multiply overshoots to 36.500002 and
            // rounds up (37) -- so an exact comparison here pins which
            // language did the arithmetic rather than whether the decal is
            // the right shape. What the layout actually depends on is that
            // FightScreen stretches the decal by the same FRACTION the tool
            // padded it by; half a pixel of tie-breaking on a 2120px canvas
            // is not a thing anyone can see.
            float padX = (glow.Width - plate.Width) * 0.5f;
            float padY = (glow.Height - plate.Height) * 0.5f;

            Assert.AreEqual(padX, padY, 0.51f, "the decal is padded unevenly on the two axes");
            Assert.AreEqual(plate.Height * PcPlateArt.GlowPadFrac, padY, 0.51f,
                "the decal's authored halo no longer matches PcPlateArt.GlowPadFrac, which is what " +
                "FightScreen stretches it by -- the halo would draw squashed or cropped");
        }

        // The baked defaults are what an unrefreshed scene draws, so each one
        // has to name a file that is actually there -- a missing key is a
        // SceneBuilder warning and a blank plate, not a failure.
        [Test]
        public void EveryBakedDefaultNamesACommittedPlate()
        {
            Assert.AreEqual(Plates.Length, PcPlateArt.BakedDefaults.Length,
                "one baked default per shipped starter");

            foreach (string key in PcPlateArt.BakedDefaults)
            {
                string path = Path.Combine(RepoTree.Root(), key.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(path), $"the baked key '{key}' names no file");
            }

            Assert.AreEqual(Plates.Length, PcPlateArt.BakedDefaults.Distinct().Count(),
                "three slots baked with one plate would read as three copies of one character");
        }

        // Same walk tools/measure_ui_kit.py's edge_pads does.
        private static (int Left, int Top, int Right, int Bottom) EdgePads(PngAlpha image, int threshold)
        {
            int w = image.Width, h = image.Height;

            int left = 0;
            while (left < w && MaxAlphaInColumn(image, left) <= threshold) left++;
            int right = 0;
            while (right < w && MaxAlphaInColumn(image, w - 1 - right) <= threshold) right++;
            int top = 0;
            while (top < h && MaxAlphaInRow(image, top) <= threshold) top++;
            int bottom = 0;
            while (bottom < h && MaxAlphaInRow(image, h - 1 - bottom) <= threshold) bottom++;

            return (left, top, right, bottom);
        }

        private static int MaxAlphaInColumn(PngAlpha image, int x)
        {
            int max = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int a = image.At(x, y);
                if (a > max) max = a;
            }

            return max;
        }

        private static int MaxAlphaInRow(PngAlpha image, int y)
        {
            int max = 0;
            for (int x = 0; x < image.Width; x++)
            {
                int a = image.At(x, y);
                if (a > max) max = a;
            }

            return max;
        }

        // Mean per-column luminance variance over an x band, sampled down the
        // plate's own middle (skipping the rim rows top and bottom, which are
        // present in every column and would drown the signal).
        private static double MeanColumnVariance(PngAlpha image, int fromX, int toX)
        {
            int y0 = (int)(image.Height * 0.15f);
            int y1 = (int)(image.Height * 0.85f);
            double total = 0;
            int columns = 0;

            for (int x = System.Math.Max(0, fromX); x < System.Math.Min(image.Width, toX); x++)
            {
                double sum = 0, sumSq = 0;
                int n = 0;
                for (int y = y0; y < y1; y++)
                {
                    double lum = image.LuminanceAt(x, y);
                    sum += lum;
                    sumSq += lum * lum;
                    n++;
                }

                if (n == 0) continue;
                double mean = sum / n;
                total += System.Math.Max(0, sumSq / n - mean * mean);
                columns++;
            }

            return columns == 0 ? 0 : total / columns;
        }
    }
}
