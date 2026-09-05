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

        private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
