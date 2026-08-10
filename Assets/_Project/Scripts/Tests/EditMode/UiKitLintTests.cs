using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // Source-level enforcement of the rules the type system cannot state.
    //
    // Tier 3 of the plan's three tiers: T1 is "the API cannot express the
    // mistake", T2 is "one code path owns the concern", and this is T3 --
    // mechanised discipline. It exists because v1 proved that a helper you are
    // ENTITLED to bypass gets bypassed: NewUiRect reached 13 of 86 sites, and
    // nobody was being careless.
    //
    // Scans production code only. Tests legitimately use string literals as
    // fixtures, and linting them would just teach people to suppress the lint.
    public class UiKitLintTests
    {
        private const int MinimumFilesExpected = 40;

        private static string ScriptsRoot()
        {
            // Unity's batch-mode CWD is the project root. Walk up regardless, so
            // the test does not silently depend on that.
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "Scripts");
        }

        private static List<string> ProductionFiles()
        {
            var root = ScriptsRoot();
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Tests/"))
                .ToList();

            // A lint that scans nothing passes everything. This is the guard
            // against a path change quietly turning every rule below into a
            // no-op that still reports green.
            Assert.Greater(files.Count, MinimumFilesExpected,
                $"Only {files.Count} production files found under {root} - the scan is not seeing the project, " +
                $"so every rule in this class would vacuously pass.");
            return files;
        }

        private static IEnumerable<(string File, int Line, string Text)> Matches(string pattern)
        {
            var rx = new Regex(pattern, RegexOptions.Compiled);
            foreach (var file in ProductionFiles())
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // Skip comments - the rules are about code, and the reasons
                    // for the rules are written in prose right next to it.
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    if (rx.IsMatch(line)) yield return (file, i + 1, line.Trim());
                }
            }
        }

        private static string Describe(IEnumerable<(string File, int Line, string Text)> hits) =>
            string.Join("\n", hits.Select(h => $"  {Path.GetFileName(h.File)}:{h.Line}  {h.Text}"));

        [Test]
        public void OnlyTheEmitterMayCreateGameObjects()
        {
            // SceneBuilder.cs is allowed the scene's ROOT FIXTURES - camera,
            // canvas, EventSystem - which are not UI widgets and have no rect
            // preamble. It is bounded below so that allowance cannot quietly
            // become "and also a few widgets".
            // CanvasCapture creates a temporary offscreen camera to render a
            // Canvas to PNG. Not a UI widget, no rect preamble, and it is
            // deleted in the same call that made it.
            // SoundController creates ONE persistent, hidden AudioSource host on
            // first use. Not a UI widget, no rect preamble, no parent in any
            // screen tree -- it is a service that has to outlive every scene, and
            // the alternative is an audio object hand-placed in each of them.
            string[] allowed =
            {
                "UiEmitter.cs", "ScreenshotTool.cs", "SceneBuilder.cs", "CanvasCapture.cs",
                "SoundController.cs",
            };

            var offenders = Matches(@"new\s+GameObject\s*\(")
                .Where(h => !allowed.Contains(Path.GetFileName(h.File)))
                .ToList();

            Assert.IsEmpty(offenders,
                "The rect preamble exists exactly once, in UiEmitter. v1 had 86 hand-rolled construction sites and a " +
                "helper that covered 13 of them.\n" + Describe(offenders));
        }

        [Test]
        public void SceneBuilderCreatesOnlyTheSceneRootFixtures()
        {
            var sites = Matches(@"new\s+GameObject\s*\(")
                .Where(h => Path.GetFileName(h.File) == "SceneBuilder.cs")
                .ToList();

            // Camera, GlobalVolume, Canvas, EventSystem. Raised from 3 when the
            // post-processing Volume was added - deliberately a NUMBER that has
            // to be edited, so growing the allowance is a visible decision
            // rather than something that drifts.
            Assert.LessOrEqual(sites.Count, 4,
                "SceneBuilder may create the camera, the global Volume, the canvas and the EventSystem, and nothing " +
                "else - every widget goes through UiEmitter.\n" + Describe(sites));
        }

        [Test]
        public void TheStringlyTypedSetFieldIdiom_IsNotReImported()
        {
            var offenders = Matches(@"\bSetField\s*\(\s*\w+\s*,\s*""").ToList();

            Assert.IsEmpty(offenders,
                "Wiring is direct field assignment now, not reflection over a field NAME. A typo in that string " +
                "produced no compile error and no runtime error in v1 - the field just stayed null.\n" + Describe(offenders));
        }

        [Test]
        public void LabelTextIsOnlyAssignedThroughUiText()
        {
            string[] allowed = { "UiText.cs", "UiEmitter.cs" };

            var offenders = Matches(@"\.text\s*=")
                .Where(h => !allowed.Contains(Path.GetFileName(h.File)))
                .ToList();

            Assert.IsEmpty(offenders,
                "Controllers set text via label.Set(UiStrings.X, args) so builder and runtime share one template. " +
                "Assigning .text directly is how v1's four drift pairs happened.\n" + Describe(offenders));
        }

        [Test]
        public void FromContent_IsNotUsedToSmuggleALiteral()
        {
            // FromContent exists for data (a character name out of JSON). An
            // inline literal there is authored UI copy dodging the manifest.
            var offenders = Matches(@"FromContent\s*\(\s*""").ToList();

            Assert.IsEmpty(offenders,
                "UiString.FromContent is for values that come from content data. Authored copy belongs in " +
                "UiStrings.\n" + Describe(offenders));
        }

        [Test]
        public void LegacyUnityUiTextComponent_IsNotUsed()
        {
            // v2 is TMP from day one. A single legacy Text would create a second
            // text type for call sites to pick wrongly, and UiText only extends
            // TMP_Text.
            var offenders = Matches(@"UnityEngine\.UI\.Text\b|<\s*Text\s*>|\bText\s+\w+\s*;")
                .Where(h => !Path.GetFileName(h.File).Equals("UiText.cs", StringComparison.Ordinal))
                .ToList();

            Assert.IsEmpty(offenders,
                "v2 uses TMP_Text exclusively.\n" + Describe(offenders));
        }

        [Test]
        public void TheLintItselfIsNotVacuous()
        {
            // Proves the machinery finds real code, so a green run above means
            // "no offenders" rather than "no files".
            var anyClass = Matches(@"\bclass\b").ToList();
            Assert.Greater(anyClass.Count, MinimumFilesExpected,
                "The scanner found production files but almost no code in them - the rules above would be vacuous.");
        }
    }
}
