using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE ASPECT LITERALS, CHECKED AGAINST THE PIXELS.
    //
    // This exists because of what happened on 2026-09-07: the kit was
    // regenerated, every group's true width/height moved (2.79 -> 3.0,
    // 4.91 -> 5.0, 5.92 -> 6.0, container 3x4 0.588 -> 0.75), and the whole
    // suite stayed green through it. Nothing compared ContainerArt's and
    // ButtonPlateArt's aspect constants to the files they claim to describe,
    // so the constants simply went stale and every screen sized off them --
    // the dossier's column A, the main menu's scrim, the fight submenu --
    // was drawing a stretched frame that no test could see. UiKitVisiblePad
    // Tests already pins the visible-edge pads this way; the aspect, the one
    // number Ui.Container refuses a rect over, was the gap.
    //
    // Same reading route as that test: PngAlpha off disk via RepoTree, no
    // engine, so this runs on the fast dotnet host. It is EXPECTED to go red
    // the day the kit is resliced -- whoever does that repins the constants
    // in ContainerArt/ButtonPlateArt and this turns green again, which is the
    // whole point of it being here.
    public class UiKitAspectPinTests
    {
        // Tight on purpose. The delivered kit lands on exact fractions
        // (1536/512 = 3.0, 768/1024 = 0.75), so anything past a rounding
        // wobble is the drift this test is for -- and Ui.Container's own
        // refusal band is 5%, ten times wider, so a miss this test tolerates
        // must stay far inside what the emitter tolerates.
        private const float AspectBand = 0.005f;

        private static readonly string[] Themes = { "blue", "crimson", "gold", "green", "silver", "violet" };

        private static string ProcessedDir() =>
            Path.Combine(RepoTree.Root(), "Assets", "_Project", "Art", "UI", "Buttons", "Processed");

        private sealed class Group
        {
            internal string Constant;     // the C# name to repaste when this fails
            internal string FileTemplate; // "{0}" is the theme
            internal float Aspect;        // width / height, as the constant claims
        }

        private static IEnumerable<Group> Groups()
        {
            // Plates. Legacy and ThreeByOne are byte-identical files since the
            // regeneration and both pin 3.0; both are listed because both keys
            // are still loadable and a future reslice could part them again.
            yield return new Group
            {
                Constant = "ButtonPlateArt.LegacyAspect",
                FileTemplate = "button_plate_{0}.png",
                Aspect = Ui.PlateAspect(ButtonPlateShape.Legacy),
            };
            yield return new Group
            {
                Constant = "ButtonPlateArt.ThreeByOneAspect",
                FileTemplate = "button_plate_{0}_3x1.png",
                Aspect = Ui.PlateAspect(ButtonPlateShape.ThreeByOne),
            };
            yield return new Group
            {
                Constant = "ButtonPlateArt.FiveByOneAspect",
                FileTemplate = "button_plate_{0}_5x1.png",
                Aspect = Ui.PlateAspect(ButtonPlateShape.FiveByOne),
            };
            yield return new Group
            {
                Constant = "ButtonPlateArt.Row6x1Aspect",
                FileTemplate = "row_plate_{0}_6x1.png",
                Aspect = Ui.PlateAspect(ButtonPlateShape.Row6x1),
            };

            // Containers and banners. Read through the size helpers rather
            // than a second forwarder: SizeForHeight(ratio, 1) IS the aspect,
            // and it is the exact path every screen sizes a frame through, so
            // a mistake in the forwarding is caught here too.
            yield return new Group
            {
                Constant = "ContainerArt.ContainerAspect3x4",
                FileTemplate = "container_{0}_3x4.png",
                Aspect = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.ContainerAspect9x16",
                FileTemplate = "container_{0}_9x16.png",
                Aspect = Ui.ContainerSizeForHeight(ContainerRatio.NineBySixteen, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.ContainerAspect3x2",
                FileTemplate = "container_{0}_3x2.png",
                Aspect = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByTwo, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.ContainerAspect2x1",
                FileTemplate = "container_{0}_2x1.png",
                Aspect = Ui.ContainerSizeForHeight(ContainerRatio.TwoByOne, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.ContainerAspect5x1",
                FileTemplate = "container_{0}_5x1.png",
                Aspect = Ui.ContainerSizeForHeight(ContainerRatio.FiveByOne, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.BannerAspect3x4",
                FileTemplate = "banner_flag_{0}_3x4.png",
                Aspect = Ui.FlagBannerSizeForHeight(ContainerRatio.ThreeByFour, 1f).X,
            };
            yield return new Group
            {
                Constant = "ContainerArt.BannerAspect9x16",
                FileTemplate = "banner_flag_{0}_9x16.png",
                Aspect = Ui.FlagBannerSizeForHeight(ContainerRatio.NineBySixteen, 1f).X,
            };
        }

        [Test]
        public void EveryPinnedAspectMatchesThePngOnDisk()
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
                    filesChecked++;

                    float measured = (float)image.Width / image.Height;
                    float diff = System.Math.Abs(measured - group.Aspect);
                    if (diff > AspectBand)
                    {
                        failures.Add(
                            $"  {group.Constant}: {fileName} is {image.Width}x{image.Height} " +
                            $"(aspect {measured:0.####}), constant says {group.Aspect:0.####} " +
                            $"-- off by {diff:0.####} (band {AspectBand:0.###}).");
                    }
                }
            }

            Assert.Greater(filesChecked, 60,
                "Only a handful of files were checked -- the scan is not seeing the kit, so every case above would pass vacuously.");

            Assert.IsEmpty(failures, "One or more kit aspect constants no longer match the art on disk:\n" +
                string.Join("\n", failures) +
                "\n\nRe-measure the delivery and repin the named constant(s) in ContainerArt/ButtonPlateArt.");
        }

        // A group whose six themes disagree on size is a broken delivery even
        // when the average happens to land on the constant -- the aspect is
        // one number per group, so six files have to be one size. Split from
        // the pin above so the failure says WHICH problem it is: a stale
        // constant, or a kit that is not internally consistent.
        [Test]
        public void EveryGroupsSixThemesShareOneSize()
        {
            string processedDir = ProcessedDir();
            var failures = new List<string>();

            foreach (var group in Groups())
            {
                string firstName = null;
                int firstW = 0, firstH = 0;

                foreach (string theme in Themes)
                {
                    string fileName = string.Format(group.FileTemplate, theme);
                    string path = Path.Combine(processedDir, fileName);
                    Assert.IsTrue(File.Exists(path), $"{fileName} is missing -- this pin would otherwise pass vacuously.");

                    var image = PngAlpha.Read(path);
                    if (firstName == null)
                    {
                        firstName = fileName;
                        firstW = image.Width;
                        firstH = image.Height;
                        continue;
                    }

                    if (image.Width != firstW || image.Height != firstH)
                    {
                        failures.Add(
                            $"  {group.Constant}: {fileName} is {image.Width}x{image.Height} " +
                            $"but {firstName} is {firstW}x{firstH}.");
                    }
                }
            }

            Assert.IsEmpty(failures, "A kit group's themes are not all the same size:\n" +
                string.Join("\n", failures) +
                "\n\nOne aspect constant cannot describe six different shapes -- reslice the odd theme(s).");
        }
    }
}
