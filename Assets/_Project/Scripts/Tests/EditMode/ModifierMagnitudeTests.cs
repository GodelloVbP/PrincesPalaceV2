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

        // ---- RiftMultiplier: the plan's own named step ------------------------

        [Test]
        public void Ordinary_IsExactlyOne()
        {
            Assert.AreEqual(1.0, ModifierMagnitude.RiftMultiplier(RiftTier.Ordinary), 1e-9);
        }

        [Test]
        public void RiftTouched_Is1Point3()
        {
            Assert.AreEqual(1.3, ModifierMagnitude.RiftMultiplier(RiftTier.RiftTouched), 1e-9);
        }

        [Test]
        public void RiftForged_Is1Point6()
        {
            Assert.AreEqual(1.6, ModifierMagnitude.RiftMultiplier(RiftTier.RiftForged), 1e-9);
        }

        [Test]
        public void Convergent_Is2Point0()
        {
            Assert.AreEqual(2.0, ModifierMagnitude.RiftMultiplier(RiftTier.Convergent), 1e-9);
        }

        [Test]
        public void AnOutOfRangeRiftTierValue_FallsBackToOrdinary()
        {
            // Defensive against a corrupted/hand-edited save -- see
            // ModifierMagnitude.RiftMultiplier's own comment.
            Assert.AreEqual(1.0, ModifierMagnitude.RiftMultiplier((RiftTier)99), 1e-9);
        }

        // ---- Scale: the combined formula ---------------------------------------

        [Test]
        public void Scale_AtOrdinaryTierZero_IsExactlyOne()
        {
            Assert.AreEqual(1.0, ModifierMagnitude.Scale(0, RiftTier.Ordinary), 1e-9);
        }

        [Test]
        public void Scale_MultipliesBothAxesTogether()
        {
            // TierMultiplier(5) x RiftMultiplier(Convergent) = 3.0517578125 x 2.0
            Assert.AreEqual(6.103515625, ModifierMagnitude.Scale(5, RiftTier.Convergent), 1e-9);
        }

        [Test]
        public void Scale_AtTierTenConvergent_IsTheTopOfTheCurve()
        {
            // 9.313225746 x 2.0 = 18.626451492
            Assert.AreEqual(18.626451492, ModifierMagnitude.Scale(10, RiftTier.Convergent), 1e-6);
        }
    }
}
