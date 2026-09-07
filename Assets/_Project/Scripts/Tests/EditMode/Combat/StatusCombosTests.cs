using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // SpendPoisonIfMatched COMPUTES AND SPENDS, AND DEALS NOTHING. It used to
    // deal the bonus itself, from inside DamagePipeline.AfterDefences -- which
    // is also what every telegraph preview runs through, so a preview spent the
    // status and took the health for it. The damage half now belongs to
    // FightSession.ResolveDetonation, which routes it through DealDamage; the
    // assertions here are about the RULE (which hits match, what the remaining
    // ticks are worth, that a spent poison cannot be spent twice), and the
    // health assertions moved to KillCreditTests where the funnel is.
    public class StatusCombosTests
    {
        private static CombatantState MakeCombatant(int maxHealth = 1000)
        {
            return new CombatantState("Test", true, maxHealth, 10, 5, 5);
        }

        [Test]
        public void NoPoison_NatureHit_DoesNothing()
        {
            var target = MakeCombatant();
            int bonus = StatusCombos.SpendPoisonIfMatched(target, DamageType.Nature);

            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void Poisoned_UnmatchedElement_DoesNotDetonate()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            int bonus = StatusCombos.SpendPoisonIfMatched(target, DamageType.Fire);

            Assert.AreEqual(0, bonus);
            Assert.IsTrue(target.Statuses.Exists(s => s.Type == StatusEffectType.Poison), "An unmatched hit should not touch the Poison at all");
        }

        [Test]
        public void Poisoned_NatureHit_Detonates()
        {
            var target = MakeCombatant();
            int before = target.CurrentHealth;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            int bonus = StatusCombos.SpendPoisonIfMatched(target, DamageType.Nature);

            Assert.AreEqual(60, bonus, "20 magnitude x 3 remaining ticks");
            Assert.AreEqual(before, target.CurrentHealth,
                "the figure is REPORTED, never dealt here -- see this class's own header");
        }

        [Test]
        public void Poisoned_PoisonTypedHit_AlsoDetonates()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 2);

            int bonus = StatusCombos.SpendPoisonIfMatched(target, DamageType.Poison);

            Assert.AreEqual(20, bonus);
        }

        [Test]
        public void Detonating_ConsumesThePoisonEntirely()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            StatusCombos.SpendPoisonIfMatched(target, DamageType.Nature);

            Assert.IsFalse(target.Statuses.Exists(s => s.Type == StatusEffectType.Poison));
        }

        // The whole point of a self-guarding detonation: a spell with two
        // Nature packets in one cast should not detonate twice, because
        // there is nothing left to detonate the second time.
        [Test]
        public void DetonatingTwiceInARow_TheSecondCallDoesNothing()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            int first = StatusCombos.SpendPoisonIfMatched(target, DamageType.Nature);
            int second = StatusCombos.SpendPoisonIfMatched(target, DamageType.Nature);

            Assert.Greater(first, 0);
            Assert.AreEqual(0, second);
        }

        [Test]
        public void NullTarget_ReturnsZeroRatherThanThrowing()
        {
            Assert.AreEqual(0, StatusCombos.SpendPoisonIfMatched(null, DamageType.Nature));
        }

        // Protect/Vulnerable/Stun on the same combatant must not interfere —
        // detonation only ever looks at Poison specifically.
        [Test]
        public void OtherStatusesPresent_DoNotBlockOrAlterDetonation()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 15, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 10, 3);

            int bonus = StatusCombos.SpendPoisonIfMatched(target, DamageType.Poison);

            Assert.AreEqual(30, bonus);
            Assert.AreEqual(2, target.Statuses.Count, "Vulnerable and Protect should both still be there");
        }
    }
}
