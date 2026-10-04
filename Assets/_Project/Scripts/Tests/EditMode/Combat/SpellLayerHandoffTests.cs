using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // A LAYER ENDING AT t AND ONE STARTING AT t SHARE NO GAP AND NO OVERLAP.
    // The schedule opens and closes layers through SpellSchedule.Passed (1e-4
    // slack); the frame cursor used to compare the raw age against 0, so on the
    // tick a cursor read a float hair under the cue (18 ticks of 1/60 s are
    // 0.29998) the wind-up was closed and the payoff drew nothing, for one frame.
    public class SpellLayerHandoffTests
    {
        private static SpellPerformance Resolve(string id)
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == id);
            // The wind-up sheets are five frames; the painted payoffs are single stills.
            return SpellPerformance.Resolve(SpellVfxJson.OwnVfx(skill), 1,
                path => path.EndsWith("_catch") || path.EndsWith("_quicken") || path.EndsWith("_lock") ? 1 : 5);
        }

        private static bool Shown(SpellLayerInstance layer, int frames, float seconds) =>
            SpellFrameCursor.SampleOf(layer, frames, seconds).Visible;

        [TestCase("ashen_reckoning", "catch")]
        [TestCase("borrowed_moment", "quicken")]
        [TestCase("censer_of_embers", "catch")]
        public void TheWindUpHandsOverToThePayoffOnTheSameTick(string spell, string payoff)
        {
            var performance = Resolve(spell);
            var windUp = performance.Instances.Single(i => i.Layer.id == "ritual");
            var accent = performance.Instances.Single(i => i.Layer.id == payoff);

            // The cursor values a 60 fps clock produces around the 0.3 s cue.
            foreach (float cursor in new[] { 0.29998f, 0.3f, 0.30002f })
            {
                bool windUpGone = SpellSchedule.Passed(windUp.ClearedSeconds, cursor);
                Assert.IsTrue(windUpGone, $"{spell}: the schedule closes the wind-up at {cursor}");
                Assert.IsTrue(Shown(accent, 1, cursor), $"{spell}: the payoff draws at {cursor}, the tick the wind-up closes");
            }

            Assert.IsTrue(Shown(windUp, 5, 0.28f), "the wind-up still draws one frame before");
            Assert.IsFalse(Shown(accent, 1, 0.28f), "and the payoff has not opened");
        }

        [Test]
        public void EveryTickOfASixtyFramesClockDrawsExactlyOneOfTheTwo()
        {
            var performance = Resolve("ashen_reckoning");
            var windUp = performance.Instances.Single(i => i.Layer.id == "ritual");
            var accent = performance.Instances.Single(i => i.Layer.id == "catch");

            float now = 0f;
            for (int tick = 0; tick < 30; tick++, now += 1f / 60f)
            {
                bool closed = SpellSchedule.Passed(windUp.ClearedSeconds, now);
                bool opened = SpellSchedule.Passed(accent.StartSeconds, now);
                Assert.AreEqual(closed, opened, $"tick {tick}: close and open are one instant");
                if (!closed) Assert.IsTrue(Shown(windUp, 5, now), $"tick {tick} wind-up");
                Assert.AreEqual(opened, Shown(accent, 1, now), $"tick {tick} payoff");
            }
        }
    }
}
