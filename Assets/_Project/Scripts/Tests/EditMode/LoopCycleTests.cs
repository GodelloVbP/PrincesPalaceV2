using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // How a looping stance is paced, which is the whole of the answer to "I
    // clearly see that it is a loop of 6 sprites".
    //
    // THE RHYTHM IS THE TELL, not the frame count -- six drawings is a normal
    // number and hand-drawn animation runs on twos all the time. What gives a
    // loop away is the hard cut at the wrap and the metronome in between, and
    // both of those are arithmetic rather than art, which is why they are
    // testable at all.
    public class LoopCycleTests
    {
        // ---- the shape of a cycle ---------------------------------------------

        // A ping-pong pass is out and back, and the two turn-around drawings
        // are shown once each rather than twice in a row -- so six frames is
        // ten steps, not twelve.
        [Test]
        public void APingPongCycleIsTwiceTheSheetLessItsTwoTurnArounds()
        {
            Assert.AreEqual(10 * 0.1f, LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.PingPong), 0.0001f);
            Assert.AreEqual(6 * 0.1f, LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.Forward), 0.0001f);
        }

        [Test]
        public void ASingleDrawingHasNoCycleAtAllRatherThanADivideByZero()
        {
            Assert.AreEqual(0f, LoopCycle.SecondsForCycle(1, 0.1f, StanceLoop.PingPong), 0.0001f);
            Assert.AreEqual(0, LoopCycle.FrameAt(9.9f, 1, 0.1f, StanceLoop.PingPong));
            Assert.AreEqual(0, LoopCycle.FrameAt(9.9f, 6, 0f, StanceLoop.PingPong));
        }

        // ---- no wrap ----------------------------------------------------------

        // THE POINT OF PING-PONG. A forward loop's last step is a jump from the
        // final drawing back to the first, and on a sheet drawn as half a
        // breath that jump is the biggest change in the whole cycle -- which is
        // exactly what the eye locks onto and reads as "it looped".
        //
        // Asserted as a property of the sequence rather than by naming frames:
        // sampled right around a full cycle, no two consecutive samples may
        // differ by more than one drawing.
        [Test]
        public void PingPongNeverJumpsMoreThanOneDrawingAtATime()
        {
            const int frames = 6;
            const float perFrame = 0.1f;
            float cycle = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong);

            int previous = LoopCycle.FrameAt(0f, frames, perFrame, StanceLoop.PingPong);
            for (int step = 1; step <= 400; step++)
            {
                int here = LoopCycle.FrameAt(cycle * step / 200f, frames, perFrame, StanceLoop.PingPong);

                Assert.LessOrEqual(System.Math.Abs(here - previous), 1,
                    $"the loop jumped from {previous} to {here} - that cut is what makes it read as a loop");
                previous = here;
            }
        }

        // Both ends are reached, or the sheet's most extreme drawings -- the
        // full inhale and the full exhale, the two the artist spent the most on
        // -- would simply never be shown.
        [Test]
        public void PingPongActuallyReachesBothEndsOfTheSheet()
        {
            const int frames = 6;
            const float perFrame = 0.1f;
            float cycle = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong);

            bool sawFirst = false;
            bool sawLast = false;
            for (int step = 0; step < 200; step++)
            {
                int here = LoopCycle.FrameAt(cycle * step / 200f, frames, perFrame, StanceLoop.PingPong);
                if (here == 0) sawFirst = true;
                if (here == frames - 1) sawLast = true;
            }

            Assert.IsTrue(sawFirst && sawLast, "a turn-around drawing is never shown");
        }

        // ---- no metronome -----------------------------------------------------

        // THE OTHER HALF. Every drawing held for the same time reads as a
        // machine; a breath is slowest at the top and bottom of itself. The
        // raised cosine spends longer at the ends by construction, and this
        // pins that it actually does rather than trusting the trigonometry.
        [Test]
        public void PingPongLingersAtTheTurnAroundsRatherThanRunningFlat()
        {
            const int frames = 6;
            const float perFrame = 0.1f;
            float cycle = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong);

            var held = new int[frames];
            const int samples = 2000;
            for (int step = 0; step < samples; step++)
            {
                held[LoopCycle.FrameAt(cycle * step / samples, frames, perFrame, StanceLoop.PingPong)]++;
            }

            // The middle drawings are passed through; the ends are dwelt on.
            Assert.Greater(held[0], held[frames / 2],
                "the first drawing is not held longer than a middle one, so the loop runs flat");
            Assert.Greater(held[frames - 1], held[frames / 2],
                "the last drawing is not held longer than a middle one, so the loop runs flat");
        }

        // A forward loop is the flat one on purpose -- it is for a sheet the
        // artist drew as a complete cycle, where the drawings carry their own
        // easing and imposing more would fight them.
        [Test]
        public void AForwardLoopWalksTheSheetInOrderAndStartsOver()
        {
            const int frames = 4;
            const float perFrame = 0.25f;

            Assert.AreEqual(0, LoopCycle.FrameAt(0.00f, frames, perFrame, StanceLoop.Forward));
            Assert.AreEqual(1, LoopCycle.FrameAt(0.30f, frames, perFrame, StanceLoop.Forward));
            Assert.AreEqual(3, LoopCycle.FrameAt(0.90f, frames, perFrame, StanceLoop.Forward));
            Assert.AreEqual(0, LoopCycle.FrameAt(1.05f, frames, perFrame, StanceLoop.Forward),
                "a forward loop starts over rather than stopping on its last drawing");
        }

        // ---- the peak hold -----------------------------------------------------

        // The hold is dead time ADDED to the cycle, not stolen from the sweep:
        // the ten sweep-steps still take their ten secondsPerFrame, and the
        // hold sits on top. So "hold the full breath half a second longer"
        // lengthens the whole breath by exactly that half second.
        [Test]
        public void ThePeakHoldIsAddedToTheCycleRatherThanSlowingTheSweep()
        {
            float sweep = LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.PingPong);
            float withHold = LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.PingPong, 0.5f);

            Assert.AreEqual(sweep + 0.5f, withHold, 0.0001f);
        }

        // A hold on a Forward loop is meaningless -- it wraps rather than
        // reverses, so there is no single peak to dwell on -- and is ignored
        // rather than quietly lengthening the cycle.
        [Test]
        public void AForwardLoopIgnoresThePeakHold()
        {
            Assert.AreEqual(
                LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.Forward),
                LoopCycle.SecondsForCycle(6, 0.1f, StanceLoop.Forward, 0.5f), 0.0001f);
        }

        // WITH NO HOLD, NOTHING MOVES. The rewrite that added the hold split the
        // single raised cosine into a rise and a fall; endHold == 0 has to leave
        // every drawing exactly where it was, or it is a silent regression to
        // every idle in the game. Sampled against the bare four-argument call.
        [Test]
        public void ZeroHoldIsIdenticalToTheOldSingleCosine()
        {
            const int frames = 6;
            const float perFrame = 0.13f;
            float cycle = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong);

            for (int step = 0; step < 300; step++)
            {
                float t = cycle * step / 300f;
                Assert.AreEqual(
                    LoopCycle.FrameAt(t, frames, perFrame, StanceLoop.PingPong),
                    LoopCycle.FrameAt(t, frames, perFrame, StanceLoop.PingPong, 0f),
                    $"the hold-aware path disagrees with the old one at {t:F3}s");
            }
        }

        // THE HOLD ACTUALLY DWELLS ON THE PEAK. With a hold long against the
        // sweep, the full-inhale drawing (the last frame) should occupy far more
        // of the cycle than it does without one -- and more than the rest frame,
        // which has no hold of its own.
        [Test]
        public void ThePeakDrawingIsHeldFarLongerWithAHoldThanWithout()
        {
            const int frames = 6;
            const float perFrame = 0.1f;
            const float hold = 1.0f;   // long against the 1.0s sweep

            float bare = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong);
            float held = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong, hold);

            int peakBare = FractionOnFrame(frames - 1, frames, perFrame, 0f, bare);
            int peakHeld = FractionOnFrame(frames - 1, frames, perFrame, hold, held);
            int restHeld = FractionOnFrame(0, frames, perFrame, hold, held);

            Assert.Greater(peakHeld, peakBare * 2,
                "the peak drawing is not dwelt on any longer with a hold than without one");
            Assert.Greater(peakHeld, restHeld,
                "the held peak is not shown longer than the unheld rest frame");
        }

        // The hold flattens the TOP, not the bottom: the loop still reaches
        // frame 0, and still never jumps more than one drawing, hold or no hold.
        [Test]
        public void AHeldPingPongStillReachesBothEndsWithoutJumping()
        {
            const int frames = 6;
            const float perFrame = 0.1f;
            const float hold = 0.6f;
            float cycle = LoopCycle.SecondsForCycle(frames, perFrame, StanceLoop.PingPong, hold);

            bool sawFirst = false, sawLast = false;
            int previous = LoopCycle.FrameAt(0f, frames, perFrame, StanceLoop.PingPong, hold);
            for (int step = 0; step <= 400; step++)
            {
                int here = LoopCycle.FrameAt(cycle * step / 200f, frames, perFrame, StanceLoop.PingPong, hold);
                Assert.LessOrEqual(System.Math.Abs(here - previous), 1,
                    $"a held loop jumped from {previous} to {here}");
                if (here == 0) sawFirst = true;
                if (here == frames - 1) sawLast = true;
                previous = here;
            }

            Assert.IsTrue(sawFirst && sawLast, "a held loop stopped reaching one of its ends");
        }

        // How many of `samples` evenly-spaced ticks across one cycle land on a
        // given drawing. A cheap proxy for "how long is this frame shown".
        private static int FractionOnFrame(int frame, int frames, float perFrame, float hold, float cycle)
        {
            const int samples = 3000;
            int count = 0;
            for (int step = 0; step < samples; step++)
            {
                if (LoopCycle.FrameAt(cycle * step / samples, frames, perFrame, StanceLoop.PingPong, hold) == frame)
                {
                    count++;
                }
            }

            return count;
        }

        // ---- the clock ---------------------------------------------------------

        // A fight can sit idle for a long time, and an index that walked off the
        // end of the array after five minutes of the player reading a tooltip
        // would be the worst possible bug: invisible in every test and certain
        // in play.
        [Test]
        public void AClockThatHasRunForHoursIsStillInsideTheSheet()
        {
            foreach (var loop in new[] { StanceLoop.PingPong, StanceLoop.Forward })
            {
                foreach (float elapsed in new[] { 0f, 3.7f, 600f, 36000f })
                {
                    int frame = LoopCycle.FrameAt(elapsed, 6, 0.14f, loop);
                    Assert.GreaterOrEqual(frame, 0, $"{loop} at {elapsed}s");
                    Assert.Less(frame, 6, $"{loop} at {elapsed}s");
                }
            }
        }
    }
}
