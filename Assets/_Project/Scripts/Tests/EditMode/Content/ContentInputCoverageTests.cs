using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE LIST IN ContentInputHash IS NOT ALLOWED TO DRIFT.
    //
    // ContentInputHash used to hash the whole of Scripts/Domain, on the
    // reasoning that a content record references enums from all over it and
    // enumerating the reachable set by hand would go stale. That reasoning was
    // sound about the risk and wrong about the price: with 267 files in the
    // hash, ANY Domain edit -- a combat formula, a map generator, a string --
    // marked the content stale, and the 4-second dotnet loop then stayed red
    // until somebody spent 14 seconds regenerating assets that could not
    // possibly have changed. The fast loop is the whole point of the fast
    // loop; a check that reddens it for unrelated work gets ignored, and an
    // ignored check is worse than none.
    //
    // So the list is explicit now, and this fixture is what stops it drifting.
    // It walks the same graph a human would have to: from every Raw* and
    // Resolved* type declared under Domain/Content -- the shapes ContentBuilder
    // deserialises into and the shapes it writes out -- through every public
    // field and property, through arrays, lists and Nullable<>, transitively.
    // Every Domain type that walk reaches must have its declaring file inside
    // the hashed set, or this fails NAMING THE FILE and the line to add.
    //
    // What it does not cover, stated rather than implied: a type a resolver
    // uses only INTERNALLY (a parser's private helper enum), and a constant
    // read by a resolver from a Domain file no record's field type points at.
    // Both are inside Domain/Content in practice -- the resolvers live there
    // and that whole folder is hashed wholesale -- so the gap is the case
    // where a resolver reaches OUT of Domain/Content for something that is not
    // a field type. Narrow, and cheaper than being red every afternoon.
    public class ContentInputCoverageTests
    {
        private const string DomainRoot = "Assets/_Project/Scripts/Domain/";
        private const string ContentRoot = "Assets/_Project/Scripts/Domain/Content/";

        // A walk that finds nothing agrees with itself perfectly and covers
        // nothing -- the same vacuity guard every lint in this folder carries.
        // Ten roots is well under the ~30 Raw*/Resolved* types actually in
        // Domain/Content and well over what a broken reflection filter yields.
        private const int MinimumRoots = 10;

        [Test]
        public void EveryDomainTypeAContentRecordReachesIsInTheHashedInputs()
        {
            string repo = RepoTree.Root();
            var declaringFiles = DeclaringFiles(repo);
            var roots = Roots(declaringFiles).ToList();

            Assert.GreaterOrEqual(roots.Count, MinimumRoots,
                $"Only {roots.Count} Raw*/Resolved* root type(s) were found under {ContentRoot}. The reflection " +
                "filter is not seeing the content records, so this rule would pass over any list at all.");

            var reachable = Closure(roots);

            var hashed = new HashSet<string>(ContentInputHash.Enumerate(repo), StringComparer.OrdinalIgnoreCase);
            var missing = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var type in reachable)
            {
                if (!declaringFiles.TryGetValue(type.Name, out var files)) continue;

                foreach (string file in files)
                {
                    // Domain/Content is hashed wholesale, so a type declared
                    // there needs no list entry; this rule is only about the
                    // files OUTSIDE it that a record's fields reach into.
                    if (!file.StartsWith(DomainRoot, StringComparison.Ordinal)) continue;
                    if (file.StartsWith(ContentRoot, StringComparison.Ordinal)) continue;
                    if (hashed.Contains(file)) continue;

                    missing[file] = type.Name;
                }
            }

            if (missing.Count == 0) return;

            var lines = missing.Select(pair =>
                $"  (\"{pair.Key}\", null, false),   // {pair.Value}");

            Assert.Fail(
                $"{missing.Count} Domain file(s) declare a type a content record reaches through its public " +
                "fields, but are not in ContentInputHash's hashed input set. A change to any of them changes " +
                "what the generated assets mean, and the freshness check would not notice.\n\n" +
                "Add to ContentInputHash.Sources:\n" + string.Join("\n", lines));
        }

        // The other direction, and the reason it is here: an entry that no
        // record reaches any more is a file whose every edit costs a needless
        // 14-second rebuild, which is exactly the cost this narrowing was for.
        // A stale entry is cheap enough to be worth reporting and not worth
        // failing over -- so it is reported, loudly, as a separate test.
        [Test]
        public void NoHashedDomainFileIsThereForATypeNothingReachesAnyMore()
        {
            string repo = RepoTree.Root();
            var declaringFiles = DeclaringFiles(repo);
            var reachable = new HashSet<string>(Closure(Roots(declaringFiles).ToList()).Select(t => t.Name),
                StringComparer.Ordinal);

            var stale = ContentInputHash.Enumerate(repo)
                .Where(f => f.StartsWith(DomainRoot, StringComparison.Ordinal))
                .Where(f => !f.StartsWith(ContentRoot, StringComparison.Ordinal))
                .Where(f => !DeclaresSomethingReachable(f, declaringFiles, reachable))
                .ToList();

            CollectionAssert.IsEmpty(stale,
                $"{stale.Count} file(s) are hashed as content inputs but declare no type any Raw*/Resolved* " +
                "record reaches. Every edit to one of them marks the content stale for nothing. Remove them " +
                "from ContentInputHash.Sources: " + string.Join(", ", stale));
        }

        private static bool DeclaresSomethingReachable(
            string file, Dictionary<string, List<string>> declaringFiles, HashSet<string> reachable)
        {
            return declaringFiles.Any(pair =>
                reachable.Contains(pair.Key) &&
                pair.Value.Contains(file, StringComparer.OrdinalIgnoreCase));
        }

        // --- the graph -------------------------------------------------------

        private static IEnumerable<Type> Roots(Dictionary<string, List<string>> declaringFiles)
        {
            return typeof(ContentInputHash).Assembly.GetTypes()
                .Where(t => t.Name.StartsWith("Raw", StringComparison.Ordinal) ||
                            t.Name.StartsWith("Resolved", StringComparison.Ordinal))
                .Where(t => declaringFiles.TryGetValue(t.Name, out var files) &&
                            files.Any(f => f.StartsWith(ContentRoot, StringComparison.Ordinal)))
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
        }

        private static IEnumerable<Type> Closure(List<Type> roots)
        {
            var assembly = typeof(ContentInputHash).Assembly;
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>(roots);

            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                if (!seen.Add(type)) continue;

                foreach (var member in MemberTypes(type))
                {
                    foreach (var unwrapped in Unwrap(member))
                    {
                        // Only this assembly: the BCL is not an input, and a
                        // reference to a type in Core or the engine cannot
                        // reach a Domain file at all.
                        if (unwrapped.Assembly != assembly) continue;
                        if (!seen.Contains(unwrapped)) queue.Enqueue(unwrapped);
                    }
                }

                // A base class carries fields the derived record serialises
                // too, so it is part of the shape whether or not anything
                // declares it directly.
                if (type.BaseType != null && type.BaseType.Assembly == assembly && !seen.Contains(type.BaseType))
                {
                    queue.Enqueue(type.BaseType);
                }
            }

            return seen;
        }

        // FIELDS AND PROPERTIES BOTH. The contract is about field types, and
        // properties are a strict superset that costs nothing: a record that
        // exposes an enum only through a computed property (ResolvedSkill's
        // AppliesStatus, FixedDamageType) is describing the same shape, and
        // leaving them out would mean the coverage depended on which of two
        // equivalent spellings the author reached for.
        private static IEnumerable<Type> MemberTypes(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(Flags)) yield return field.FieldType;
            foreach (var property in type.GetProperties(Flags)) yield return property.PropertyType;
        }

        // Arrays, List<T>, Nullable<T>, Dictionary<K,V> -- a record's field is
        // as often a container as it is the thing itself, and the container is
        // never the type whose file matters.
        private static IEnumerable<Type> Unwrap(Type type)
        {
            if (type == null) yield break;

            if (type.IsArray)
            {
                foreach (var inner in Unwrap(type.GetElementType())) yield return inner;
                yield break;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    foreach (var inner in Unwrap(argument)) yield return inner;
                }

                yield break;
            }

            yield return type;
        }

        // --- type name to file ------------------------------------------------

        // Reflection knows the type and not the file it came from, so the map
        // is read out of the source. Declarations only: the pattern anchors at
        // the start of a line (modifiers allowed) so a type NAMED in a comment
        // or a using does not claim to be declared there. A name mapping to
        // several files is a partial class, and all its parts count.
        private static readonly Regex Declaration = new Regex(
            @"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new)\s+)*" +
            @"(?:class|struct|enum|interface|record)\s+(\w+)",
            RegexOptions.Compiled);

        private static Dictionary<string, List<string>> DeclaringFiles(string repo)
        {
            string root = Path.Combine(repo, DomainRoot.Replace('/', Path.DirectorySeparatorChar));
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (string absolute in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = absolute.Substring(repo.Length).TrimStart('\\', '/').Replace('\\', '/');

                foreach (string line in File.ReadLines(absolute))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("*", StringComparison.Ordinal) ||
                        trimmed.StartsWith("/*", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var match = Declaration.Match(line);
                    if (!match.Success) continue;

                    string name = match.Groups[1].Value;
                    if (!map.TryGetValue(name, out var files)) map[name] = files = new List<string>();
                    if (!files.Contains(relative, StringComparer.OrdinalIgnoreCase)) files.Add(relative);
                }
            }

            Assert.Greater(map.Count, 100,
                $"Only {map.Count} type declaration(s) found under {DomainRoot}; the source scan is broken.");

            return map;
        }
    }
}
