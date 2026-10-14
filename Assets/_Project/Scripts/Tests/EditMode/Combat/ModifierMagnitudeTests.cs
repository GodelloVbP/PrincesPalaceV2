using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Pins the item-modifier plan's Magnitude formula:
    //     base x TierMultiplier(itemTier) x RiftMultiplier(riftTier)
    // Every value here is a literal, never recomputed from GearScaling.TierGrowth
    // itself (CLAUDE.md gotcha 5) -- these are the exact numbers a designer will
    // see and retune, so a change to either curve should have to touch this file.
    public class ModifierMagnitudeTests
    {
        // ---- TierMultiplier: reuses GearScaling.TierGrowth (1.25) ------------

        [Test]
        public void TierZero_IsExactlyOne()
        {
            Assert.AreEqual(1.0, ModifierMagnitude.TierMultiplier(0), 1e-9);
        }

        [Test]
        public void NegativeTier_IsTreatedAsTierZero()
        {
            Assert.AreEqual(1.0, ModifierMagnitude.TierMultiplier(-3), 1e-9);
        }

        [Test]
        public void TierOne_IsTheGrowthRateItself()
        {
            Assert.AreEqual(1.25, ModifierMagnitude.TierMultiplier(1), 1e-9);
        }

        [Test]
        public void TierFive_MatchesTheGeometricCurve()
        {
            // 1.25^5 = 3.0517578125
            Assert.AreEqual(3.0517578125, ModifierMagnitude.TierMultiplier(5), 1e-9);
        }

        [Test]
        public void TierTen_MatchesTheGeometricCurve()
        {
            // 1.25^10 = 9.31322574615478515625
            Assert.AreEqual(9.313225746, ModifierMagnitude.TierMultiplier(10), 1e-6);
        }

        // ---- Scale: the tier curve ---------------------------------------------

        [Test]
        public void Scale_AtTierZero_IsExactlyOne()
        {
            Assert.AreEqual(1.0, ModifierMagnitude.Scale(0), 1e-9);
        }

        [Test]
        public void Scale_IsTheTierMultiplier()
        {
            // 1.25^5 = 3.0517578125
            Assert.AreEqual(3.0517578125, ModifierMagnitude.Scale(5), 1e-9);
        }

        [Test]
        public void Scale_AtTierTen_IsTheTopOfTheArtLadder()
        {
            Assert.AreEqual(9.313225746, ModifierMagnitude.Scale(10), 1e-6);
        }

        [Test]
        public void Scale_KeepsClimbingToTheCopyCap()
        {
            // 1.25^12 = 14.5519152283...
            Assert.AreEqual(14.551915228, ModifierMagnitude.Scale(12), 1e-6);
        }
    }
}
