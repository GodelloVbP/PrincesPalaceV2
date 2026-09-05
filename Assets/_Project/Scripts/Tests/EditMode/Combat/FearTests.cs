using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (b): FEAR = Stunned (skips the holder's turn) + Vulnerable
    // (extra damage taken), one status, one duration.
    public class FearTests
    {
        private static CombatantState Dummy() => new CombatantState("Dummy", false, 100, 0, 10, 10);

        [Test]
        public void FearedReadsAsStunned()
        {
            var target = Dummy();
            Fear.Apply(target, 2);

            Assert.IsTrue(StatusEffects.HasStun(target.Statuses), "Feared must read as Stun everywhere Stun is checked");
        }

        [Test]
        public void FearedIsAlsoVulnerable()
        {
            var target = Dummy();
            Fear.Apply(target, 2);

            // 25% (Fear.VulnerablePercent) more damage taken -- pinned
            // literal: 1 + 25/100 = 1.25.
            Assert.AreEqual(1.25f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        [Test]
        public void FearDoesNotConsumeOnASingleSkippedTurn()
        {
            var target = Dummy();
            Fear.Apply(target, 2);

            // ConsumeStun only ever removes an actual Stun entry -- Feared is
            // a standing malus that decays by turn count, not a spent status,
            // so a skipped turn must leave it standing.
            StatusEffects.ConsumeStun(target.Statuses);

            Assert.IsTrue(StatusEffects.HasStun(target.Statuses), "Fear must survive ConsumeStun -- it decays by turns, not by being spent");
        }

        [Test]
        public void FearExpiresAfterItsOwnDuration()
        {
            var target = Dummy();
            Fear.Apply(target, 2);

            StatusEffects.Tick(target); // 2 -> 1
            Assert.IsTrue(StatusEffects.HasStun(target.Statuses));

            StatusEffects.Tick(target); // 1 -> 0, expires
            Assert.IsFalse(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void UnfearedTargetIsNeitherStunnedNorVulnerable()
        {
            var target = Dummy();
            Assert.IsFalse(StatusEffects.HasStun(target.Statuses));
            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(target.Statuses));
        }
    }
}
