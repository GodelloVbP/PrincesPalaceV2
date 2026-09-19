using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // COPY() IS THE ONLY BOUNDARY BETWEEN THE CATALOGUE AND A FIGHT, and it is
    // a hand-written field list.
    //
    // That shape has already dropped fields twice on this chain (AUDIT #60
    // records FightEncounterAdapter losing `transform`, then `bookOnly`/
    // `bookTier`), and the test that was supposed to guard this one is written
    // out by hand as well -- so it would pass unchanged with a whole new field
    // missing from the copy. A hand-written guard over a hand-written list
    // guards nothing but the fields somebody thought of twice.
    //
    // These two kill the CLASS rather than the instance, which is
    // docs/CODE_STANDARDS.md section 9's T1 rung: the pin walks the type by
    // reflection, so a field added and not copied fails on the next run with
    // its own name in the message and nobody has to remember anything.
    public class SpellPresentationCopyTests
    {
        // A presentation with EVERY field set to something that is not its
        // default, so "came across" and "happens to match" cannot be confused.
        private static SpellPresentation FullyPopulated() => new SpellPresentation
        {
            path = "Spells/x",
            seconds = 1.25f,
            impactFrame = 7,
            anchor = "travel-centre",
            size = 512f,
            departFrame = 3,
            impactX = 0.61f,
            impactY = 0.42f,
            sfxPath = "Audio/Sfx/x",
            groundPath = "Spells/x_ground",
            groundSeconds = 0.9f,
            groundImpactFrame = 4,
            groundAspect = 2.5f,
            groundImpactY = 0.07f,
            castSfxPath = "Audio/Sfx/x_cast",
            layerFormat = 1,
            hitCueSeconds = 0.35f,
            layers = new[]
            {
                new SpellLayer
                {
                    id = "core",
                    render = "sprite",
                    place = "caster-centre",
                    follow = true,
                    at = "release",
                    offset = 0.05f,
                    path = "Spells/x_core",
                    seconds = 0.4f,
                    fps = 24f,
                    startFrame = 2,
                    until = "loop",
                    fade = 0.1f,
                    dx = -70f,
                    dy = 12f,
                    scale = 0.85f,
                    size = 190f,
                    facing = "auto",
                    sort = "effects",
                    align = "span",
                    punch = 0.22f,
                    glow = 1.3f,
                    impactX = 0.94f,
                    impactY = 0.5f,
                    aspect = 1.5f,
                    travelSeconds = 0.25f,
                    travelDelay = 0.05f,
                    emitter = new SpellEmitter
                    {
                        path = "Spells/x_drops",
                        rate = 40f,
                        burst = 18,
                        window = 0.25f,
                        sourceDx = -40f,
                        sourceDy = 6f,
                        spreadDegrees = 55f,
                        aimDegrees = 200f,
                        speedMin = 90f,
                        speedMax = 220f,
                        inherit = 0.35f,
                        drag = 3.5f,
                        gravity = -1400f,
                        lifeMin = 0.22f,
                        lifeMax = 0.38f,
                        sizeMin = 0.35f,
                        sizeMax = 0.7f,
                        spinMin = -180f,
                        spinMax = 180f,
                        fadeFrom = 0.6f,
                        endScale = 0.8f,
                        seed = 991,
                    },
                },
            },
        };

        private static IEnumerable<FieldInfo> FieldsOf(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        [Test]
        public void EveryPublicFieldOfAPresentationSurvivesTheCopy()
        {
            var original = FullyPopulated();
            var copy = original.Copy();
            var blank = new SpellPresentation();

            var dropped = new List<string>();
            foreach (var field in FieldsOf(typeof(SpellPresentation)))
            {
                object mine = field.GetValue(copy);
                object theirs = field.GetValue(original);
                object untouched = field.GetValue(blank);

                Assert.IsFalse(Equals(theirs, untouched),
                    $"SpellPresentation.{field.Name} is still at its default on the FULLY POPULATED " +
                    "original, so this pin cannot tell whether Copy() carries it. Set it in " +
                    "FullyPopulated().");

                if (field.FieldType == typeof(SpellLayer[]))
                {
                    if (((SpellLayer[])mine).Length != ((SpellLayer[])theirs).Length) dropped.Add(field.Name);
                    continue;
                }

                if (!Equals(mine, theirs)) dropped.Add(field.Name);
            }

            CollectionAssert.IsEmpty(dropped,
                "SpellPresentation.Copy() does not carry these fields, so a fight sees the default where " +
                "content authored a value: " + string.Join(", ", dropped));
        }

        [Test]
        public void EveryPublicFieldOfALayerAndItsEmitterSurvivesTheCopy()
        {
            var original = FullyPopulated().layers[0];
            var copy = original.Copy();

            foreach (var field in FieldsOf(typeof(SpellLayer)))
            {
                if (field.FieldType == typeof(SpellEmitter)) continue;
                Assert.AreEqual(field.GetValue(original), field.GetValue(copy),
                    $"SpellLayer.Copy() drops {field.Name}");
            }

            foreach (var field in FieldsOf(typeof(SpellEmitter)))
            {
                Assert.AreEqual(field.GetValue(original.emitter), field.GetValue(copy.emitter),
                    $"SpellEmitter.Copy() drops {field.Name}");
            }
        }

        // THE HALF A SHALLOW ARRAY COPY WOULD PASS. Copy()'s whole reason for
        // existing is that a fight must not be able to edit the catalogue it
        // was dealt from -- and an array of references copied by Array.Clone
        // hands the fight the catalogue's own layer objects, which is the same
        // bug one level in and invisible to any assertion about values.
        [Test]
        public void MutatingACopiedLayerLeavesTheOriginalAlone()
        {
            var original = FullyPopulated();
            var copy = original.Copy();

            Assert.AreNotSame(original.layers, copy.layers, "the array itself is shared");
            Assert.AreNotSame(original.layers[0], copy.layers[0], "the layer objects are shared");
            Assert.AreNotSame(original.layers[0].emitter, copy.layers[0].emitter,
                "the emitter blocks are shared");

            copy.layers[0].size = 4242f;
            copy.layers[0].emitter.rate = 9999f;

            Assert.AreEqual(190f, original.layers[0].size,
                "editing a copied layer reached back into the catalogue");
            Assert.AreEqual(40f, original.layers[0].emitter.rate,
                "editing a copied emitter reached back into the catalogue");
        }

        // An empty presentation copies to an empty one rather than to a null
        // array, because every reader below walks `layers` without a guard and
        // a null would be a NullReferenceException per cast instead of a spell
        // with no layers.
        [Test]
        public void APresentationWithNoLayersCopiesToAnEmptyArrayRatherThanNull()
        {
            var copy = new SpellPresentation().Copy();

            Assert.IsNotNull(copy.layers);
            Assert.AreEqual(0, copy.layers.Length);
            Assert.IsFalse(copy.HasLayers);
        }
    }
}
