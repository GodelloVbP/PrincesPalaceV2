using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // A GATE THAT DEPENDS ON THE SHELL IT WAS STARTED FROM IS NOT A GATE.
    //
    // tools/preview.ps1 narrows its capture fixture to one id by setting an
    // environment variable (the prefix is assembled in `Prefix` below -- this
    // file cannot spell it, for the reason given there). That is fine for
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
        //
        // THE PREFIX, NOT ONE NAME -- and the same split applies to it, so
        // this comment cannot spell that either.
        //
        // The rule used to name the enemy mode's variable exactly, which held
        // for as long as the preview had one mode. -Spell and -Character each
        // brought their own, and a rule naming only the first would have let
        // the second be copied into a gate on the day it was added: the
        // precise failure this file exists to prevent, arriving through the
        // door the narrow version left open.
        private static readonly string Prefix = "PP_" + "PREVIEW_";
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
                .Where(f => File.ReadAllText(f).Contains(Prefix, StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(new[] { OwningFile }, mentions,
                $"{Prefix}* is how tools/preview.ps1 narrows its capture to one id. Exactly one test file may " +
                $"read any of those variables, and that file is {OwningFile}, which is [Explicit] and never runs " +
                "in the gate. Any other fixture reading one would test a different thing depending on the shell " +
                "it was started from -- green here, and quietly covering less. Files naming one: " +
                string.Join(", ", mentions));
        }
    }
}
