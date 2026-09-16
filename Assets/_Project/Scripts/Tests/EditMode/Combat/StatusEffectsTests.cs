using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    public class StatusEffectsTests
    {
        private static CombatantState MakeCombatant(int maxHealth = 1000)
        {
            return new CombatantState("Test", true, maxHealth, 10, 5, 5);
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

        // ---- Rooted (Phase D3, item-modifier plan) ----------------------------
        //
        // The mechanism-level coverage (the plain-attack gate, the no-legal-
        // skill forfeit, coexistence with Chilled/Dodge) lives in
        // RootedStatusTests -- these mirror HasStun's own small, direct
        // shape: Rooted is queried like Stun, but decays by turn count like
        // Chilled/Protect/Vulnerable, so there is no ConsumeRooted to test.

        [Test]
        public void HasRooted_FalseWithoutOne()
        {
            var target = MakeCombatant();
            Assert.IsFalse(StatusEffects.HasRooted(target.Statuses));
        }

        [Test]
        public void HasRooted_TrueOnceApplied()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 3);

            Assert.IsTrue(StatusEffects.HasRooted(target.Statuses));
        }

        [Test]
        public void Rooted_ExpiresAfterItsDuration_LikeAnyOtherTurnCountedStatus()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 1);

            var report = StatusEffects.Tick(target);

            Assert.IsFalse(StatusEffects.HasRooted(target.Statuses), "a one-turn Rooted must be gone after one tick");
            Assert.IsTrue(report.Expired.Contains(StatusEffectType.Rooted));
        }

        [Test]
        public void Rooted_SurvivesATickWithTurnsRemaining_UnlikeStunWhichIsSpentOutright()
        {
            var target = MakeCombatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 2);

            StatusEffects.Tick(target);

            Assert.IsTrue(StatusEffects.HasRooted(target.Statuses), "a two-turn Rooted must survive one tick");
            Assert.AreEqual(1, target.Statuses[0].TurnsRemaining);
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

        // ---- the ward pool (Magical Shield, and every other ward) ---------
        //
        // WardTests owns the model in full; these four are what this file's
        // own Magical Shield coverage always asserted, repinned as points.

        [Test]
        public void ConsumeWard_WithNoWard_ReturnsDamageUnchanged()
        {
            var target = MakeCombatant();

            Assert.AreEqual(100, StatusEffects.ConsumeWard(target, 100).Damage);
        }

        [Test]
        public void ConsumeWard_EatsThePoolAndRemovesTheStatus()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(50, StatusEffects.ConsumeWard(target, 100).Damage,
                "50 points of a 100 hit");
            Assert.IsFalse(target.Statuses.Exists(s => s.Type == StatusEffectType.Shielded),
                "The shield should be gone once its pool is empty");
        }

        // It is spent by damage, not by a turn count, and a hit bigger than
        // the pool takes the whole pool with it.
        [Test]
        public void ConsumeWard_HasNothingLeftForASecondBigHit()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(50, StatusEffects.ConsumeWard(target, 100).Damage);
            Assert.AreEqual(100, StatusEffects.ConsumeWard(target, 100).Damage,
                "The pool was emptied by the first hit");
        }

        // Re-raising while the shield still stands must not stack -- the same
        // does-not-stack promise this relic always made, now enforced by
        // ApplyWard's replacement rule rather than by Apply's merge.
        [Test]
        public void ReapplyingAWard_WhileStillUp_DoesNotStack()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(50, StatusEffects.WardPoints(target));
        }

        [Test]
        public void ConsumeWard_DoesNotSpendOnNonPositiveDamage()
        {
            var target = MakeCombatant();
            StatusEffects.ApplyWard(target.Statuses, 50, 99);

            StatusEffects.ConsumeWard(target, 0);

            Assert.AreEqual(50, StatusEffects.WardPoints(target),
                "A non-hit should not spend the shield");
        }

        // ---- a poison tick a signature pool eats whole -----------------------

        [Test]
        public void APoisonTickAWoolPoolAbsorbsIsStillCountedAndStillSaid()
        {
            // StatusEffects.Tick measures poison as HEALTH LOST, and
            // CombatMath.ApplyDamage spends the signature pool BEFORE health --
            // so a tick the pool ate in full reported zero. TickStatuses is
            // gated on report.PoisonDamage > 0, which meant no log line, no
            // RecordUnattributedDamage, no Ledger.Took absorbed column, and no
            // NoteDamageForPools: the exact bookkeeping d6814ce0 moved into one
            // funnel precisely so it could not be forgotten.
            //
            // Two contracts say so. FightSession.Ledger.cs:219-225: "THE POOLS
            // ARE TOLD HERE TOO, which is the whole reason this is not just a
            // Ledger.Took call. A status tick is damage its victim took by
            // every account that matters to a resource pool." And
            // CombatMath.cs:508-513: "Absorption lives HERE, in the single
            // funnel every damage source already goes through... one of them
            // forgetting would make the resource silently stop being armour
            // depending on who hit you."
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10)
            {
                SignaturePool = new ResourcePool("wool", "Wool", 20, 0, 0, 0,
                    absorbPerPoint: 1, absorbsDamage: true),
            };
            hero.SignaturePool.Current = 20;

            var encounter = new CombatEncounter(new[] { hero }, new[]
            {
                new CombatantState("Rat", false, 5000, 10, 8, 1),
            });
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(2)) { DamageVarianceRange = 0f };

            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 5, 3);

            int healthBefore = hero.CurrentHealth;
            session.TickStatusesForTest(hero);

            Assert.AreEqual(healthBefore, hero.CurrentHealth, "fixture: the pool ate the whole tick");
            Assert.AreEqual(15, hero.SignaturePool.Current, "fixture: and paid five points for it");

            Assert.AreEqual(5, session.Ledger.For("shawn").Shielded,
                "the pool ate five points of poison and the ledger never heard about it");
            Assert.AreEqual(0, session.Ledger.For("shawn").DamageTaken,
                "and none of it reached health, so none of it is damage taken");

            // Immediate messages as well as beat ones: nothing has opened a
            // beat in this fixture (TickStatusesForTest is called directly, by
            // design -- see its own header), so AppendMessage files the lines
            // under _immediateMessages rather than onto a beat.
            var lines = session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages())
                .ToList();
            Assert.IsTrue(lines.Any(l => l.Contains("poison damage")),
                "a tick the player's armour absorbed was never mentioned at all");
            Assert.IsTrue(lines.Any(l => l.Contains("Wool soaks 5 of it")),
                "and the armour doing its job is the half worth saying");
        }
    }
}
