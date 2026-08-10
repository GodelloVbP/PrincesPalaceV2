using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    public class StatusEffectsTests
    {
        private static CombatantState MakeCombatant(int maxHealth = 1000)
        {
            return new CombatantState("Test", true, maxHealth, 10, 5, 2, 5);
        }

        // ---- Apply / stacking ----------------------------------------------

        [Test]
        public void Apply_NewType_AddsAnEntry()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusEffectType.Poison, target.Statuses[0].Type);
            Assert.AreEqual(10, target.Statuses[0].Magnitude);
            Assert.AreEqual(3, target.Statuses[0].TurnsRemaining);
        }

        // Re-casting the same status must never be strictly better than
        // casting it once — the same anti-compounding rule ScalingProfile
        // and SpeedScale hold everywhere else in this game.
        [Test]
        public void Apply_SameTypeTwice_RefreshesRatherThanStacking()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            Assert.AreEqual(1, target.Statuses.Count, "A second application should refresh, not add a second entry");
        }

        [Test]
        public void Apply_SameTypeAgain_TakesTheStrongerMagnitudeAndLongerDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 25, 5);

            Assert.AreEqual(25, target.Statuses[0].Magnitude);
            Assert.AreEqual(5, target.Statuses[0].TurnsRemaining);
        }

        [Test]
        public void Apply_AWeakerReapplication_NeverWeakensTheExistingOne()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 25, 5);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 1);

            Assert.AreEqual(25, target.Statuses[0].Magnitude, "A weaker re-application should not downgrade the stronger one already active");
            Assert.AreEqual(5, target.Statuses[0].TurnsRemaining);
        }

        [Test]
        public void Apply_DifferentTypes_CoexistIndependently()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 2);

            Assert.AreEqual(2, target.Statuses.Count);
        }

        // ---- Stun ------------------------------------------------------------

        [Test]
        public void HasStun_FalseWithoutOne()
        {
            var target = MakeCombatant();
            Assert.IsFalse(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void HasStun_TrueOnceApplied()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 1);

            Assert.IsTrue(StatusEffects.HasStun(target.Statuses));
        }

        [Test]
        public void ConsumeStun_RemovesItEntirely_NotJustDecrementsDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 5);

            StatusEffects.ConsumeStun(target.Statuses);

            Assert.IsFalse(StatusEffects.HasStun(target.Statuses),
                "A spent Stun should be gone outright, not merely a shorter Stun");
            Assert.AreEqual(0, target.Statuses.Count);
        }

        [Test]
        public void ConsumeStun_LeavesOtherStatusesUntouched()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 1, 1);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            StatusEffects.ConsumeStun(target.Statuses);

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusEffectType.Poison, target.Statuses[0].Type);
        }

        // ---- DamageTakenMultiplier --------------------------------------------

        [Test]
        public void DamageTakenMultiplier_NoStatuses_IsOne()
        {
            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(new System.Collections.Generic.List<ActiveStatus>()));
        }

        [Test]
        public void DamageTakenMultiplier_Protect30_ReducesByThirtyPercent()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 30, 3);

            Assert.AreEqual(0.7f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        [Test]
        public void DamageTakenMultiplier_Vulnerable25_IncreasesByTwentyFivePercent()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 25, 3);

            Assert.AreEqual(1.25f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        // Additive, not compounding — a player can add the two contributions
        // up in their head, same rule ScalingProfile.MultiplierFor follows.
        [Test]
        public void DamageTakenMultiplier_ProtectAndVulnerableTogether_AreAdditive()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 20, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Vulnerable, 50, 3);

            // 1 - 0.20 + 0.50 = 1.30, not (1 - 0.20) * (1 + 0.50) = 1.20.
            Assert.AreEqual(1.30f, StatusEffects.DamageTakenMultiplier(target.Statuses), 0.0001f);
        }

        [Test]
        public void DamageTakenMultiplier_HugeProtect_NeverReachesZeroOrNegative()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Protect, 500, 3);

            Assert.AreEqual(StatusEffects.MinimumDamageTakenMultiplier, StatusEffects.DamageTakenMultiplier(target.Statuses));
        }

        // ---- Tick --------------------------------------------------------------

        [Test]
        public void Tick_Poison_DealsItsMagnitudeAsDamage()
        {
            var target = MakeCombatant();
            int before = target.CurrentHealth;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 40, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(40, report.PoisonDamage);
            Assert.AreEqual(before - 40, target.CurrentHealth);
        }

        [Test]
        public void Tick_Regen_HealsItsMagnitude()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 100;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Regen, 40, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(40, report.RegenHealed);
            Assert.AreEqual(140, target.CurrentHealth);
        }

        [Test]
        public void Tick_DecrementsDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            StatusEffects.Tick(target);

            Assert.AreEqual(2, target.Statuses[0].TurnsRemaining);
        }

        [Test]
        public void Tick_ExpiresAndRemovesAStatusThatReachesZeroDuration()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 1);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(0, target.Statuses.Count);
            CollectionAssert.Contains(report.Expired, StatusEffectType.Poison);
        }

        [Test]
        public void Tick_NotYetExpired_StaysOnTheList()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 10, 3);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(1, target.Statuses.Count);
            CollectionAssert.IsEmpty(report.Expired);
        }

        [Test]
        public void Tick_MultipleStatuses_AllTickIndependently()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 500;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 30, 2);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Regen, 20, 2);

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(30, report.PoisonDamage);
            Assert.AreEqual(20, report.RegenHealed);
            Assert.AreEqual(500 - 30 + 20, target.CurrentHealth);
        }

        [Test]
        public void Tick_NoStatuses_ReturnsAnEmptyReport()
        {
            var target = MakeCombatant();
            var report = StatusEffects.Tick(target);

            Assert.IsTrue(report.IsEmpty);
        }

        // Poison respects the same floor everything else in the damage
        // pipeline does — it can bring a combatant to 0, never negative.
        [Test]
        public void Tick_Poison_NeverDropsHealthBelowZero()
        {
            var target = MakeCombatant();
            target.CurrentHealth = 10;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 9999, 1);

            StatusEffects.Tick(target);

            Assert.AreEqual(0, target.CurrentHealth);
        }

        // ---- ConsumeShieldedReduction (Magical Shield) --------------------

        [Test]
        public void ConsumeShieldedReduction_WithNoShield_ReturnsDamageUnchanged()
        {
            var target = MakeCombatant();

            int result = StatusEffects.ConsumeShieldedReduction(target, 100);

            Assert.AreEqual(100, result);
        }

        [Test]
        public void ConsumeShieldedReduction_HalvesTheHit_AndRemovesTheStatus()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Shielded, 50, 99);

            int result = StatusEffects.ConsumeShieldedReduction(target, 100);

            Assert.AreEqual(50, result);
            Assert.IsFalse(target.Statuses.Exists(s => s.Type == StatusEffectType.Shielded),
                "The shield should be spent by the hit it reduced");
        }

        // The whole point: it is consumed by the NEXT HIT, not by a turn
        // count, so a second hit after the first one landed should not be
        // reduced again.
        [Test]
        public void ConsumeShieldedReduction_OnlyReducesTheOneHitItCatches()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Shielded, 50, 99);

            int first = StatusEffects.ConsumeShieldedReduction(target, 100);
            int second = StatusEffects.ConsumeShieldedReduction(target, 100);

            Assert.AreEqual(50, first);
            Assert.AreEqual(100, second, "The shield was already spent by the first hit");
        }

        // Re-applying while the shield still stands must not stack — the
        // same refresh-not-stack rule Apply already gives every status, and
        // exactly what "does not stack" means for this relic.
        [Test]
        public void ReapplyingShielded_WhileStillUp_DoesNotStack()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Shielded, 50, 99);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Shielded, 50, 99);

            Assert.AreEqual(1, target.Statuses.Count);

            int result = StatusEffects.ConsumeShieldedReduction(target, 100);
            Assert.AreEqual(50, result);
        }

        [Test]
        public void ConsumeShieldedReduction_DoesNotConsumeOnNonPositiveDamage()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Shielded, 50, 99);

            StatusEffects.ConsumeShieldedReduction(target, 0);

            Assert.IsTrue(target.Statuses.Exists(s => s.Type == StatusEffectType.Shielded),
                "A non-hit should not spend the shield");
        }
    }
}
