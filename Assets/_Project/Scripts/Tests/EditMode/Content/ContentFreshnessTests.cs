using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // IS THE GENERATED CONTENT TREE THE ONE THE CURRENT INPUTS PRODUCE?
    //
    // "Fresh" is defined narrowly and on purpose (ContentStamp's header has
    // the long version): the tree was produced from the current inputs by the
    // current builder, the build ran to completion, and the assets it wrote
    // are all present and are the only ones present. It does NOT claim an
    // asset's contents were not hand-edited -- nothing here hashes an output,
    // and the "content is generated, never hand-edited" rule plus the
    // destructive rebuild are what cover that.
    //
    // Runs under `dotnet test` with no Unity at all, which is why every piece
    // it touches is engine-free: ContentInputHash and ContentStamp both live
    // in Domain, and the stamp's format is read by the same file that writes
    // it rather than through JsonUtility. Roughly 4s, dominated by hashing
    // ~450 source files.
    //
    // THE FIX IS ALWAYS THE SAME and every message says it, because a
    // freshness failure is not a bug report -- it is a build that has not been
    // run yet, and the reader should not have to work that out:
    //
    //     powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
    public class ContentFreshnessTests
    {
        private const string Fix =
            "\n\nRun: powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1" +
            "\n(or tools/preview.ps1 -Build, which picks that route or the open-Editor one for you).";

        // A build writes eleven folders. Fewer than eight means the stamp is
        // describing something other than this catalogue, and every check
        // below would agree with it.
        private const int MinimumFolders = 8;

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

        private static string ContentRoot() =>
            Path.Combine(RepoRoot(), "Assets", "_Project", "Resources", "Content");

        private static string StampPath() => Path.Combine(ContentRoot(), "content_stamp.json");

        private static ContentStamp Stamp()
        {
            string path = StampPath();

            // NO STAMP IS A REAL ANSWER, not a missing precondition. The tree
            // is deleted whole at the start of a build and the stamp is
            // written last, so its absence means the build never finished --
            // which is exactly what a resolver error in one entry produces.
            Assert.IsTrue(File.Exists(path),
                "There is no Resources/Content/content_stamp.json, so no content build has completed against " +
                "this tree. Either it has never been built, or the last build died before it got to the stamp " +
                "(a resolver error in one entry does that -- the build logs [ContentBuilder] and writes no stamp)." + Fix);

            return ContentStamp.Parse(File.ReadAllText(path));
        }

        private static IEnumerable<string> AssetsOnDisk()
        {
            string root = ContentRoot();
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();

            return Directory.EnumerateFiles(root, "*.asset", SearchOption.AllDirectories)
                .Select(f => f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'));
        }

        [Test]
        public void TheStampDescribesTheWholeCatalogue()
        {
            var stamp = Stamp();

            Assert.GreaterOrEqual(stamp.IdsByFolder.Count, MinimumFolders,
                $"content_stamp.json lists only {stamp.IdsByFolder.Count} folder(s); a completed build writes " +
                "eleven. Every other check here would pass vacuously against a stamp that describes almost nothing." + Fix);

            CollectionAssert.IsNotEmpty(stamp.InputHash ?? "",
                "content_stamp.json carries no inputHash, so nothing can tell whether the tree matches its inputs." + Fix);
        }

        [Test]
        public void TheInputsAreTheOnesTheTreeWasBuiltFrom()
        {
            var stamp = Stamp();
            string current = ContentInputHash.Compute(RepoRoot());

            Assert.AreEqual(stamp.InputHash, current,
                "The generated content under Resources/Content was built from different inputs than the ones on " +
                "disk now. Something under ContentData/, Scripts/Domain/, Scripts/Core/Content/ or " +
                "Scripts/Editor/ContentBuilder.cs has changed since the last build, so every generated asset is " +
                "suspect -- the fight, the shop and the talent tree are all reading stale records." + Fix);
        }

        [Test]
        public void EveryAssetTheBuildClaimsToHaveWrittenIsThere()
        {
            var stamp = Stamp();
            string root = ContentRoot();
            var missing = new List<string>();
            int expected = 0;

            foreach (var pair in stamp.IdsByFolder)
            {
                foreach (string name in pair.Value)
                {
                    expected++;
                    if (!File.Exists(Path.Combine(root, pair.Key, name + ".asset")))
                    {
                        missing.Add($"{pair.Key}/{name}.asset");
                    }
                }
            }

            Assert.Greater(expected, 100,
                $"content_stamp.json names only {expected} asset(s), which is not this catalogue." + Fix);

            CollectionAssert.IsEmpty(missing,
                $"{missing.Count} asset(s) the last content build wrote are no longer on disk. Whatever removed " +
                "them (a hand delete, a half-finished sync, a merge) left the catalogue with holes in it, and the " +
                "code that loads them degrades quietly rather than failing -- missing: " +
                string.Join(", ", missing.Take(20)) + Fix);
        }

        [Test]
        public void NothingIsUnderResourcesContentThatTheBuildDidNotWrite()
        {
            var stamp = Stamp();

            var listed = new HashSet<string>(
                stamp.IdsByFolder.SelectMany(p => p.Value.Select(name => $"{p.Key}/{name}.asset")),
                StringComparer.Ordinal);

            var onDisk = AssetsOnDisk().ToList();

            Assert.Greater(onDisk.Count, 100,
                $"Only {onDisk.Count} .asset file(s) found under Resources/Content -- the scan is not seeing the " +
                "generated tree, so this rule would pass however much junk were sitting in it." + Fix);

            var strays = onDisk.Where(a => !listed.Contains(a)).ToList();

            CollectionAssert.IsEmpty(strays,
                $"{strays.Count} asset(s) under Resources/Content were not written by the last content build. " +
                "That folder is generated and regenerating it is destructive, so anything hand-authored there is " +
                "on its way to being deleted without warning -- and until then ContentDatabase loads it as though " +
                "it were authored content. Move it somewhere ContentBuilder does not own -- strays: " +
                string.Join(", ", strays.Take(20)) + Fix);
        }

        [Test]
        public void TheStampIsNotOlderThanTheAssetsItDescribes()
        {
            var stamp = Stamp();
            Assert.IsNotNull(stamp);

            string root = ContentRoot();
            DateTime stamped = File.GetLastWriteTimeUtc(StampPath());

            // A TOLERANCE, and what it is for. The stamp is written after every
            // asset and, in run_tests_parallel.ps1's sync-back, copied after
            // every asset too -- so what this catches is a tree assembled some
            // other way, where the assets arrived after the stamp that claims
            // to describe them. It is NOT trying to resolve sub-second
            // ordering: a `git checkout` rewrites every file's timestamp to
            // about-now in index order, and the stamp is not guaranteed to be
            // last in that order. A minute is far below the cost of a real
            // rebuild and far above that noise.
            var tolerance = TimeSpan.FromMinutes(1);

            var newer = Directory.EnumerateFiles(root, "*.asset", SearchOption.AllDirectories)
                .Where(f => File.GetLastWriteTimeUtc(f) - stamped > tolerance)
                .Select(f => f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'))
                .Take(20)
                .ToList();

            CollectionAssert.IsEmpty(newer,
                $"{newer.Count} generated asset(s) are newer than the stamp that claims to describe them, so the " +
                "stamp is evidence about an older tree than the one on disk. The usual cause is a sync-back that " +
                "was interrupted, or assets copied in by hand after a build -- " +
                string.Join(", ", newer) + Fix);
        }

        // The hash is only worth anything if it actually reads the inputs.
        // Same vacuity guard every lint test in this folder carries: a rule
        // that can scan nothing and still report a stable answer agrees with
        // itself perfectly while covering nothing.
        [Test]
        public void TheInputHashCoversTheFilesItClaimsTo()
        {
            var inputs = ContentInputHash.Enumerate(RepoRoot()).ToList();

            Assert.Greater(inputs.Count, ContentInputHash.MinimumFilesHashed,
                $"ContentInputHash enumerated only {inputs.Count} file(s).");

            CollectionAssert.Contains(inputs, "Assets/_Project/ContentData/enemies.json");
            CollectionAssert.Contains(inputs, "Assets/_Project/Scripts/Editor/ContentBuilder.cs");

            Assert.IsTrue(inputs.Any(p => p.StartsWith("Assets/_Project/Scripts/Domain/", StringComparison.Ordinal)),
                "no Domain source is in the hashed set, so a resolver edit would not mark content stale");
            Assert.IsTrue(inputs.Any(p => p.StartsWith("Assets/_Project/Scripts/Core/Content/", StringComparison.Ordinal)),
                "no Core/Content source is in the hashed set, so a ValidateContent edit would not mark content stale");

            // Sorted and de-duplicated, or the hash is machine-dependent.
            var sorted = inputs.OrderBy(p => p, StringComparer.Ordinal).ToList();
            CollectionAssert.AreEqual(sorted, inputs, "ContentInputHash.Enumerate is not in a stable order.");
            Assert.AreEqual(inputs.Count, inputs.Distinct().Count(), "ContentInputHash.Enumerate lists a file twice.");
        }

        // Round-trip and refusal, so a change to the stamp format cannot
        // quietly become a stamp nothing can read back.
        [Test]
        public void TheStampFormatRoundTrips()
        {
            var original = new ContentStamp { InputHash = new string('a', 64) };
            original.IdsByFolder["Enemies"] = new List<string> { "rat", "golem" };
            original.IdsByFolder["SpellTiers"] = new List<string> { "level_1" };
            original.IdsByFolder["Empty"] = new List<string>();

            var restored = ContentStamp.Parse(original.Render());

            Assert.AreEqual(original.InputHash, restored.InputHash);
            CollectionAssert.AreEqual(new[] { "Empty", "Enemies", "SpellTiers" }, restored.IdsByFolder.Keys);
            CollectionAssert.AreEqual(new[] { "golem", "rat" }, restored.IdsByFolder["Enemies"]);
            CollectionAssert.IsEmpty(restored.IdsByFolder["Empty"]);
        }

        [Test]
        public void AnUnreadableStampThrowsRatherThanReadingAsEmpty()
        {
            Assert.Throws<FormatException>(() => ContentStamp.Parse(""));
            Assert.Throws<FormatException>(() => ContentStamp.Parse("{ \"types\": {} }"));
            Assert.Throws<FormatException>(() => ContentStamp.Parse("{ \"inputHash\": \"abc\" }"));
        }
    }
}
