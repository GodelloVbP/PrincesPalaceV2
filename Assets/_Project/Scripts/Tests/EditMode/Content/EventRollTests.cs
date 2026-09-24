using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    public class EventRollTests
    {
        private static ResolvedEventDefinition Event(string id, int[] floors = null, EventRequirement[] requires = null) =>
            new ResolvedEventDefinition(id, 0, floors ?? System.Array.Empty<int>(),
                requires ?? System.Array.Empty<EventRequirement>(), new[] { new ResolvedEventPage("p", "", "", "", System.Array.Empty<ResolvedEventChoice>()) });

        [Test]
        public void Pick_ReturnsEmpty_WhenThereAreNoCandidates()
        {
            string picked = EventRoll.Pick(new List<ResolvedEventDefinition>(), 3, new List<string>(),
                new FakeEventContext(), new SeededRandom(1));

            Assert.AreEqual("", picked);
        }

        [Test]
        public void Pick_ExcludesAnEventWhoseFloorsListDoesNotContainTheCurrentFloor()
        {
            var candidates = new List<ResolvedEventDefinition> { Event("a", floors: new[] { 5 }) };

            string picked = EventRoll.Pick(candidates, 3, new List<string>(), new FakeEventContext(), new SeededRandom(1));

            Assert.AreEqual("", picked, "floor 3 must not roll an event authored only for floor 5");
        }

        [Test]
        public void Pick_AnEmptyFloorsListIsEligibleOnEveryFloor()
        {
            var candidates = new List<ResolvedEventDefinition> { Event("a") };

            string picked = EventRoll.Pick(candidates, 3, new List<string>(), new FakeEventContext(), new SeededRandom(1));

            Assert.AreEqual("a", picked);
        }

        [Test]
        public void Pick_ExcludesAnEventAlreadySeenThisRun()
        {
            var candidates = new List<ResolvedEventDefinition> { Event("a") };

            string picked = EventRoll.Pick(candidates, 3, new List<string> { "a" }, new FakeEventContext(), new SeededRandom(1));

            Assert.AreEqual("", picked);
        }

        [Test]
        public void Pick_ExcludesAnEventWhoseEventLevelRequirementFails()
        {
            var candidates = new List<ResolvedEventDefinition>
            {
                Event("a", requires: new[] { EventRequirement.Gold(999) }),
            };

            string picked = EventRoll.Pick(candidates, 3, new List<string>(), new FakeEventContext { GoldValue = 0 }, new SeededRandom(1));

            Assert.AreEqual("", picked);
        }

        [Test]
        public void Pick_IsDeterministicForAFixedRngState()
        {
            var candidates = new List<ResolvedEventDefinition> { Event("a"), Event("b"), Event("c") };

            // Pinned literal: SplitMix64 seeded at 42, first draw over a
            // 3-wide range. A change to this result means EventRoll's pick
            // (or SeededRandom itself) changed, which is exactly what this
            // test exists to catch.
            string picked = EventRoll.Pick(candidates, 1, new List<string>(), new FakeEventContext(), new SeededRandom(42));

            Assert.AreEqual("b", picked);
        }
    }
}
