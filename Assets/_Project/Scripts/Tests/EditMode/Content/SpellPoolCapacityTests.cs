using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE THREE POOL CONSTANTS, DERIVED FROM CONTENT RATHER THAN RE-MEASURED.
    //
    // This is the part that carries the weight. FightHudSpec's numbers are
    // reservations with headroom, and a reservation nobody checks is a number
    // somebody measured once and then defended -- which is exactly how a
    // hand-measured tab-width table shipped wrong for months. So the check is
    // not "is 12 still a good number", it is "what does the authored content of
    // every skill and every element actually ask for", counted per band, and
    // failing NAMING THE SKILL.
    //
    // COUNTED PER BAND, because a spell whose ground layers outgrew
    // SpellGroundRenderers would otherwise be found by a player rather than by
    // the build: the two bands have separate pools and a total that fits says
    // nothing about either half.
    //
    // WHAT IT DELIBERATELY DOES NOT COUNT: two casts overlapping. That is a
    // property of a FIGHT rather than of content -- a tail from the previous
    // round still drawing when the next opens -- and it is what the headroom in
    // each constant is for, with the overflow policy (a dropped picture, never
    // a dropped cue) as the floor under it.
    public class SpellPoolCapacityTests
    {
        private sealed class Demand
        {
            internal string Skill;
            internal int Effects;
            internal int Ground;
            internal int Particles;
        }

        private static string SkillsJson() =>
            File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project", "ContentData", "skills.json"));

        // Every vfx block in the game, as the layers it resolves to -- an
        // authored list for a layered spell, and the adapter's output for a
        // pre-layer one, because a pre-layer block spends the same pool.
        private static List<Demand> Demands()
        {
            var found = new List<Demand>();

            foreach (string skill in JsonBlocks.ObjectsInArray(SkillsJson(), "skills"))
            {
                string id = JsonBlocks.String(skill, "id") ?? "?";

                int targets = TargetsOf(skill);

                var vfx = SpellVfxJson.OwnVfx(skill);
                if (vfx != null) found.Add(DemandOf(id, vfx, targets));

                // ELEMENTS COUNT SEPARATELY AND NEVER TOGETHER: a cast picks
                // one element, so the pool has to hold the biggest of them
                // rather than all of them at once.
                foreach (string element in JsonBlocks.ObjectsInArray(skill, "elements"))
                {
                    var choice = SpellVfxJson.OwnVfx(element);
                    if (choice != null)
                    {
                        found.Add(DemandOf($"{id} element '{JsonBlocks.String(element, "type")}'",
                            choice, targets));
                    }
                }
            }

            return found;
        }

        // HOW MANY THINGS THIS SKILL CAN ACTUALLY STRIKE, off its own targeting
        // rather than a worst case applied to everything.
        //
        // This is the correction that makes the pin mean something. Fanning
        // every spell out to a full formation says the pilot's five layers want
        // 84 particles -- and prismatic_orb is a single-target skill, so that
        // describes a cast the game cannot produce. A reservation sized against
        // a fiction is not a measurement.
        //
        // THROUGH THE RESOLVER'S OWN DefaultTargetingFor, because `targeting`
        // is almost always unauthored and DERIVED from `effect`: reading the
        // key alone reports every spell in the game as single-target, including
        // the one spell with a formation layer. A second copy of that mapping
        // here would be free to disagree with the one the game resolves by.
        private static int TargetsOf(string skill)
        {
            string authored = (JsonBlocks.String(skill, "targeting") ?? "").Trim();

            var effect = SkillEffect.DamageSingle;
            System.Enum.TryParse(JsonBlocks.String(skill, "effect") ?? "", ignoreCase: true, out effect);

            var targeting = SkillEntryResolver.DefaultTargetingFor(effect);
            if (!string.IsNullOrWhiteSpace(authored))
            {
                System.Enum.TryParse(authored, ignoreCase: true, out targeting);
            }

            return targeting == SkillTargeting.AllEnemies || targeting == SkillTargeting.Party
                ? FightHudSpec.StageSlotsPerSide
                : 1;
        }

        private static Demand DemandOf(string label, SpellPresentation vfx, int targets)
        {
            // The frame count only reaches travelDelay/travelSeconds, neither
            // of which changes how many renderers a cast wants, so a nominal
            // one is honest here.
            var performance = SpellPerformance.Resolve(vfx, targets, _ => 9);

            var demand = new Demand { Skill = label };
            foreach (var instance in performance.Instances)
            {
                if (instance.Layer.Render == SpellRender.Emitter)
                {
                    demand.Particles += SpellEmitterSim.CountOf(instance.Layer.emitter);
                    continue;
                }

                if (instance.Layer.Sort == SpellSort.Ground) demand.Ground++;
                else demand.Effects++;
            }

            return demand;
        }

        [Test]
        public void NoSpellAsksForMoreEffectsRenderersThanTheBandReserves()
        {
            var over = Demands()
                .Where(d => d.Effects > FightHudSpec.SpellLayerRenderers)
                .Select(d => $"{d.Skill} wants {d.Effects}")
                .ToList();

            CollectionAssert.IsEmpty(over,
                $"these spells outgrew FightHudSpec.SpellLayerRenderers ({FightHudSpec.SpellLayerRenderers}), " +
                "so a layer of each would silently draw nothing: " + string.Join(", ", over));
        }

        [Test]
        public void NoSpellAsksForMoreGroundRenderersThanTheBandReserves()
        {
            var over = Demands()
                .Where(d => d.Ground > FightHudSpec.SpellGroundRenderers)
                .Select(d => $"{d.Skill} wants {d.Ground}")
                .ToList();

            CollectionAssert.IsEmpty(over,
                $"these spells outgrew FightHudSpec.SpellGroundRenderers ({FightHudSpec.SpellGroundRenderers})" +
                ": " + string.Join(", ", over));
        }

        [Test]
        public void NoSpellAsksForMoreParticlesThanThePoolReserves()
        {
            var over = Demands()
                .Where(d => d.Particles > FightHudSpec.SpellParticles)
                .Select(d => $"{d.Skill} wants {d.Particles}")
                .ToList();

            CollectionAssert.IsEmpty(over,
                $"these spells outgrew FightHudSpec.SpellParticles ({FightHudSpec.SpellParticles}): " +
                string.Join(", ", over));
        }

        // THE VACUITY GUARD. A pin that scans nothing passes everything, and
        // the three tests above would all be green against a skills.json this
        // failed to read at all.
        [Test]
        public void TheSweepActuallyFindsThePresentationsItIsGuarding()
        {
            var demands = Demands();

            Assert.GreaterOrEqual(demands.Count, 6,
                "the sweep found " + demands.Count + " presentations. At least six author a vfx block " +
                "today -- lightning_bolt, frost_flare, mud_burst, bog_mud_burst, cinderfault, and " +
                "prismatic_orb's Water element -- so a sweep finding fewer has stopped reading the file.");

            // THE ONE LAYERED BLOCK IN THE FILE, found where a skill-level
            // lookup would not look. prismatic_orb authors no vfx of its own,
            // so a reader that took the first "vfx" at any depth would report
            // the element's block as the skill's -- counting one cast twice and
            // naming it after a key that is not in the file.
            var water = demands.FirstOrDefault(d => d.Skill == "prismatic_orb element 'Water'");
            Assert.IsNotNull(water,
                "the pilot's layered block was not read out of skills.json at all, so the only spell " +
                "in the game that spends the particle pool is not being counted against it.");
            Assert.Greater(water.Particles, 0, "the pilot's two emitters were read as spending nothing");

            var cinderfault = demands.FirstOrDefault(d => d.Skill == "cinderfault");
            Assert.IsNotNull(cinderfault, "cinderfault is the only spell with a ground layer and it was missed");
            Assert.AreEqual(1, cinderfault.Ground, "a formation layer draws once however many enemies stand in it");
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, cinderfault.Effects,
                "a target-placed layer draws once per struck slot, so a full formation is the worst case");
        }

        // The two proof spells the design was validated against, counted the
        // same way -- because the capacity has to hold what M5 and M6 are about
        // to author, not only what ships today.
        [Test]
        public void TheTwoProofSpellsFitTheBandsTheyDrawIn()
        {
            // prismatic_orb is SingleEnemy and cinderfault is AllEnemies, which
            // is what each is authored as and therefore what each may spend.
            var water = DemandOf("water pilot", SpellLayerFixtures.Water(), 1);
            var cinderfault = DemandOf("cinderfault layered", SpellLayerFixtures.Cinderfault(),
                FightHudSpec.StageSlotsPerSide);

            Assert.LessOrEqual(water.Effects, FightHudSpec.SpellLayerRenderers);
            Assert.LessOrEqual(water.Particles, FightHudSpec.SpellParticles);
            Assert.LessOrEqual(cinderfault.Ground, FightHudSpec.SpellGroundRenderers);
            Assert.LessOrEqual(cinderfault.Effects, FightHudSpec.SpellLayerRenderers);

            // Stated as literals so the numbers behind "12, 2 and 64 are
            // reservations with headroom" are readable rather than implied:
            // prismatic_orb is DamageSingle, so its five layers fan out to one
            // target -- three sprite instances and two emitters, whose 10 shed
            // drops and 18-drop burst are the 28 the particle pool is sized
            // against.
            Assert.AreEqual(3, water.Effects);
            Assert.AreEqual(28, water.Particles);

            // AND THE SHIPPED BLOCK SPENDS WHAT THE FIXTURE SAYS. The two are
            // kept the same by hand, so this is the line that notices when they
            // stop being -- against the demand rather than field by field,
            // because what this file guards is what a cast COSTS.
            var authored = Demands().First(d => d.Skill == "prismatic_orb element 'Water'");
            Assert.AreEqual(water.Effects, authored.Effects,
                "skills.json's Water block draws a different number of sprite layers than the fixture " +
                "every other pin in this suite is written against");
            Assert.AreEqual(water.Particles, authored.Particles,
                "skills.json's Water block spends a different number of particles than the fixture");
            Assert.AreEqual(1, cinderfault.Ground);
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, cinderfault.Effects);
        }
    }
}
