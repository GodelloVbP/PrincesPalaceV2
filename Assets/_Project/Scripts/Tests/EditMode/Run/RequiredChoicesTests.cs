using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // The rule that stops a party descending with a dead Fury meter. Literal
    // cases only: which effect types count as a root, and which characters
    // are never blocked.
    public class RequiredChoicesTests
    {
        private static ResolvedPool Pool(bool engineFed) =>
            new ResolvedPool("fixture", "Fixture", "FIX",
                PoolCapacityRule.Fixed, 100,
                0, 0, 0,
                0, PoolDecayTrigger.Damage,
                PoolStartRule.Zero, 0,
                "#FFFFFF", "#000000", "#CCCCCC",
                false, true, true, false,
                0, engineFed);

        [Test]
        public void EngineFedPoolWithNoRoot_IsMissing()
        {
            Assert.IsTrue(RequiredChoices.IsMissingEngineRoot(Pool(true), new TalentEffectType[0]));
        }

        [Test]
        public void EngineFedPoolWithOnlyNonRootTalents_IsMissing()
        {
            var unlocked = new[] { TalentEffectType.IronRetortPercent, TalentEffectType.PlantedShieldBreakShards };
            Assert.IsTrue(RequiredChoices.IsMissingEngineRoot(Pool(true), unlocked));
        }

        [TestCase(TalentEffectType.FuryEngineSentinel)]
        [TestCase(TalentEffectType.FuryEngineEinherjar)]
        [TestCase(TalentEffectType.FuryEngineJuggernaut)]
        public void AnyOfTheThreeRoots_Clears(TalentEffectType root)
        {
            var unlocked = new[] { TalentEffectType.IronRetortPercent, root };
            Assert.IsFalse(RequiredChoices.IsMissingEngineRoot(Pool(true), unlocked));
        }

        [Test]
        public void PoolThatIsNotEngineFed_IsNeverMissing()
        {
            Assert.IsFalse(RequiredChoices.IsMissingEngineRoot(Pool(false), new TalentEffectType[0]));
        }

        [Test]
        public void NoResolvablePool_IsNeverMissing()
        {
            Assert.IsFalse(RequiredChoices.IsMissingEngineRoot(null, null));
        }

        [Test]
        public void FirstMissing_NamesTheFirstBlockedSeat_AndNullWhenAllClear()
        {
            var squad = new[]
            {
                new ChoiceCandidate("sheep", false),
                new ChoiceCandidate("bear", true),
                new ChoiceCandidate("owl", true),
            };
            Assert.AreEqual("bear", RequiredChoices.FirstMissing(squad));

            var clear = new[] { new ChoiceCandidate("sheep", false), new ChoiceCandidate("bear", false) };
            Assert.IsNull(RequiredChoices.FirstMissing(clear));
            Assert.IsNull(RequiredChoices.FirstMissing(null));
        }
    }
}
