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
        public void TheCourtOrbitsThenLeansInAndTheFearLandsOnTheCue()
        {
            var vfx = Court();
            Assert.AreEqual(0.7f, vfx.hitCueSeconds, 1e-6f,
                "the payoff: sound, Fear badges and the violet rim on one frame; the cast riser is 0.7 s");

            // Far arc behind the body (ground band), near arc in front, then the lean.
            var back = vfx.layers.Single(l => l.id == "orbit_back");
            var front = vfx.layers.Single(l => l.id == "orbit_front");
            var lean = vfx.layers.Single(l => l.id == "lean");
            Assert.AreEqual("effects", back.sort, "the ground band holds 2 renderers and a rack needs 3, so the far arc cannot go behind the bodies yet");
            Assert.AreEqual("effects", front.sort);
            Assert.AreEqual("none", back.facing);
            Assert.AreEqual("reverse", front.facing, "the near arc runs the other way, so it is mirrored");
            Assert.Less(back.size, front.size, "the far arc is smaller");
            Assert.Less(back.opacity, front.opacity, "and dimmer");
            Assert.AreEqual(0.36f, back.offset + back.seconds - 0.02f, 0.05f, "the far arc ends where the near one begins");
            // On screen a layer sits at drift * size + dx, and a reverse layer mirrors both.
            Assert.AreEqual(back.drift.toX * back.size + back.dx, -(front.drift.toX * front.size + front.dx), 2f,
                "the hand-off happens at the same x (within 2 units)");
            Assert.AreEqual(0.7f, front.offset + front.seconds - 0.02f, 0.05f, "the near arc ends on the cue");

            Assert.AreEqual("Spells/court_of_whispers_courtier", lean.path, "the painted leaning courtier, one still");
            Assert.AreEqual(0.58f, lean.offset, 1e-6f, "it crossfades in over the orbit's last 0.12 s and is full on the cue");
            Assert.AreEqual(0.7f, lean.offset + lean.fadeIn, 1e-6f);
            Assert.AreEqual(front.drift.toX * front.size + front.dx, lean.drift.fromX * lean.size + lean.dx, 1f,
                "the lean starts where the near arc stopped (both mirrored)");
            Assert.AreEqual("reverse", lean.facing, "it whispers from behind: hood to the head, body trailing away from the snout");
            Assert.AreEqual(-0.2f, lean.drift.viaX, 1e-6f, "closest to the head at the waypoint");
            Assert.AreEqual(-0.3f, lean.drift.toX, 1e-6f, "and a little back out, where the smoke starts");
            Assert.IsNull(vfx.layers.FirstOrDefault(l => l.id == "core"), "no ring at the payoff: the rim is the payoff");
            Assert.AreEqual(3, vfx.layers.Count(l => l.render == "sprite" && l.place == "target-head" && l.fit == "head"),
                "three sprite layers a body, all at the head (place target-head, fit head): the band reserves 12 renderers for a formation of three");
            Assert.LessOrEqual(lean.size, 80f, "small enough that three courtiers on a rack stay three");

            var rim = vfx.bodyRim;
            Assert.IsTrue(rim.HasRim);
            Assert.AreEqual("#B58CFF", rim.tint);
            Assert.AreEqual(4, rim.pulses);
            Assert.AreEqual(0.07f, rim.spacing, 1e-6f);
        }

        [Test]
        public void TheCourtComesApartIntoSmokeThatThinsInThreeSteps()
        {
            var vfx = Court();
            var steps = new[] { "smoke_a", "smoke_b", "curl" }.Select(id => vfx.layers.Single(l => l.id == id)).ToArray();
            Assert.Less(steps[0].offset, steps[1].offset);
            Assert.Less(steps[1].offset, steps[2].offset);
            Assert.Greater(steps[0].emitter.rate, steps[1].emitter.rate, "fewer each step");
            Assert.AreEqual(1, steps[2].emitter.burst, "the last thing on the stage is one curl");
            Assert.Greater(steps[0].emitter.gravity, 0f, "smoke rises");
            Assert.IsTrue(steps.All(l => l.place == "above" && l.height == 0.3f), "the smoke is anchored where the courtier is: the head");
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
            Assert.AreEqual(0.4f, vfx.gloom.fadeIn, 1e-6f);
            Assert.AreEqual(0.4f, vfx.gloom.fadeOut, 1e-6f);
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
