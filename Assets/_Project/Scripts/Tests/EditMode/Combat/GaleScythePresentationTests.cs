using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // GALE SCYTHE'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.4,
    // decision 8): a quiet 0.2 s, one crescent swept across the line and gone
    // before the cue, then ONE payoff frame for every enemy.
    public class GaleScythePresentationTests
    {
        private static SpellPresentation Gale()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "gale_scythe");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheSweepCrossesTheLineAndIsGoneByTheCue()
        {
            var vfx = Gale();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var gather = vfx.layers.Single(l => l.id == "gather");
            Assert.AreEqual("caster-centre", gather.place);
            Assert.AreEqual(0.2f, gather.seconds, 1e-6f, "the quiet: nothing is on the enemies for 0.2 s");

            var sweep = vfx.layers.Single(l => l.id == "sweep");
            Assert.AreEqual("formation", sweep.place);
            Assert.AreEqual("span", sweep.align, "laid along the line from the first body to the last");
            Assert.AreEqual(0.13f, sweep.offset, 1e-6f);
            Assert.AreEqual(0.13f, sweep.seconds, 1e-6f);
            Assert.AreEqual(0.26f, sweep.offset + sweep.seconds, 1e-6f, "done before the 0.3 s cue; its fade ends on it");
            Assert.AreEqual(0.05f, sweep.fade, 1e-6f);
            Assert.AreEqual(-0.45f, sweep.drift.fromX, 1e-6f, "starts on the caster's side of the front body");
            Assert.AreEqual(0.22f, sweep.drift.toX, 1e-6f);
        }

        [Test]
        public void EveryEnemyIsCutOnTheSameFrame()
        {
            var vfx = Gale();
            var cut = vfx.layers.Single(l => l.id == "cut");
            Assert.AreEqual("target-centre", cut.place);
            Assert.AreEqual("hit", cut.at);
            Assert.AreEqual(0f, cut.offset, 1e-6f, "one payoff frame for the whole line: numbers, sound and crescents together");
            Assert.AreEqual(6, cut.startFrame, "f5, the big crescent");
            Assert.AreEqual(0.43f, cut.seconds, 1e-6f);
            Assert.AreEqual(300f, cut.size, 1e-6f, "three of them must not wall over the numbers (the old ritual was 380)");
            Assert.AreEqual(1, vfx.layers.Count(l => l.render == "sprite" && l.place == "target-centre"),
                "one sprite a body: with the gather and the sweep, five of the 12 renderers a formation of three may use");
            Assert.AreEqual("hit", vfx.layers.Single(l => l.id == "settle").at);
        }
    }
}
