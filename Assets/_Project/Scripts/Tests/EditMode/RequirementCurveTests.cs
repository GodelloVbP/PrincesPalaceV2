using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class RequirementCurveTests
    {
        [TearDown]
        public void ResetTheKnobs()
        {
            RequirementCurve.Percent = RequirementCurve.DefaultPercent;

            // Both knobs, not just the one this file used to touch. A static
            // left flipped outlives the test that flipped it for the whole
            // process, and the gear tests below are the first thing here to
            // write the second one.
            RequirementCurve.GearRequirementsEnabled = false;
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

        // ---- the gear switch -----------------------------------------------
        //
        // GearRequirementsEnabled is off, and its header promises that
        // "flipping this back on restores the whole system untouched" -- the
        // authored requirements are all still on the items, nothing was
        // deleted. Nothing exercised the `true` branch, so that promise was an
        // untested claim about a code path that has been dark since 9f624fd.
        // These two are what make it a statement about behaviour again.

        [Test]
        public void GearRequirementsOff_DemandNothingHoweverTheyWereAuthored()
        {
            var authored = new AbilityScoreBlock(15, 12, 0, 0, 0, 0);
            var demanded = RequirementCurve.ApplyGear(authored);

            Assert.AreEqual(0, demanded.strength, "an off switch that still demands STR is not off");
            Assert.AreEqual(0, demanded.dexterity);
        }

        [Test]
        public void GearRequirementsOn_DemandExactlyWhatWasAuthored()
        {
            RequirementCurve.GearRequirementsEnabled = true;

            var authored = new AbilityScoreBlock(15, 12, 0, 0, 0, 0);
            var demanded = RequirementCurve.ApplyGear(authored);

            Assert.AreEqual(15, demanded.strength, "at 100% the requirement is the authored one, untouched");
            Assert.AreEqual(12, demanded.dexterity);
        }

        [Test]
        public void GearRequirementsOn_StillGoThroughThePercentKnob()
        {
            // The two knobs compose rather than shadowing each other: the
            // switch decides whether gear gates at all, Percent decides how
            // hard every gate in the game is. Pinned because turning gear back
            // on with Percent already moved is the state a difficulty setting
            // would actually arrive in.
            RequirementCurve.GearRequirementsEnabled = true;
            RequirementCurve.Percent = 50;

            var demanded = RequirementCurve.ApplyGear(new AbilityScoreBlock(15, 0, 0, 0, 0, 0));

            Assert.AreEqual(7, demanded.strength, "15 * 0.5 = 7.5, floored to 7 -- same curve as everything else");
        }
    }
}
