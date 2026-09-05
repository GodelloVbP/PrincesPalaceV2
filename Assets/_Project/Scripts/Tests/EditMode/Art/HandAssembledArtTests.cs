using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace PrincesPalace.Domain.Tests
{
    // ART THE TOOLS CANNOT REMAKE, pinned by content hash.
    //
    // Most frames under Resources/Spells are cut from a sheet by
    // tools/slice_spell_sheet.py and are reproducible: delete them, re-run it,
    // get the same bytes. A few are not. golem_boulder is hand-assembled --
    // f2==f3 is a held peak, f4/f5 are a stepped alpha fade, f0 is a
    // deliberately blank wind-up -- and the sheet it came from does not even
    // share a grid with it. Regenerating it does not restore it. Nothing
    // restores it except the copy in git.
    //
    // WHY THIS EXISTS, stated plainly because the reason is the whole value.
    //
    // The slicer grew a --new flag that scaffolds a sheet nobody has cut yet.
    // Its first version guarded on "is this id already in the VFX manifest"
    // and golem_boulder is not in VFX -- it is in HAND_ASSEMBLED, the register
    // immediately below it, whose entire purpose is naming the sequences the
    // tool must never touch. It was then pointed at that id to test whether
    // the guard worked, and cut six cells of the wrong sheet over all six
    // frames.
    //
    // The recovery was git checkout, and the file-level lesson is "add the
    // second guard", which has been done. But a guard inside one tool only
    // protects against that tool. This is the rule outside every tool: if the
    // bytes change, the suite fails, whatever changed them and whyever.
    //
    // UPDATING A HASH IS ALLOWED and is meant to cost one deliberate edit. Art
    // is supposed to change. What is not supposed to happen is art changing
    // without anyone choosing it, which is exactly what a green suite over
    // silently-clobbered frames looks like.
    public class HandAssembledArtTests
    {
        // Content hashes as of 2026-08-23, read from the working tree and
        // confirmed byte-identical to the repository's first commit.
        //
        // f2 and f3 SHARE A HASH on purpose. That is the held peak the register
        // describes, and a change that made them differ would be a real change
        // to how the boulder lands even though every file still existed.
        private static readonly Dictionary<string, string[]> Pinned =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["golem_boulder"] = new[]
                {
                    "e358d523753f08cf724422c042c7c1b76f0b861d04a8c0112f0f1a98057508a9",
                    "9dfae0727a0922ad00f410d166a797ab2922731a95633fe6ffb1cd6aef6637a0",
                    "af0c432b08189dbfeaec02eabd7153eb0fe05997e21602fa04a2f4d8f62a5488",
                    "af0c432b08189dbfeaec02eabd7153eb0fe05997e21602fa04a2f4d8f62a5488",
                    "40a99e0a997064667021f0b85b285947441053b17fe4f5b3d4f6def275da0f4a",
                    "b6056eba90b2ede6d9ff9b113bf9cd9e3557a0f399a6f0fc84bc466ef3630de8",
                },
            };

        private static string FramesDir(string id) =>
            Path.Combine(Application.dataPath, "_Project", "Resources", "Spells", id);

        private static string HashOf(string file)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        [Test]
        public void HandAssembledFramesAreUnchanged()
        {
            var broken = new List<string>();

            foreach (var pair in Pinned)
            {
                string dir = FramesDir(pair.Key);
                Assert.IsTrue(Directory.Exists(dir),
                    $"'{pair.Key}' has no frames directory at all. This art cannot be regenerated -- " +
                    "recover it from git rather than re-running the slicer.");

                for (int i = 0; i < pair.Value.Length; i++)
                {
                    string file = Path.Combine(dir, $"f{i}.png");
                    if (!File.Exists(file))
                    {
                        broken.Add($"{pair.Key}/f{i}.png is MISSING");
                        continue;
                    }

                    string actual = HashOf(file);
                    if (actual != pair.Value[i])
                    {
                        broken.Add($"{pair.Key}/f{i}.png changed ({pair.Value[i].Substring(0, 12)} -> {actual.Substring(0, 12)})");
                    }
                }
            }

            Assert.IsEmpty(broken,
                "Hand-assembled art changed:\n  " + string.Join("\n  ", broken) + "\n\n" +
                "These frames are NOT reproducible from their source sheet -- re-running " +
                "tools/slice_spell_sheet.py does not restore them. If this was not deliberate, " +
                "recover with:\n\n" +
                "    git checkout -- Assets/_Project/Resources/Spells/\n\n" +
                "If it WAS deliberate, update the hashes in this file in the same commit as the art.");
        }

        // THE PIN AND THE REGISTER HAVE TO AGREE.
        //
        // The hashes above are a second list, and a second list drifts. It can
        // only drift one way that matters: a sequence added to the register and
        // never pinned here would be unprotected while this file still read as
        // coverage. So this reads the register and refuses that.
        //
        // The register is a JSON file under Assets rather than a dict inside
        // the Python tool, and that is not tidiness. The headless runner these
        // tests execute in mirrors Assets and nothing else -- a register living
        // beside the tool is invisible here, which means the one list saying
        // what is irreplaceable would be readable only by the tool most likely
        // to overwrite it.
        //
        // The other direction is deliberately NOT an error. A pin here for
        // something the register no longer names still guards real bytes, and
        // failing on it would turn tidying the register into a puzzle about a
        // passing test.
        [Test]
        public void EveryRegisteredSequenceIsPinnedHere()
        {
            string register = Path.Combine(Application.dataPath,
                "_Project", "Art", "Sheets", "hand_assembled.json");

            Assert.IsTrue(File.Exists(register),
                $"the hand-assembled register is not at '{register}'. Both the slicer and this test " +
                "read it; without it neither knows which art is irreplaceable, and this test is " +
                "guarding nothing.");

            // Hand-parsed rather than via JsonUtility, which cannot deserialise
            // a dictionary with author-chosen keys -- and inventing a wrapper
            // type for a six-line file would put the shape in two places again.
            string body = File.ReadAllText(register);
            int start = body.IndexOf("\"sequences\"", StringComparison.Ordinal);
            Assert.Greater(start, -1, "the register has no \"sequences\" block");

            var ids = Regex.Matches(body.Substring(start), @"""([A-Za-z0-9_]+)""\s*:")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(id => id != "sequences")
                .ToList();

            Assert.IsNotEmpty(ids,
                "no ids were parsed out of the register. Either it is empty -- in which case delete " +
                "this test rather than leaving it passing on nothing -- or its shape changed and " +
                "this parse needs updating.");

            var unpinned = ids.Where(id => !Pinned.ContainsKey(id)).ToList();

            Assert.IsEmpty(unpinned,
                "The register names these sequences as hand-assembled, but nothing pins their bytes: " +
                string.Join(", ", unpinned) + ". Add each to Pinned above with the hash of every " +
                "frame, or the register is the only thing standing between them and the next tool " +
                "that writes into that folder.");
        }
    }
}
