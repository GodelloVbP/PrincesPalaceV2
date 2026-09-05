using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PrincesPalace.Domain.Content
{
    // EVERY INPUT ContentBuilder READS, hashed to one hex string.
    //
    // Written into content_stamp.json by the build and recomputed by
    // ContentFreshnessTests, so "is the generated tree current?" is one string
    // comparison rather than a guess about what has been touched since.
    //
    // WHY THE DOMAIN TREE WHOLESALE, rather than the handful of files that
    // obviously matter. A content record references enums and effect types
    // from all over Domain -- DamageType, SkillEffect, RelicModifier,
    // EquipmentSlot -- and the resolvers validate against them. Enumerating
    // the exact reachable set would be a list that drifts the first time
    // somebody adds an enum, and a freshness check that misses an input is
    // worse than none: it reports fresh over a tree built by a different
    // builder. So the whole assembly's source is the input, and the cost of
    // that decision is stated plainly rather than hidden: an unrelated edit
    // anywhere in Domain marks the content stale and asks for a 14-second
    // rebuild. That is the trade, and it is the right way round.
    //
    // Core/Content/ is in for the same reason -- ContentDatabase.ValidateContent
    // is what decides whether a build is allowed to finish at all.
    //
    // NOT INCLUDED, deliberately: the generated tree itself (that is the
    // output), Art/ (an asset the content merely names by path), and every
    // .meta (a GUID is assigned by whichever Unity imported it first and says
    // nothing about the content).
    public static class ContentInputHash
    {
        // Repo-root-relative, forward slashes. Each entry is either a single
        // file or a folder walked recursively for one extension.
        private static readonly (string Path, string Pattern, bool Recursive)[] Sources =
        {
            ("Assets/_Project/ContentData", "*.json", false),
            ("Assets/_Project/Scripts/Domain", "*.cs", true),
            ("Assets/_Project/Scripts/Core/Content", "*.cs", true),
            ("Assets/_Project/Scripts/Editor/ContentBuilder.cs", null, false),
        };

        // The number of files below which the hash is not measuring anything.
        // A rule that can scan zero files and still report a stable answer is
        // the failure mode this project's lint tests all carry a guard for:
        // it would agree with itself perfectly while covering nothing.
        public const int MinimumFilesHashed = 100;

        public static string Compute(string repoRoot)
        {
            var files = Enumerate(repoRoot).ToList();

            if (files.Count < MinimumFilesHashed)
            {
                throw new InvalidOperationException(
                    $"ContentInputHash found only {files.Count} input file(s) under '{repoRoot}' -- " +
                    $"expected at least {MinimumFilesHashed}. The root is wrong, or a source folder moved; " +
                    "hashing what is left would produce a confident answer about nothing.");
            }

            using (var sha = SHA256.Create())
            {
                foreach (string relative in files)
                {
                    // THE PATH GOES IN THE HASH, not just the bytes. Without
                    // it, renaming a file or moving a resolver between folders
                    // leaves the hash unchanged, and the whole point is to
                    // notice that the builder is not the same builder.
                    byte[] name = Encoding.UTF8.GetBytes(relative + "\n");
                    sha.TransformBlock(name, 0, name.Length, null, 0);

                    byte[] body = Normalised(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                    sha.TransformBlock(body, 0, body.Length, null, 0);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        // LINE ENDINGS ARE STRIPPED OUT OF THE HASH, and this is not a detail.
        //
        // Every input here is text (.cs and .json), and this repo is checked
        // out with core.autocrlf on: `git checkout` on a file git reports as
        // clean can still change its bytes on disk, and a clone on a machine
        // with a different setting gets different bytes for the same commit.
        // Hashing raw bytes made both of those "the content is stale, rebuild"
        // -- a false alarm that costs 14s and, worse, teaches the reader that
        // the freshness check cries wolf.
        //
        // Nothing is lost: a resolver cannot see a line ending, so two files
        // that differ only there produce identical content by construction.
        // Only \r immediately before \n is dropped, so a lone \r inside a
        // string literal still counts as the change it is.
        private static byte[] Normalised(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            var kept = new byte[raw.Length];
            int length = 0;

            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n') continue;
                kept[length++] = raw[i];
            }

            if (length == raw.Length) return raw;

            var trimmed = new byte[length];
            Array.Copy(kept, trimmed, length);
            return trimmed;
        }

        // Sorted, ordinal, forward-slashed -- the same order on every machine
        // and every filesystem. Directory.EnumerateFiles' own order is not
        // specified and has been observed to differ between the Editor and
        // the dotnet host on the same folder.
        public static IEnumerable<string> Enumerate(string repoRoot)
        {
            var found = new List<string>();

            foreach (var source in Sources)
            {
                string absolute = Path.Combine(repoRoot, source.Path.Replace('/', Path.DirectorySeparatorChar));

                if (source.Pattern == null)
                {
                    if (File.Exists(absolute)) found.Add(source.Path);
                    continue;
                }

                if (!Directory.Exists(absolute)) continue;

                var option = source.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string file in Directory.EnumerateFiles(absolute, source.Pattern, option))
                {
                    string relative = file.Substring(repoRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                    found.Add(relative);
                }
            }

            found.Sort(StringComparer.Ordinal);
            return found;
        }
    }
}
