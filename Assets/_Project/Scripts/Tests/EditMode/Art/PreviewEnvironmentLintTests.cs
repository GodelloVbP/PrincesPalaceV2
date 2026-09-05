using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // A GATE THAT DEPENDS ON THE SHELL IT WAS STARTED FROM IS NOT A GATE.
    //
    // tools/preview.ps1 narrows its capture fixture to one monster by setting
    // an environment variable (the name is assembled in `Variable` below --
    // this file cannot spell it, for the reason given there). That is fine for
    // a picture nobody gates on, and it would be poison anywhere else: a
    // verification fixture reading it would quietly
    // test a subset on the machine where the variable happened to be exported,
    // report green, and be doing less than it claimed. Nothing about the
    // resulting run would look wrong.
    //
    // The plan's acceptance for Step 1 is literally this -- run the whole suite
    // with that variable deliberately left set and watch verification ignore
    // it. This is that promise turned into a rule instead of a habit.
    //
    // A SOURCE-TEXT CHECK, and its limits are worth naming: it proves the
    // NAME appears in one file, not that no fixture reads the variable by some
    // other route (a computed string, a shared helper). It is a lint, not a
    // guarantee -- but it catches the way this would actually happen, which is
    // somebody copying the two-line read into a second fixture.
    public class PreviewEnvironmentLintTests
    {
        // SPLIT SO THIS FILE DOES NOT CONTAIN THE LITERAL IT LOOKS FOR. The
        // obvious alternative -- scan everything except this file -- puts an
        // exemption into a rule whose whole content is "there are no
        // exemptions", and an exemption keyed on a filename is one a second
        // fixture can be renamed into. Concatenated, the scan can stay honest
        // and cover every file including this one.
        private static readonly string Variable = "PP_PREVIEW" + "_IDS";
        private const string OwningFile = "PreviewCaptureTests.cs";

        private static string TestsRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts", "Tests")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts/Tests from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "Scripts", "Tests");
        }

        [Test]
        public void ExactlyOneTestFileNamesThePreviewVariable()
        {
            var all = Directory.EnumerateFiles(TestsRoot(), "*.cs", SearchOption.AllDirectories).ToList();

            Assert.Greater(all.Count, 100,
                $"only {all.Count} test file(s) scanned -- the walk is not seeing the suite, so this rule " +
                "would pass however many fixtures read the variable.");

            var mentions = all
                .Where(f => File.ReadAllText(f).Contains(Variable, StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(new[] { OwningFile }, mentions,
                $"{Variable} is what tools/preview.ps1 uses to narrow its capture to one monster. Exactly one " +
                $"test file may read it, and that file is {OwningFile}, which is [Explicit] and never runs in " +
                "the gate. Any other fixture reading it would test a different thing depending on the shell it " +
                "was started from -- green here, and quietly covering less. Files naming it: " +
                string.Join(", ", mentions));
        }
    }
}
