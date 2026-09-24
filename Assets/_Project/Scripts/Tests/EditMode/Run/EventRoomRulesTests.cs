using System;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The pure halves of an event choice that RunOrchestrator.Event.cs leans
    // on: the gate that greys / refuses / hides a choice, the heal/damage
    // arithmetic, and the effects line. Every expected value is a literal
    // (docs/CODE_STANDARDS.md 8).
    public class EventRoomRulesTests
    {
        private static ResolvedEventChoice Choice(EventRequirement[] requires, EventEffect[] effects,
            bool hiddenUntilMet = false) =>
            new ResolvedEventChoice("choice", requires, hiddenUntilMet, effects,
                new[] { new ResolvedEventOutcome(Array.Empty<EventRequirement>(), Array.Empty<EventEffect>(), "", "", true) });

        // ---- the gate ---------------------------------------------------------------

        [Test]
        public void AChoiceWithNoRequirementsIsVisibleAndEnabled()
        {
            var state = EventChoiceGate.Evaluate(
                Choice(Array.Empty<EventRequirement>(), Array.Empty<EventEffect>()), new FakeEventContext());

            Assert.IsTrue(state.Visible);
            Assert.IsTrue(state.Enabled);
            Assert.AreEqual("", state.LockReason);
        }

        [Test]
        public void ALockedChoiceShowsGreyedWithTheFailingRequirementsCaption()
        {
            var context = new FakeEventContext();
            context.Squad.Add("bear");
            context.Levels["bear"] = 14;

            var state = EventChoiceGate.Evaluate(
                Choice(new[] { EventRequirement.MemberLevel(15) }, Array.Empty<EventEffect>()), context);

            Assert.IsTrue(state.Visible);
            Assert.IsFalse(state.Enabled);
            Assert.AreEqual("Requires a level 15 party member", state.LockReason);
        }

        [Test]
        public void HiddenUntilMetHidesOnlyWhileLocked()
        {
            var context = new FakeEventContext();
            context.Squad.Add("owl");
            context.Abilities[("owl", AbilityScore.Charisma)] = 19;
            var choice = Choice(new[] { EventRequirement.AbilityAtLeast(AbilityScore.Charisma, 20) },
                Array.Empty<EventEffect>(), hiddenUntilMet: true);

            var locked = EventChoiceGate.Evaluate(choice, context);
            Assert.IsFalse(locked.Visible);
            Assert.IsFalse(locked.Enabled);

            context.Abilities[("owl", AbilityScore.Charisma)] = 20;
            var open = EventChoiceGate.Evaluate(choice, context);
            Assert.IsTrue(open.Visible);
            Assert.IsTrue(open.Enabled);
        }

        // Contract 6: the spend gates itself. 4 gold cannot toss a 5-gold coin.
        [TestCase(4, false, "Requires 5 gold")]
        [TestCase(5, true, "")]
        public void AGoldSpendImpliesItsOwnGate(int gold, bool expectedEnabled, string expectedReason)
        {
            var context = new FakeEventContext { GoldValue = gold };

            var state = EventChoiceGate.Evaluate(
                Choice(Array.Empty<EventRequirement>(), new[] { EventEffect.Gold(-5) }), context);

            Assert.AreEqual(expectedEnabled, state.Enabled);
            Assert.AreEqual(expectedReason, state.LockReason);
        }

        // The author's caption wins when both fail: it is the one they wrote.
        [Test]
        public void AnAuthoredRequirementsCaptionComesBeforeTheImpliedGoldOne()
        {
            var context = new FakeEventContext { GoldValue = 0 };

            var state = EventChoiceGate.Evaluate(
                Choice(new[] { EventRequirement.InParty("sheep", "Shawn") }, new[] { EventEffect.Gold(-5) }),
                context);

            Assert.AreEqual("Requires Shawn", state.LockReason);
        }

        // ---- heal and damage ----------------------------------------------------------

        [TestCase(50, 120, 30, 86)]   // 36 of 120
        [TestCase(110, 120, 30, 120)] // clamped at the maximum
        [TestCase(0, 50, 25, 13)]     // 12.5 rounds up; a downed character is healed too
        [TestCase(10, 10, 1, 10)]
        public void HealedAddsAPercentOfMaximumRoundedUp(int current, int max, int percent, int expected)
        {
            Assert.AreEqual(expected, EventHealth.Healed(current, max, percent));
        }

        [TestCase(100, 120, 30, 64)] // 36 of 120
        [TestCase(20, 120, 30, 1)]   // would die: floored at 1
        [TestCase(1, 120, 100, 1)]
        [TestCase(0, 120, 30, 0)]    // downed stays downed -- damage is never a revive
        public void DamagedNeverKillsAndNeverRevives(int current, int max, int percent, int expected)
        {
            Assert.AreEqual(expected, EventHealth.Damaged(current, max, percent));
        }

        // ---- the effects line ---------------------------------------------------------

        [Test]
        public void TheEffectsLineNamesEachAppliedEffectInOrderAndHidesCounters()
        {
            var line = EventEffectSummary.Describe(new[]
            {
                EventEffect.Gold(25),
                EventEffect.Counter("wishing_well_tosses", 1),
                EventEffect.HealPercent(30),
                EventEffect.Gold(-5),
                EventEffect.DamagePercent(10),
                EventEffect.Exp(50),
                EventEffect.ItemGrant("health_potion", 2),
            }, id => id == "health_potion" ? "Health Potion" : null);

            Assert.AreEqual(
                "+25 gold  ·  Party healed 30%  ·  -5 gold  ·  Party hurt 10%  ·  +50 XP  ·  +2 Health Potion",
                line);
        }

        [Test]
        public void AnItemTheLookupCannotNameFallsBackToItsId()
        {
            Assert.AreEqual("+1 mystery_box",
                EventEffectSummary.Describe(new[] { EventEffect.ItemGrant("mystery_box") }, _ => null));
        }

        [Test]
        public void NoEffectsIsAnEmptyLine()
        {
            Assert.AreEqual("", EventEffectSummary.Describe(Array.Empty<EventEffect>(), null));
            Assert.AreEqual("", EventEffectSummary.Describe(new[] { EventEffect.Counter("c", 1) }, null));
        }
    }
}
