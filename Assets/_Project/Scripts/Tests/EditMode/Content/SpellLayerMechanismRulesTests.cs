using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE LAYER WORDS for rank spans, bodies and moves: `overhang` on a rank
    // span, `follow` on a body placement, the `counterpart` placements and the
    // `whenConsumed` gate. What they MEAN on screen is Core's to draw; what is
    // legal and what the gate answers is pinned here with literals.
    public class SpellLayerMechanismRulesTests
    {
        private static SpellLayer Layer(string place, System.Action<SpellLayer> edit = null)
        {
            var layer = new SpellLayer
            {
                id = "x", render = "sprite", place = place, path = "Spells/x", seconds = 0.5f,
            };
            edit?.Invoke(layer);
            return layer;
        }

        private static List<string> Problems(params SpellLayer[] layers)
        {
            var vfx = new SpellPresentation { layerFormat = 1, hitCueSeconds = 0.2f, layers = layers };
            return SpellLayerRules.Check("skill 't'", vfx);
        }

        // ---- overhang -------------------------------------------------------------

        [Test]
        public void OverhangIsLegalOnAFormationSpan()
        {
            CollectionAssert.IsEmpty(Problems(Layer("formation", l => l.overhang = 0.5f)));
        }

        [Test]
        public void OverhangIsRefusedWhereThereIsNoSpan()
        {
            Assert.That(Problems(Layer("target", l => l.overhang = 0.5f)), Has.Some.Contains("overhang 0.5"));
        }

        [Test]
        public void OverhangPastTwoHalfWidthsIsRefused()
        {
            Assert.That(Problems(Layer("formation", l => l.overhang = 2.5f)), Has.Some.Contains("between 0 and 2"));
        }

        [Test]
        public void ALoneBodysSpanGrowsByTheOverhangOfItsHalfWidth()
        {
            // One body 100 wide: pads of 50 are flush (100); an overhang of
            // 0.5 pads 75 a side (150). The Core caller computes the pad as
            // halfWidth * (1 + overhang).
            var flush = FormationSpan.Between(new UiVec(500, 0), new UiVec(500, 0), 50f, 50f);
            var reaching = FormationSpan.Between(new UiVec(500, 0), new UiVec(500, 0), 75f, 75f);

            Assert.AreEqual(100f, flush.Length, 0.001f);
            Assert.AreEqual(150f, reaching.Length, 0.001f);
        }

        // ---- follow ---------------------------------------------------------------

        [Test]
        public void FollowIsLegalOnABodyPlacement()
        {
            CollectionAssert.IsEmpty(Problems(Layer("target-centre", l => l.follow = true)));
            CollectionAssert.IsEmpty(Problems(Layer("counterpart-centre", l => l.follow = true)));
        }

        [Test]
        public void FollowIsRefusedOnTheFormationAndTheSky()
        {
            Assert.That(Problems(Layer("formation", l => l.follow = true)), Has.Some.Contains("not one body"));
            Assert.That(Problems(Layer("sky", l => l.follow = true)), Has.Some.Contains("not one body"));
        }

        [Test]
        public void FollowIsRefusedOnAProjectile()
        {
            var problems = Problems(Layer("caster-centre", l => { l.follow = true; l.travelSeconds = 0.2f; }));
            Assert.That(problems, Has.Some.Contains("travels"));
        }

        // ---- counterpart ----------------------------------------------------------

        [Test]
        public void TheCounterpartWordsAreKnownAndCastLevel()
        {
            Assert.IsTrue(SpellPlaceNames.IsKnown("counterpart"));
            Assert.IsTrue(SpellPlaceNames.IsKnown("counterpart-centre"));
            Assert.AreEqual(SpellPlace.CounterpartCentre, SpellPlaceNames.Parse("counterpart-center"));
            Assert.IsFalse(SpellPlaceNames.PerTarget(SpellPlace.CounterpartCentre),
                "a swap strikes nobody; the counterpart is one instance, not a fan-out");
            Assert.IsTrue(SpellPlaceNames.Centred(SpellPlace.CounterpartCentre));
            Assert.IsFalse(SpellPlaceNames.Centred(SpellPlace.Counterpart));
        }

        [Test]
        public void FitTargetIsLegalOnTheCounterpart()
        {
            CollectionAssert.IsEmpty(Problems(Layer("counterpart-centre", l => l.fit = "target")));
        }

        // ---- whenConsumed ---------------------------------------------------------

        [Test]
        public void ABlankGateAlwaysPlays_AndAnUnearnedOneNever()
        {
            Assert.IsTrue(SpellGateNames.Passes("", new List<StatusEffectType>()));
            Assert.IsFalse(SpellGateNames.Passes("Poison", new List<StatusEffectType>()));
            Assert.IsFalse(SpellGateNames.Passes("Poison", null));
        }

        [Test]
        public void AGateNamesTheStatusItWaitsFor()
        {
            var spentPoison = new List<StatusEffectType> { StatusEffectType.Poison };

            Assert.IsTrue(SpellGateNames.Passes("Poison", spentPoison));
            Assert.IsTrue(SpellGateNames.Passes("poison", spentPoison), "words are case-blind like every other");
            Assert.IsTrue(SpellGateNames.Passes("any", spentPoison));
            Assert.IsFalse(SpellGateNames.Passes("Burn", spentPoison));
        }

        [Test]
        public void AnUnknownGateWordIsRefused_AndPlaysNothingIfItGetsThrough()
        {
            Assert.That(Problems(Layer("target-centre", l => l.whenConsumed = "Poisn")),
                Has.Some.Contains("'Poisn' is not a status"));
            Assert.IsFalse(SpellGateNames.Passes("Poisn", new List<StatusEffectType> { StatusEffectType.Poison }));
        }

        [Test]
        public void ALayerRidingAGatedLayerMustCarryTheSameGate()
        {
            var gated = Layer("target-centre", l => { l.id = "burst"; l.whenConsumed = "Poison"; });
            var tail = new SpellLayer
            {
                id = "tail", render = "sprite", place = "layer:burst", path = "Spells/x", seconds = 0.2f,
            };

            Assert.That(Problems(gated, tail), Has.Some.Contains("same whenConsumed"));

            tail.whenConsumed = "poison";
            CollectionAssert.IsEmpty(Problems(gated, tail));
        }

        // ---- target-head / fit: head -----------------------------------------------

        [Test]
        public void TargetHeadAndFitHeadAreKnownWords()
        {
            Assert.IsTrue(SpellPlaceNames.IsKnown("target-head"));
            Assert.AreEqual(SpellPlace.TargetHead, SpellPlaceNames.Parse("Target-Head"));
            Assert.IsTrue(SpellPlaceNames.PerTarget(SpellPlace.TargetHead));
            Assert.IsTrue(SpellPlaceNames.Centred(SpellPlace.TargetHead));
            Assert.IsTrue(SpellPlaceNames.OnABody(SpellPlace.TargetHead));
            Assert.IsTrue(SpellFitNames.IsKnown("head"));
            Assert.AreEqual(SpellFit.Head, SpellFitNames.Parse("head"));
        }

        [Test]
        public void TargetHeadTakesFollowAndFitHead()
        {
            CollectionAssert.IsEmpty(Problems(Layer("target-head", l => { l.follow = true; l.fit = "head"; })));
        }

        [Test]
        public void FitHeadIsLegalWhereFitTargetIsAndRefusedWhereItIsNot()
        {
            CollectionAssert.IsEmpty(Problems(Layer("target", l => l.fit = "head")));
            CollectionAssert.IsEmpty(Problems(Layer("sky", l => l.fit = "head")));
            CollectionAssert.IsEmpty(Problems(Layer("counterpart-centre", l => l.fit = "head")));
            CollectionAssert.IsEmpty(Problems(Layer("caster", l => { l.fit = "head"; l.travelSeconds = 0.2f; })));
            Assert.That(Problems(Layer("caster", l => l.fit = "head")), Has.Some.Contains("fit 'head'"));
            Assert.That(Problems(Layer("formation", l => l.fit = "head")), Has.Some.Contains("fit 'head'"));
        }

        [Test]
        public void FitHeadIsLegalOnAnEmitterRidingAHeadLayer()
        {
            var host = Layer("target-head", l => l.id = "eyes");
            var emitter = Layer("layer:eyes", l =>
            {
                l.id = "sparks"; l.render = "emitter"; l.path = ""; l.fit = "head";
                l.emitter = new SpellEmitter { path = "Spells/x", rate = 10f, window = 0.25f };
            });
            Assert.That(Problems(host, emitter), Has.None.Contains("fit 'head'"));
        }

        [Test]
        public void HeightIsStillRefusedOnTargetHead()
        {
            Assert.That(Problems(Layer("target-head", l => l.height = 1f)), Has.Some.Contains("only place 'above'"));
        }
    }
}
