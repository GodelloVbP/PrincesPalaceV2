using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

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

        // PROTECTED LEGACY ACTORS, pinned the same way and for the same reason.
        //
        // These stills were PICKED out of frame sheets that no longer drive
        // anything (48131f4) and re-composited; which cell became which stance
        // was a judgement made against contact sheets, never an invocation. The
        // register beside them says so per actor, in as much detail as survives.
        //
        // Their hashes are over the delivered PNGs under Resources, not over the
        // sheets, because the delivered PNG is what a tool run would overwrite.
        // Recorded 2026-09-06 from the working tree.
        private static readonly Dictionary<string, string> PinnedActorStills =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Enemies/rat/attack"] = "f64e74052d52dbe92deb25d068e2c5fbb7ad6760427b4a1f834fc9e80ce392d2",
                ["Enemies/rat/cast"] = "a2e753915d7556da839275ebc7edd9064829961060fe0e5fa7dd7a5cead2721f",
                ["Enemies/rat/defeated"] = "d33b1a3a8f3c0a6ae074ae1b907f6881bc893445ad13e3a86c8d3d98de67e728",
                ["Enemies/rat/extra"] = "fed49c57a50c82e8561778ae2a19d219f8763cbce1d0f70697c123c11765d9da",
                ["Enemies/rat/guard"] = "fed49c57a50c82e8561778ae2a19d219f8763cbce1d0f70697c123c11765d9da",
                ["Enemies/rat/hurt"] = "4e6481875b2ddee10c4ccee29c3185225b1e4759a4352588d9cefadf2983d572",
                ["Enemies/rat/idle"] = "557d349d4a4d039f4d6233b50066b64c66e0a9c4f96acd0e21bcf10b5924a7bc",
                ["Enemies/golem/attack"] = "694c992a903c8c43ace18253603a9e90aa36ff8a85da3dce89351ae1ad21bd1b",
                ["Enemies/golem/cast"] = "694c992a903c8c43ace18253603a9e90aa36ff8a85da3dce89351ae1ad21bd1b",
                ["Enemies/golem/defeated"] = "537fb115d4c7522b67ecfd7d4898d3f1cc2c8aaf7f7d192e64854e301c0bbd0b",
                ["Enemies/golem/guard"] = "62550badfeeb3a83a1790c42f43299a738711b3467737d6aece1d3fa20b475d8",
                ["Enemies/golem/hurt"] = "8c88b9daf62b369697246fe493677d15a1dc12013b463a0b970b0b18d19e9c5a",
                ["Enemies/golem/idle"] = "4f72eec9a23c53cf215f4a22da0aedca2208611c91fd7a4d25bb06bc74c312f1",
                ["Enemies/bog_witch/attack"] = "1b98fc6e20b47f8586c4f52a74cb77b847c59ed4ffd496378053161a4b17225f",
                ["Enemies/bog_witch/cast"] = "4288a40a681cc54d944f7f9f8f209c0d68aecff24b473a5bec9ee0b65b20942c",
                ["Enemies/bog_witch/defeated"] = "6ce9624c612b65612e001bcdab872d09690492d35bfad7849b6709bbbccf4e86",
                ["Enemies/bog_witch/hurt"] = "79cffd6a7f8bf15853962c7430397cd8a7454b05a44c171da9515307fe452b6e",
                ["Enemies/bog_witch/idle"] = "19b8c39660f1e0a00b38eb47c194928e866fe4fa60e96ac7c715b6e0f342a563",
                ["Enemies/bog_witch/taunt"] = "d7b650898eb9db64abe402b5be809f51d0f3992248102abbd08087f477f4f265",
                ["Enemies/beetle/attack"] = "8437e20434d436fc4a90c7b313262187f2e5ce3337613f0fd6c0d90b5df2c4b9",
                ["Enemies/beetle/defeated"] = "846b6b34dd107625556d654d7eb81f1641f9bdc46f173b9eceb9a398746eafba",
                ["Enemies/beetle/hurt"] = "c5aa0b90e6434c4a9ac7fc785b40562db12c83d4bcef1b6b53fd9ca525d8e297",
                ["Enemies/beetle/idle"] = "3603bf04401f70a710353562a79e84e7b7a4e9c77e54f61bfb8ea5200471ca57",
                ["Enemies/beetle/shell_closed"] = "a655934f7859ee6ba3651b4dd9b5611828dd7e4bf164b5dd32bcd34098dbf499",
                ["Enemies/beetle/turtle_up"] = "3e6daa84d24bc4bdceafc2982278061d275cda25c2188a7888aa02e2cb60e4a0",
                ["Enemies/forest_warden/attack"] = "393c97f954a356075ce254fca74ccf0e994971d515e07d2a0daf91315028ceea",
                ["Enemies/forest_warden/attack_charge"] = "684ecc734790e79b7e42ecc91530d29f55612c1bd22d5b65d3068b4e0749da09",
                ["Enemies/forest_warden/attack_roar"] = "327731fa7e11314339f52bc09dbcb2e054fd9a063f493dc988aa2e0cc5bd0fb8",
                ["Enemies/forest_warden/defeated"] = "923d788391ccad54d62926f88eb0d9bec0f111e149f93600b420d9711f34aa07",
                ["Enemies/forest_warden/hurt"] = "209cc3098e541f314357d849dae7b72139767f06001a308f8d39d2e819477b98",
                ["Enemies/forest_warden/idle"] = "5e9149258df79dde6fb5a6352f85c81e09cc0400dc1cf5586c4d5a9a01b11bb2",
                ["Characters/sheep/attack"] = "42419370b854d70546ddae9e89ae6ef778c001706a1f1c89c43ff4b409d26b70",
                ["Characters/sheep/cast"] = "1c4b1f79bf3574409eee1f170b2d5e2da33a44ca36623493eae9643d66a9278b",
                ["Characters/sheep/defeated"] = "8ea040a7d9cd6868565d8b348b66a71327f0639301ae41895e69f17bc611df13",
                ["Characters/sheep/hurt"] = "1cc9dab1776baace4232aba4ce0bd810ef14504fc112d8f0bf9922756cdfba12",
                ["Characters/sheep/idle"] = "9b932a6da5449cda249b820eb4611fa3d5405d60fca04d997d1fb0b1a41cc298",
                ["Characters/sheep/victory"] = "71e8bb591a365a328e93f80d2f5133e8043a2c949509983aa32c361c64f82d03",
            };

        private static string FramesDir(string id) =>
            Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources", "Spells", id);

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
            var ids = JsonBlocks.KeysOfObject(Register(), "sequences");

            Assert.IsNotEmpty(ids,
                "no ids were parsed out of the register's sequences block. Either it is empty -- in " +
                "which case delete this test rather than leaving it passing on nothing -- or its " +
                "shape changed and this parse needs updating.");

            var unpinned = ids.Where(id => !Pinned.ContainsKey(id)).ToList();

            Assert.IsEmpty(unpinned,
                "The register names these sequences as hand-assembled, but nothing pins their bytes: " +
                string.Join(", ", unpinned) + ". Add each to Pinned above with the hash of every " +
                "frame, or the register is the only thing standing between them and the next tool " +
                "that writes into that folder.");
        }

        // ---- the actor half ----------------------------------------------------

        [Test]
        public void ProtectedLegacyActorStillsAreUnchanged()
        {
            var broken = new List<string>();

            foreach (var pair in PinnedActorStills)
            {
                string file = Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources",
                    pair.Key.Replace('/', Path.DirectorySeparatorChar) + ".png");

                if (!File.Exists(file))
                {
                    broken.Add($"{pair.Key}.png is MISSING");
                    continue;
                }

                string actual = HashOf(file);
                if (actual != pair.Value)
                {
                    broken.Add($"{pair.Key}.png changed ({pair.Value.Substring(0, 12)} -> {actual.Substring(0, 12)})");
                }
            }

            Assert.IsEmpty(broken,
                "Protected-legacy actor art changed:\n  " + string.Join("\n  ", broken) + "\n\n" +
                "These stills were picked out of frame sheets by hand and re-composited; no recorded " +
                "invocation reproduces them, so re-running tools/slice_actor_sheet.py does not " +
                "restore them. If this was not deliberate:\n\n" +
                "    git checkout -- Assets/_Project/Resources/Enemies/ Assets/_Project/Resources/Characters/\n\n" +
                "If it WAS deliberate, update the hashes here in the same commit as the art -- and " +
                "consider whether the actor now HAS a recipe, in which case it belongs in the other " +
                "category and out of the register entirely.");
        }

        [Test]
        public void EveryRegisteredActorIsPinnedHere()
        {
            var registered = RegisteredActors();

            Assert.IsNotEmpty(registered,
                "no actors were parsed out of the register. Either the actors block is empty -- delete " +
                "this test rather than leaving it passing on nothing -- or its shape changed.");

            var unpinned = registered
                .Where(a => !PinnedActorStills.Keys.Any(k => k.StartsWith(a + "/", StringComparison.Ordinal)))
                .ToList();

            Assert.IsEmpty(unpinned,
                "The register calls these actors protected legacy, and nothing pins a single one of " +
                "their stills: " + string.Join(", ", unpinned) + ". A register entry with no hash " +
                "behind it is a claim, not a guard.");
        }

        // EXACTLY ONE CATEGORY, EVERY DELIVERED ACTOR.
        //
        // REPRODUCIBLE means a recipe.json beside the actor's source art under
        // Art/, which slice_actor_sheet.py --recipe replays. PROTECTED LEGACY
        // means an entry in the register above, hash-pinned, explicitly not
        // reproducible.
        //
        // NEITHER is the state that matters, and it is the one that is invisible:
        // an actor nobody wrote down how to rebuild reads exactly like one
        // somebody did, right up until the sheet is needed and is not there.
        // golem_boulder sat in that state until a scaffold run cut over all six
        // of its frames.
        //
        // BOTH is a contradiction rather than belt and braces -- it says the art
        // is reproducible and also that nothing can remake it -- and the likely
        // cause is an actor that gained a recipe and was never taken out of the
        // register, at which point the register is lying about the roster.
        [Test]
        public void EveryDeliveredActorIsInExactlyOneCategory()
        {
            var registered = new HashSet<string>(RegisteredActors(), StringComparer.OrdinalIgnoreCase);
            var neither = new List<string>();
            var both = new List<string>();
            int delivered = 0;

            foreach (string kind in new[] { "Enemies", "Characters" })
            {
                string root = Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources", kind);
                if (!Directory.Exists(root)) continue;

                foreach (string folder in Directory.GetDirectories(root))
                {
                    string actor = kind + "/" + Path.GetFileName(folder);
                    if (Directory.GetFiles(folder, "*.png").Length == 0) continue;
                    delivered++;

                    bool hasRecipe = File.Exists(Path.Combine(RepoTree.Root(), "Assets", "_Project", "Art",
                        actor.Replace('/', Path.DirectorySeparatorChar), "recipe.json"));
                    bool isProtected = registered.Contains(actor);

                    if (hasRecipe && isProtected) both.Add(actor);
                    else if (!hasRecipe && !isProtected) neither.Add(actor);
                }
            }

            Assert.Greater(delivered, 5,
                $"only {delivered} delivered actors were found, so this rule is close to vacuous -- " +
                "eight ship today. Either Resources moved or the walk stopped finding folders.");

            Assert.IsEmpty(neither,
                "these actors ship art whose origin nothing records:\n  " + string.Join("\n  ", neither) +
                "\n\nEither slice them with tools/slice_actor_sheet.py so a recipe.json lands beside " +
                "their source sheet, or add them to Art/Sheets/hand_assembled.json's actors block " +
                "with their bytes pinned in this file. Both answers are fine; the gap is not.");

            Assert.IsEmpty(both,
                "these actors have a recipe AND are registered as unreproducible:\n  " +
                string.Join("\n  ", both) + "\n\nThose statements contradict each other. If the recipe " +
                "replays the committed bytes, take the actor out of the register and out of " +
                "PinnedActorStills; if it does not, delete the recipe rather than leaving a file " +
                "that claims a reproduction nobody has seen.");
        }

        private static string Register()
        {
            string register = Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "Art", "Sheets", "hand_assembled.json");

            Assert.IsTrue(File.Exists(register),
                $"the hand-assembled register is not at '{register}'. Both the slicer and this test " +
                "read it; without it neither knows which art is irreplaceable, and this test is " +
                "guarding nothing.");

            return File.ReadAllText(register);
        }

        private static List<string> RegisteredActors()
        {
            return JsonBlocks.ObjectsInArray(Register(), "actors")
                .Select(block => JsonBlocks.String(block, "actor"))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();
        }
    }
}
