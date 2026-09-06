using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // A DIAGNOSTIC, NOT A GUARANTEE. Say it first because a source-text rule
    // that reads like a guarantee is worse than no rule at all.
    //
    // What this can say: the two textual shapes of "somebody wrote to a content
    // record" do not appear in the tree today. What it cannot say: that a fight
    // does not mutate the catalogue. The write that matters can go through a
    // local of an inferred type, an out parameter, an array element, a
    // reference passed three calls deep -- none of which look like an
    // assignment through a definition. ContentIsolationTests is the guarantee:
    // it plays a real fight and compares every record before and after, by
    // value AND by reference identity. This is here to catch the easy case
    // early, at 4 seconds instead of at a PlayMode run, and to make the rule
    // greppable.
    //
    // THE RULE IT ENFORCES: a Resolved* record is written by the resolver that
    // builds it and by ContentBuilder, which puts it on the asset. Nothing
    // else. Those are the two places named below; everywhere else, a record is
    // read-only by convention -- and by convention is exactly what it is. The
    // definitions' `data` field is private behind a `Data` getter now, which
    // stops the whole record being REPLACED and nothing more: the record is a
    // class with public fields, so the compiler has no opinion about
    // `definition.Data.Cost = 3`. That gap is what this scans for.
    public class ContentOwnershipLintTests
    {
        // The number of source files below which this is not scanning the
        // project, it is agreeing with itself about an empty list. The tree is
        // ~450 .cs files; 200 is comfortably under that and comfortably over
        // "the walk started in the wrong directory".
        private const int MinimumFilesScanned = 200;

        // Writing through a definition's accessor: `definition.Data.Cost = 3`.
        // The capital letter is what keeps this off `foo.data.bar` in some
        // unrelated type -- content records are the only PascalCase-field
        // objects reached through a `.Data.`.
        private static readonly Regex ThroughData =
            new Regex(@"\.Data\.[A-Z]\w*\s*=[^=]", RegexOptions.Compiled);

        // The lowercase form, kept after the field was made private and the
        // tree renamed to `.Data.`. A new `.data.` appearing anywhere outside
        // the definitions themselves means somebody added a second public field
        // called `data`, which is how the first one got everywhere.
        private static readonly Regex ThroughLowercaseData =
            new Regex(@"\.data\.[A-Z]\w*\s*=[^=]", RegexOptions.Compiled);

        // An explicitly-typed Resolved* variable, however it was declared:
        // local, field, parameter. `var` is deliberately not chased -- see the
        // header on what this cannot see.
        private static readonly Regex ResolvedDeclaration =
            new Regex(@"\bResolved[A-Z]\w*\s+(?<name>[a-z_]\w*)\s*[,;=)]", RegexOptions.Compiled);

        [Test]
        public void NothingOutsideAResolverOrContentBuilderWritesToAContentRecord()
        {
            var offences = new List<string>();
            int scanned = 0;

            foreach (string path in SourceFiles())
            {
                scanned++;
                if (Owns(path)) continue;

                string[] lines = File.ReadAllLines(path);
                string relative = Relative(path);

                // Every Resolved*-typed name this file declares, so
                // `enemy.AttackType = ...` can be told from
                // `builder.AttackType = ...`.
                var names = new HashSet<string>();
                foreach (Match m in ResolvedDeclaration.Matches(string.Join("\n", lines)))
                {
                    names.Add(m.Groups["name"].Value);
                }

                var memberWrites = names.Count == 0
                    ? null
                    : new Regex(@"\b(" + string.Join("|", names.Select(Regex.Escape)) + @")\.[A-Z]\w*\s*=[^=]",
                        RegexOptions.Compiled);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    // Comments describe the rule constantly in this codebase --
                    // this file included -- and a comment is not a write.
                    string code = Uncommented(line);
                    if (code.Length == 0) continue;

                    if (ThroughData.IsMatch(code) || ThroughLowercaseData.IsMatch(code))
                    {
                        offences.Add($"{relative}:{i + 1}: {line.Trim()}");
                        continue;
                    }

                    if (memberWrites != null && memberWrites.IsMatch(code))
                    {
                        offences.Add($"{relative}:{i + 1}: {line.Trim()}");
                    }
                }
            }

            Assert.GreaterOrEqual(scanned, MinimumFilesScanned,
                $"Scanned {scanned} source file(s), which is not this project. The walk started somewhere " +
                "unexpected, and a lint that reads nothing passes perfectly.");

            Assert.IsEmpty(offences,
                "These write to a content record outside the two places allowed to (a *Resolver.cs under " +
                "Domain/Content, and Editor/ContentBuilder.cs). Every Resolved* instance is SHARED -- one " +
                "object per row for the whole session -- so a write here is a change to the catalogue that " +
                "outlives whatever made it:\n  " + string.Join("\n  ", offences));
        }

        // The guard above only says the walk found files. This says it found
        // the RIGHT ones: a filter that quietly stopped matching .cs would pass
        // the count and cover nothing that matters.
        [Test]
        public void TheScanReachesTheFilesTheRuleIsAbout()
        {
            var scanned = SourceFiles().Select(Relative).ToList();

            CollectionAssert.Contains(scanned, "Assets/_Project/Scripts/Domain/Content/ResolvedEnemy.cs");
            CollectionAssert.Contains(scanned, "Assets/_Project/Scripts/Core/Content/ContentDatabase.cs");
            CollectionAssert.Contains(scanned, "Assets/_Project/Scripts/Editor/ContentBuilder.cs");
        }

        // And the rule itself is not vacuous: the patterns must actually fire
        // on the text they are written for. Both regexes have a way of going
        // quiet -- a lookahead that never matches, a character class typo --
        // and a lint that matches nothing is indistinguishable from a clean
        // tree.
        [Test]
        public void ThePatternsFireOnTheShapesTheyDescribe()
        {
            Assert.IsTrue(ThroughData.IsMatch("definition.Data.Cost = 3;"));
            Assert.IsTrue(ThroughLowercaseData.IsMatch("definition.data.Cost = 3;"));

            // Reads are not writes, and == is not =.
            Assert.IsFalse(ThroughData.IsMatch("int c = definition.Data.Cost;"));
            Assert.IsFalse(ThroughData.IsMatch("if (definition.Data.Cost == 3)"));

            var declaration = ResolvedDeclaration.Match("ResolvedEnemy enemy = definition.Data;");
            Assert.IsTrue(declaration.Success);
            Assert.AreEqual("enemy", declaration.Groups["name"].Value);

            var write = new Regex(@"\b(enemy)\.[A-Z]\w*\s*=[^=]");
            Assert.IsTrue(write.IsMatch("enemy.AttackType = DamageType.Fire;"));
            Assert.IsFalse(write.IsMatch("var t = enemy.AttackType;"));
        }

        // ---- the walk ----------------------------------------------------------

        private static IEnumerable<string> SourceFiles() =>
            Directory.EnumerateFiles(
                    Path.Combine(RepoTree.Root(), "Assets", "_Project", "Scripts"), "*.cs",
                    SearchOption.AllDirectories)
                .OrderBy(p => p, System.StringComparer.Ordinal);

        // The two owners, and nothing else. A resolver builds the record; the
        // builder puts it on the asset.
        private static bool Owns(string path)
        {
            string relative = Relative(path);

            // AND THIS FILE. ThePatternsFireOnTheShapesTheyDescribe holds the
            // forbidden shapes as string literals on purpose -- a lint whose
            // patterns have gone quiet is indistinguishable from a clean tree,
            // so they have to be exercised somewhere. Skipping the file that
            // exercises them is the cost, and it is why that test exists rather
            // than being folded into this one.
            return relative == "Assets/_Project/Scripts/Editor/ContentBuilder.cs"
                   || relative.EndsWith("Tests/EditMode/Content/ContentOwnershipLintTests.cs")
                   || (relative.StartsWith("Assets/_Project/Scripts/Domain/Content/")
                       && (relative.EndsWith("Resolver.cs") || relative.EndsWith("Resolvers.cs")));
        }

        private static string Relative(string path) =>
            path.Substring(RepoTree.Root().Length).TrimStart('\\', '/').Replace('\\', '/');

        // Line comments only. A block comment spanning lines would need a
        // parser, and this is a diagnostic -- the cost of a false positive is
        // one line moved, and it says so in the failure.
        private static string Uncommented(string line)
        {
            int slash = line.IndexOf("//", System.StringComparison.Ordinal);
            return (slash < 0 ? line : line.Substring(0, slash)).Trim();
        }
    }
}
