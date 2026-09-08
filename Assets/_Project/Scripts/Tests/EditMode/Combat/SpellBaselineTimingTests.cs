using System.IO;
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

        // L7, as an assertion rather than as prose. The pilot spell has no
        // "before" in game, which is why M0 could not photograph one.
        [Test]
        public void ThePilotSpellStillAuthorsNoPresentationAtAll()
        {
            string skill = JsonBlocks.ObjectsInArray(SkillsJson(), "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == "prismatic_orb");
            Assert.IsNotNull(skill, "skills.json has no prismatic_orb");

            Assert.IsNull(JsonBlocks.ObjectFor(skill, "vfx"),
                "prismatic_orb now authors a vfx block. That is M5's work, and when it lands this " +
                "assertion is what should be replaced by the layered pin -- not deleted.");
        }
    }
}
