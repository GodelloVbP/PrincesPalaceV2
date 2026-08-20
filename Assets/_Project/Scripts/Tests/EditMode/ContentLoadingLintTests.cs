using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // Content is loaded in exactly one place, so it can be ordered in exactly
    // one place.
    //
    // `ContentDatabase.LoadOrdered<T>` is constrained on IOrderedContent, which
    // makes "this type has not said how it is ordered" a compile error. That
    // constraint only binds code that goes THROUGH the helper, so this is the
    // other half: a direct `Resources.LoadAll` for content sidesteps it
    // entirely and produces a list in filename order that looks fine.
    //
    // Same shape and same reason as UiEmitter owning `new GameObject`, and the
    // same evidence behind it: v1 proved that a helper you are entitled to
    // bypass gets bypassed -- NewUiRect reached 13 of 86 sites, and nobody was
    // being careless.
    public class ContentLoadingLintTests
    {
        private const int MinimumFilesExpected = 40;

        // Where content loading is allowed to live. One entry, and it should
        // stay that way; a second is a decision worth arguing for in a commit
        // message rather than a line added here quietly.
        private static readonly string[] AllowedFiles = { "ContentDatabase.cs" };

        private static List<string> ProductionFiles()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts from the working directory.");

            var files = Directory
                .GetFiles(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Tests/"))
                .ToList();

            Assert.Greater(files.Count, MinimumFilesExpected,
                $"Only {files.Count} production files found - the scan is not seeing the project, " +
                $"so every rule in this class would vacuously pass.");

            return files;
        }

        // Calls only. Four files carry a COMMENT about Resources.LoadAll --
        // including FrameSequenceLoader, whose comment exists to say it
        // deliberately does not use it -- and linting prose would turn this
        // into a rule against explaining yourself.
        private static IEnumerable<(string File, int Line)> LoadAllCalls()
        {
            var rx = new Regex(@"Resources\.LoadAll\s*<", RegexOptions.Compiled);

            foreach (string file in ProductionFiles())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i];
                    int comment = code.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) code = code.Substring(0, comment);

                    if (rx.IsMatch(code)) yield return (Path.GetFileName(file), i + 1);
                }
            }
        }

        [Test]
        public void OnlyContentDatabaseLoadsContentFromResources()
        {
            var offenders = LoadAllCalls()
                .Where(c => !AllowedFiles.Contains(c.File))
                .Select(c => $"{c.File}:{c.Line}")
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "content loaded outside ContentDatabase.LoadOrdered bypasses the ordering constraint, " +
                "so the list arrives in filename order and nothing says so -- " + string.Join("; ", offenders));
        }

        [Test]
        public void TheRuleIsNotVacuous()
        {
            // A regex that matches nothing passes everything. The one legitimate
            // call site is what proves this rule can see a real one -- if
            // LoadOrdered is ever renamed or rewritten to load some other way,
            // this fails and asks for the rule to be re-aimed rather than
            // quietly protecting nothing.
            var allowed = LoadAllCalls().Where(c => AllowedFiles.Contains(c.File)).ToList();

            CollectionAssert.IsNotEmpty(allowed,
                "no Resources.LoadAll call was found even in ContentDatabase, so this rule matches " +
                "nothing at all and would pass however content was loaded");
        }
    }
}
