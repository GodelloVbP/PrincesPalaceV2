using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE WALKER'S BOB, which is the whole of the walk animation -- there is no
    // walk cycle in the art, so this curve is what sells a step.
    //
    // MapWalk.BobsPerStep says two and its own comment says "two bounces per
    // step", so two is what the curve has to deliver. It is the only statement
    // of that number anywhere: MapController.Walk.cs just samples PositionAt
    // every frame, so nothing downstream would notice the count being wrong.
    public class MapWalkBobTests
    {
        [Test]
        public void TheFigureIsOnTheGroundAtBothEndsOfAStep()
        {
            Assert.AreEqual(0f, MapWalk.BobAt(0f), 0.001f);
            Assert.AreEqual(0f, MapWalk.BobAt(1f), 0.001f);
        }

        // Two bounces over one step puts a peak at a quarter and at three
        // quarters, with a touchdown between them. Literal fractions rather
        // than a recomputed phase: the point is what the curve DOES, not that
        // it agrees with its own arithmetic.
        [Test]
        public void TwoBouncesPutTheirPeaksAtAQuarterAndThreeQuarters()
        {
            Assert.AreEqual(MapWalk.BobHeight, MapWalk.BobAt(0.25f), 0.001f);
            Assert.AreEqual(MapWalk.BobHeight, MapWalk.BobAt(0.75f), 0.001f);
        }

        [Test]
        public void TheFigureTouchesDownOnceInTheMiddle()
        {
            Assert.AreEqual(0f, MapWalk.BobAt(0.5f), 0.001f);
        }

        // The count itself, counted rather than asserted at three chosen
        // samples -- a curve with twice as many bounces still passes the two
        // pins above if its extra peaks happen to land elsewhere.
        [Test]
        public void ThereAreExactlyBobsPerStepPeaksAcrossOneStep()
        {
            const int samples = 2000;
            int peaks = 0;
            float previous = MapWalk.BobAt(0f);
            float current = MapWalk.BobAt(1f / samples);

            for (int i = 2; i <= samples; i++)
            {
                float next = MapWalk.BobAt(i / (float)samples);
                if (current > previous && current >= next) peaks++;
                previous = current;
                current = next;
            }

            Assert.AreEqual(MapWalk.BobsPerStep, peaks,
                "the walker bounces a different number of times than BobsPerStep claims");
        }

        // Never below the ground line: the bob only ever lifts the figure, the
        // same "it only grows" bargain BreathCurve makes about the authored
        // size.
        [Test]
        public void TheBobNeverGoesBelowTheGroundLine()
        {
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f;
                Assert.GreaterOrEqual(MapWalk.BobAt(t), 0f, $"at t={t}");
                Assert.LessOrEqual(MapWalk.BobAt(t), MapWalk.BobHeight + 0.001f, $"at t={t}");
            }
        }
    }
}
