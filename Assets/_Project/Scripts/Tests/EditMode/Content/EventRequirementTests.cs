using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class EventRequirementTests
    {
        // ---- InParty ------------------------------------------------------

        [Test]
        public void InParty_PassesWhenCharacterIsInSquad_AndReportsTheirName()
        {
            var context = new FakeEventContext { Squad = { "sheep" } };
            var requirement = EventRequirement.InParty("sheep", "Shawn");

            var result = requirement.Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires Shawn", result.Reason);
        }

        [Test]
        public void InParty_FailsWhenCharacterIsNotInSquad()
        {
            var context = new FakeEventContext { Squad = { "owl" } };
            var requirement = EventRequirement.InParty("sheep", "Shawn");

            var result = requirement.Evaluate(context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("Requires Shawn", result.Reason);
        }

        // ---- MemberLevel ----------------------------------------------------

        [Test]
        public void MemberLevel_AnyMember_PassesWhenOneSquadMemberMeetsTheLevel()
        {
            var context = new FakeEventContext { Squad = { "sheep", "bear" } };
            context.Levels["sheep"] = 10;
            context.Levels["bear"] = 15;
            var requirement = EventRequirement.MemberLevel(15);

            var result = requirement.Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires a level 15 party member", result.Reason);
        }

        [Test]
        public void MemberLevel_AnyMember_FailsWhenNoSquadMemberMeetsTheLevel()
        {
            var context = new FakeEventContext { Squad = { "sheep", "bear" } };
            context.Levels["sheep"] = 10;
            context.Levels["bear"] = 14;
            var requirement = EventRequirement.MemberLevel(15);

            var result = requirement.Evaluate(context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("Requires a level 15 party member", result.Reason);
        }

        [Test]
        public void MemberLevel_NamedCharacter_IgnoresEveryoneElsesLevel()
        {
            var context = new FakeEventContext { Squad = { "sheep", "bear" } };
            context.Levels["sheep"] = 5;
            context.Levels["bear"] = 30;
            var requirement = EventRequirement.MemberLevel(15, "sheep", "Shawn");

            var result = requirement.Evaluate(context);

            Assert.IsFalse(result.Passed, "bear's level 30 must not satisfy a requirement named at sheep");
            Assert.AreEqual("Requires Shawn at level 15", result.Reason);
        }

        // ---- Ability --------------------------------------------------------

        [Test]
        public void Ability_AnyMember_PassesAtExactlyTheThreshold()
        {
            var context = new FakeEventContext { Squad = { "owl" } };
            context.Abilities[("owl", AbilityScore.Charisma)] = 20;
            var requirement = EventRequirement.AbilityAtLeast(AbilityScore.Charisma, 20);

            var result = requirement.Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires 20 CHA", result.Reason);
        }

        [Test]
        public void Ability_AnyMember_FailsOneBelowTheThreshold()
        {
            var context = new FakeEventContext { Squad = { "owl" } };
            context.Abilities[("owl", AbilityScore.Charisma)] = 19;
            var requirement = EventRequirement.AbilityAtLeast(AbilityScore.Charisma, 20);

            var result = requirement.Evaluate(context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("Requires 20 CHA", result.Reason);
        }

        // ---- Counter ----------------------------------------------------------

        // Contract 8: min 10 max 10 fires exactly once, on the 10th pick.
        [TestCase(9, false)]
        [TestCase(10, true)]
        [TestCase(11, false)]
        public void Counter_MinEqualsMax_FiresOnlyOnTheExactValue(int counterValue, bool expectedPassed)
        {
            var context = new FakeEventContext();
            context.Counters["wishing_well_tosses"] = counterValue;
            var requirement = EventRequirement.Counter("wishing_well_tosses", 10, 10);

            var result = requirement.Evaluate(context);

            Assert.AreEqual(expectedPassed, result.Passed);
        }

        [TestCase(9, false)]
        [TestCase(10, true)]
        [TestCase(11, true)]
        public void Counter_MinOnly_FiresFromTheThresholdOnward(int counterValue, bool expectedPassed)
        {
            var context = new FakeEventContext();
            context.Counters["wishing_well_tosses"] = counterValue;
            var requirement = EventRequirement.Counter("wishing_well_tosses", 10, null);

            var result = requirement.Evaluate(context);

            Assert.AreEqual(expectedPassed, result.Passed);
        }

        // ---- Gold ---------------------------------------------------------------

        [Test]
        public void Gold_PassesWhenRunHasEnough()
        {
            var context = new FakeEventContext { GoldValue = 5 };
            var requirement = EventRequirement.Gold(5);

            var result = requirement.Evaluate(context);

            Assert.IsTrue(result.Passed);
            Assert.AreEqual("Requires 5 gold", result.Reason);
        }

        [Test]
        public void Gold_FailsWhenRunIsOneShort()
        {
            var context = new FakeEventContext { GoldValue = 4 };
            var requirement = EventRequirement.Gold(5);

            var result = requirement.Evaluate(context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual("Requires 5 gold", result.Reason);
        }

        // ---- Implied gold gate (contract 6) --------------------------------------

        [Test]
        public void ImpliedGoldRequirement_SumsEveryGoldSpendInTheChoicesEffects()
        {
            var effects = new[]
            {
                EventEffect.Gold(-5),
                EventEffect.Gold(-3),
                EventEffect.HealPercent(10),
            };

            var implied = EventRequirement.ImpliedGoldRequirement(effects);

            Assert.IsNotNull(implied);
            Assert.AreEqual(EventRequirementKind.Gold, implied.Kind);
            Assert.AreEqual(8, implied.Min);
        }

        [Test]
        public void ImpliedGoldRequirement_IsNullWhenNoEffectSpendsGold()
        {
            var effects = new[] { EventEffect.Gold(10), EventEffect.HealPercent(10) };

            var implied = EventRequirement.ImpliedGoldRequirement(effects);

            Assert.IsNull(implied, "a gold GAIN must not imply a gold requirement");
        }
    }
}
