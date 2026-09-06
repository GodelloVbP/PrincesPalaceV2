using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

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
            internal int ImpactFrame;
            internal int DepartFrame;
            internal string Field;     // "path" or "groundPath"
        }

        // Every vfx block in skills.json, plus the ground layer a spell may
        // author beside it -- cinderfault plays two folders on one timeline and
        // the second one is just as capable of not existing.
        private static List<SkillVfx> SkillVfxBlocks()
        {
            string file = Path.Combine(Root(), "Assets", "_Project", "ContentData", "skills.json");
            Assert.IsTrue(File.Exists(file), $"no skills.json at '{file}'");

            var found = new List<SkillVfx>();

            foreach (string skill in JsonBlocks.ObjectsInArray(File.ReadAllText(file), "skills"))
            {
                string vfx = JsonBlocks.ObjectFor(skill, "vfx");
                if (vfx == null) continue;

                string id = JsonBlocks.String(skill, "id") ?? "?";
                int impact = (int)(JsonBlocks.Number(vfx, "impactFrame") ?? 0d);
                int depart = (int)(JsonBlocks.Number(vfx, "departFrame") ?? 0d);

                string path = JsonBlocks.String(vfx, "path");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    found.Add(new SkillVfx { SkillId = id, Path = path, ImpactFrame = impact, DepartFrame = depart, Field = "path" });
                }

                string ground = JsonBlocks.String(vfx, "groundPath");
                if (!string.IsNullOrWhiteSpace(ground))
                {
                    found.Add(new SkillVfx { SkillId = id, Path = ground, ImpactFrame = impact, DepartFrame = depart, Field = "groundPath" });
                }
            }

            Assert.IsNotEmpty(found,
                "no vfx blocks parsed out of skills.json -- either every spell lost its art, or the " +
                "parse stopped matching the file's shape and this fixture is guarding nothing.");
            return found;
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

        [Test]
        public void EveryFolderASkillPlaysHasARecordedProvenance()
        {
            var recipes = RecipeIds();
            var handAssembled = HandAssembledIds();

            Assert.IsNotEmpty(recipes,
                $"no recipes under '{RecipeDir()}'. Either they moved and this fixture is asserting on an " +
                "empty set, or the tool's manifest was inlined again -- a record only the Python can read " +
                "is one the suite cannot enforce.");

            var orphans = new List<string>();

            foreach (var vfx in SkillVfxBlocks())
            {
                string id = vfx.Path.Replace('\\', '/').Trim('/');
                int slash = id.LastIndexOf('/');
                if (slash >= 0) id = id.Substring(slash + 1);

                if (recipes.Contains(id) || handAssembled.Contains(id)) continue;

                orphans.Add($"{vfx.SkillId}'s {vfx.Field} '{vfx.Path}'");
            }

            Assert.IsEmpty(orphans,
                "these skills play frames whose origin nothing records:\n  " + string.Join("\n  ", orphans) +
                "\n\nEvery played folder is either produced by a recipe under " +
                "Art/Sheets/recipes/ or named in Art/Sheets/hand_assembled.json as art no tool can " +
                "remake. A folder in neither is one nobody wrote down how to rebuild, which is exactly " +
                "the state golem_boulder was in when a scaffold run cut over all six of its frames.");
        }

        [Test]
        public void NoSkillTimesABeatToAFrameItsFolderDoesNotHave()
        {
            var broken = new List<string>();
            int checkedBlocks = 0;

            foreach (var vfx in SkillVfxBlocks())
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
                checkedBlocks++;

                if (frames == 0)
                {
                    broken.Add($"{vfx.SkillId}: '{vfx.Path}' holds no f*.png at all");
                    continue;
                }

                // 1-BASED, both of them -- see the vfx table in
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
            }

            Assert.Greater(checkedBlocks, 0, "no folder was measured, so this rule is vacuous");
            Assert.IsEmpty(broken,
                "these skills time a beat to a frame that does not exist. The player clamps rather than " +
                "throwing, so the symptom is a blow landing at the wrong moment rather than an error:\n  "
                + string.Join("\n  ", broken));
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
