using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THE "BEFORE" OF THE LAYERED-SPELL REWRITE, as numbers rather than as
    // pictures.
    //
    // docs/PLAN_SPELL_LAYERS.md M0 asks for a baseline capture of five spells
    // so M3 can prove that routing every legacy block through the new
    // orchestration path changed nothing. Its pictures cannot be the pin:
    // tools/screenshots/ is gitignored, so a capture set is evidence a human
    // looks at once and never a thing a later run can compare against.
    //
    // What CAN be committed is the arithmetic those pictures were sampled at.
    // FightController.ImpactDelayFor is `vfx.seconds * ImpactFraction(
    // vfx.impactFrame, framesOnDisk)`, and all three of its inputs are files
    // in this repository. Pinned here as LITERAL seconds -- not recomputed
    // from the production expression, which would pass for any expression --
    // so the day the adapter retimes a shipped spell by a millisecond, this
    // says which spell and by how much.
    //
    // The sixth row is prismatic_orb, and it is the honest baseline the plan's
    // L7 records: the orb authors no vfx block at all, so its "before" is no
    // art and a zero delay. Manufacturing a temporary block to photograph
    // would have made the baseline a thing this branch invented rather than a
    // thing it inherited.
    public class SpellBaselineTimingTests
    {
        private static string SkillsJson() =>
            File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project", "ContentData", "skills.json"));

        private static string FramesRoot() =>
            Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources", "Spells");

        // How many frames FrameSequenceLoader would find: f0 upward, stopping
        // at the first gap, which is the loader's own rule (it breaks on the
        // first miss rather than globbing, so f10 cannot precede f2).
        private static int FramesOnDisk(string resourcesFolder)
        {
            string dir = Path.Combine(FramesRoot(), resourcesFolder.Replace("Spells/", ""));
            if (!Directory.Exists(dir)) return 0;

            int count = 0;
            while (File.Exists(Path.Combine(dir, $"f{count}.png"))) count++;
            return count;
        }

        private static string VfxOf(string skillId)
        {
            string skill = JsonBlocks.ObjectsInArray(SkillsJson(), "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == skillId);
            Assert.IsNotNull(skill, $"skills.json has no skill '{skillId}'");
            return JsonBlocks.ObjectFor(skill, "vfx");
        }

        // seconds, impactFrame, frames on disk, and the delay in seconds that
        // falls out of them. Every number a literal.
        [TestCase("lightning_bolt", "Spells/lightning_bolt", 0.52, 5, 9, 0.28888889)]
        [TestCase("frost_flare", "Spells/frost_flare", 0.52, 5, 9, 0.28888889)]
        [TestCase("mud_burst", "Spells/mud_burst", 0.65, 13, 26, 0.325)]
        [TestCase("cinderfault", "Spells/cinderfault_eruption", 0.78, 5, 9, 0.43333334)]
        public void TheImpactDelayOfEveryBaselineSpellIsWhatItWasBeforeTheLayers(
            string skillId, string path, double seconds, int impactFrame, int frames, double delaySeconds)
        {
            string vfx = VfxOf(skillId);
            Assert.IsNotNull(vfx, $"{skillId} authors no vfx block");

            Assert.AreEqual(path, JsonBlocks.String(vfx, "path"), $"{skillId}: vfx.path moved");
            Assert.AreEqual(seconds, JsonBlocks.Number(vfx, "seconds").Value, 1e-6,
                $"{skillId}: vfx.seconds changed, which retimes the whole cast");
            Assert.AreEqual(impactFrame, (int)JsonBlocks.Number(vfx, "impactFrame").Value,
                $"{skillId}: vfx.impactFrame changed, which moves the blow inside the sequence");
            Assert.AreEqual(frames, FramesOnDisk(path),
                $"{skillId}: '{path}' has a different number of frames on disk, which moves the impact " +
                "delay even with the authored numbers untouched -- that is the coupling the layered " +
                "format exists to remove, and changing it is a retiming, not a re-cut");

            Assert.AreEqual(delaySeconds, seconds * CombatBeat.ImpactFraction(impactFrame, frames), 1e-6,
                $"{skillId}: the impact delay is no longer the baseline this branch started from");
        }

        // The ground layer rides the per-target sequence's numbers today
        // (SpellPresentation.GroundSeconds / GroundImpactFrame fall back), so
        // the fault and the eruption rupture on the same instant. The layered
        // re-authoring in M6 states 0.433 explicitly; this is the number it
        // has to equal.
        [Test]
        public void TheCinderfaultGroundLayerRupturesOnTheSameInstantAsItsPlumes()
        {
            string vfx = VfxOf("cinderfault");

            Assert.AreEqual("Spells/cinderfault_ground", JsonBlocks.String(vfx, "groundPath"));
            Assert.IsNull(JsonBlocks.Number(vfx, "groundSeconds"),
                "cinderfault authors no groundSeconds, so the fault inherits the eruption's 0.78s");
            Assert.IsNull(JsonBlocks.Number(vfx, "groundImpactFrame"),
                "cinderfault authors no groundImpactFrame, so the fault inherits the eruption's frame 5");
            Assert.AreEqual(9, FramesOnDisk("Spells/cinderfault_ground"));

            Assert.AreEqual(0.43333334, 0.78 * CombatBeat.ImpactFraction(5, 9), 1e-6);
        }

        // WHAT M5 PUT WHERE THE BASELINE'S ABSENCE WAS. This assertion used to
        // read "prismatic_orb authors no presentation at all", which is why M0
        // could not photograph a "before"; the note on it said the layered pin
        // should REPLACE it rather than delete it, so here it is.
        //
        // ON THE ELEMENT AND NOT ON THE SKILL, which is the whole shape of the
        // pilot: the orb itself draws nothing and each element may bring its
        // own art. Water is the one that has any, and the other three staying
        // empty is what makes "one element authored, three not" a case the
        // resolver and the pool pin both have to handle.
        [Test]
        public void ThePilotSpellAuthorsItsWaterElementAndOnlyThat()
        {
            string skill = JsonBlocks.ObjectsInArray(SkillsJson(), "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == "prismatic_orb");
            Assert.IsNotNull(skill, "skills.json has no prismatic_orb");

            Assert.IsNull(SpellVfxJson.OwnVfx(skill),
                "the orb itself authors a vfx block. Its art belongs to whichever element is cast, " +
                "and a skill-level block would draw for all four.");

            var authored = new List<string>();
            foreach (string element in JsonBlocks.ObjectsInArray(skill, "elements"))
            {
                if (SpellVfxJson.OwnVfx(element) != null) authored.Add(JsonBlocks.String(element, "type"));
            }

            CollectionAssert.AreEqual(new[] { "Water" }, authored,
                "exactly one of the orb's four elements has art today");

            var water = SpellVfxJson.OwnVfx(JsonBlocks.ObjectsInArray(skill, "elements")
                .First(e => JsonBlocks.String(e, "type") == "Water"));

            Assert.AreEqual(1, water.layerFormat);
            Assert.AreEqual(0.35f, water.hitCueSeconds, 1e-6f,
                "the cue is arrival (0.25s) plus 0.10s of compression, authored in seconds rather " +
                "than derived from a frame index -- which is the whole of the brief's point 6");
            Assert.IsEmpty(water.path,
                "a layered block authors no single-block path; the rules refuse one that authors both");

            CollectionAssert.AreEqual(new[] { "core", "wake", "shed", "splash", "spray" },
                water.layers.Select(l => l.id).ToList(),
                "authored order is draw order, so it is part of the content rather than an accident");
        }
    }
}
