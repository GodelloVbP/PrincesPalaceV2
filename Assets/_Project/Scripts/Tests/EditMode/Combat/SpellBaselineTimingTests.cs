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
    // The spell-layers plan asks for a baseline capture of five spells
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

        // WHAT M6 PUT WHERE CINDERFAULT'S OWN BASELINE ROW WAS, on the same
        // terms M5 replaced the pilot's: the fifth TestCase above read
        // path/seconds/impactFrame off a pre-layer block cinderfault no longer
        // authors, so deleting it would have deleted the only committed record
        // of what the re-authoring had to reproduce.
        //
        // THE NUMBER THAT MATTERS IS 0.43333334, and it is stated twice on
        // purpose. Once as what the legacy derivation produced -- 0.78s times
        // ImpactFraction(5, 9), the expression this spell's cue came out of for
        // its whole life -- and once as what skills.json now authors outright.
        // A layered block owes nothing to a frame index, which is the brief's
        // point 6; what it owes is landing the blow on the same instant, and
        // that is an equality between two literals rather than a claim.
        //
        // The frame counts stay pinned even though timing no longer depends on
        // them: with `seconds` authored and `fps` unauthored the folder is
        // FITTED into `seconds`, so a re-cut sheet changes the per-frame rate
        // of both layers, and this is where that becomes visible rather than a
        // capture nobody diffs. Nine frames in 0.39s is 43ms each -- fast, and
        // the point: the rupture is HELD for two of those nine (the recipe's
        // own `hold: 2`), so the peak still reads as a peak rather than as a
        // dropped frame.
        [Test]
        public void TheCinderfaultLayersRuptureOnTheInstantItsLegacyBlockDid()
        {
            string skill = JsonBlocks.ObjectsInArray(SkillsJson(), "skills")
                .FirstOrDefault(s => JsonBlocks.String(s, "id") == "cinderfault");
            Assert.IsNotNull(skill, "skills.json has no cinderfault");

            var vfx = SpellVfxJson.OwnVfx(skill);
            Assert.IsNotNull(vfx, "cinderfault authors no vfx block");

            Assert.AreEqual(1, vfx.layerFormat, "cinderfault is the second spell through the layered model");
            Assert.IsEmpty(vfx.path,
                "a layered block authors no single-block path; the rules refuse one that authors both");
            Assert.IsEmpty(vfx.groundPath,
                "the shared fault is a layer placed on the formation now, not a second block beside the first");

            // HALVED 2026-09-19, ON THE OWNER'S WORD ("the animation is too
            // slow ... it should feel more like a POP"). The legacy equality
            // below is kept because it is the record of what the cast used to
            // be: 5/9 of 0.78s is where the blow landed for this spell's whole
            // life, and the cue now sits at exactly half of it. Stated as an
            // exact halving rather than as a new free number so that a later
            // retune has to argue with a relationship rather than overwrite a
            // literal nobody can source.
            Assert.AreEqual(0.21666667f, vfx.hitCueSeconds, 1e-6f,
                "the authored cue moved off half the instant the legacy block landed the blow on");
            Assert.AreEqual(0.43333334, 0.78 * CombatBeat.ImpactFraction(5, 9), 1e-6,
                "the legacy instant was 5/9 of 0.78s, which is what impactFrame 5 over nine frames meant");
            Assert.AreEqual(0.21666667, 0.43333334 / 2.0, 1e-6,
                "and today's cue is exactly half of it, because the whole cast was halved");

            Assert.AreEqual(2, vfx.layers.Length, "one shared fault and one plume per struck target");
            CollectionAssert.AreEqual(new[] { "fault", "erupt" }, vfx.layers.Select(l => l.id).ToList());

            Assert.AreEqual("formation", vfx.layers[0].place,
                "one fault however many enemies stand in it -- the property that needs no spell id");
            Assert.AreEqual("ground", vfx.layers[0].sort);
            Assert.AreEqual("Spells/cinderfault_ground", vfx.layers[0].path);

            Assert.AreEqual("target", vfx.layers[1].place, "the plumes fan out over the struck slots");
            Assert.AreEqual("Spells/cinderfault_eruption", vfx.layers[1].path);

            foreach (var layer in vfx.layers)
            {
                Assert.AreEqual("release", layer.at,
                    "both open with the beat, which is what makes their peaks share an instant");
                Assert.AreEqual(0.39f, layer.seconds, 1e-6f,
                    "half of the 0.78s both layers ran for until 2026-09-19");
                Assert.AreEqual(0f, layer.fps,
                    "an unauthored rate fits the folder into `seconds`, which is what the pre-layer " +
                    "block's own frame rate was");
                Assert.AreEqual("none", layer.facing,
                    "a fault is symmetrical about the rack it opens under, and so is what erupts out of it");
            }

            Assert.AreEqual(9, FramesOnDisk("Spells/cinderfault_ground"));
            Assert.AreEqual(9, FramesOnDisk("Spells/cinderfault_eruption"));
        }

        // WHAT M5 PUT WHERE THE BASELINE'S ABSENCE WAS. This assertion used to
        // read "prismatic_orb authors no presentation at all", which is why M0
        // could not photograph a "before"; the note on it said the layered pin
        // should REPLACE it rather than delete it, so here it is.
        //
        // ON THE ELEMENT AND NOT ON THE SKILL, which is the whole shape of the
        // pilot: the orb itself draws nothing and each element may bring its
        // own art. Water was the only one that had any through M9; Fire, Wind
        // and Earth each got the same four-recipe, five-layer treatment as a
        // content-only delivery afterward (no C# touched), so all four of the
        // orb's elements now author their own presentation and this pin was
        // updated to say so -- it was a literal list of "today"'s state, not a
        // rule that a fourth element could not be added.
        [Test]
        public void ThePilotSpellAuthorsAllFourElementsAndTheOrbItselfNone()
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

            CollectionAssert.AreEqual(new[] { "Earth", "Water", "Fire", "Wind" }, authored,
                "all four of the orb's elements have art today, in authored order");

            var water = SpellVfxJson.OwnVfx(JsonBlocks.ObjectsInArray(skill, "elements")
                .First(e => JsonBlocks.String(e, "type") == "Water"));

            Assert.AreEqual(1, water.layerFormat);
            // Was 0.327 (arrival + two contact frames at 26fps); pulled to
            // arrival exactly so the flinch lands on the touch (frame 20-22)
            // rather than the end of the crown's opening (frame 27) -- 2026-09-08
            // second battle-speed pass, measured against the 1x strip.
            // Was 0.25 (== travelSeconds, no charge). 2026-09-09: a caster-side
            // charge layer was added ahead of the core with offset 0.1667s
            // (core opens exactly when the charge's frame 6 begins), so arrival
            // and the cue both slid by that same 0.1667s to keep the touch and
            // the damage together -- 0.25 + 0.1667 = 0.4167.
            Assert.AreEqual(0.4167f, water.hitCueSeconds, 1e-6f,
                "the cue is arrival exactly -- the reaction is timed to the touch, not to the " +
                "contact art finishing -- authored in seconds rather than derived from a frame index");
            Assert.IsEmpty(water.path,
                "a layered block authors no single-block path; the rules refuse one that authors both");

            // "charge" added 2026-09-09 ahead of "core": the caster-side
            // gather-then-release beat that plays before the flight core opens.
            CollectionAssert.AreEqual(new[] { "charge", "core", "wake", "shed", "splash", "spray" },
                water.layers.Select(l => l.id).ToList(),
                "authored order is draw order, so it is part of the content rather than an accident");
        }
    }
}
