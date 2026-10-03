using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // A spell's gloom: how it composes with an event's darkness, how it
    // fades, and what the content build refuses. The wiring onto the real
    // image is SpellGloomStageTests (PlayMode).
    public class SpellGloomTests
    {
        private const string Label = "skill 'test_spell'";

        // Court of Whispers' shipped numbers; hit cue 0.3, cleared 0.86.
        private static SpellGloom Court() =>
            new SpellGloom { strength = 0.35f, fadeIn = 0.2f, until = "hit", fadeOut = 0.25f };

        private const float Hit = 0.3f;
        private const float Cleared = 0.86f;

        // ---- composition: the larger wins, never a sum ---------------------------

        [Test]
        public void TheStageShowsTheLargerOfTheEventAndTheSpell()
        {
            Assert.AreEqual(0.9f, StageDarkness.Compose(0.9f, 0.35f), 1e-6f, "a spell never darkens past the event");
            Assert.AreEqual(0.35f, StageDarkness.Compose(0.15f, 0.35f), 1e-6f, "event at its floor, the spell shows");
            Assert.AreEqual(0.35f, StageDarkness.Compose(0f, 0.35f), 1e-6f, "no event darkness at all");
            Assert.AreEqual(0f, StageDarkness.Compose(0f, 0f), 1e-6f);
        }

        // ---- the curve, in authored seconds --------------------------------------

        [Test]
        public void ItRisesToStrengthHoldsThroughTheWindUpAndLiftsOnThePayoff()
        {
            var g = Court();
            Assert.AreEqual(0f, g.AlphaAt(0f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0.175f, g.AlphaAt(0.1f, Hit, Cleared), 1e-6f, "halfway up the fade-in");
            Assert.AreEqual(0.35f, g.AlphaAt(0.2f, Hit, Cleared), 1e-6f, "full at the end of the fade-in");
            Assert.AreEqual(0.35f, g.AlphaAt(0.3f, Hit, Cleared), 1e-6f, "still full on the payoff frame");
            Assert.AreEqual(0.175f, g.AlphaAt(0.425f, Hit, Cleared), 1e-6f, "halfway down");
            Assert.AreEqual(0f, g.AlphaAt(0.55f, Hit, Cleared), 1e-6f, "lifted");
            Assert.AreEqual(0f, g.AlphaAt(5f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0.55f, g.EndSeconds(Hit, Cleared), 1e-6f);
        }

        // The cast clock is authored seconds scaled by the pace, so a 1.5x
        // fight reads the same curve at 1.5 times the age: 0.1 real seconds
        // is 0.15 authored.
        [Test]
        public void AtOnePointFiveXTheSameCurveRunsFaster()
        {
            var g = Court();
            Assert.AreEqual(0.2625f, g.AlphaAt(0.15f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0.35f, g.AlphaAt(0.3f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0.14f, g.AlphaAt(0.45f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0f, g.AlphaAt(0.555f, Hit, Cleared), 1e-6f);
        }

        [Test]
        public void APayoffBeforeFullStrengthLiftsFromWhereTheRiseGot()
        {
            var g = new SpellGloom { strength = 0.4f, fadeIn = 0.4f, fadeOut = 0.2f };
            Assert.AreEqual(0.2f, g.AlphaAt(0.2f, 0.2f, 1f), 1e-6f, "half risen at the payoff");
            Assert.AreEqual(0.1f, g.AlphaAt(0.3f, 0.2f, 1f), 1e-6f, "then lifting from that level");
        }

        [Test]
        public void UntilClearedHoldsThroughTheResidue()
        {
            var g = new SpellGloom { strength = 0.2f, fadeIn = 0.1f, until = "cleared", fadeOut = 0.2f };
            Assert.AreEqual(0.2f, g.AlphaAt(0.5f, Hit, Cleared), 1e-6f, "past the payoff, still held");
            Assert.AreEqual(0.1f, g.AlphaAt(0.96f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(1.06f, g.EndSeconds(Hit, Cleared), 1e-6f);
        }

        [Test]
        public void NoFadesMeansAStepOnAndOff()
        {
            var g = new SpellGloom { strength = 0.5f };
            Assert.AreEqual(0.5f, g.AlphaAt(0f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0.5f, g.AlphaAt(0.29f, Hit, Cleared), 1e-6f);
            Assert.AreEqual(0f, g.AlphaAt(0.3f, Hit, Cleared), 1e-6f);
        }

        [Test]
        public void AnUnauthoredGloomIsNothing()
        {
            var g = new SpellGloom();
            Assert.IsFalse(g.HasGloom);
            Assert.AreEqual(0f, g.AlphaAt(0.1f, Hit, Cleared));
            Assert.AreEqual(0f, g.EndSeconds(Hit, Cleared));
        }

        // ---- the resolver's refusals ---------------------------------------------

        private static List<string> Problems(SpellGloom gloom) =>
            SpellLayerRules.Check(Label, new SpellPresentation
            {
                layerFormat = 1,
                hitCueSeconds = Hit,
                gloom = gloom,
                layers = new[]
                {
                    new SpellLayer { id = "core", render = "sprite", place = "target", path = "Spells/x", seconds = 0.5f },
                },
            });

        private static string Only(SpellGloom gloom)
        {
            var problems = Problems(gloom);
            Assert.AreEqual(1, problems.Count, "expected one problem, got: " + string.Join(" | ", problems));
            return problems[0];
        }

        [Test]
        public void TheShippedShapeIsLegal() => CollectionAssert.IsEmpty(Problems(Court()));

        [Test]
        public void AStrengthOutsideZeroToOneIsRefused()
        {
            var g = Court();
            g.strength = 1.5f;
            StringAssert.Contains("vfx.gloom.strength 1.5 is outside 0..1", Only(g));

            g.strength = -0.1f;
            StringAssert.Contains("vfx.gloom.strength -0.1 is outside 0..1", Only(g));
        }

        [Test]
        public void AFadeOutsideItsBoundIsRefused()
        {
            var g = Court();
            g.fadeIn = -0.1f;
            StringAssert.Contains("vfx.gloom.fadeIn -0.1 is outside 0..2 seconds", Only(g));

            g = Court();
            g.fadeOut = 9f;
            StringAssert.Contains("vfx.gloom.fadeOut 9 is outside 0..2 seconds", Only(g));
        }

        [Test]
        public void AnUnknownReleaseIsRefusedAndTheKnownOnesAreListed()
        {
            var g = Court();
            g.until = "payoff";
            string message = Only(g);
            StringAssert.Contains("vfx.gloom.until 'payoff' is not a release point", message);
            StringAssert.Contains("Known: hit, cleared", message);
        }

        [Test]
        public void AGloomWithNoArtToRideOnIsRefused()
        {
            var problems = SpellLayerRules.Check(Label, new SpellPresentation { gloom = Court() });
            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains("vfx.gloom has no art to ride on", problems[0]);
        }
    }
}
