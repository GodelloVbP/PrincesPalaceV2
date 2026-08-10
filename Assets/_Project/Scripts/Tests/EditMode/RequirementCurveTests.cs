using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class RequirementCurveTests
    {
        [TearDown]
        public void ResetPercent()
        {
            RequirementCurve.Percent = RequirementCurve.DefaultPercent;
        }

        [Test]
        public void AtOneHundredPercent_RequirementsAreExactlyAsAuthored()
        {
            Assert.AreEqual(15, RequirementCurve.Apply(15));
        }

        [Test]
        public void BelowOneHundred_MakesRequirementsEasier()
        {
            RequirementCurve.Percent = 50;
            Assert.AreEqual(7, RequirementCurve.Apply(15), "15 * 0.5 = 7.5, floored to 7");
        }

        [Test]
        public void AboveOneHundred_MakesRequirementsHarder()
        {
            RequirementCurve.Percent = 150;
            Assert.AreEqual(22, RequirementCurve.Apply(15), "15 * 1.5 = 22.5, floored to 22");
        }

        [Test]
        public void ZeroRequirement_StaysZeroRegardlessOfPercent()
        {
            RequirementCurve.Percent = 500;
            Assert.AreEqual(0, RequirementCurve.Apply(0), "'No requirement' must not become one just because the knob moved");
        }

        [Test]
        public void ANegativePercent_ClampsToZero()
        {
            RequirementCurve.Percent = -50;
            Assert.AreEqual(0, RequirementCurve.Apply(15));
        }

        [Test]
        public void Apply_OnAWholeBlock_ScalesEveryScore()
        {
            RequirementCurve.Percent = 50;
            var block = new AbilityScoreBlock(15, 20, 0, 10, 8, 0);
            var scaled = RequirementCurve.Apply(block);

            Assert.AreEqual(7, scaled.strength);
            Assert.AreEqual(10, scaled.dexterity);
            Assert.AreEqual(0, scaled.constitution);
            Assert.AreEqual(5, scaled.wisdom);
            Assert.AreEqual(4, scaled.intelligence);
            Assert.AreEqual(0, scaled.charisma);
        }
    }
}
