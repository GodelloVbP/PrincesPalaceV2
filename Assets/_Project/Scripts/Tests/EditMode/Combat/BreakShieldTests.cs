using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    public class BreakShieldTests
    {
        [Test]
        public void StartsFull()
        {
            var shield = new BreakShield(10);
            Assert.AreEqual(10, shield.Current);
            Assert.AreEqual(10, shield.Max);
            Assert.IsFalse(shield.IsBroken);
        }

        [Test]
        public void Construction_FloorsMaxAtOne()
        {
            var shield = new BreakShield(0);
            Assert.AreEqual(1, shield.Max);
            Assert.AreEqual(1, shield.Current);
        }

        [Test]
        public void Deplete_ReducesCurrentByTheAmount()
        {
            var shield = new BreakShield(10);
            shield.Deplete(3);
            Assert.AreEqual(7, shield.Current);
            Assert.IsFalse(shield.IsBroken, "Still above zero");
        }

        [Test]
        public void Deplete_NeverGoesBelowZero()
        {
            var shield = new BreakShield(5);
            shield.Deplete(100);
            Assert.AreEqual(0, shield.Current);
        }

        [Test]
        public void Deplete_ToExactlyZero_Breaks()
        {
            var shield = new BreakShield(4);
            bool justBroke = shield.Deplete(4);
            Assert.IsTrue(justBroke);
            Assert.IsTrue(shield.IsBroken);
        }

        [Test]
        public void Deplete_PastZero_AlsoBreaks()
        {
            var shield = new BreakShield(4);
            bool justBroke = shield.Deplete(999);
            Assert.IsTrue(justBroke);
            Assert.IsTrue(shield.IsBroken);
        }

        // A multi-packet spell (Frost Flare: Fire + Frost) calls Deplete
        // twice in the same action. Only the packet that actually crosses
        // zero should report "just broke" — otherwise the break message
        // would print once per remaining packet in the same cast.
        [Test]
        public void Deplete_OnceAlreadyBroken_ReportsNoFurtherBreakAndLeavesCurrentAtZero()
        {
            var shield = new BreakShield(3);
            Assert.IsTrue(shield.Deplete(3), "This hit crosses zero");
            Assert.IsFalse(shield.Deplete(2), "Already broken — this hit does nothing further");
            Assert.AreEqual(0, shield.Current);
            Assert.IsTrue(shield.IsBroken);
        }

        [Test]
        public void Deplete_IgnoresNonPositiveAmounts()
        {
            var shield = new BreakShield(10);
            Assert.IsFalse(shield.Deplete(0));
            Assert.IsFalse(shield.Deplete(-5));
            Assert.AreEqual(10, shield.Current);
        }

        [Test]
        public void Reset_RefillsAndClearsBroken()
        {
            var shield = new BreakShield(8);
            shield.Deplete(8);
            Assert.IsTrue(shield.IsBroken);

            shield.Reset();

            Assert.AreEqual(8, shield.Current);
            Assert.IsFalse(shield.IsBroken);
        }

        // Proves the shield is a genuinely fresh obstacle after Reset, not
        // merely refilled — a hit right after Reset should be able to
        // deplete it again rather than being silently swallowed by a stale
        // IsBroken flag.
        [Test]
        public void Reset_ThenDeplete_WorksNormallyAgain()
        {
            var shield = new BreakShield(5);
            shield.Deplete(5);
            shield.Reset();

            bool justBroke = shield.Deplete(5);

            Assert.IsTrue(justBroke);
            Assert.AreEqual(0, shield.Current);
        }

        [Test]
        public void DepletionFor_WeaknessHit_CostsMoreThanAnOrdinaryHit()
        {
            int ordinary = BreakShield.DepletionFor(wasWeaknessHit: false);
            int weakness = BreakShield.DepletionFor(wasWeaknessHit: true);

            Assert.AreEqual(BreakShield.BaseDepletion, ordinary);
            Assert.AreEqual(BreakShield.WeaknessDepletion, weakness);
            Assert.Greater(weakness, ordinary);
        }
    }
}
