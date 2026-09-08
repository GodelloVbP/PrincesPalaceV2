using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;

namespace PrincesPalace.Domain.Tests
{
    // EACH CUE DELIVERED EXACTLY ONCE, whatever the clock did between two
    // ticks.
    //
    // This is the behaviour a coroutine per layer cannot state. A long frame, a
    // scene load or a test running the fight at 60x advances the clock past
    // several cues at once, and four coroutines each lose that race
    // independently -- which is AUDIT #61: three runs on an unchanged tree, a
    // different test failing each time. A function of the half-open window
    // (previous, now] has no race to lose.
    public class SpellScheduleTests
    {
        private static readonly List<SpellEvent> Buffer = new List<SpellEvent>();

        private static SpellSchedule Of(params float[] seconds) =>
            new SpellSchedule(seconds.Select((s, i) => new SpellEvent(SpellEventKind.LayerStart, i, s)));

        private static List<SpellEvent> Crossed(SpellSchedule schedule, float from, float to)
        {
            schedule.Crossed(from, to, Buffer);
            return new List<SpellEvent>(Buffer);
        }

        [Test]
        public void ACueInsideTheWindowIsDeliveredOnce()
        {
            var schedule = Of(0.25f);

            CollectionAssert.IsEmpty(Crossed(schedule, 0f, 0.2f));
            Assert.AreEqual(1, Crossed(schedule, 0.2f, 0.3f).Count);
            CollectionAssert.IsEmpty(Crossed(schedule, 0.3f, 0.4f));
        }

        // A TICK SPANNING THREE CUES DELIVERS THREE, IN ORDER. The case a
        // coroutine answers with "whichever ones resumed in time".
        [Test]
        public void AStepSpanningThreeCuesDeliversEachOnceInOrder()
        {
            var schedule = Of(0.10f, 0.25f, 0.35f);

            var crossed = Crossed(schedule, 0f, 0.5f);

            Assert.AreEqual(3, crossed.Count);
            Assert.AreEqual(0.10f, crossed[0].Seconds, 1e-6f);
            Assert.AreEqual(0.25f, crossed[1].Seconds, 1e-6f);
            Assert.AreEqual(0.35f, crossed[2].Seconds, 1e-6f);
        }

        // HALF-OPEN AT BOTH ENDS. A cue at exactly the boundary belongs to the
        // window that ends on it and never to the one that starts there --
        // which is the difference between "exactly once" and "nearly always
        // once".
        [Test]
        public void ACueOnTheWindowBoundaryIsDeliveredOnceAndNotTwice()
        {
            var schedule = Of(0.25f);

            Assert.AreEqual(1, Crossed(schedule, 0.20f, 0.25f).Count, "the window that ENDS on it");
            CollectionAssert.IsEmpty(Crossed(schedule, 0.25f, 0.30f), "the window that STARTS on it");
        }

        // A ZERO-DELAY CUE IS LEGAL AND COMMON -- every melee beat's is -- so a
        // player's first window has to be able to contain it. That is what the
        // BeforeAnything sentinel is for, and it is stated on the schedule
        // rather than typed at a call site.
        [Test]
        public void AZeroDelayCueFiresOnTheFirstTick()
        {
            var schedule = Of(0f);

            Assert.AreEqual(1, Crossed(schedule, SpellSchedule.BeforeAnything, 0f).Count);
            CollectionAssert.IsEmpty(Crossed(schedule, 0f, 0.1f));
        }

        // TIES COME OUT IN THE ORDER THEY WERE BUILT, which is authored order,
        // which is draw order. List.Sort is not stable, so this is a property
        // of the comparer rather than an accident -- and the ties are the
        // interesting case: every layer of a Cinderfault opens at release.
        [Test]
        public void CuesAtOneInstantComeOutInTheOrderTheyWereDeclared()
        {
            var schedule = new SpellSchedule(new[]
            {
                new SpellEvent(SpellEventKind.LayerStart, 7, 0f),
                new SpellEvent(SpellEventKind.LayerStart, 3, 0f),
                new SpellEvent(SpellEventKind.LayerStart, 5, 0f),
            });

            var crossed = Crossed(schedule, SpellSchedule.BeforeAnything, 0f);

            CollectionAssert.AreEqual(new[] { 7, 3, 5 }, crossed.Select(e => e.Instance).ToList());
        }

        // TWO CASTS DO NOT SHARE A CURSOR. Each schedule is asked about its own
        // window, so a second cast opening mid-flight of the first cannot skip
        // or repeat a cue of either.
        [Test]
        public void TwoConcurrentSchedulesDoNotInterfere()
        {
            var first = Of(0.10f, 0.40f);
            var second = Of(0.30f);

            CollectionAssert.IsEmpty(Crossed(second, SpellSchedule.BeforeAnything, 0.20f),
                "the second cast has not opened its own cue yet");
            Assert.AreEqual(1, Crossed(first, SpellSchedule.BeforeAnything, 0.20f).Count);

            Assert.AreEqual(1, Crossed(second, 0.20f, 0.35f).Count);
            CollectionAssert.IsEmpty(Crossed(first, 0.20f, 0.35f).Where(e => e.Seconds < 0.4f).ToList());
        }

        [Test]
        public void AWindowThatDoesNotAdvanceDeliversNothing()
        {
            var schedule = Of(0f, 0.1f);

            CollectionAssert.IsEmpty(Crossed(schedule, 0.5f, 0.5f));
            CollectionAssert.IsEmpty(Crossed(schedule, 0.5f, 0.2f),
                "a clock that went backwards -- a scene reload, a held override released -- delivers " +
                "nothing rather than re-firing everything behind it");
        }

        // The buffer form exists so the tick path allocates nothing. If it ever
        // stopped clearing, a cast would accumulate its whole history every
        // frame and every cue would be delivered again on the next one.
        [Test]
        public void TheBufferIsClearedBeforeEachWindowIsWritten()
        {
            var schedule = Of(0.1f, 0.2f);
            var buffer = new List<SpellEvent> { new SpellEvent(SpellEventKind.HitCue, -1, 99f) };

            schedule.Crossed(0f, 0.15f, buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(0.1f, buffer[0].Seconds, 1e-6f);
        }
    }
}
