using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Enforces the two halves of the field-list-cannot-drift promise
    // docs/CONTENT_SCHEMA.md makes: every Raw*Entry field carries a
    // [ContentDoc], and the committed markdown is exactly what
    // ContentSchema.Generate() produces from the Raw*Entry types right now.
    //
    // Same shape and reasoning as ContentLoadingLintTests: a rule that can
    // scan zero files and still report green is worse than no rule, so both
    // tests here carry a vacuity guard.
    public class ContentSchemaTests
    {
        private const int MinimumFieldsExamined = 150;

        // Same walk used by ContentLoadingLintTests to find the repo root
        // from wherever `dotnet test` happens to run from.
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate the repo root from the working directory.");
            return dir.FullName;
        }

        private static IEnumerable<Type> RawEntryTypes() =>
            typeof(RawSkillEntry).Assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace == "PrincesPalace.Domain.Content"
                            && t.Name.StartsWith("Raw", StringComparison.Ordinal)
                            && t.Name.EndsWith("Entry", StringComparison.Ordinal));

        [Test]
        public void EveryRawEntryFieldCarriesContentDoc()
        {
            int examined = 0;
            var offenders = new List<string>();

            foreach (Type t in RawEntryTypes())
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    examined++;
                    if (f.GetCustomAttribute<ContentDocAttribute>() == null)
                    {
                        offenders.Add($"{t.Name}.{f.Name}");
                    }
                }
            }

            Assert.Greater(examined, MinimumFieldsExamined,
                $"Only {examined} Raw*Entry fields examined - the scan is not seeing the content " +
                "types, so this rule would vacuously pass.");

            CollectionAssert.IsEmpty(offenders,
                "Every Raw*Entry field needs a [ContentDoc(\"...\")] so docs/CONTENT_SCHEMA.md can " +
                "describe it - missing: " + string.Join(", ", offenders));
        }

        [Test]
        public void TheRuleIsNotVacuous()
        {
            CollectionAssert.IsNotEmpty(RawEntryTypes().ToList(),
                "no Raw*Entry type was found at all, so EveryRawEntryFieldCarriesContentDoc would " +
                "pass however many fields went undocumented");
        }

        // CONTENT_SCHEMA_WRITE=1 makes this WRITE docs/CONTENT_SCHEMA.md
        // instead of comparing to it -- see tools/content_schema.ps1, the
        // only intended way to set that variable.
        [Test]
        public void GeneratedMarkdownMatchesCommittedFile()
        {
            string docPath = Path.Combine(RepoRoot(), "docs", "CONTENT_SCHEMA.md");
            string generated = ContentSchema.Generate();

            if (Environment.GetEnvironmentVariable("CONTENT_SCHEMA_WRITE") == "1")
            {
                File.WriteAllText(docPath, generated);
                return;
            }

            Assert.IsTrue(File.Exists(docPath),
                $"{docPath} does not exist - run tools/content_schema.ps1 to regenerate.");

            string committed = File.ReadAllText(docPath);

            Assert.AreEqual(Normalize(generated), Normalize(committed),
                "docs/CONTENT_SCHEMA.md is stale - run tools/content_schema.ps1 to regenerate.");
        }

        // THE ONE NUMBER THE BASELINE HAD TO GO AND LOOK UP.
        //
        // docs/CONTENT_SCHEMA.md is the per-field reference an author opens
        // first, and its six ability-score rows said "the resolver's budget"
        // and never what the budget was -- so the Step 0 authoring baseline
        // spent one lookup on characters.json's _readme and another on
        // CharacterEntryResolver to find the 60 (see
        // docs/measurements/2026-09-authoring-baseline.md, "Rules that had to
        // be looked up because nothing enforces or announces them").
        //
        // The number is written into those [ContentDoc] strings now, because
        // an attribute argument has to be a compile-time constant and C# will
        // not concatenate an int into one. This is what keeps that copy
        // honest: the EXPECTED value is built from the constant, so changing
        // AbilityScoreBudget without touching the six strings fails here
        // rather than leaving the reference quietly wrong.
        [Test]
        public void TheSchemaNamesTheAbilityScoreBudget()
        {
            string generated = ContentSchema.Generate();
            string expected = "must total exactly " + CharacterEntryResolver.AbilityScoreBudget;

            int rows = generated.Split(new[] { expected }, StringSplitOptions.None).Length - 1;

            Assert.AreEqual(6, rows,
                $"docs/CONTENT_SCHEMA.md should say '{expected}' on all six ability-score rows and says it on " +
                $"{rows}. Either a row lost the number, or AbilityScoreBudget changed and the six [ContentDoc] " +
                "strings on RawCharacterEntry still name the old one.");
        }

        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
