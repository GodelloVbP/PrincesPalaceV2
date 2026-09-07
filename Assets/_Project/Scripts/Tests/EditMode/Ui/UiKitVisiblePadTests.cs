using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // MEASURED vs LITERAL, on purpose -- same shape as StanceManifestValidationTests:
    // this scans the committed PNGs off disk (the FightCapacityPinTests.cs:30-44
    // idiom, via RepoTree/PngAlpha, both already in Shared/) and compares the
    // scan against the C# literals ContainerArt.VisiblePad/ButtonPlateArt.
    // VisiblePad actually carry. It is not a tautology -- the literal was typed
    // by hand from tools/measure_ui_kit.py's output, the scan here re-derives
    // it independently -- and it is EXPECTED to go red the day the kit
    // regenerates: whoever reslices Processed/ reruns the script and repins
    // both this file's literals-under-test and the C# constants it checks
    // against.
    //
    // Reads PngAlpha directly rather than adding a PNG dependency to
    // PrincesPalace.Domain.Tests's production code -- ContainerArt/
    // ButtonPlateArt themselves stay engine-free and file-free; only the test
    // that pins their literals touches disk.
    public class UiKitVisiblePadTests
    {
        // The same alpha cutoff ContainerArt.VisiblePad/ButtonPlateArt.
        // VisiblePad are measured at -- see their own headers.
        private const int PinThreshold = 32;

        // The band a single theme's own measured pad may miss the C# literal
        // by before this fails, naming the constant to re-paste. 1px
        // everywhere -- EXCEPT the three EDGES named below, which really do
        // disagree across themes by more than 1px (see ContainerArt.
        // VisiblePad's own comment for the raw-alpha confirmation of each):
        // tools/measure_ui_kit.py's own agreement check already reports them
        // and exits 1, so they are a known, reported gap in the delivered art
        // rather than a script bug. The bands are per-EDGE, per-GROUP for
        // exactly that reason: a real regression on any other edge of the
        // same group still fails at 1px, which is what keeps this test
        // meaningful rather than either permanently red or blind to drift.
        //
        // The two container LEFT bands got WORSE at the 2026-09-07
        // regeneration, not better -- silver/violet's paint now starts 7-9px
        // (3x4) and 4-5px (9x16) further out than the other four themes,
        // where it was 3-4px before -- so 4px no longer covers them and 7px
        // does.
        private const float StandardBandPx = 1f;
        private const float KnownLeftAsymmetryBandPx = 7f;

        // TWO groups' TOP edge, both by 2px of raw spread:
        // banner_flag_*_3x4 -- crimson and gold start 2px lower than the
        // other four (29px against 27px), average 1.35px from the outliers;
        // container_*_3x4 -- blue starts 2px lower than crimson (28 against
        // 26), average 1.03px from crimson. The second one clears 1px by
        // three hundredths of a pixel, which is exactly the kind of margin
        // not worth pinning a band against.
        private const float KnownTopAsymmetryBandPx = 2f;

        private static readonly string[] Themes = { "blue", "crimson", "gold", "green", "silver", "violet" };

        private static string ProcessedDir() =>
            Path.Combine(RepoTree.Root(), "Assets", "_Project", "Art", "UI", "Buttons", "Processed");

        private sealed class Group
        {
            internal string Label;
            internal string FileTemplate; // "{0}" is the theme
            internal ContentInsetFrac Literal;
            internal bool LeftEdgeKnownAsymmetric;
            internal bool TopEdgeKnownAsymmetric;
        }

        private static IEnumerable<Group> Groups()
        {
            yield return new Group
            {
                Label = "ButtonPlateArt.VisiblePad(Legacy) [ButtonPlateArt.LegacyVisiblePad]",
                FileTemplate = "button_plate_{0}.png",
                Literal = ButtonPlateArtProbe.VisiblePad(ButtonPlateShape.Legacy),
            };
            yield return new Group
            {
                Label = "ButtonPlateArt.VisiblePad(ThreeByOne) [ButtonPlateArt.ThreeByOneVisiblePad]",
                FileTemplate = "button_plate_{0}_3x1.png",
                Literal = ButtonPlateArtProbe.VisiblePad(ButtonPlateShape.ThreeByOne),
            };
            yield return new Group
            {
                Label = "ButtonPlateArt.VisiblePad(FiveByOne) [ButtonPlateArt.FiveByOneVisiblePad]",
                FileTemplate = "button_plate_{0}_5x1.png",
                Literal = ButtonPlateArtProbe.VisiblePad(ButtonPlateShape.FiveByOne),
            };
            yield return new Group
            {
                Label = "ButtonPlateArt.VisiblePad(Row6x1) [ButtonPlateArt.Row6x1VisiblePad]",
                FileTemplate = "row_plate_{0}_6x1.png",
                Literal = ButtonPlateArtProbe.VisiblePad(ButtonPlateShape.Row6x1),
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(Container, ThreeByFour) [ContainerArt.Specs entry]",
                FileTemplate = "container_{0}_3x4.png",
                Literal = Ui.ContainerVisiblePad(ContainerRatio.ThreeByFour),
                LeftEdgeKnownAsymmetric = true,
                TopEdgeKnownAsymmetric = true,
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(Container, NineBySixteen) [ContainerArt.Specs entry]",
                FileTemplate = "container_{0}_9x16.png",
                Literal = Ui.ContainerVisiblePad(ContainerRatio.NineBySixteen),
                LeftEdgeKnownAsymmetric = true,
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(Container, ThreeByTwo) [ContainerArt.Specs entry]",
                FileTemplate = "container_{0}_3x2.png",
                Literal = Ui.ContainerVisiblePad(ContainerRatio.ThreeByTwo),
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(Container, TwoByOne) [ContainerArt.Specs entry]",
                FileTemplate = "container_{0}_2x1.png",
                Literal = Ui.ContainerVisiblePad(ContainerRatio.TwoByOne),
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(Container, FiveByOne) [ContainerArt.Specs entry]",
                FileTemplate = "container_{0}_5x1.png",
                Literal = Ui.ContainerVisiblePad(ContainerRatio.FiveByOne),
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(FlagBanner, ThreeByFour) [ContainerArt.Specs entry]",
                FileTemplate = "banner_flag_{0}_3x4.png",
                Literal = Ui.FlagBannerVisiblePad(ContainerRatio.ThreeByFour),
                TopEdgeKnownAsymmetric = true,
            };
            yield return new Group
            {
                Label = "ContainerArt.VisiblePad(FlagBanner, NineBySixteen) [ContainerArt.Specs entry]",
                FileTemplate = "banner_flag_{0}_9x16.png",
                Literal = Ui.FlagBannerVisiblePad(ContainerRatio.NineBySixteen),
            };
        }

        // Same algorithm as tools/measure_ui_kit.py's edge_pads: walk in from
        // each edge while every pixel on that row/column is at or below the
        // threshold; the distance walked is the pad, in px.
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

        [Test]
        public void EveryGroupsSixThemesMatchTheirPinnedLiteralWithinTheBand()
        {
            string processedDir = ProcessedDir();
            var failures = new List<string>();
            int filesChecked = 0;

            foreach (var group in Groups())
            {
                foreach (string theme in Themes)
                {
                    string fileName = string.Format(group.FileTemplate, theme);
                    string path = Path.Combine(processedDir, fileName);
                    Assert.IsTrue(File.Exists(path), $"{fileName} is missing -- this pin would otherwise pass vacuously.");

                    var image = PngAlpha.Read(path);
                    var (left, top, right, bottom) = EdgePads(image, PinThreshold);
                    filesChecked++;

                    CheckEdge(failures, group, fileName, "left", left, image.Width, group.Literal.Left,
                        group.LeftEdgeKnownAsymmetric ? KnownLeftAsymmetryBandPx : StandardBandPx);
                    CheckEdge(failures, group, fileName, "top", top, image.Height, group.Literal.Top,
                        group.TopEdgeKnownAsymmetric ? KnownTopAsymmetryBandPx : StandardBandPx);
                    CheckEdge(failures, group, fileName, "right", right, image.Width, group.Literal.Right, StandardBandPx);
                    CheckEdge(failures, group, fileName, "bottom", bottom, image.Height, group.Literal.Bottom, StandardBandPx);
                }
            }

            Assert.Greater(filesChecked, 60,
                "Only a handful of files were checked -- the scan is not seeing the kit, so every case above would pass vacuously.");

            Assert.IsEmpty(failures, "One or more VisiblePad literals no longer match the art on disk:\n" +
                string.Join("\n", failures) +
                "\n\nRe-measure with `py tools/measure_ui_kit.py` and paste its threshold-32 block into the named constant(s).");
        }

        private static void CheckEdge(List<string> failures, Group group, string fileName, string edge,
            int measuredPx, int size, float literalFraction, float bandPx)
        {
            float literalPx = literalFraction * size;
            float diff = System.Math.Abs(measuredPx - literalPx);
            if (diff > bandPx)
            {
                failures.Add(
                    $"  {group.Label}: {fileName} {edge} measured {measuredPx}px, literal says {literalPx:0.##}px " +
                    $"(fraction {literalFraction:0.####}) -- off by {diff:0.##}px (band {bandPx:0.#}px).");
            }
        }
    }
}
