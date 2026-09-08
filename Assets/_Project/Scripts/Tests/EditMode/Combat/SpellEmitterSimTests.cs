using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // DROPS COMPUTED FROM AGE, NEVER INTEGRATED, and these are the three
    // properties that buys.
    //
    // The closed form is not an optimisation. An integrator would give a
    // different droplet field for a 200ms frame than for fifty 4ms ones, and
    // the difference would show up only on a slow machine, only sometimes, and
    // only as "the water looks wrong" -- which is the class of bug this shape
    // makes unrepresentable rather than rare.
    public class SpellEmitterSimTests
    {
        private static SpellEmitter Shed() => new SpellEmitter
        {
            path = "Spells/prismatic_orb_water_drops",
            rate = 40f,
            window = 0.25f,
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
        };

        // ---- birth is a time, not an event ---------------------------------------

        // PARTICLE i IS BORN AT windowStart + i / rate. A tick spanning six
        // spawn intervals therefore births six particles AT SIX DIFFERENT
        // TIMES, which is the whole difference between a shed laid along the
        // path a projectile took and six drops stacked where the step ended.
        [Test]
        public void ParticlesAreBornOnTheCastsOwnClockAtTheRateTheyWereAuthoredAt()
        {
            var spec = Shed();

            Assert.AreEqual(0f, SpellEmitterSim.BirthOf(spec, 0, 0f), 1e-6f);
            Assert.AreEqual(0.025f, SpellEmitterSim.BirthOf(spec, 1, 0f), 1e-6f);
            Assert.AreEqual(0.2f, SpellEmitterSim.BirthOf(spec, 8, 0f), 1e-6f);
            Assert.AreEqual(0.35f, SpellEmitterSim.BirthOf(spec, 4, 0.25f), 1e-6f,
                "a window that opens later moves every birth with it");
        }

        // A BURST IS THE SAME RULE WITH NO INTERVAL, not a second mechanism:
        // every one of its particles is born at the instant the window opens.
        [Test]
        public void EveryParticleOfABurstIsBornAtTheInstantItsWindowOpens()
        {
            var spray = new SpellEmitter { burst = 18, lifeMin = 0.18f, lifeMax = 0.34f };

            for (int i = 0; i < 18; i++)
            {
                Assert.AreEqual(0.35f, SpellEmitterSim.BirthOf(spray, i, 0.35f), 1e-6f);
            }
        }

        [Test]
        public void AnEmittersTotalCountIsItsBurstPlusItsRateOverItsWindow()
        {
            Assert.AreEqual(10, SpellEmitterSim.CountOf(Shed()));
            Assert.AreEqual(18, SpellEmitterSim.CountOf(new SpellEmitter { burst = 18 }));
            Assert.AreEqual(0, SpellEmitterSim.CountOf(new SpellEmitter()));
        }

        // ---- the closed form ------------------------------------------------------

        // WITH NO DRAG IT IS THE SCHOOLBOOK PARABOLA, pinned as literals so a
        // sign error in the gravity term cannot hide behind the drag branch.
        [Test]
        public void ADraglessDropFollowsTheParabolaItsSpeedAndGravityImply()
        {
            var spec = new SpellEmitter
            {
                burst = 1, drag = 0f, gravity = -1000f, lifeMin = 1f, lifeMax = 1f,
                sizeMin = 1f, sizeMax = 1f, fadeFrom = 1f, endScale = 1f,
            };

            var drop = SpellEmitterSim.At(spec, seed: 7, index: 0,
                p0: new UiVec(0f, 0f), v0: new UiVec(100f, 200f), age: 0.5f, frameCount: 1);

            Assert.IsTrue(drop.Alive);
            Assert.AreEqual(50f, drop.Position.X, 1e-3f, "x is v0.x * t with no drag");
            Assert.AreEqual(-25f, drop.Position.Y, 1e-3f, "y is v0.y * t + gravity * t^2 / 2");
        }

        // DRAG SLOWS IT SHORT OF THE DRAGLESS ANSWER AND NEVER PAST IT. The
        // exponential is the part a sign slip inverts, and an inverted one
        // accelerates the drop away rather than settling it.
        [Test]
        public void DragLeavesADropShortOfWhereItWouldHaveReachedWithout()
        {
            var dragless = new SpellEmitter
            {
                burst = 1, drag = 0f, gravity = 0f, lifeMin = 1f, lifeMax = 1f,
                sizeMin = 1f, sizeMax = 1f, fadeFrom = 1f, endScale = 1f,
            };
            var dragged = new SpellEmitter
            {
                burst = 1, drag = 3.5f, gravity = 0f, lifeMin = 1f, lifeMax = 1f,
                sizeMin = 1f, sizeMax = 1f, fadeFrom = 1f, endScale = 1f,
            };

            var v0 = new UiVec(200f, 0f);
            float far = SpellEmitterSim.At(dragless, 1, 0, UiVec.Zero, v0, 0.4f, 1).Position.X;
            float near = SpellEmitterSim.At(dragged, 1, 0, UiVec.Zero, v0, 0.4f, 1).Position.X;

            Assert.Greater(near, 0f, "drag stopped the drop dead, which is not drag");
            Assert.Less(near, far, "drag did not slow the drop at all");
        }

        // A LARGE CLOCK STEP IS CORRECT BY CONSTRUCTION, and this is the
        // assertion that says so. Nothing accumulates, so sampling at t gives
        // the same answer whether one tick or fifty got there.
        [Test]
        public void FiftySmallStepsAndOneLongStepPutADropInTheSamePlace()
        {
            var spec = Shed();
            var p0 = new UiVec(12f, -30f);
            var v0 = new UiVec(140f, 220f);

            var walked = UiVec.Zero;
            for (int i = 1; i <= 50; i++)
            {
                walked = SpellEmitterSim.At(spec, 3, 0, p0, v0, 0.2f * i / 50f, 8).Position;
            }

            var jumped = SpellEmitterSim.At(spec, 3, 0, p0, v0, 0.2f, 8).Position;

            Assert.AreEqual(jumped.X, walked.X, 1e-4f);
            Assert.AreEqual(jumped.Y, walked.Y, 1e-4f);
        }

        // ---- seeds ---------------------------------------------------------------

        // A SEEDED PREVIEW REPEATS EXACTLY, and a particle's history does not
        // depend on how many other particles existed -- which is what a shared
        // RNG stream would have made it, so adding an unrelated spell to the
        // same fight would have changed the picture.
        [Test]
        public void OneSeedGivesTheIdenticalFieldEveryTime()
        {
            var spec = Shed();

            var first = Field(spec, seed: 991);
            var again = Field(spec, seed: 991);
            var different = Field(spec, seed: 992);

            CollectionAssert.AreEqual(first, again);
            CollectionAssert.AreNotEqual(first, different,
                "two seeds produced the identical field, so the seed is not reaching the hash");
        }

        [Test]
        public void EachParticleLeavesOnItsOwnDirectionAndSpeed()
        {
            var spec = Shed();

            var launches = Enumerable.Range(0, 10)
                .Select(i => SpellEmitterSim.LaunchOf(spec, 991, i, 1f).ToString())
                .Distinct()
                .ToList();

            Assert.GreaterOrEqual(launches.Count, 8,
                "the drops all left on the same vector, so the spread and speed range do nothing");
        }

        // MIRRORED WITH THE CAST, so a monster's spray leaves the way its blow
        // arrived. Reflecting the ANGLE about the vertical rather than negating
        // the result's x keeps the cone the same shape rather than turning a
        // narrow fan into a wide one.
        [Test]
        public void AMirroredCastThrowsItsDropsTheOtherWayWithTheSameConeShape()
        {
            var spec = new SpellEmitter
            {
                burst = 4, spreadDegrees = 40f, aimDegrees = 30f,
                speedMin = 100f, speedMax = 100f, lifeMin = 1f, lifeMax = 1f,
            };

            var forward = SpellEmitterSim.LaunchOf(spec, 5, 0, 1f);
            var mirrored = SpellEmitterSim.LaunchOf(spec, 5, 0, -1f);

            Assert.AreEqual(forward.X, -mirrored.X, 1e-3f);
            Assert.AreEqual(forward.Y, mirrored.Y, 1e-3f, "mirroring flipped the drop's vertical too");
        }

        // ---- life and fade --------------------------------------------------------

        [Test]
        public void ADropIsDeadOnceItIsOlderThanItsOwnLife()
        {
            var spec = new SpellEmitter
            {
                burst = 1, lifeMin = 0.3f, lifeMax = 0.3f, sizeMin = 1f, sizeMax = 1f,
                fadeFrom = 1f, endScale = 1f,
            };

            Assert.IsTrue(SpellEmitterSim.At(spec, 1, 0, UiVec.Zero, UiVec.Zero, 0.29f, 1).Alive);
            Assert.IsFalse(SpellEmitterSim.At(spec, 1, 0, UiVec.Zero, UiVec.Zero, 0.31f, 1).Alive);
            Assert.IsFalse(SpellEmitterSim.At(spec, 1, 0, UiVec.Zero, UiVec.Zero, -0.01f, 1).Alive,
                "a drop is not alive before it is born");
        }

        // FULL STRENGTH UNTIL fadeFrom OF THE WAY THROUGH, then down to nothing.
        // 1 means no fade at all, which is why it is the field's default and
        // why a "non-zero means authored" test would have refused every sprite
        // layer in the game.
        [TestCase(0.6f, 0.0f, 1.0f)]
        [TestCase(0.6f, 0.6f, 1.0f)]
        [TestCase(0.6f, 0.8f, 0.5f)]
        [TestCase(0.6f, 1.0f, 0.0f)]
        [TestCase(1.0f, 0.9f, 1.0f)]
        public void ADropHoldsItsAlphaUntilItsFadeBeginsAndThenFallsToNothing(
            float fadeFrom, float through, float alpha)
        {
            Assert.AreEqual(alpha, SpellEmitterSim.AlphaThrough(fadeFrom, through), 1e-4f);
        }

        [Test]
        public void ADropPicksItsStillFromTheFolderAndAFolderOfOneSelectsThatOne()
        {
            var spec = Shed();

            var frames = Enumerable.Range(0, 40)
                .Select(i => SpellEmitterSim.At(spec, 991, i, UiVec.Zero, UiVec.Zero, 0.01f, 8).Frame)
                .ToList();

            Assert.IsTrue(frames.All(f => f >= 0 && f < 8), "a drop chose a still its folder does not have");
            Assert.GreaterOrEqual(frames.Distinct().Count(), 4,
                "every drop drew the same still, so an eight-cell atlas is doing the work of one");

            var single = SpellEmitterSim.At(spec, 991, 3, UiVec.Zero, UiVec.Zero, 0.01f, 1);
            Assert.AreEqual(0, single.Frame);
        }

        // THE WHOLE FIELD, launches included. A helper handing every drop the
        // same v0 would compare ten copies of one trajectory and call them
        // repeatable -- which they are, and which proves nothing about the
        // hash that is supposed to make them differ.
        private static List<string> Field(SpellEmitter spec, int seed) =>
            Enumerable.Range(0, SpellEmitterSim.CountOf(spec))
                .Select(i => SpellEmitterSim
                    .At(spec, seed, i, UiVec.Zero, SpellEmitterSim.LaunchOf(spec, seed, i, 1f), 0.1f, 8)
                    .Position.ToString())
                .ToList();
    }
}
