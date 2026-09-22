using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Pins HitStrength.Classify's table against literals -- CLAUDE.md gotcha
    // 5 -- and the boundary behaviour ("distinguish impact strength"'s three
    // owner rules): a miss never slides, a fully-absorbed hit never slides,
    // and a kill is always Heavy regardless of the fraction that produced it.
    public class HitStrengthTests
    {
        private const int MaxHealth = 1000;

        [Test]
        public void AMiss_NeverSlides()
        {
            Assert.AreEqual(HitStrength.Tier.None,
                HitStrength.Classify(amount: 500, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: true, killed: false));
        }

        [Test]
        public void FullyAbsorbed_NeverSlides()
        {
            // The whole swing landed on a shield -- zero reached health.
            Assert.AreEqual(HitStrength.Tier.None,
                HitStrength.Classify(amount: 60, absorbed: 60, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void PartiallyAbsorbed_ReadsOffWhatReachedHealth()
        {
            // 60 thrown, 55 eaten by a shield -- 5 reached health, 0.5% of a
            // 1000 max, which is Light rather than whatever a raw 60 would
            // have been.
            Assert.AreEqual(HitStrength.Tier.Light,
                HitStrength.Classify(amount: 60, absorbed: 55, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void BelowFivePercent_IsLight()
        {
            // 49/1000 = 4.9%, just under LightCeiling.
            Assert.AreEqual(HitStrength.Tier.Light,
                HitStrength.Classify(amount: 49, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void AtFivePercent_IsMedium()
        {
            // The ceiling itself belongs to the next band up -- "< 0.05" is
            // Light, so exactly 0.05 is Medium.
            Assert.AreEqual(HitStrength.Tier.Medium,
                HitStrength.Classify(amount: 50, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void JustUnderTwentyPercent_IsMedium()
        {
            Assert.AreEqual(HitStrength.Tier.Medium,
                HitStrength.Classify(amount: 199, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void AtTwentyPercent_IsHeavy()
        {
            Assert.AreEqual(HitStrength.Tier.Heavy,
                HitStrength.Classify(amount: 200, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: false, killed: false));
        }

        [Test]
        public void ALightKill_IsHeavyAnyway()
        {
            // A one-point tap that happens to be the killing blow (a target
            // already at 1 HP) still gets the heavy reaction, on top of the
            // existing defeated handling.
            Assert.AreEqual(HitStrength.Tier.Heavy,
                HitStrength.Classify(amount: 1, absorbed: 0, targetMaxHealth: MaxHealth,
                    missed: false, killed: true));
        }

        [Test]
        public void NoMaxHealthToReadAFractionAgainst_DefaultsHeavy()
        {
            // A fixture with an unset denominator -- never a real fight, but
            // a real hit should still react rather than going silently limp.
            Assert.AreEqual(HitStrength.Tier.Heavy,
                HitStrength.Classify(amount: 1, absorbed: 0, targetMaxHealth: 0,
                    missed: false, killed: false));
        }

        // ---- the distance/dwell table, pinned literally -------------------

        [Test]
        public void TheDistanceTable_IsPinned()
        {
            Assert.AreEqual(0f, HitStrength.DistanceFor(HitStrength.Tier.None));
            Assert.AreEqual(12f, HitStrength.DistanceFor(HitStrength.Tier.Light));
            Assert.AreEqual(28f, HitStrength.DistanceFor(HitStrength.Tier.Medium));
            Assert.AreEqual(45f, HitStrength.DistanceFor(HitStrength.Tier.Heavy));
        }

        [Test]
        public void TheDwellFractionTable_IsPinned()
        {
            Assert.AreEqual(0f, HitStrength.DwellFractionFor(HitStrength.Tier.None));
            Assert.AreEqual(0.35f, HitStrength.DwellFractionFor(HitStrength.Tier.Light));
            Assert.AreEqual(0.65f, HitStrength.DwellFractionFor(HitStrength.Tier.Medium));

            // Heavy stays at 1x the existing dwell -- the reeling behaviour
            // that already shipped (owner, 2026-09-19) is untouched.
            Assert.AreEqual(1f, HitStrength.DwellFractionFor(HitStrength.Tier.Heavy));
        }
    }
}
