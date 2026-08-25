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
