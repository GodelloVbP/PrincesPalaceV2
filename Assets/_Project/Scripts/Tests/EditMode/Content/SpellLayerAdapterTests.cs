using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE PRE-LAYER FORMAT, SAID IN LAYERS.
    //
    // This is the whole of the compatibility story: five shipped spells keep
    // their single blocks in skills.json and pass through ToLayers at run time,
    // so there is one orchestration path rather than two forever. Its value is
    // that NOTHING CHANGES, which makes every number below a literal taken from
    // what the player does today rather than a re-derivation of it.
    //
    // The departure/travel split is the part that needs care and is where a
    // wrong answer is least visible. Today a travelling sheet is DRAWN AND
    // ANIMATING on the caster from frame 0 and only its MOTION waits for the
    // departure frame; mapping the departure to a schedule offset would delay
    // the drawing too, which is a different animation that no assertion about
    // arrival positions would catch.
    public class SpellLayerAdapterTests
    {
        // mud_burst as it is authored today, with the 26 frames its folder
        // holds. Travel-centre, a nine-frame charge and an impact on frame 13.
        private static SpellPresentation MudBurst() => new SpellPresentation
        {
            path = "Spells/mud_burst",
            seconds = 0.65f,
            impactFrame = 13,
            departFrame = 9,
            anchor = "travel-centre",
            impactX = 0.69f,
            impactY = 0.52f,
        };

        private static SpellPresentation FrostFlare() => new SpellPresentation
        {
            path = "Spells/frost_flare",
            seconds = 0.52f,
            impactFrame = 5,
            impactX = 0.5f,
            impactY = 0.18f,
            sfxPath = "Audio/Sfx/frost_flare",
        };

        private static SpellPresentation Cinderfault() => new SpellPresentation
        {
            path = "Spells/cinderfault_eruption",
            seconds = 0.78f,
            impactFrame = 5,
            impactX = 0.5f,
            impactY = 0.129f,
            sfxPath = "Audio/Sfx/cinderfault_impact",
            groundPath = "Spells/cinderfault_ground",
            groundImpactY = 0.063f,
            castSfxPath = "Audio/Sfx/cinderfault_pressure",
        };

        // ---- an ordinary, stationary effect --------------------------------------

        [Test]
        public void AStationaryBlockBecomesOneSpriteLayerOnTheTarget()
        {
            var layers = FrostFlare().ToLayers(9);

            Assert.AreEqual(1, layers.Length);
            var layer = layers[0];

            Assert.AreEqual("sprite", layer.render);
            Assert.AreEqual("target", layer.place);
            Assert.AreEqual("release", layer.at);
            Assert.AreEqual("Spells/frost_flare", layer.path);
            Assert.AreEqual(0.52f, layer.seconds, 1e-6f);
            Assert.AreEqual("once", layer.until);
            Assert.AreEqual("effects", layer.sort);
            Assert.AreEqual(0.5f, layer.impactX, 1e-6f);
            Assert.AreEqual(0.18f, layer.impactY, 1e-6f);
            Assert.IsFalse(layer.Travels);
        }

        // FIT-TO-SECONDS AND NOT AN AUTHORED RATE. A pre-layer block's frame
        // rate IS its duration over its frame count -- perFrame = total /
        // frames.Length -- and was never authored, so an adapter emitting any
        // fps at all would retime every shipped spell.
        [Test]
        public void AnAdaptedLayerFitsItsWholeFolderIntoTheAuthoredSeconds()
        {
            Assert.AreEqual(0f, FrostFlare().ToLayers(9)[0].fps);
            Assert.AreEqual(0f, MudBurst().ToLayers(26)[0].fps);
        }

        // A NON-TRAVELLING EFFECT IS 'none', NOT 'auto'. Today those are
        // unconditionally SetFacing(1f); mapping them to auto would mirror a
        // monster's flare and break
        // AnOrdinaryEffectAfterAMirroredOneIsNotItselfMirrored.
        [Test]
        public void AStationaryEffectNeverMirrorsAndATravellingOneTakesTheCastsFacing()
        {
            Assert.AreEqual("none", FrostFlare().ToLayers(9)[0].facing);
            Assert.AreEqual("auto", MudBurst().ToLayers(26)[0].facing);
        }

        [TestCase("", "target")]
        [TestCase("target", "target")]
        [TestCase("caster", "caster")]
        [TestCase("target-centre", "target-centre")]
        [TestCase("caster-centre", "caster-centre")]
        [TestCase("travel", "caster")]
        [TestCase("travel-centre", "caster-centre")]
        public void EveryAnchorMapsToItsPlacementWord(string anchor, string place)
        {
            var vfx = FrostFlare();
            vfx.anchor = anchor;

            Assert.AreEqual(place, vfx.ToLayers(9)[0].place);
        }

        // ---- the departure/travel split ------------------------------------------

        // mud_burst: 26 frames over 0.65s is 0.025s a frame. It departs on
        // index 8 (departFrame 9, 1-based) and arrives on index 12
        // (impactFrame 13), so it holds at the caster for 0.2s and then crosses
        // in 0.1s. Both literals; the alternative -- recomputing them from the
        // same expression the adapter uses -- would pass for any expression.
        [Test]
        public void ATravellingBlockHoldsAtTheCasterForItsChargeAndThenCrosses()
        {
            var layer = MudBurst().ToLayers(26)[0];

            Assert.AreEqual("caster-centre", layer.place);
            Assert.AreEqual(0.2f, layer.travelDelay, 1e-6f,
                "the hold is the departure frame's own start time, not a schedule offset -- the sheet is " +
                "drawn and animating on the caster through it");
            Assert.AreEqual(0.1f, layer.travelSeconds, 1e-6f);
            Assert.AreEqual(0.65f, layer.seconds, 1e-6f,
                "the authored seconds is what ENDS it, including past its arrival: today a travelling " +
                "sheet keeps playing at the target until the sequence runs out");
        }

        // The three clamps the player applies, in the player's own order: the
        // arrival is held inside the sequence and the departure is held below
        // the arrival, so the two cannot cross and produce a negative flight.
        [Test]
        public void AnImpactFramePastTheEndOfTheFolderArrivesOnTheLastFrame()
        {
            var vfx = MudBurst();
            vfx.impactFrame = 99;

            var layer = vfx.ToLayers(26)[0];

            Assert.AreEqual(0.2f, layer.travelDelay, 1e-6f);
            Assert.AreEqual(0.425f, layer.travelSeconds, 1e-6f,
                "clamped to index 25 of 26, which is 17 frames of 0.025s after the departure");
        }

        [Test]
        public void ADepartureAfterTheArrivalIsHeldAtTheArrivalAndDoesNotTravel()
        {
            var vfx = MudBurst();
            vfx.departFrame = 20;

            var layer = vfx.ToLayers(26)[0];

            Assert.AreEqual(0.3f, layer.travelDelay, 1e-6f, "held down to the arrival's own index, 12");
            Assert.AreEqual(0f, layer.travelSeconds, 1e-6f);
            Assert.IsFalse(layer.Travels,
                "a sheet whose departure and arrival land on one frame does not move today either -- the " +
                "flight lerp is guarded on arrival > departure");
        }

        // MISSING ART IS NOT A RETIMING. FrameSequenceLoader caches an empty
        // result for a folder with no frames, and PlayRoutine returns before it
        // computes anything -- so a travelling block whose folder failed to
        // load must not become a projectile with a nonsense flight.
        [Test]
        public void AFolderWithNoFramesYieldsALayerThatDoesNotTravel()
        {
            var layer = MudBurst().ToLayers(0)[0];

            Assert.AreEqual(0f, layer.travelSeconds);
            Assert.AreEqual(0f, layer.travelDelay);
            Assert.AreEqual("Spells/mud_burst", layer.path);
        }

        // ---- the shared ground layer ---------------------------------------------

        [Test]
        public void AGroundBlockBecomesASecondLayerOnTheFormationInTheGroundBand()
        {
            var layers = Cinderfault().ToLayers(9);

            Assert.AreEqual(2, layers.Length);
            var ground = layers[1];

            Assert.AreEqual("sprite", ground.render);
            Assert.AreEqual("formation", ground.place);
            Assert.AreEqual("ground", ground.sort);
            Assert.AreEqual("release", ground.at);
            Assert.AreEqual("none", ground.facing);
            Assert.AreEqual("Spells/cinderfault_ground", ground.path);
            Assert.AreEqual(0.78f, ground.seconds, 1e-6f,
                "an unauthored groundSeconds falls back to the per-target sequence's own, which is what " +
                "makes the two layers rupture together");
            Assert.AreEqual(0.063f, ground.impactY, 1e-6f);
            Assert.AreEqual(SpellPresentation.Unauthored, ground.impactX,
                "the pre-layer block has no groundImpactX to have come from, and a formation's horizontal " +
                "centre is the measured midpoint of the struck span");
        }

        [Test]
        public void ASpellWithOnlyAGroundLayerProducesOnlyTheGroundLayer()
        {
            var vfx = new SpellPresentation
            {
                path = "",
                groundPath = "Spells/cinderfault_ground",
                groundSeconds = 0.9f,
                groundAspect = 2.5f,
            };

            var layers = vfx.ToLayers(0);

            Assert.AreEqual(1, layers.Length);
            Assert.AreEqual("formation", layers[0].place);
            Assert.AreEqual(0.9f, layers[0].seconds, 1e-6f);
            Assert.AreEqual(2.5f, layers[0].aspect, 1e-6f);
        }

        [Test]
        public void ASpellWithNoArtAtAllProducesNoLayers()
        {
            CollectionAssert.IsEmpty(new SpellPresentation().ToLayers(0));
        }

        // ---- the adapter never emits something the rules would refuse ------------
        //
        // These rules police AUTHORED content and never the adapter's output --
        // it is built at run time from a block the resolver already validated,
        // so there is no author to report to. But an adapter that emitted an
        // illegal layer would be saying something the vocabulary cannot mean,
        // and that is worth knowing here rather than as a spell that draws
        // nothing much later.
        [Test]
        public void EveryLayerTheAdapterEmitsForAShippedSpellIsItselfLegal()
        {
            var cases = new[]
            {
                ("frost_flare", FrostFlare(), 9),
                ("mud_burst", MudBurst(), 26),
                ("cinderfault", Cinderfault(), 9),
            };

            foreach (var (id, vfx, frames) in cases)
            {
                var asAuthored = new SpellPresentation { layerFormat = 1, layers = vfx.ToLayers(frames) };
                CollectionAssert.IsEmpty(SpellLayerRules.Check($"adapted '{id}'", asAuthored));
            }
        }
    }
}
