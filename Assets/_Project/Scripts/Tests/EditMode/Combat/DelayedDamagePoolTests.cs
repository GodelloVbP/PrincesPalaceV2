using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Plan 4c, Ignore Pain: a percent of every HIT goes into a pool paid as
    // damage over the holder's next N turn starts in equal parts; T3 lets a
    // heal pay the pool down first -- except under Cursed Blood, where the
    // heal converts and the pool is untouched.
    public class DelayedDamagePoolTests
    {
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight()
        {
            var hero = new CombatantState("Bjorn", true, 200, 0, 20, 20) { CritChancePercent = 0 };
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1);
            var kit = new PlayerKit("bjorn", CharacterRole.Tank, null, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return (session, hero, foe);
        }

        // ---- the pool on its own ---------------------------------------------------

        [Test]
        public void Defer_TakesTheFlooredPercent()
        {
            var pool = new DelayedDamagePool(20, 3);
            Assert.AreEqual(10, pool.Defer(50, DamageType.Physical));
            Assert.AreEqual(0, pool.Defer(4, DamageType.Physical), "20% of 4 floors to 0");
            Assert.AreEqual(6, pool.Defer(33, DamageType.Physical), "20% of 33 = 6.6 floors to 6");
            Assert.AreEqual(16, pool.Pending);
        }

        [Test]
        public void ATranchePaysInEqualParts_LargerFirst_NothingLost()
        {
            var pool = new DelayedDamagePool(100, 3);
            pool.Defer(20, DamageType.Physical);

            Assert.AreEqual(7, pool.TakeDue().Single().Amount);
            Assert.AreEqual(7, pool.TakeDue().Single().Amount);
            Assert.AreEqual(6, pool.TakeDue().Single().Amount);
            Assert.AreEqual(0, pool.TakeDue().Count);
            Assert.AreEqual(0, pool.Pending);
        }

        [Test]
        public void EachHitSpreadsOverItsOwnTurns()
        {
            var pool = new DelayedDamagePool(100, 2);
            pool.Defer(10, DamageType.Physical);          // 5, 5
            Assert.AreEqual(5, pool.TakeDue().Single().Amount);
            pool.Defer(20, DamageType.Physical);          // 10, 10
            Assert.AreEqual(15, pool.TakeDue().Single().Amount, "the old tranche's last 5 plus the new one's first 10");
            Assert.AreEqual(10, pool.TakeDue().Single().Amount);
            Assert.AreEqual(0, pool.Pending);
        }

        [Test]
        public void InstallmentsAreGroupedByDamageType()
        {
            var pool = new DelayedDamagePool(100, 1);
            pool.Defer(10, DamageType.Physical);
            pool.Defer(4, DamageType.Fire);
            pool.Defer(6, DamageType.Physical);

            var due = pool.TakeDue();
            Assert.AreEqual(2, due.Count);
            Assert.AreEqual((DamageType.Physical, 16), due[0]);
            Assert.AreEqual((DamageType.Fire, 4), due[1]);
        }

        [Test]
        public void ReduceByHeal_OnlyWithTheFlag_OldestFirst()
        {
            var off = new DelayedDamagePool(100, 3);
            off.Defer(20, DamageType.Physical);
            Assert.AreEqual(0, off.ReduceByHeal(15));
            Assert.AreEqual(20, off.Pending);

            var on = new DelayedDamagePool(100, 3, healsReducePool: true);
            on.Defer(20, DamageType.Physical);
            Assert.AreEqual(15, on.ReduceByHeal(15));
            Assert.AreEqual(5, on.Pending);
            Assert.AreEqual(5, on.ReduceByHeal(10), "only what is pending is consumed");
            Assert.AreEqual(0, on.Pending);
        }

        // ---- through the session ---------------------------------------------------

        [Test]
        public void Off_AHitLandsWhole()
        {
            var (session, hero, foe) = Fight();

            session.DealDamageForTest(foe, hero, 50, DamageType.Physical);

            Assert.AreEqual(150, hero.CurrentHealth);
        }

        [Test]
        public void On_PartOfAHitIsPaidOverTheNextTurns()
        {
            var (session, hero, foe) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(20, 3);

            session.DealDamageForTest(foe, hero, 50, DamageType.Physical);

            Assert.AreEqual(160, hero.CurrentHealth, "40 now");
            Assert.AreEqual(10, hero.DelayedDamage.Pending, "10 later");
            Assert.AreEqual(40, session.Ledger.For("bjorn").DamageTaken, "only what landed is booked now");

            // 10 over 3 turn starts: 4, 3, 3.
            session.TickStatusesForTest(hero);
            Assert.AreEqual(156, hero.CurrentHealth);
            session.TickStatusesForTest(hero);
            Assert.AreEqual(153, hero.CurrentHealth);
            session.TickStatusesForTest(hero);
            Assert.AreEqual(150, hero.CurrentHealth);
            Assert.AreEqual(0, hero.DelayedDamage.Pending);
            Assert.AreEqual(50, session.Ledger.For("bjorn").DamageTaken, "the whole blow, in the end");

            session.TickStatusesForTest(hero);
            Assert.AreEqual(150, hero.CurrentHealth, "nothing left to pay");
        }

        [Test]
        public void APaymentHasItsOwnBeatAndLine()
        {
            var (session, hero, foe) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(100, 1);
            session.DealDamageForTest(foe, hero, 30, DamageType.Physical);
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.TickStatusesForTest(hero);

            var beat = session.DrainBeats().Single(b => b.Messages.Contains("Bjorn suffers 30 delayed damage!"));
            Assert.AreEqual(30, beat.Amount);
            Assert.AreSame(hero, beat.Target);
            Assert.IsFalse(beat.IsHealing);
        }

        [Test]
        public void AStatusTickIsNotAHit_AndIsNeverDeferred()
        {
            var (session, hero, _) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(50, 3);
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 10, 1);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(190, hero.CurrentHealth);
            Assert.AreEqual(0, hero.DelayedDamage.Pending);
        }

        // Plan decision 2026-09-28: after wards and defences, before health.
        [Test]
        public void TheWardEatsTheBlowFirst_OnlyWhatWouldReachHealthIsDeferred()
        {
            var (session, hero, foe) = Fight();
            foe.DelayedDamage = new DelayedDamagePool(20, 3);
            StatusEffects.ApplyWard(foe.Statuses, points: 10, turns: 2, source: null);

            session.ExecuteAttack(foe);

            // 24-point swing (20 Attack x 1.2): the ward eats 10, 14 is left,
            // 20% of 14 floors to 2 deferred, 12 lands now.
            Assert.AreEqual(2, foe.DelayedDamage.Pending);
            Assert.AreEqual(988, foe.CurrentHealth);
        }

        [Test]
        public void T3_AHealPaysThePoolDownBeforeRestoringHealth()
        {
            var (session, hero, foe) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(20, 3, healsReducePool: true);
            session.DealDamageForTest(foe, hero, 50, DamageType.Physical); // 160, 10 pending

            session.HealForTest(hero, 25);

            Assert.AreEqual(0, hero.DelayedDamage.Pending);
            Assert.AreEqual(175, hero.CurrentHealth, "10 of the 25 went to the pool, 15 to health");
            Assert.AreEqual(15, session.Ledger.For("bjorn").Healed);
        }

        [Test]
        public void WithoutT3_AHealLeavesThePoolAlone()
        {
            var (session, hero, foe) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(20, 3);
            session.DealDamageForTest(foe, hero, 50, DamageType.Physical);

            session.HealForTest(hero, 25);

            Assert.AreEqual(10, hero.DelayedDamage.Pending);
            Assert.AreEqual(185, hero.CurrentHealth);
        }

        // Plan section 6, review finding 7.
        [Test]
        public void UnderCursedBlood_TheHealConverts_AndThePoolIsUntouched()
        {
            var (session, hero, foe) = Fight();
            hero.DelayedDamage = new DelayedDamagePool(20, 3, healsReducePool: true);
            session.DealDamageForTest(foe, hero, 50, DamageType.Physical); // 160, 10 pending
            session.OpenCursedBlood(hero, 2);

            session.HealForTest(hero, 25);

            Assert.AreEqual(10, hero.DelayedDamage.Pending, "a converted heal clears nothing");
            Assert.AreEqual(160, hero.CurrentHealth);
            Assert.AreEqual(975, foe.CurrentHealth, "all 25 converted (40 missing)");
        }

        [Test]
        public void DelayedDamageCanStillBeCheatedByCheatDeath()
        {
            var (session, hero, foe) = Fight();
            hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0) });
            hero.DelayedDamage = new DelayedDamagePool(100, 1);
            session.DealDamageForTest(foe, hero, 500, DamageType.Physical);
            Assert.AreEqual(200, hero.CurrentHealth, "fixture: all of it deferred");

            session.TickStatusesForTest(hero);

            Assert.AreEqual(1, hero.CurrentHealth, "delayed damage is damage: cheat death answers it");
            Assert.IsTrue(hero.CheatDeathSpent);
        }
    }
}
