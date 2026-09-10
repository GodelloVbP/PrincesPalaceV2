using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // Every fixture here opts INTO absorption (absorbsDamage: true), which
    // is no longer the default and is no longer how Wool is authored — the
    // talent rework made wool a spend-only resource (handoff §2). These
    // tests are kept, and kept passing, because the soak rules themselves
    // are unchanged, tuned, and the shape any future armour-flavoured
    // signature resource will use. WoolDoesNotSoakDamageByDefault below is
    // the test that pins the new default.
    public class SignatureResourceTests
    {
        private static ResourcePool Wool(int max = 16, int perTurn = 2, int onAttack = 3, int onDamage = 1)
        {
            return new ResourcePool("wool", "Wool", max, perTurn, onAttack, onDamage, absorbsDamage: true);
        }

        // The arc the whole character is built on: empty at the start, so
        // turn one is genuinely his weakest.
        [Test]
        public void StartsEmpty()
        {
            Assert.AreEqual(0, Wool().Current);
        }

        [Test]
        public void Gain_ReportsWhatWasActuallyGained_NotWhatWasAsked()
        {
            var wool = Wool(max: 5);

            Assert.AreEqual(4, wool.Gain(4));
            Assert.AreEqual(1, wool.Gain(3), "Only one point of headroom was left");
            Assert.AreEqual(5, wool.Current);
            Assert.AreEqual(0, wool.Gain(3), "Already full");
        }

        [Test]
        public void Gain_IgnoresNonPositiveAmounts()
        {
            var wool = Wool();
            Assert.AreEqual(0, wool.Gain(0));
            Assert.AreEqual(0, wool.Gain(-5));
            Assert.AreEqual(0, wool.Current);
        }

        [Test]
        public void IsFull_OnlyAtCapacity()
        {
            var wool = Wool(max: 3);
            Assert.IsFalse(wool.IsFull);
            wool.Gain(2);
            Assert.IsFalse(wool.IsFull);
            wool.Gain(1);
            Assert.IsTrue(wool.IsFull);
        }

        // All or nothing. A partial spend would fire an ability at a
        // fraction of its cost for a fraction of its effect, which is a
        // balance hole rather than graceful degradation.
        [Test]
        public void TrySpend_IsAllOrNothing()
        {
            var wool = Wool();
            wool.Gain(5);

            Assert.IsFalse(wool.TrySpend(6));
            Assert.AreEqual(5, wool.Current, "A refused spend must not have taken anything");

            Assert.IsTrue(wool.TrySpend(5));
            Assert.AreEqual(0, wool.Current);
        }

        [Test]
        public void CanSpend_MatchesWhatTrySpendWillDo()
        {
            var wool = Wool();
            wool.Gain(4);

            Assert.IsTrue(wool.CanSpend(4));
            Assert.IsFalse(wool.CanSpend(5));
            Assert.IsFalse(wool.CanSpend(-1), "A negative cost is not a spend anyone should be allowed to make");
        }

        // The armour half. Absorb takes what it can and reports it, rather
        // than refusing like TrySpend — a partial soak is exactly right for
        // damage, where the remainder simply carries on to health.
        [Test]
        public void Absorb_TakesWhatItCanAndReportsIt()
        {
            var wool = Wool();
            wool.Gain(4);

            Assert.AreEqual(3, wool.Absorb(3), "Enough to cover it");
            Assert.AreEqual(1, wool.Current);
            Assert.AreEqual(1, wool.Absorb(9), "Only one left, so only one is soaked");
            Assert.AreEqual(0, wool.Current);
            Assert.AreEqual(0, wool.Absorb(9), "Nothing left to give");
        }
    }

    // Absorption through the real damage funnel, which is where every
    // damage source in the game already goes.
    public class SignatureAbsorptionTests
    {
        private static CombatantState Fighter(int maxHealth = 30)
        {
            return new CombatantState("Shawn", true, maxHealth, 10, 5, 8);
        }

        [Test]
        public void ApplyDamage_WithNoSignature_BehavesExactlyAsBefore()
        {
            var plain = Fighter();

            int absorbed = CombatMath.ApplyDamage(plain, 7);

            Assert.AreEqual(0, absorbed);
            Assert.AreEqual(23, plain.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_SpendsTheResourceBeforeHealth()
        {
            var shawn = Fighter();
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbsDamage: true);
            shawn.SignaturePool.Gain(5);

            int absorbed = CombatMath.ApplyDamage(shawn, 3);

            Assert.AreEqual(3, absorbed);
            Assert.AreEqual(30, shawn.CurrentHealth, "Health should be untouched while the fleece holds");
            Assert.AreEqual(2, shawn.SignaturePool.Current);
        }

        [Test]
        public void ApplyDamage_OverflowsIntoHealthOnceTheResourceIsGone()
        {
            var shawn = Fighter();
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbsDamage: true);
            shawn.SignaturePool.Gain(2);

            int absorbed = CombatMath.ApplyDamage(shawn, 9);

            Assert.AreEqual(2, absorbed);
            Assert.AreEqual(23, shawn.CurrentHealth, "The remaining 7 should land on health");
            Assert.AreEqual(0, shawn.SignaturePool.Current);
        }

        // Absorption must never keep someone standing who should have died,
        // nor kill someone it covered.
        [Test]
        public void ApplyDamage_CanStillBeLethalOnceTheResourceIsExhausted()
        {
            var shawn = Fighter(maxHealth: 10);
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbsDamage: true);
            shawn.SignaturePool.Gain(4);

            CombatMath.ApplyDamage(shawn, 100);

            Assert.IsFalse(shawn.IsAlive);
            Assert.AreEqual(0, shawn.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_OfZero_TouchesNothing()
        {
            var shawn = Fighter();
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbsDamage: true);
            shawn.SignaturePool.Gain(5);

            Assert.AreEqual(0, CombatMath.ApplyDamage(shawn, 0));
            Assert.AreEqual(5, shawn.SignaturePool.Current);
            Assert.AreEqual(30, shawn.CurrentHealth);
        }

        // The bridge between the two scales the resource straddles. It is
        // counted in small whole numbers because that is what its skills
        // spend — Shear costs three — while damage lives on the x10
        // health scale. One point therefore soaks ten damage, and without
        // that a 16-point fleece would be worth less than two hits.
        [Test]
        public void AbsorbPerPoint_LetsOnePointSoakAWholeScaledHit()
        {
            var wool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbPerPoint: 10, absorbsDamage: true);
            wool.Gain(4);

            Assert.AreEqual(30, wool.Absorb(30), "Three points' worth");
            Assert.AreEqual(1, wool.Current);
        }

        [Test]
        public void AbsorbPerPoint_CoversOnlyWhatIsActuallyHeld()
        {
            var wool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbPerPoint: 10, absorbsDamage: true);
            wool.Gain(2);

            Assert.AreEqual(20, wool.Absorb(500), "Two points is twenty damage and not a point more");
            Assert.AreEqual(0, wool.Current);
        }

        // Rounds UP against the holder. Nothing reaches this today (the
        // smallest hit in the game is exactly one point's worth), but a graze
        // must cost SOMETHING or a sufficiently small damage source would
        // make the pool infinite.
        [Test]
        public void APartialPointOfAbsorption_StillCostsAWholePoint()
        {
            var wool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbPerPoint: 10, absorbsDamage: true);
            wool.Gain(3);

            Assert.AreEqual(1, wool.Absorb(1));
            Assert.AreEqual(2, wool.Current, "A graze still scuffs a whole point of fleece");
        }

        // The default is the un-scaled behaviour, so a resource authored
        // without thinking about scale behaves like plain temporary HP
        // rather than silently becoming ten times stronger.
        [Test]
        public void WithoutAnAuthoredScale_OnePointSoaksExactlyOneDamage()
        {
            var plain = new ResourcePool("x", "X", 16, 2, 3, 1, absorbsDamage: true);
            plain.Gain(5);

            Assert.AreEqual(5, plain.Absorb(40));
            Assert.AreEqual(0, plain.Current);
        }

        // ApplyDamage is the funnel every source goes through, so the scale
        // has to survive the trip — not just work when Absorb is called
        // directly.
        [Test]
        public void ApplyDamage_ReportsScaledAbsorptionAndSpendsHealthForTheRest()
        {
            var shawn = Fighter(maxHealth: 300);
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1, absorbPerPoint: 10, absorbsDamage: true);
            shawn.SignaturePool.Gain(5);

            Assert.AreEqual(50, CombatMath.ApplyDamage(shawn, 80), "Five points soaked fifty of the eighty");
            Assert.AreEqual(0, shawn.SignaturePool.Current);
            Assert.AreEqual(270, shawn.CurrentHealth, "The remaining thirty reached health");
        }

        // The rework's actual behaviour, and the reason every fixture above
        // has to ask for absorption explicitly. Wool stopped being a damage
        // sponge and became the thing the talent tree spends; a resource that
        // is both armour and ammunition asks "bank or spend?" twice and
        // answers it differently each time.
        [Test]
        public void WithoutOptingIn_TheResourceSoaksNothing()
        {
            var shawn = Fighter();
            shawn.SignaturePool = new ResourcePool("wool", "Wool", 10, 1, 0, 0);
            shawn.SignaturePool.Gain(9);

            Assert.AreEqual(0, CombatMath.ApplyDamage(shawn, 12));
            Assert.AreEqual(18, shawn.CurrentHealth, "Every point of it should have reached health");
            Assert.AreEqual(9, shawn.SignaturePool.Current, "And the fleece should be untouched, ready to spend");
        }
    }
}
