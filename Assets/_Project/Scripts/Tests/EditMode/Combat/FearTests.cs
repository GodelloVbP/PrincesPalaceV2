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

            StatusEffects.ConsumeStun(target.Statuses); // one skipped turn: 2 -> 1
            Assert.IsTrue(StatusEffects.HasStun(target.Statuses));

            StatusEffects.ConsumeStun(target.Statuses); // the second: 1 -> 0, expires
            Assert.IsFalse(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void FearSurvivesTheTurnStartTickThatOpensTheTurnItSkips()
        {
            // WHICH CLOCK OWNS THE COUNTDOWN, and it is not this one.
            //
            // GrantTurnStart ticks, and only then does ResolveSkippedTurn ask
            // whether the holder can act. A Fear counted down by the tick is
            // therefore one turn short of what it promises -- and at the
            // authored duration World Ender's Crown actually uses (1), short
            // by the only turn it had.
            var target = Dummy();
            Fear.Apply(target, 1);

            StatusEffects.Tick(target);

            Assert.IsTrue(StatusEffects.HasStun(target.Statuses),
                "a one-turn Fear must still be standing when the skip is decided");
        }

        [Test]
        public void FearsVulnerableHalfLastsExactlyAsLongAsItsSkipHalf()
        {
            // ONE STATUS, ONE DURATION -- the whole premise of this file. The
            // two halves cannot come off separate clocks, so moving the
            // countdown to the skip has to move both.
            var target = Dummy();
            Fear.Apply(target, 1);

            StatusEffects.ConsumeStun(target.Statuses);

            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(target.Statuses),
                "the vulnerability went with the skip it was authored beside");
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
