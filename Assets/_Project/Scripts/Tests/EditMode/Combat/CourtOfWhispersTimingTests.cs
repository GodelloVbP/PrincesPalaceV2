using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // COURT OF WHISPERS' BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md
    // 3.1). Wind-up and payoff, the price after the payoff, and when the stage
    // is clean. A retune says which number moved.
    public class CourtOfWhispersTimingTests
    {
        private static SpellPresentation Court()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "court_of_whispers");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheCourtLeansInForTheWindUpAndTheFearLandsOnTheCue()
        {
            var vfx = Court();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f,
                "the payoff: sound, Fear badges and the core on one frame; the cast riser is 0.3 s");

            foreach (string id in new[] { "lean-a", "lean-b" })
            {
                var lean = vfx.layers.Single(l => l.id == id);
                Assert.AreEqual(0.65f, lean.seconds, 1e-6f);
                Assert.AreEqual(16f, lean.fps, 1e-6f, "f5, where the ring opens, is frame 6 of 16 fps: 0.31 s");
                Assert.AreEqual(-0.7f, lean.drift.fromX, 1e-6f);
                Assert.AreEqual(-0.25f, lean.drift.viaX, 1e-6f, "closest to the body at the waypoint");
                Assert.AreEqual(0.46f, lean.drift.viaAt, 1e-6f, "0.46 of 0.65 s is the 0.3 s cue");
                Assert.AreEqual(-0.7f, lean.drift.toX, 1e-6f, "and back out again");
            }

            Assert.AreEqual("reverse", vfx.layers.Single(l => l.id == "lean-b").facing, "the second courtier is the mirror");
            var core = vfx.layers.Single(l => l.id == "core");
            Assert.AreEqual("hit", core.at);
            Assert.AreEqual(0f, core.offset, 1e-6f);
            Assert.AreEqual(3, vfx.layers.Count(l => l.render == "sprite" && l.place == "target-centre"),
                "three sprite layers a body: the band reserves 12 renderers for a formation of three");
        }

        [Test]
        public void ShawnsPriceLandsAfterTheFearAndHisBadgeWaitsForIt()
        {
            var vfx = Court();
            var price = vfx.layers.Single(l => l.id == "price");
            Assert.AreEqual("caster-centre", price.place);
            Assert.AreEqual("hit", price.at);
            Assert.AreEqual(0.18f, price.offset, 1e-6f);
            Assert.AreEqual(0.18f, vfx.casterCostSeconds, 1e-6f, "badge and pulse arrive together");
        }

        [Test]
        public void TheGloomLiftsOnThePayoff()
        {
            var vfx = Court();
            Assert.AreEqual(0.35f, vfx.gloom.strength, 1e-6f);
            Assert.AreEqual(0.2f, vfx.gloom.fadeIn, 1e-6f);
            Assert.AreEqual(0.25f, vfx.gloom.fadeOut, 1e-6f);
            Assert.AreEqual(SpellGloomEnd.Hit, vfx.gloom.Until);

            Assert.AreEqual(0.5f, vfx.layers.Single(l => l.id == "settle").emitter.lifeMax, 1e-6f);
        }

        // The slide itself, as literals: quadratic ease-out, so the layer is
        // three quarters of the way at half its life.
        [Test]
        public void ADriftWithAWaypointGoesOutAndComesBack()
        {
            var drift = new SpellDrift { fromX = -0.7f, viaX = -0.25f, viaAt = 0.5f, toX = -0.7f };
            Assert.AreEqual(-0.7f, drift.At(0f, 1f).x, 1e-6f);
            Assert.AreEqual(-0.25f, drift.At(0.5f, 1f).x, 1e-6f);
            Assert.AreEqual(-0.3625f, drift.At(0.25f, 1f).x, 1e-6f, "first leg, three quarters of the way at half of it");
            Assert.AreEqual(-0.7f, drift.At(1f, 1f).x, 1e-6f);
        }

        [Test]
        public void ADriftEasesOutOverTheLayersLifetime()
        {
            var drift = new SpellDrift { fromX = -0.6f, toX = -0.2f, fromY = 0f, toY = 0.1f };
            var start = drift.At(0f, 0.4f);
            var half = drift.At(0.2f, 0.4f);
            var end = drift.At(0.5f, 0.4f);
            Assert.AreEqual(-0.6f, start.x, 1e-6f);
            Assert.AreEqual(-0.3f, half.x, 1e-6f);
            Assert.AreEqual(0.075f, half.y, 1e-6f);
            Assert.AreEqual(-0.2f, end.x, 1e-6f, "past the end it holds the end");
            Assert.IsFalse(new SpellDrift().IsAuthored);
        }
    }
}
