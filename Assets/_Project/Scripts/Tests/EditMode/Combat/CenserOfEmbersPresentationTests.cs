using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // CENSER OF EMBERS' BEATS as literal seconds (docs/PLAN_SPELL_ANIMATION.md
    // section 3): a 1.0 s wind-up (the owner's call, over contract 10's cap) in
    // which the censer hangs from off-stage and swings left, right, left about
    // its chain top, breaks into an ember shower on the third extreme with the
    // sound and the Burn badge, and leaves a living fire at the feet that dies
    // down over 0.5 s.
    public class CenserOfEmbersPresentationTests
    {
        private static SpellPresentation Censer()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "censer_of_embers");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheCenserIsPulledBackLeftAndReleasedThenSwingsRightAndLeftAndBreaks()
        {
            var vfx = Censer();
            Assert.AreEqual(1.0f, vfx.hitCueSeconds, 1e-6f);

            var censer = vfx.layers.Single(l => l.id == "censer");
            Assert.AreEqual("above", censer.place);
            Assert.AreEqual(3f, censer.height, 1e-6f, "a cap only: the hanging box is held on the stage");
            Assert.AreEqual(300f, censer.size, 1e-6f);
            Assert.AreEqual("", censer.fit, "one size on every body");
            Assert.AreEqual("release", censer.at);
            Assert.AreEqual(1.0f, censer.seconds, 1e-6f, "it lasts to the cue and no longer");
            Assert.AreEqual(0f, censer.fade, 1e-6f, "the break replaces it");
            Assert.AreEqual(0.2f, censer.fadeIn, 1e-6f);
            Assert.AreEqual("Spells/censer_hanging", censer.path);
            var r = censer.rotate;
            Assert.AreEqual(-9.5f, r.amplitude, 1e-6f, "clockwise: the lantern is on the left");
            Assert.AreEqual(0.8f, r.period, 1e-6f);
            Assert.AreEqual(-0.4f, r.damping, 1e-6f, "negative: each swing a little larger");
            Assert.AreEqual(90f, r.phase, 1e-6f, "released from the extreme, a cosine");
            Assert.AreEqual(0.2f, r.delay, 1e-6f, "held still for the fade-in");
            Assert.AreEqual(r.delay, censer.fadeIn, 1e-6f, "it does not move while it appears");
            Assert.AreEqual(0.5f, r.pivotX, 1e-6f, "the chain leaves the top edge at the centre (opaque columns 596-657 of 1254)");
            Assert.AreEqual(4.5f, r.pivotY, 1e-6f, "4.5 box heights up: a 1250 px chain, so a wide arc and a small tilt");

            // Held at the left extreme for the fade-in, then right at 0.6 and left again
            // at 1.0, the cue. Through the vertical at 0.4 and 0.8, where it is fastest.
            Assert.AreEqual(-9.5f, r.AngleAt(0f), 1e-3f);
            Assert.AreEqual(-9.5f, r.AngleAt(0.2f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.4f), 1e-3f);
            Assert.AreEqual(11.1484f, r.AngleAt(0.6f), 1e-3f);
            Assert.AreEqual(0f, r.AngleAt(0.8f), 1e-3f);
            Assert.AreEqual(-13.0827f, r.AngleAt(1.0f), 1e-3f);
        }

        // THE LANTERN'S PATH (lantern centre 50 below the box centre, 300 box, pivot
        // 1200 over the centre): (-206.3, -32.9) held at the start, (0, -50) at the
        // bottom, (241.7, -26.4) at the right extreme, (-282.9, -17.6) at the break.
        [Test]
        public void TheLanternTravelsAWideArcFromAHeldLeftExtreme()
        {
            var r = Censer().layers.Single(l => l.id == "censer").rotate;
            var expected = new[]
            {
                (0.0f, -206.3, -32.9), (0.2f, -206.3, -32.9), (0.4f, 0.0, -50.0), (0.6f, 241.7, -26.4),
                (0.8f, 0.0, -50.0), (1.0f, -282.9, -17.6),
            };
            foreach (var (t, ex, ey) in expected)
            {
                double rad = r.AngleAt(t) * System.Math.PI / 180.0;
                double px = (r.pivotX - 0.5) * 300, py = (r.pivotY - 0.5) * 300;
                double ox = 0, oy = -50;
                double x = px + System.Math.Cos(rad) * (ox - px) - System.Math.Sin(rad) * (oy - py);
                double y = py + System.Math.Sin(rad) * (ox - px) + System.Math.Cos(rad) * (oy - py);
                Assert.AreEqual(ex, x, 0.1, "x at " + t);
                Assert.AreEqual(ey, y, 0.1, "y at " + t);
            }
        }

        [Test]
        public void TheSmokeTrailIsBornOnTheLanternAsTheCenserSwingsAndStaysWhereItWasBorn()
        {
            var trail = Censer().layers.Single(l => l.id == "trail");
            Assert.AreEqual("layer:censer", trail.place);
            Assert.AreEqual("release", trail.at);
            Assert.AreEqual(10f, trail.emitter.rate, 1e-6f);
            Assert.AreEqual(0.95f, trail.emitter.window, 1e-6f);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 1f, 0f }, trail.emitter.weights, "only the smoke wisp");
            Assert.AreEqual(0f, trail.emitter.inherit, 1e-6f, "it does not follow the censer");
            Assert.AreEqual(20f, trail.emitter.gravity, 1e-6f, "barely up: it hangs in the air");
            Assert.AreEqual(-10f, trail.emitter.sourceDy, 1e-6f, "the lantern's body");
        }

        [Test]
        public void TheCenserBreaksOnTheCueIntoAPunchedStillThenAShowerOfEmbersAndCoals()
        {
            var vfx = Censer();
            var censer = vfx.layers.Single(l => l.id == "censer");
            var crack = vfx.layers.Single(l => l.id == "crack");
            Assert.AreEqual("hit", crack.at);
            Assert.AreEqual("Spells/censer_hanging", crack.path, "the censer's own still");
            Assert.AreEqual(0.04f, crack.seconds, 1e-6f);
            Assert.AreEqual(0.05f, crack.fade, 1e-6f);
            Assert.AreEqual(0f, crack.punch, 1e-6f, "no scale punch: a scale about a pivot 1250 px away moves the lantern");
            Assert.AreEqual(censer.rotate.AngleAt(1.0f), crack.rotate.degrees, 1e-3f, "exactly the angle the swinging layer has on the cue");
            Assert.AreEqual(0f, crack.rotate.amplitude, 1e-6f);
            Assert.AreEqual(censer.rotate.pivotY, crack.rotate.pivotY, 0f, "the same pivot as the layer it replaces");
            Assert.AreEqual(censer.rotate.pivotX, crack.rotate.pivotX, 0f);
            Assert.AreEqual(censer.size, crack.size, 0f);
            Assert.AreEqual(censer.height, crack.height, 0f);

            var burst = vfx.layers.Single(l => l.id == "burst");
            Assert.AreEqual("hit", burst.at);
            Assert.AreEqual("layer:censer", burst.place, "born off the censer's own art");
            Assert.AreEqual(38, burst.emitter.burst);
            Assert.AreEqual(0.8f, burst.glow, 1e-6f, "the embers bloom as they fall");
            Assert.AreEqual(360f, burst.emitter.spreadDegrees, 1e-6f, "every direction");
            Assert.AreEqual(-760f, burst.emitter.gravity, 1e-6f, "negative: pulled down");
            CollectionAssert.AreEqual(new[] { 2f, 1f, 0f, 1f }, burst.emitter.weights, "coals and flecks, never the smoke wisp");
            Assert.AreEqual(1.2f, burst.emitter.lifeMax, 1e-6f);
            Assert.AreEqual(0.9f, burst.emitter.sizeMax, 1e-6f);
            Assert.AreEqual(0f, burst.emitter.sourceDx, 1e-6f);
            Assert.AreEqual(-50f, burst.emitter.sourceDy, 1e-6f, "the lantern's centre on the art (rows 265-670 of 700)");

            var coals = vfx.layers.Single(l => l.id == "coals");
            Assert.AreEqual("hit", coals.at);
            Assert.AreEqual(5, coals.emitter.burst);
            Assert.AreEqual(0.6f, coals.glow, 1e-6f);
            Assert.AreEqual(2.2f, coals.emitter.sizeMax, 1e-6f, "big next to the embers");
            Assert.AreEqual(540f, coals.emitter.spinMax, 1e-6f, "they tumble");
            Assert.AreEqual(-50f, coals.emitter.sourceDy, 1e-6f);
        }

        [Test]
        public void TheStageDarkensDuringTheSwingsAndLiftsOnTheBreak()
        {
            var gloom = Censer().gloom;
            Assert.AreEqual(0.2f, gloom.strength, 1e-6f);
            Assert.AreEqual(0.4f, gloom.fadeIn, 1e-6f);
            Assert.AreEqual("hit", gloom.until);
            Assert.AreEqual(0.3f, gloom.fadeOut, 1e-6f);
        }

        [Test]
        public void TheLanternsGlowRampsToTheBreakAndPulsesAtTheBottomOfEachSwing()
        {
            var censer = Censer().layers.Single(l => l.id == "censer");
            var curve = censer.glowCurve;
            Assert.AreEqual(0.2f, censer.glow, 1e-6f);
            Assert.AreEqual(0.8f, curve.to, 1e-6f, "brightest at the break");
            Assert.AreEqual(0.7f, curve.pulse, 1e-6f);
            Assert.AreEqual(0.4f, curve.knee, 1e-6f, "low enough to reach the lantern's gold, not only its hottest pixels");
            Assert.AreEqual(0.4f, curve.period, 1e-6f, "one pulse per pass through the bottom");
            Assert.AreEqual(0.4f, curve.crest, 1e-6f, "the first pass through the bottom is at 0.4 s");
            Assert.AreEqual(0.32f, curve.At(censer.glow, 0.2f, 1f), 1e-3f, "held at the left: the ramp's 0.12, no pulse yet");
            Assert.AreEqual(0.2f + 0.6f * 0.4f + 0.7f, curve.At(censer.glow, 0.4f, 1f), 1e-3f, "ramp 0.44 plus a full pulse");
            Assert.AreEqual(0.2f + 0.6f * 0.6f, curve.At(censer.glow, 0.6f, 1f), 1e-3f, "right extreme, between pulses");
            Assert.AreEqual(0.8f, curve.At(censer.glow, 1.0f, 1f), 1e-3f, "the break: ramp done, no pulse");
        }

        [Test]
        public void SparksAreFlungAlongTheSwingFromTheRimAtTheReleaseAndAtTheRightExtreme()
        {
            var vfx = Censer();
            var out_ = vfx.layers.Single(l => l.id == "sparks");
            var back = vfx.layers.Single(l => l.id == "sparks_back");
            Assert.AreEqual("layer:censer", out_.place, "born on the lantern");
            CollectionAssert.AreEqual(new[] { 0.2f }, out_.emitter.burstTimes);
            CollectionAssert.AreEqual(new[] { 0.6f }, back.emitter.burstTimes);
            Assert.AreEqual(16, out_.emitter.burstEach);
            Assert.AreEqual(16, back.emitter.burstEach);
            Assert.AreEqual(0f, out_.emitter.aimDegrees, "to the right, the way it is about to swing");
            Assert.AreEqual(180f, back.emitter.aimDegrees, "then back to the left");
            Assert.AreEqual(60f, out_.emitter.sourceDx, "the right rim");
            Assert.AreEqual(-60f, back.emitter.sourceDx, "the left rim");
            Assert.AreEqual(0f, out_.emitter.burst, "nothing at the opening instant");
            Assert.AreEqual(1.5f, out_.glow, 1e-6f);
            Assert.IsTrue(out_.unkicked && back.unkicked);
            Assert.AreEqual(-420f, out_.emitter.gravity, 1e-6f, "they fall");
        }

        [Test]
        public void TheSwingingCenserAndItsCrackAndEmbersStayPutWhenTheStageIsKicked()
        {
            var vfx = Censer();
            foreach (var id in new[] { "censer", "trail", "crack", "burst", "coals", "sparks", "sparks_back" })
            {
                Assert.IsTrue(vfx.layers.Single(l => l.id == id).unkicked, id + " hangs in the air");
            }

            foreach (var id in new[] { "fire", "rise", "smoke", "ash" })
            {
                Assert.IsFalse(vfx.layers.Single(l => l.id == id).unkicked, id + " is on the body and kicks with it");
            }
        }

        [Test]
        public void TheStagePushesTowardTheTargetOnTheCueAndEasesBack()
        {
            var kick = Censer().kick;
            Assert.AreEqual(0.025f, kick.strength, 1e-6f);
            Assert.AreEqual(0.03f, kick.rise, 1e-6f);
            Assert.AreEqual(0.18f, kick.fall, 1e-6f);
            Assert.AreEqual(48f, kick.strength * SpellKick.StageWidth, 1e-3f, "48 px of a 1920 px stage");
        }

        [Test]
        public void SmokeRisesAndAshDriftsDownAfterTheFireAndTheSmokeIsTheLastOnScreen()
        {
            var vfx = Censer();
            var smoke = vfx.layers.Single(l => l.id == "smoke");
            var ash = vfx.layers.Single(l => l.id == "ash");
            var fire = vfx.layers.Single(l => l.id == "fire");
            Assert.AreEqual(fire.offset, smoke.offset, 1e-6f, "born while the fire lives");
            Assert.AreEqual(18f, smoke.emitter.gravity, 1e-6f, "positive: it rises");
            Assert.AreEqual(-120f, smoke.emitter.sourceDy, 1e-6f, "born at the feet, under the flames");
            Assert.AreEqual(-120f, ash.emitter.gravity, 1e-6f, "negative: it drifts down");
            Assert.AreEqual("#7A6E64", ash.tint, "warm ash grey, no green");
            Assert.AreEqual("Spells/glow_disc", ash.emitter.path, "soft discs: the particle sheet has no grey flake");
            Assert.AreEqual(0.6f, ash.offset, 1e-6f);

            var performance = PrincesPalace.Domain.Combat.Presentation.SpellPerformance.Resolve(vfx, 1, _ => 4);
            float last = performance.Instances.Max(i => i.EndSeconds);
            var smokeInstance = performance.Instances.Single(i => i.Layer.id == "smoke");
            Assert.AreEqual(last, smokeInstance.EndSeconds, 1e-4f, "nothing outlives the smoke");
            Assert.AreEqual(1.0f + 0.45f + 0.9f + 1.6f, smokeInstance.EndSeconds, 1e-4f);
        }

        // The censer throws more particles over its life than the pool has members and
        // fits because they die and are handed back (SpellPoolCapacityTests counts the
        // peak, not the total): the literals say why.
        [Test]
        public void TheCenserSpendsMoreParticlesThanThePoolHoldsButNeverHasThemAllAlive()
        {
            var performance = PrincesPalace.Domain.Combat.Presentation.SpellPerformance.Resolve(Censer(), 1, _ => 4);
            int total = performance.Instances
                .Where(i => i.Layer.Render == SpellRender.Emitter)
                .Sum(i => PrincesPalace.Domain.Combat.Presentation.SpellEmitterSim.CountOf(i.Layer.emitter));

            Assert.AreEqual(102, total, "everything it ever throws: 10 trail, 32 sparks, 38 embers, 5 coals, 6 rise, 5 smoke, 6 ash");
            Assert.Greater(total, 64);
            Assert.AreEqual(62, SpellPoolPeak.ParticlesOf(performance), "alive together: 2 under the pool's 64");
            Assert.AreEqual(2, SpellPoolPeak.EffectsRenderersOf(performance), "of 12: the lantern and the crack as it breaks");
        }

        [Test]
        public void ThePaintedArtCarriesItsOwnSmokeAndFlameSoNoStandInLayersRemain()
        {
            var ids = Censer().layers.Select(l => l.id).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "censer", "trail", "sparks", "sparks_back", "crack", "burst", "coals", "fire", "rise", "smoke", "ash",
            }, ids);
        }

        [Test]
        public void TheFireAtTheFeetLoopsBreathesAndDiesDownIntoTheCoalGlowOverHalfASecond()
        {
            var vfx = Censer();
            var fire = vfx.layers.Single(l => l.id == "fire");
            Assert.AreEqual("hit", fire.at);
            Assert.AreEqual("target", fire.place, "at the feet, not a column in front of the face");
            Assert.AreEqual("Spells/censer_flame_loop", fire.path);
            Assert.AreEqual("loop", fire.until, "four painted frames, the fourth leading back to the first");
            Assert.AreEqual(8f, fire.fps, 1e-6f);
            Assert.AreEqual(0.45f, fire.offset, 1e-6f, "it catches as the embers land, not on the cue");
            Assert.AreEqual(0.25f, fire.fadeIn, 1e-6f);
            Assert.AreEqual(0.5f, fire.seconds, 1e-6f);
            Assert.AreEqual(0.12f, fire.fade, 1e-6f, "a longer fade turns the flame's yellow core olive over the dark ground");
            Assert.AreEqual(0.125f, fire.impactY, 1e-6f, "the coal line, row 390 of 444");
            Assert.AreEqual(0.35f, fire.breathe.alpha, 1e-6f);
            Assert.AreEqual(0.07f, fire.breathe.scale, 1e-6f);
            Assert.AreEqual(4f, fire.breathe.rate, 1e-6f);
            Assert.AreEqual(1f, fire.stoke, 1e-6f, "the coal glow on the body flickers with this fire");

            var rise = vfx.layers.Single(l => l.id == "rise");
            Assert.AreEqual(0.45f, rise.offset, 1e-6f);
            Assert.AreEqual(140f, rise.emitter.gravity, 1e-6f, "positive: they float up");
            Assert.AreEqual(0.06f, rise.emitter.sizeMin, 1e-6f, "the last thing on screen is small");
            Assert.AreEqual(0.9f, rise.emitter.lifeMax, 1e-6f);
        }
    }
}
