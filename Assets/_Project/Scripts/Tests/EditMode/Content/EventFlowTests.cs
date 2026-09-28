using System;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Tests
{
    public class EventFlowTests
    {
        // The demo event's shape: a "toss a coin" choice whose 10th outcome
        // (counter min 10 max 10) goes to a special page, and whose last,
        // unconditional outcome loops back to the same page. Built by hand
        // here rather than read from events.json, so the assertions are
        // pinned against literal values (CLAUDE.md gotcha 5), not
        // against whatever the content file happens to say today.
        private static ResolvedEventPage WellPage()
        {
            var tossChoice = new ResolvedEventChoice(
                "Toss a coin",
                Array.Empty<EventRequirement>(),
                false,
                new[] { EventEffect.Gold(-5), EventEffect.Counter("wishing_well_tosses", 1) },
                new[]
                {
                    new ResolvedEventOutcome(
                        new[] { EventRequirement.Counter("wishing_well_tosses", 10, 10) },
                        new[] { EventEffect.Exp(50) },
                        "The well glimmers gold.",
                        "wish_granted",
                        false),
                    new ResolvedEventOutcome(
                        Array.Empty<EventRequirement>(),
                        Array.Empty<EventEffect>(),
                        "The coin sinks without a sound.",
                        "well",
                        false),
                });

            return new ResolvedEventPage("well", "", "A Wishing Well", "...", new[] { tossChoice });
        }

        [TestCase(9, "The coin sinks without a sound.", "well", false)]
        [TestCase(10, "The well glimmers gold.", "wish_granted", false)]
        [TestCase(11, "The coin sinks without a sound.", "well", false)]
        public void Resolve_TheTenthTossTakesTheSpecialOutcome_EveryOtherLoopsBack(
            int counterValueAfterThisToss, string expectedResult, string expectedNextPage, bool expectedIsLeave)
        {
            var context = new FakeEventContext();

            // EventFlow.Resolve does not apply the choice's own effects
            // itself (phase 2's RunOrchestrator.Event.cs does, before asking
            // for the resolution) -- so the context here already carries the
            // counter value the outcome check evaluates against, exactly as
            // phase 2 would leave it after ticking the counter.
            context.Counters["wishing_well_tosses"] = counterValueAfterThisToss;

            var resolution = EventFlow.Resolve(WellPage(), 0, context);

            Assert.AreEqual(expectedResult, resolution.Result);
            Assert.AreEqual(expectedNextPage, resolution.NextPageId);
            Assert.AreEqual(expectedIsLeave, resolution.IsLeave);
        }

        [Test]
        public void Resolve_AlwaysIncludesTheChoicesOwnEffectsBeforeTheOutcomes()
        {
            var context = new FakeEventContext();
            context.Counters["wishing_well_tosses"] = 1;

            var resolution = EventFlow.Resolve(WellPage(), 0, context);

            Assert.AreEqual(2, resolution.Effects.Count, "the choice's gold spend and counter tick must both be present");
            Assert.AreEqual(EventEffectKind.Gold, resolution.Effects[0].Kind);
            Assert.AreEqual(-5, resolution.Effects[0].Amount);
            Assert.AreEqual(EventEffectKind.Counter, resolution.Effects[1].Kind);
        }

        [Test]
        public void Resolve_OnTheTenthToss_AlsoIncludesTheOutcomesOwnEffects()
        {
            var context = new FakeEventContext();
            context.Counters["wishing_well_tosses"] = 10;

            var resolution = EventFlow.Resolve(WellPage(), 0, context);

            Assert.AreEqual(3, resolution.Effects.Count, "choice effects (2) plus the outcome's own exp effect (1)");
            Assert.AreEqual(EventEffectKind.Exp, resolution.Effects[2].Kind);
            Assert.AreEqual(50, resolution.Effects[2].Amount);
        }

        [Test]
        public void Resolve_ALeaveOutcomeReportsIsLeaveTrue()
        {
            var leaveChoice = new ResolvedEventChoice(
                "Leave", Array.Empty<EventRequirement>(), false, Array.Empty<EventEffect>(),
                new[]
                {
                    new ResolvedEventOutcome(Array.Empty<EventRequirement>(), Array.Empty<EventEffect>(), "", "", true),
                });
            var page = new ResolvedEventPage("well", "", "", "", new[] { leaveChoice });

            var resolution = EventFlow.Resolve(page, 0, new FakeEventContext());

            Assert.IsTrue(resolution.IsLeave);
        }
    }
}
