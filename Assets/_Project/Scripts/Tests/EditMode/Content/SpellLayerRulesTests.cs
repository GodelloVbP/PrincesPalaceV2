using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHAT A LAYERED PRESENTATION MAY SAY, one case per rule.
    //
    // Every one of these asserts the MESSAGE and not merely the refusal. A
    // validator's whole product is the sentence an author reads: a rule that
    // fires with the wrong text sends someone looking for a field that is not
    // in their file, and a test asserting only `problems.Count == 1` cannot
    // tell the two apart.
    //
    // The legal cases matter as much as the illegal ones. Four of the six
    // discriminators default when blank, and a rule table that refused an
    // omission would make every optional word mandatory -- so a layer authoring
    // none of sort/until/facing/at is here as a case that must PASS.
    public class SpellLayerRulesTests
    {
        private const string Label = "skill 'test_spell'";

        private static SpellPresentation With(params SpellLayer[] layers) =>
            new SpellPresentation { layerFormat = 1, layers = layers };

        private static List<string> Problems(SpellPresentation vfx) => SpellLayerRules.Check(Label, vfx);

        private static string Only(SpellPresentation vfx)
        {
            var problems = Problems(vfx);
            Assert.AreEqual(1, problems.Count,
                "expected exactly one problem, got: " + string.Join(" | ", problems));
            return problems[0];
        }

        // A layer that satisfies every rule, for a test to break in one place.
        private static SpellLayer Sprite(string id = "core") => new SpellLayer
        {
            id = id,
            render = "sprite",
            place = "target",
            path = "Spells/frost_flare",
            seconds = 0.5f,
        };

        // ---- the six words ------------------------------------------------------

        [Test]
        public void AnUnknownRendererIsRefusedAndTheKnownOnesAreListed()
        {
            var layer = Sprite();
            layer.render = "beam";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].render 'beam' is not a renderer", message);
            StringAssert.Contains("Known: sprite, still, emitter", message);
        }

        [Test]
        public void ABlankRendererIsRefusedBecauseThereIsNoDefaultForWhatDraws()
        {
            var layer = Sprite();
            layer.render = "";

            var problems = Problems(With(layer));
            Assert.IsTrue(problems.Any(p => p.Contains("vfx.layers[0].render is blank")),
                string.Join(" | ", problems));
        }

        [Test]
        public void AnUnknownPlacementIsRefusedAndTheLayerFormIsAdvertised()
        {
            var layer = Sprite();
            layer.place = "projectile";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].place 'projectile' is not a placement", message);
            StringAssert.Contains("layer:<id>", message);
        }

        [Test]
        public void ABlankPlacementIsRefusedBecauseThereIsNoDefaultForWhereItGoes()
        {
            var layer = Sprite();
            layer.place = "";

            var problems = Problems(With(layer));
            Assert.IsTrue(problems.Any(p => p.Contains("vfx.layers[0].place is blank")),
                string.Join(" | ", problems));
        }

        [Test]
        public void AnUnknownScheduleWordIsRefused()
        {
            var layer = Sprite();
            layer.at = "impact";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].at 'impact' is not a schedule point", message);
            StringAssert.Contains("Known: release, arrival, hit", message);
        }

        [Test]
        public void AnUnknownEndPolicyIsRefused()
        {
            var layer = Sprite();
            layer.until = "forever";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].until 'forever' is not an end policy", message);
            StringAssert.Contains("Known: once, loop, hold", message);
        }

        [Test]
        public void AnUnknownFacingPolicyIsRefused()
        {
            var layer = Sprite();
            layer.facing = "mirrored";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].facing 'mirrored' is not a mirroring policy", message);
        }

        [Test]
        public void AnUnknownDrawBandIsRefused()
        {
            var layer = Sprite();
            layer.sort = "overlay";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].sort 'overlay' is not a draw band", message);
            StringAssert.Contains("Known: ground, effects", message);
        }

        // THE CASE THAT PINS THE DEFAULTS. The synthetic two-burst proof of the
        // plan's section 8 authors none of these four words, so a rule table
        // that treated a blank as unknown would refuse the very fixture written
        // to prove the scheduler handles overlapping instances.
        [Test]
        public void ALayerAuthoringNoSortNoUntilNoFacingAndNoCueIsLegalAndTakesTheDefaults()
        {
            var layer = new SpellLayer
            {
                render = "sprite",
                place = "caster-centre",
                path = "Vfx/impact_burst",
                seconds = 0.24f,
            };

            CollectionAssert.IsEmpty(Problems(With(layer)));

            Assert.AreEqual(SpellSort.Effects, layer.Sort);
            Assert.AreEqual(SpellEnd.Once, layer.Until);
            Assert.AreEqual(SpellFacing.Auto, layer.Facing);
            Assert.AreEqual(SpellCue.Release, layer.At);
        }

        // ---- lifetimes ----------------------------------------------------------

        [Test]
        public void ALoopWithNoSecondsNoTravelAndNothingToFollowIsRefused()
        {
            var layer = Sprite();
            layer.until = "loop";
            layer.seconds = 0f;

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] is until 'loop' with no seconds, no travel and nothing " +
                                  "to follow", message);
            StringAssert.Contains("draw for the rest of the fight", message);
        }

        // A LOOP THAT TRAVELS IS BOUNDED BY ITS OWN ARRIVAL, which is exactly
        // what the Water core does: it loops its six frames while it crosses
        // the stage and is gone the instant it lands.
        [Test]
        public void ALoopThatTravelsIsBoundedByItsArrivalAndIsLegal()
        {
            var layer = Sprite();
            layer.place = "caster-centre";
            layer.until = "loop";
            layer.seconds = 0f;
            layer.travelSeconds = 0.25f;

            CollectionAssert.IsEmpty(Problems(With(layer)));
        }

        // A HOLD ON A FOLLOWER IS BOUNDED BY ITS SOURCE, which is the Water
        // wake: it ends when the core does and never after it.
        [Test]
        public void AHoldPlacedOnAnotherLayerIsBoundedByThatLayerAndIsLegal()
        {
            var core = Sprite();
            core.place = "caster-centre";
            core.travelSeconds = 0.25f;
            core.seconds = 0f;

            var wake = new SpellLayer
            {
                id = "wake",
                render = "still",
                place = "layer:core",
                path = "Spells/prismatic_orb_water_wake",
                until = "hold",
                fade = 0.1f,
            };

            CollectionAssert.IsEmpty(Problems(With(core, wake)));
        }

        [Test]
        public void AFadeLongerThanAnEndingMayTakeIsRefused()
        {
            var layer = Sprite();
            layer.fade = 1.2f;

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].fade 1.2 is longer than the 0.5s a layer's ending may take",
                message);
        }

        [Test]
        public void AFadeExactlyAtTheCeilingIsLegal()
        {
            var layer = Sprite();
            layer.fade = SpellLayerRules.MaxFadeSeconds;

            CollectionAssert.IsEmpty(Problems(With(layer)));
        }

        // ---- references ---------------------------------------------------------

        [Test]
        public void AReferenceToNoDeclaredLayerIsRefusedAndTheDeclaredOnesAreListed()
        {
            var wake = new SpellLayer
            {
                id = "wake",
                render = "still",
                place = "layer:core",
                path = "Spells/wake",
                seconds = 0.2f,
            };
            var splash = Sprite("splash");

            string message = Only(With(wake, splash));
            StringAssert.Contains("vfx.layers[0].place 'layer:core' names no layer", message);
            StringAssert.Contains("Declared: wake, splash", message);
        }

        [Test]
        public void TwoLayersPlacedOnEachOtherAreRefusedAsACycle()
        {
            var a = new SpellLayer
            { id = "a", render = "sprite", place = "layer:b", path = "Spells/x", seconds = 0.2f };
            var b = new SpellLayer
            { id = "b", render = "sprite", place = "layer:a", path = "Spells/x", seconds = 0.2f };

            var problems = Problems(With(a, b));
            Assert.IsTrue(problems.Any(p => p.Contains("which is a cycle")), string.Join(" | ", problems));
        }

        [Test]
        public void ADuplicateIdIsRefusedBecauseAReferenceCouldNameEither()
        {
            var problems = Problems(With(Sprite("core"), Sprite("core")));
            Assert.IsTrue(problems.Any(p => p.Contains("vfx.layers id 'core' is used twice")),
                string.Join(" | ", problems));
        }

        // ---- the formation ------------------------------------------------------

        [Test]
        public void AFormationLayerAuthoringASizeIsRefusedBecauseTheSpanOverridesIt()
        {
            var layer = Sprite();
            layer.place = "formation";
            layer.size = 380f;

            string message = Only(With(layer));
            StringAssert.Contains("placed on the formation and authors size 380, which the measured span " +
                                  "overrides", message);
        }

        // CINDERFAULT'S OWN FAULT STATES impactY AND NO impactX, and the
        // exemption is not a courtesy: a formation's horizontal centre is the
        // MEASURED midpoint of the struck span, so there is nothing for an
        // impactX to correct -- and the pre-layer block it replaces has no
        // groundImpactX field to have come from either.
        [Test]
        public void AFormationLayerMayStateItsGroundLineWithoutAHorizontalPoint()
        {
            var fault = new SpellLayer
            {
                id = "fault",
                render = "sprite",
                place = "formation",
                path = "Spells/cinderfault_ground",
                seconds = 0.78f,
                impactY = 0.063f,
                facing = "none",
                sort = "ground",
            };

            CollectionAssert.IsEmpty(Problems(With(fault)));
        }

        // THE SAME LONE impactY ON A TARGET IS STILL REFUSED. The exemption is
        // the formation's, not a relaxation of the both-or-neither rule.
        [Test]
        public void ATargetLayerStatingOnlyItsGroundLineIsStillRefused()
        {
            var layer = Sprite();
            layer.impactY = 0.5f;

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] states impactY and not impactX", message);
        }

        [Test]
        public void ALayerStatingOnlyAHorizontalPointIsRefusedEverywhere()
        {
            var layer = Sprite();
            layer.place = "formation";
            layer.impactX = 0.5f;

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] states impactX and not impactY", message);
        }

        // ---- what each kind needs -----------------------------------------------

        [Test]
        public void AnEmitterThatAuthorsNeitherRateNorBurstIsRefused()
        {
            var layer = new SpellLayer
            {
                id = "shed",
                render = "emitter",
                place = "target-centre",
                emitter = new SpellEmitter { path = "Spells/drops" },
            };

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] is an emitter and authors neither rate nor burst, so it " +
                                  "emits nothing", message);
        }

        [Test]
        public void ASpriteWithNoPathIsRefused()
        {
            var layer = Sprite();
            layer.path = "";

            string message = Only(With(layer));
            StringAssert.Contains("is a sprite and authors no path", message);
        }

        // THE RULE COMPARED AGAINST A DEFAULT-CONSTRUCTED EMITTER RATHER THAN
        // AGAINST ZERO. sizeMin, sizeMax and fadeFrom all initialise to 1f, so
        // a "non-zero" test would refuse every sprite layer that ships -- this
        // is the Water core, and it must pass.
        [Test]
        public void ASpriteCarryingAnUntouchedEmitterBlockIsNotRefused()
        {
            var core = new SpellLayer
            {
                id = "core",
                render = "sprite",
                place = "caster-centre",
                travelSeconds = 0.25f,
                path = "Spells/prismatic_orb_water_core",
                fps = 24f,
                until = "loop",
                size = 190f,
                facing = "auto",
                sort = "effects",
            };

            Assert.IsFalse(core.emitter.IsAuthored,
                "a default-constructed SpellEmitter must not read as authored, or every sprite layer in " +
                "the game is refused");
            CollectionAssert.IsEmpty(Problems(With(core)));
        }

        [Test]
        public void ASpriteThatAuthorsEmitterSettingsIsRefused()
        {
            var layer = Sprite();
            layer.emitter.gravity = -1400f;

            string message = Only(With(layer));
            StringAssert.Contains("is a sprite and authors emitter settings, which are inert", message);
        }

        // A sprite layer that authors ONLY weights, everything else left at
        // its default, is the case IsAuthored's array-aware equality exists
        // for: a bare `Equals` on two `float[]` references would have missed
        // this the same way it would have flagged EVERY untouched emitter.
        [Test]
        public void ASpriteThatAuthorsOnlyEmitterWeightsIsRefused()
        {
            var layer = Sprite();
            layer.emitter.weights = new float[] { 1f, 1f, 1f };

            string message = Only(With(layer));
            StringAssert.Contains("is a sprite and authors emitter settings, which are inert", message);
        }

        // ---- emitter weights ------------------------------------------------------

        [Test]
        public void AnEmitterWeightBelowZeroIsRefused()
        {
            var layer = new SpellLayer
            {
                render = "emitter",
                place = "target-centre",
                emitter = new SpellEmitter
                {
                    path = "Spells/drops", burst = 8,
                    weights = new float[] { 1f, -0.5f, 1f },
                },
            };

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].emitter.weights[1] -0.5 cannot be negative", message);
        }

        [Test]
        public void AnEmitterWeightsArrayThatIsAllZeroIsRefused()
        {
            var layer = new SpellLayer
            {
                render = "emitter",
                place = "target-centre",
                emitter = new SpellEmitter
                {
                    path = "Spells/drops", burst = 8,
                    weights = new float[] { 0f, 0f, 0f },
                },
            };

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].emitter.weights are all zero, so no cell could ever be " +
                                  "picked", message);
        }

        [Test]
        public void AnEmitterWeightThatIsNotFiniteIsRefused()
        {
            var layer = new SpellLayer
            {
                render = "emitter",
                place = "target-centre",
                emitter = new SpellEmitter
                {
                    path = "Spells/drops", burst = 8,
                    weights = new float[] { 1f, float.NaN, 1f },
                },
            };

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0].emitter.weights[1] NaN is not a finite number", message);
        }

        [Test]
        public void AnEmitterWithLegalWeightsIsNotRefused()
        {
            var layer = new SpellLayer
            {
                render = "emitter",
                place = "target-centre",
                emitter = new SpellEmitter
                {
                    path = "Spells/drops", burst = 8,
                    weights = new float[] { 0.2f, 0f, 0.8f },
                },
            };

            CollectionAssert.IsEmpty(Problems(With(layer)),
                "a zero entry is legal -- only ALL zero, negative or non-finite are refused");
        }

        [Test]
        public void AnEmitterThatAuthorsSequenceWordsIsRefused()
        {
            var layer = new SpellLayer
            {
                render = "emitter",
                place = "target-centre",
                until = "loop",
                emitter = new SpellEmitter { path = "Spells/drops", burst = 18 },
            };

            string message = Only(With(layer));
            StringAssert.Contains("authors fps, startFrame or until", message);
        }

        // ---- travel -------------------------------------------------------------

        [Test]
        public void ALayerThatTravelsFromTheTargetIsRefusedBecauseItHasNowhereToLeaveFrom()
        {
            var layer = Sprite();
            layer.travelSeconds = 0.25f;

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] travels but is placed on target, so it has nowhere to " +
                                  "travel from", message);
        }

        [Test]
        public void ATravelDelayWithNoTravelIsRefused()
        {
            var layer = Sprite();
            layer.travelDelay = 0.1f;

            string message = Only(With(layer));
            StringAssert.Contains("authors travelDelay 0.1 but does not travel", message);
        }

        // ---- arrival ------------------------------------------------------------

        [Test]
        public void ACueAtArrivalInASpellThatDoesNotTravelIsRefused()
        {
            var layer = Sprite();
            layer.at = "arrival";

            string message = Only(With(layer));
            StringAssert.Contains("vfx.layers[0] fires at arrival, but nothing in this spell travels",
                message);
        }

        // TWO TRAVELLERS MAKE 'arrival' NAME TWO INSTANTS, which is what lets
        // "the first travelling layer in authored order" be a definition rather
        // than an arbitrary tie-break.
        [Test]
        public void TwoTravellingLayersInACastThatSchedulesArrivalAreRefused()
        {
            var core = Sprite("core");
            core.place = "caster-centre";
            core.travelSeconds = 0.25f;

            var shard = Sprite("shard");
            shard.place = "caster";
            shard.travelSeconds = 0.3f;

            var splash = Sprite("splash");
            splash.at = "arrival";

            var problems = Problems(With(core, shard, splash));
            Assert.IsTrue(problems.Any(p => p.Contains("'core' and 'shard' both travel, so 'arrival' names " +
                                                       "two instants")), string.Join(" | ", problems));
        }

        // WATER'S OWN SPLASH IS PLACED ON THE TARGET, SCHEDULED AT ARRIVAL, AND
        // NEITHER TRAVELS NOR NAMES THE CORE. A layer-scoped arrival rule would
        // have refused the pilot spell its own content build.
        [Test]
        public void ALayerMayFireAtArrivalWithoutTravellingOrNamingTheTraveller()
        {
            var core = Sprite("core");
            core.place = "caster-centre";
            core.travelSeconds = 0.25f;

            var splash = Sprite("splash");
            splash.at = "arrival";

            CollectionAssert.IsEmpty(Problems(With(core, splash)));
        }

        // ---- the block as a whole -----------------------------------------------

        [Test]
        public void LayersWithNoLayerFormatAreRefusedWithTheKeyToAdd()
        {
            var vfx = new SpellPresentation { layers = new[] { Sprite() } };

            string message = Only(vfx);
            StringAssert.Contains("vfx authors layers but no layerFormat", message);
            StringAssert.Contains("\"layerFormat\": 1", message);
        }

        [Test]
        public void LayersBesideTheSingleBlockPathAreRefused()
        {
            var vfx = With(Sprite());
            vfx.path = "Spells/frost_flare";

            string message = Only(vfx);
            StringAssert.Contains("vfx authors both layers and the single-block path", message);
        }

        [Test]
        public void AHitCueOnAPreLayerBlockIsRefusedBecauseItDerivesItsCueFromTheFrame()
        {
            var vfx = new SpellPresentation { hitCueSeconds = 0.433f, path = "Spells/frost_flare" };

            string message = Only(vfx);
            StringAssert.Contains("vfx.hitCueSeconds 0.433 needs layerFormat 1", message);
        }

        // Every spell that ships today authors no layers, so the whole rule set
        // has to be silent on one -- otherwise M1 would have refused the
        // existing catalogue.
        [Test]
        public void APreLayerBlockPassesEveryRuleUntouched()
        {
            var cinderfault = new SpellPresentation
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

            CollectionAssert.IsEmpty(Problems(cinderfault));
        }

        // ---- the two proof spells, whole ----------------------------------------

        // THE PILOT'S OWN FIVE LAYERS, exactly as the plan's section 6d authors
        // them. Everything above tests one rule against one layer; this is the
        // case that catches a rule which is individually right and collectively
        // refuses the thing the design exists to express.
        [Test]
        public void TheWaterPilotAsAuthoredPassesEveryRule()
        {
            CollectionAssert.IsEmpty(Problems(SpellLayerFixtures.Water()));
        }

        [Test]
        public void CinderfaultReAuthoredAsLayersPassesEveryRule()
        {
            CollectionAssert.IsEmpty(Problems(SpellLayerFixtures.Cinderfault()));
        }

        [Test]
        public void TheSyntheticTwoBurstPassesEveryRule()
        {
            CollectionAssert.IsEmpty(Problems(SpellLayerFixtures.TwoBurst()));
        }
    }
}
