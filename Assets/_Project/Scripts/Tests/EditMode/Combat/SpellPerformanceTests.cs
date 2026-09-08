using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHEN EVERY LAYER OF A CAST OPENS AND WHEN IT STOPS, resolved once from
    // authored content and a frame count.
    //
    // Every expected number below is a LITERAL. Recomputing them from the
    // production expression would make each test pass for any expression --
    // including the one that reads "the layer draws for the rest of the fight",
    // which is what two of the pilot's own five layers said before the lifetime
    // table existed.
    public class SpellPerformanceTests
    {
        // The folders the two proof spells play, with the frame counts they
        // actually hold. A function rather than a folder read, which is the
        // seam that keeps this whole resolution EditMode-testable.
        private static readonly Dictionary<string, int> Frames = new Dictionary<string, int>
        {
            ["Spells/prismatic_orb_water_core"] = 6,
            ["Spells/prismatic_orb_water_contact"] = 9,
            ["Spells/prismatic_orb_water_wake"] = 1,
            ["Spells/prismatic_orb_water_drops"] = 8,
            ["Spells/cinderfault_eruption"] = 9,
            ["Spells/cinderfault_ground"] = 9,
            ["Spells/mud_burst"] = 26,
            ["Spells/frost_flare"] = 9,
            ["Vfx/impact_burst"] = 6,
        };

        private static int FrameCountOf(string path) => Frames.TryGetValue(path ?? "", out int n) ? n : 0;

        private static SpellPerformance Resolve(SpellPresentation vfx, int targets = 1) =>
            SpellPerformance.Resolve(vfx, targets, FrameCountOf);

        private static SpellLayerInstance Named(SpellPerformance performance, string id) =>
            performance.Instances.First(i => i.Layer.id == id);

        // ---- the lifetime table, layer by layer ---------------------------------

        // THE PILOT'S FIVE LAYERS, EACH WITH A FINITE END, and two of them
        // author no `seconds` at all. `core` is until 'loop' and `wake` is
        // until 'hold'; read as end policies rather than as descriptions of
        // what the sheet does while it lives, both would draw forever.
        [Test]
        public void EveryLayerOfTheWaterPilotEndsAtAStatedInstant()
        {
            var performance = Resolve(SpellLayerFixtures.Water());

            // Travels 0.25s with no hold, so it ends at its arrival however
            // many times it looped its six frames getting there.
            Assert.AreEqual(0.25f, Named(performance, "core").EndSeconds, 1e-5f);

            // Follows the core, so it ends when the core does and never after
            // it; its 0.10 fade then clears it.
            Assert.AreEqual(0.25f, Named(performance, "wake").EndSeconds, 1e-5f);
            Assert.AreEqual(0.35f, Named(performance, "wake").ClearedSeconds, 1e-5f);

            // window + lifeMax, which is what lets a shed that stopped at the
            // target keep falling.
            Assert.AreEqual(0.63f, Named(performance, "shed").EndSeconds, 1e-5f);

            // Opens at the arrival and runs eight of nine contact frames at
            // 26fps -- startFrame 2 is 1-based, so one frame is skipped.
            Assert.AreEqual(0.25f, Named(performance, "splash").StartSeconds, 1e-5f);
            Assert.AreEqual(0.5576923f, Named(performance, "splash").EndSeconds, 1e-5f);

            // A burst at the hit cue plus the longest life it can throw. Cue
            // moved from 0.327 to 0.25 (arrival exactly, not arrival plus two
            // contact frames) in the 2026-09-08 second battle-speed pass, so
            // 0.327+0.34=0.667 became 0.25+0.34=0.59 -- now under the shed's
            // own 0.63s clear, so the shed is the last layer standing rather
            // than the spray.
            Assert.AreEqual(0.25f, Named(performance, "spray").StartSeconds, 1e-5f);
            Assert.AreEqual(0.59f, Named(performance, "spray").EndSeconds, 1e-5f);

            Assert.AreEqual(0.63f, performance.ClearedSeconds, 1e-5f,
                "the last of the cast to clear is now the shed (window + lifeMax), not the burst -- " +
                "the tightened cue moved the burst's own clear ahead of it");
        }

        [Test]
        public void BothCinderfaultLayersRunTheSameLengthFromRelease()
        {
            var performance = Resolve(SpellLayerFixtures.Cinderfault(), targets: 3);

            Assert.AreEqual(0f, Named(performance, "fault").StartSeconds, 1e-5f);
            Assert.AreEqual(0.78f, Named(performance, "fault").EndSeconds, 1e-5f);
            Assert.AreEqual(0.78f, Named(performance, "erupt").EndSeconds, 1e-5f);
            Assert.AreEqual(0.43333334f, performance.HitCueSeconds, 1e-5f);
        }

        [Test]
        public void TheSyntheticTwoBurstsOverlapWithTheSecondOpeningWhileTheFirstStillDraws()
        {
            var performance = Resolve(SpellLayerFixtures.TwoBurst());

            var first = performance.Instances[0];
            var second = performance.Instances[1];

            Assert.AreEqual(0f, first.StartSeconds, 1e-5f);
            Assert.AreEqual(0.24f, first.EndSeconds, 1e-5f);
            Assert.AreEqual(0.18f, second.StartSeconds, 1e-5f);
            Assert.AreEqual(0.42f, second.EndSeconds, 1e-5f);

            Assert.Less(second.StartSeconds, first.EndSeconds,
                "the second burst opens while the first is 75% through -- that overlap is the whole point " +
                "of the fixture");
        }

        // A FOLLOWER ENDS WITH ITS SOURCE, not at its own authored length. The
        // wake authors no seconds at all; if it did, the source's ending would
        // still be the one that counts.
        [Test]
        public void AFollowerTakesItsSourcesEndingEvenWhenItAuthorsALongerOne()
        {
            var water = SpellLayerFixtures.Water();
            water.layers.First(l => l.id == "wake").seconds = 5f;

            var performance = Resolve(water);

            Assert.AreEqual(0.25f, Named(performance, "wake").EndSeconds, 1e-5f);
        }

        [Test]
        public void AFollowersFadeIsCappedAtWhatAnEndingMayTake()
        {
            var water = SpellLayerFixtures.Water();
            water.layers.First(l => l.id == "wake").fade = 9f;

            var performance = Resolve(water);

            Assert.AreEqual(0.25f + SpellLayerRules.MaxFadeSeconds,
                Named(performance, "wake").ClearedSeconds, 1e-5f);
        }

        // ---- arrival -------------------------------------------------------------

        // ONE TIME FOR THE WHOLE CAST AT N PLACES. Three projectiles leave one
        // caster and land on three slots at the same instant; only their
        // POSITIONS differ. Revision 3 said a three-target cast had three
        // arrival times, and nothing in the model made that true.
        [Test]
        public void EveryInstanceOfAnArrivalScheduledLayerOpensAtTheSameInstant()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 3);

            var splashes = performance.Instances.Where(i => i.Layer.id == "splash").ToList();

            Assert.AreEqual(3, splashes.Count, "a target-placed layer draws once per struck target");
            Assert.AreEqual(0.25f, performance.ArrivalSeconds, 1e-5f);
            foreach (var splash in splashes) Assert.AreEqual(0.25f, splash.StartSeconds, 1e-5f);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, splashes.Select(s => s.TargetIndex).ToList());
        }

        [Test]
        public void ACastWithNoTravellingLayerArrivesAtRelease()
        {
            var performance = Resolve(SpellLayerFixtures.Cinderfault(), targets: 3);

            Assert.AreEqual(0f, performance.ArrivalSeconds,
                "defined rather than undefined, so the adapter's output -- every legacy layer is at " +
                "release -- has no hole to fall into");
        }

        [Test]
        public void ArrivalIsTheFirstTravellingLayersOwnFlightAndNotTheLongestOne()
        {
            var vfx = new SpellPresentation
            {
                layerFormat = 1,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "core", render = "sprite", place = "caster",
                        path = "Spells/frost_flare", travelSeconds = 0.25f, travelDelay = 0.05f,
                    },
                    new SpellLayer
                    {
                        id = "late", render = "sprite", place = "caster",
                        path = "Spells/frost_flare", travelSeconds = 0.9f,
                    },
                },
            };

            Assert.AreEqual(0.30f, Resolve(vfx).ArrivalSeconds, 1e-5f);
        }

        // ---- scope ---------------------------------------------------------------

        [Test]
        public void AFormationLayerDrawsOnceHoweverManyTargetsWereStruck()
        {
            var performance = Resolve(SpellLayerFixtures.Cinderfault(), targets: 3);

            Assert.AreEqual(1, performance.Instances.Count(i => i.Layer.id == "fault"));
            Assert.AreEqual(3, performance.Instances.Count(i => i.Layer.id == "erupt"));
        }

        // A PROJECTILE FANS OUT PER TARGET EVEN THOUGH ITS PLACE IS CASTER-SIDE,
        // which is the one exception in the scope table: `caster` says where
        // the box STARTS, and a cast reaching three enemies needs three boxes
        // leaving the same point.
        [Test]
        public void ACasterPlacedProjectileStillDrawsOncePerStruckTarget()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 3);

            Assert.AreEqual(3, performance.Instances.Count(i => i.Layer.id == "core"));
        }

        // A FOLLOWER INHERITS THE SCOPE OF WHAT IT FOLLOWS, and that is a
        // property of the placement word rather than of any spell id -- which
        // is the brief's "per-target visual fan-out must not accidentally
        // repeat a shared ground effect".
        [Test]
        public void AFollowerIsPerTargetWhenItsSourceIsAndRidesItsOwnTargetsInstance()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 3);

            var wakes = performance.Instances.Where(i => i.Layer.id == "wake").ToList();
            Assert.AreEqual(3, wakes.Count);

            foreach (var wake in wakes)
            {
                var source = performance.Instances[wake.SourceInstance];
                Assert.AreEqual("core", source.Layer.id);
                Assert.AreEqual(wake.TargetIndex, source.TargetIndex,
                    "a wake on target 2 must ride the core flying at target 2");
            }
        }

        [Test]
        public void ACastThatStruckNothingDrawsItsCastLevelLayersAndNoPerTargetOnes()
        {
            var performance = Resolve(SpellLayerFixtures.Cinderfault(), targets: 0);

            Assert.AreEqual(1, performance.Instances.Count(i => i.Layer.id == "fault"));
            Assert.AreEqual(0, performance.Instances.Count(i => i.Layer.id == "erupt"));
        }

        // ---- draw order ----------------------------------------------------------

        // AUTHORED ARRAY ORDER IS DRAW ORDER, so a spray authored after its
        // splash gets the higher member index and draws over it. Nothing in
        // content authors a z-order and there is nothing to keep in sync.
        [Test]
        public void InstancesComeOutInAuthoredOrderWithTargetsInStruckOrder()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 2);

            var order = performance.Instances.Select(i => (i.Layer.id, i.TargetIndex)).ToList();

            CollectionAssert.AreEqual(new[]
            {
                ("core", 0), ("core", 1),
                ("wake", 0), ("wake", 1),
                ("shed", 0), ("shed", 1),
                ("splash", 0), ("splash", 1),
                ("spray", 0), ("spray", 1),
            }, order);
        }

        // ---- the legacy path -----------------------------------------------------

        // THE ADAPTER READ THROUGH THE SAME RESOLUTION. mud_burst's hit cue is
        // 0.65 * 13/26 -- the identical expression ImpactDelayFor used before
        // this existed, pinned as the literal it produces.
        [Test]
        public void APreLayerBlockResolvesToTheTimesItHasAlwaysHad()
        {
            var vfx = new SpellPresentation
            {
                path = "Spells/mud_burst",
                seconds = 0.65f,
                impactFrame = 13,
                departFrame = 9,
                anchor = "travel-centre",
            };

            var performance = Resolve(vfx);

            Assert.AreEqual(0.325f, performance.HitCueSeconds, 1e-5f);
            Assert.AreEqual(0.30f, performance.ArrivalSeconds, 1e-5f,
                "0.2s held at the caster then 0.1s of flight, which is frames 9 to 13 of 26 at 0.025s each");
            Assert.AreEqual(0.65f, performance.Instances[0].EndSeconds, 1e-5f,
                "the authored seconds ends it, not the arrival -- today a travelling sheet keeps playing " +
                "its remaining frames at the target until the sequence runs out");
        }

        // MISSING ART DOES NOT MOVE A PRE-LAYER SPELL'S BLOW TO A NEW PLACE, it
        // moves it to the halfway point ImpactFraction has always answered with.
        // Preserved on purpose: fixing it would retime a shipped spell whose
        // folder went momentarily unreadable, which is a different and larger
        // change than this one.
        [Test]
        public void APreLayerBlockWithNoArtKeepsItsHalfwayFallbackCue()
        {
            var vfx = new SpellPresentation
            {
                path = "Spells/does_not_exist",
                seconds = 0.6f,
                impactFrame = 3,
            };

            Assert.AreEqual(0.3f, Resolve(vfx).HitCueSeconds, 1e-5f);
        }

        // AND A LAYERED SPELL DOES NOT HAVE THAT ACCIDENT TO INHERIT. This is
        // the contract the brief asks for: missing cosmetic artwork must not
        // affect gameplay timing.
        [Test]
        public void ALayeredSpellsCueIsAuthoredSecondsAndDoesNotMoveWhenItsArtIsMissing()
        {
            var water = SpellLayerFixtures.Water();
            foreach (var layer in water.layers) layer.path = "Spells/does_not_exist";

            Assert.AreEqual(0.25f, Resolve(water).HitCueSeconds, 1e-5f);
        }

        [Test]
        public void ASpellWithNoArtAtAllHasAZeroCueAndNoLayers()
        {
            var performance = Resolve(new SpellPresentation());

            Assert.AreEqual(0f, performance.HitCueSeconds);
            CollectionAssert.IsEmpty(performance.Instances);
        }

        // ---- the eased path ------------------------------------------------------

        // THE ONE EVALUATION OF A FLIGHT PATH IN THE PROGRAM, which the sprite
        // renderer and the emitter simulation both read. Two homes for the ease
        // would mean a scheduler lerping linearly beside a renderer easing
        // quadratically -- a silent reshaping of every travelling spell.
        [Test]
        public void APositionOnTheFlightIsTheSquaredEaseAndNotALine()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer { travelSeconds = 0.4f },
                StartSeconds = 0f,
                From = new UiVec(0f, 0f),
                To = new UiVec(100f, 0f),
            };

            Assert.AreEqual(0f, SpellPerformance.PositionOf(instance, 0f).X, 1e-4f);
            Assert.AreEqual(25f, SpellPerformance.PositionOf(instance, 0.2f).X, 1e-4f,
                "halfway through the flight in time is a QUARTER of the way across, because the ease is " +
                "t*t -- it leaves fast and hard rather than being dragged the whole way");
            Assert.AreEqual(100f, SpellPerformance.PositionOf(instance, 0.4f).X, 1e-4f);
            Assert.AreEqual(100f, SpellPerformance.PositionOf(instance, 9f).X, 1e-4f,
                "clamped past the arrival rather than overshooting");
        }

        [Test]
        public void TheHoldBeforeDepartureKeepsTheLayerExactlyWhereItWasCast()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer { travelSeconds = 0.4f, travelDelay = 0.2f },
                StartSeconds = 0f,
                From = new UiVec(0f, 0f),
                To = new UiVec(100f, 0f),
            };

            Assert.AreEqual(0f, SpellPerformance.PositionOf(instance, 0.19f).X, 1e-4f);
            Assert.AreEqual(0f, SpellPerformance.PositionOf(instance, 0.2f).X, 1e-4f);
            Assert.AreEqual(25f, SpellPerformance.PositionOf(instance, 0.4f).X, 1e-4f);
        }

        // The exact derivative, so inherited velocity is not a function of the
        // frame rate the way differencing two samples would make it.
        [Test]
        public void VelocityIsTheEasesOwnDerivativeAndGrowsThroughTheFlight()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer { travelSeconds = 0.4f },
                StartSeconds = 0f,
                From = new UiVec(0f, 0f),
                To = new UiVec(100f, 0f),
            };

            Assert.AreEqual(0f, SpellPerformance.VelocityOf(instance, 0f).X, 1e-4f);
            Assert.AreEqual(250f, SpellPerformance.VelocityOf(instance, 0.2f).X, 1e-4f);
            Assert.AreEqual(500f, SpellPerformance.VelocityOf(instance, 0.4f).X, 1e-4f);
        }

        [Test]
        public void ANonTravellingLayerSitsOnItsAnchorAndCarriesNoVelocity()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer(),
                From = new UiVec(40f, 12f),
                To = new UiVec(40f, 12f),
            };

            Assert.AreEqual(40f, SpellPerformance.PositionOf(instance, 0.3f).X, 1e-4f);
            Assert.AreEqual(12f, SpellPerformance.PositionOf(instance, 0.3f).Y, 1e-4f);
            Assert.AreEqual(UiVec.Zero, SpellPerformance.VelocityOf(instance, 0.3f));
        }

        // ---- the schedule the performance publishes ------------------------------

        [Test]
        public void TheScheduleCarriesOneStartAndOneEndPerInstancePlusExactlyOneHitCue()
        {
            var performance = Resolve(SpellLayerFixtures.Cinderfault(), targets: 3);

            int instances = performance.Instances.Count;
            Assert.AreEqual(instances * 2 + 1, performance.Schedule.Count);

            int cues = 0;
            for (int i = 0; i < performance.Schedule.Count; i++)
            {
                if (performance.Schedule[i].Kind == SpellEventKind.HitCue) cues++;
            }

            Assert.AreEqual(1, cues, "a layer cannot own the cue, or two layers could disagree about when " +
                                     "the blow landed");
        }

        [Test]
        public void WalkingTheWholeCastInOneTickDeliversEveryEventExactlyOnce()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 3);
            var buffer = new List<SpellEvent>();

            performance.Schedule.Crossed(SpellSchedule.BeforeAnything, 99f, buffer);

            Assert.AreEqual(performance.Schedule.Count, buffer.Count);
        }

        // Fifty small ticks and one big one deliver the same events in the same
        // order. The property the whole half-open design exists for, asserted
        // over a real cast rather than over a synthetic schedule.
        [Test]
        public void ManySmallTicksAndOneLongTickDeliverTheSameEvents()
        {
            var performance = Resolve(SpellLayerFixtures.Water(), targets: 3);
            var buffer = new List<SpellEvent>();

            var stepped = new List<string>();
            float previous = SpellSchedule.BeforeAnything;
            for (int i = 0; i <= 50; i++)
            {
                float now = i * 0.02f;
                performance.Schedule.Crossed(previous, now, buffer);
                stepped.AddRange(buffer.Select(e => e.ToString()));
                previous = now;
            }

            performance.Schedule.Crossed(SpellSchedule.BeforeAnything, 1f, buffer);
            var oneStep = buffer.Select(e => e.ToString()).ToList();

            CollectionAssert.AreEqual(oneStep, stepped);
        }

        // ---- which way an instance actually draws --------------------------------
        //
        // THE CAST'S FACING AND THE LAYER'S POLICY ARE TWO NUMBERS, and every
        // mirroring decision in the renderer wants the combination rather than
        // either half. Written out at two call sites and read raw at a third,
        // it drifted: an emitter's cone mirrored on a `facing: none` layer whose
        // own sheet did not.

        [Test]
        public void AMirroredCastDrawsMirroredUnlessTheLayerRefuses()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer { render = "sprite", place = "target", facing = "auto" },
                Facing = -1f,
            };

            Assert.AreEqual(-1f, instance.DrawFacing, 0.0001f,
                "a mirrored cast's auto-facing layer has to draw mirrored");

            instance.Layer.facing = "none";
            Assert.AreEqual(1f, instance.DrawFacing, 0.0001f,
                "'none' is what every pre-layer non-travelling effect becomes, and it means the sheet " +
                "is drawn as authored however the cast is aimed");
        }

        [Test]
        public void AnUnmirroredCastDrawsUnmirroredWhateverTheLayerSays()
        {
            var instance = new SpellLayerInstance
            {
                Layer = new SpellLayer { render = "sprite", place = "target", facing = "auto" },
                Facing = 1f,
            };

            Assert.AreEqual(1f, instance.DrawFacing, 0.0001f);

            instance.Layer.facing = "none";
            Assert.AreEqual(1f, instance.DrawFacing, 0.0001f);
        }
    }
}
