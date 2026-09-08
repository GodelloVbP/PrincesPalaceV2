using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // EVERY SPELL A SKILL PLAYS HAS A RECORDED PROVENANCE, AND ENOUGH FRAMES.
    //
    // WHAT THIS DELIBERATELY DOES NOT CHECK. Not that a skill's timing matches
    // its recipe's -- there is no such thing. The recipe describes how frames
    // are PRODUCED (which sheet, which grid, which cells, how they compose);
    // the skill's vfx block describes how they PLAY (how long, which frame the
    // blow lands on, where it anchors). Two skills may point at one folder with
    // different timing and both be right: mud_burst and bog_mud_burst share
    // Spells/mud_burst today, and the 2026-09-05 baseline's own spell exercise
    // was a second skill over frost_flare's frames at a different speed. A test
    // that made a skill match a recipe would forbid the thing the split exists
    // to allow.
    //
    // What is left is the part that is arithmetic rather than judgement, and it
    // is genuinely two things:
    //
    //   PROVENANCE -- every folder a skill plays was either produced by a
    //   recipe or is named in hand_assembled.json as art no tool can remake.
    //   Exactly the two categories, and a folder in neither is one whose
    //   origin nobody wrote down; that is how golem_boulder came to be cut over
    //   by a scaffold run, and how "we don't know how this was made" turns into
    //   a silent gap that reads as nothing to see.
    //
    //   ARITHMETIC -- a skill cannot land its impact on a frame the folder does
    //   not have. impactFrame and departFrame are 1-based and the player clamps
    //   silently, so an off-by-a-lot reads as a beat that fires at the wrong
    //   moment rather than as an error.
    public class SpellVfxRecipeDriftTests
    {
        private static string Root() => RepoTree.Root();

        private static string RecipeDir() =>
            Path.Combine(Root(), "Assets", "_Project", "Art", "Sheets", "recipes");

        private static string SpellsRoot() =>
            Path.Combine(Root(), "Assets", "_Project", "Resources", "Spells");

        private sealed class SkillVfx
        {
            internal string SkillId;
            internal string Path;      // "Spells/frost_flare"
            internal int ImpactFrame;  // 1-based; a pre-layer block's blow
            internal int DepartFrame;  // 1-based; a pre-layer block's throw
            internal int StartFrame;   // 1-based; a layer's first drawn frame
            internal string Field;     // "vfx.path", "vfx.layers[3].emitter.path", ...
        }

        // EVERY FOLDER ANY SPELL PLAYS, from all four places one can be named:
        // a pre-layer block's `path` and `groundPath`, and a layered block's
        // `layers[].path` and `layers[].emitter.path`.
        //
        // AND FROM AN ELEMENT'S OWN BLOCK, not only a skill's. That is where the
        // first layered spell in the game actually lives -- prismatic_orb
        // authors no vfx of its own and its Water element authors five layers --
        // so a sweep reading skills only would have declared the whole pilot
        // absent and passed.
        private static List<SkillVfx> SkillVfxBlocks()
        {
            string file = Path.Combine(Root(), "Assets", "_Project", "ContentData", "skills.json");
            Assert.IsTrue(File.Exists(file), $"no skills.json at '{file}'");

            var found = new List<SkillVfx>();

            foreach (string skill in JsonBlocks.ObjectsInArray(File.ReadAllText(file), "skills"))
            {
                string id = JsonBlocks.String(skill, "id") ?? "?";

                Collect(found, id, SpellVfxJson.OwnVfx(skill));

                foreach (string element in JsonBlocks.ObjectsInArray(skill, "elements"))
                {
                    Collect(found, $"{id} element '{JsonBlocks.String(element, "type")}'",
                        SpellVfxJson.OwnVfx(element));
                }
            }

            Assert.IsNotEmpty(found,
                "no vfx blocks parsed out of skills.json -- either every spell lost its art, or the " +
                "parse stopped matching the file's shape and this fixture is guarding nothing.");
            return found;
        }

        private static void Collect(List<SkillVfx> found, string label, SpellPresentation vfx)
        {
            if (vfx == null) return;

            if (!string.IsNullOrWhiteSpace(vfx.path))
            {
                found.Add(new SkillVfx
                {
                    SkillId = label, Path = vfx.path, Field = "vfx.path",
                    ImpactFrame = vfx.impactFrame, DepartFrame = vfx.departFrame,
                });
            }

            if (!string.IsNullOrWhiteSpace(vfx.groundPath))
            {
                found.Add(new SkillVfx
                {
                    SkillId = label, Path = vfx.groundPath, Field = "vfx.groundPath",
                    ImpactFrame = vfx.GroundImpactFrame, DepartFrame = 0,
                });
            }

            var layers = vfx.layers ?? System.Array.Empty<SpellLayer>();
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;

                // A LAYER HAS NO impactFrame AND NO departFrame. Its cue is
                // hitCueSeconds on the block and its flight is travelSeconds on
                // itself, both in seconds -- decoupling those from the frame
                // count is the whole of the brief's point 6. What a layer CAN
                // index into its folder is startFrame, so that is the number
                // this range-checks, and it is the only guard `startFrame: 2`
                // has anywhere.
                if (!string.IsNullOrWhiteSpace(layer.path))
                {
                    found.Add(new SkillVfx
                    {
                        SkillId = label, Path = layer.path, Field = $"vfx.layers[{i}].path",
                        StartFrame = layer.startFrame,
                    });
                }

                string drops = layer.emitter == null ? "" : layer.emitter.path;
                if (!string.IsNullOrWhiteSpace(drops))
                {
                    found.Add(new SkillVfx
                    {
                        SkillId = label, Path = drops, Field = $"vfx.layers[{i}].emitter.path",
                    });
                }
            }
        }

        private static HashSet<string> RecipeIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(RecipeDir())) return ids;

            foreach (string file in Directory.GetFiles(RecipeDir(), "*.json"))
            {
                ids.Add(Path.GetFileNameWithoutExtension(file));
            }

            return ids;
        }

        private static HashSet<string> HandAssembledIds()
        {
            string register = Path.Combine(Root(), "Assets", "_Project", "Art", "Sheets", "hand_assembled.json");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(register)) return ids;

            string body = File.ReadAllText(register);
            int at = body.IndexOf("\"sequences\"", StringComparison.Ordinal);
            if (at < 0) return ids;

            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(body.Substring(at), @"""([A-Za-z0-9_]+)""\s*:"))
            {
                if (m.Groups[1].Value != "sequences") ids.Add(m.Groups[1].Value);
            }

            return ids;
        }

        // The walk itself, so the test below can be aimed at content this file
        // fabricates as well as at content the game ships. A lint that scans
        // nothing passes everything, and the only way to know this one still
        // scans is to hand it something it must refuse.
        private static List<string> OrphansAmong(IEnumerable<SkillVfx> blocks)
        {
            var recipes = RecipeIds();
            var handAssembled = HandAssembledIds();
            var orphans = new List<string>();

            foreach (var vfx in blocks)
            {
                string id = vfx.Path.Replace('\\', '/').Trim('/');
                int slash = id.LastIndexOf('/');
                if (slash >= 0) id = id.Substring(slash + 1);

                if (recipes.Contains(id) || handAssembled.Contains(id)) continue;

                orphans.Add($"{vfx.SkillId}'s {vfx.Field} '{vfx.Path}'");
            }

            return orphans;
        }

        [Test]
        public void EveryFolderASkillPlaysHasARecordedProvenance()
        {
            Assert.IsNotEmpty(RecipeIds(),
                $"no recipes under '{RecipeDir()}'. Either they moved and this fixture is asserting on an " +
                "empty set, or the tool's manifest was inlined again -- a record only the Python can read " +
                "is one the suite cannot enforce.");

            var orphans = OrphansAmong(SkillVfxBlocks());

            Assert.IsEmpty(orphans,
                "these skills play frames whose origin nothing records:\n  " + string.Join("\n  ", orphans) +
                "\n\nEvery played folder is either produced by a recipe under " +
                "Art/Sheets/recipes/ or named in Art/Sheets/hand_assembled.json as art no tool can " +
                "remake. A folder in neither is one nobody wrote down how to rebuild, which is exactly " +
                "the state golem_boulder was in when a scaffold run cut over all six of its frames.");
        }

        // The frame-range walk, pulled out for the same reason the provenance
        // walk above was: the only proof a range check still ranges is a case
        // built to fail it.
        private static List<string> OutOfRangeAmong(IEnumerable<SkillVfx> blocks, out int measured)
        {
            var broken = new List<string>();
            measured = 0;

            foreach (var vfx in blocks)
            {
                string folder = Path.Combine(SpellsRoot(),
                    vfx.Path.StartsWith("Spells/", StringComparison.OrdinalIgnoreCase)
                        ? vfx.Path.Substring("Spells/".Length).Replace('/', Path.DirectorySeparatorChar)
                        : vfx.Path.Replace('/', Path.DirectorySeparatorChar));

                if (!Directory.Exists(folder))
                {
                    broken.Add($"{vfx.SkillId}: {vfx.Field} '{vfx.Path}' is not a folder on disk");
                    continue;
                }

                int frames = Directory.GetFiles(folder, "f*.png").Length;
                measured++;

                if (frames == 0)
                {
                    broken.Add($"{vfx.SkillId}: '{vfx.Path}' holds no f*.png at all");
                    continue;
                }

                // 1-BASED, all three -- see the vfx table in
                // docs/ART_PIPELINE.md 5b. impactFrame 5 is the fifth frame, so
                // a folder of nine is fine and a folder of four is not.
                if (vfx.ImpactFrame > frames)
                {
                    broken.Add($"{vfx.SkillId}: impactFrame {vfx.ImpactFrame} but '{vfx.Path}' has {frames} frames");
                }

                if (vfx.DepartFrame > frames)
                {
                    broken.Add($"{vfx.SkillId}: departFrame {vfx.DepartFrame} but '{vfx.Path}' has {frames} frames");
                }

                // START ON A FRAME THE FOLDER HAS, and one it has something
                // AFTER: a layer beginning on the last frame plays a single
                // still and calls it an animation. The pilot's splash authors
                // startFrame 2 of nine to skip a contact frame that draws the
                // ball still approaching, and this is the only thing anywhere
                // that checks the 2 against the nine.
                if (vfx.StartFrame > frames)
                {
                    broken.Add($"{vfx.SkillId}: {vfx.Field} starts on frame {vfx.StartFrame} but " +
                               $"'{vfx.Path}' has {frames} frames");
                }
            }

            return broken;
        }

        [Test]
        public void NoSkillTimesABeatToAFrameItsFolderDoesNotHave()
        {
            var broken = OutOfRangeAmong(SkillVfxBlocks(), out int measured);

            Assert.Greater(measured, 0, "no folder was measured, so this rule is vacuous");
            Assert.IsEmpty(broken,
                "these skills time a beat to a frame that does not exist. The player clamps rather than " +
                "throwing, so the symptom is a blow landing at the wrong moment rather than an error:\n  "
                + string.Join("\n  ", broken));
        }

        // ---- and the two proofs that either rule still refuses anything ----------
        //
        // A LINT THAT SCANS NOTHING PASSES EVERYTHING, and both rules above
        // fail by going quiet: a sweep that stopped seeing layers reports no
        // orphans and no bad frames, and reads exactly like a clean tree. So
        // each is aimed at a fabricated block built to be refused. The
        // fabrication is deliberately shaped like the pilot -- a layer path and
        // an emitter path -- because those are the two places the sweep grew.

        [Test]
        public void TheProvenanceSweepRefusesALayerPlayingAnUnrecordedFolder()
        {
            var made = new List<SkillVfx>
            {
                new SkillVfx
                {
                    SkillId = "fabricated", Field = "vfx.layers[0].path",
                    Path = "Spells/no_recipe_records_this",
                },
                new SkillVfx
                {
                    SkillId = "fabricated", Field = "vfx.layers[0].emitter.path",
                    Path = "Spells/nor_this_one",
                },
            };

            var orphans = OrphansAmong(made);

            Assert.AreEqual(2, orphans.Count,
                "the provenance walk did not refuse two folders no recipe produces, so it would not " +
                "refuse a real one either: " + string.Join("; ", orphans));
            Assert.IsTrue(orphans[0].Contains("vfx.layers[0].path"),
                "the refusal has to name the FIELD, or an author is told a spell is wrong without " +
                "being told which of its six paths: " + orphans[0]);
        }

        [Test]
        public void TheFrameRangeSweepRefusesAStartFramePastTheEndOfTheFolder()
        {
            // A REAL FOLDER with a startFrame it does not reach, which is the
            // shape of the mistake: the folder exists, the recipe is recorded,
            // the provenance check is happy, and the layer opens on a frame
            // that is not there.
            var madeUp = new List<SkillVfx>
            {
                new SkillVfx
                {
                    SkillId = "fabricated", Field = "vfx.layers[0].path",
                    Path = "Spells/prismatic_orb_water_contact", StartFrame = 99,
                },
            };

            var broken = OutOfRangeAmong(madeUp, out int measured);

            Assert.AreEqual(1, measured, "the fabricated folder was not measured at all");
            Assert.AreEqual(1, broken.Count,
                "a layer starting on frame 99 of a nine-frame folder was accepted, so startFrame is " +
                "range-checked by nothing: " + string.Join("; ", broken));
            Assert.IsTrue(broken[0].Contains("99") && broken[0].Contains("9 frames"),
                "the refusal states neither the authored frame nor the count it exceeded: " + broken[0]);
        }

        // TWO SKILLS, ONE FOLDER, DIFFERENT TIMING IS LEGAL, and this is the
        // test that says so out loud rather than leaving it as an absence. The
        // recipe owns the frames; the skill owns the beat. A future tightening
        // that made a folder belong to one skill would break bog_mud_burst,
        // every reuse of an existing effect, and the cheapest way there is to
        // author a spell at all.
        [Test]
        public void OneFolderMayBePlayedByMoreThanOneSkill()
        {
            var byFolder = SkillVfxBlocks()
                .GroupBy(v => v.Path, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(v => v.SkillId).Distinct().Count() > 1)
                .ToList();

            Assert.IsNotEmpty(byFolder,
                "no frame folder is shared by two skills any more. That is not a failure of the game -- it " +
                "is a failure of this test, which exists to pin that sharing is ALLOWED and can only " +
                "demonstrate it while an example ships. Point it at the current example, or delete it " +
                "rather than leaving it passing on nothing.");

            // Each sharer is checked on its OWN numbers, which is the whole
            // content of "the skill owns the beat": the folder constrains how
            // many frames there are and nothing else, so two skills over one
            // folder are two independent questions rather than one shared one.
            foreach (var group in byFolder)
            {
                string folder = Path.Combine(SpellsRoot(),
                    group.Key.Substring(group.Key.IndexOf('/') + 1).Replace('/', Path.DirectorySeparatorChar));
                int frames = Directory.Exists(folder) ? Directory.GetFiles(folder, "f*.png").Length : 0;

                foreach (var sharer in group)
                {
                    Assert.LessOrEqual(sharer.ImpactFrame, frames,
                        $"{sharer.SkillId} shares '{group.Key}' and is held to that folder's {frames} " +
                        "frames on its own numbers, not to whatever the other sharer authored.");
                }
            }
        }
    }
}
