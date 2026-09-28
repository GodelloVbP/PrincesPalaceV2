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
    // Plan 4d, Blood Price: the part of a skill's primary cost the pool cannot
    // cover is paid in health at ShortfallHealthPermille thousandths of max
    // health per point, never below 1 HP, never through the damage funnel
    // (so never cheat death), and SkillResolution.CanAfford -- the one
    // predicate the menu, the bots and the cast all ask -- agrees.
    public class BloodPriceTests
    {
        // flatAmount 0 HealSelf, the HealthCostTests fixture shape: the heal
        // lands nothing, so every health figure below is the payment alone.
        private static ResolvedSkill Skill(int furyCost, int healthCostPercent = 0) =>
            new ResolvedSkill("gorge", "Gorge", "test fixture", "bjorn", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, furyCost, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                healthCostPercent: healthCostPercent);

        private static CombatantState Bjorn(int maxHealth, int fury, int permille)
        {
            var pool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            pool.Gain(fury);
            return new CombatantState("Bjorn", true, maxHealth, pool, 20, 20) { ShortfallHealthPermille = permille };
        }

        private static FightSession Fight(CombatantState hero, ResolvedSkill skill)
        {
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1);
            var kit = new PlayerKit("bjorn", CharacterRole.Tank, new[] { skill }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();
            return session;
        }

        // ---- rounding ------------------------------------------------------------

        [Test]
        public void HalfAPercentPerPoint_RoundedUp()
        {
            Assert.AreEqual(20, BloodPrice.HealthFor(Bjorn(400, 0, 5), 10), "10 x 0.5% of 400 = 20 exactly");
            Assert.AreEqual(16, BloodPrice.HealthFor(Bjorn(450, 0, 5), 7), "7 x 0.5% of 450 = 15.75 -> 16");
            Assert.AreEqual(1, BloodPrice.HealthFor(Bjorn(10, 0, 5), 1), "0.05 still costs 1");
            Assert.AreEqual(0, BloodPrice.HealthFor(Bjorn(400, 0, 0), 10), "off: no health price");
        }

        [Test]
        public void ShortfallIsWhatThePoolLacks_OnlyWithTheFlag()
        {
            Assert.AreEqual(20, BloodPrice.ShortfallOf(Bjorn(400, 10, 5), 30));
            Assert.AreEqual(0, BloodPrice.ShortfallOf(Bjorn(400, 30, 5), 30), "affordable: no shortfall");
            Assert.AreEqual(0, BloodPrice.ShortfallOf(Bjorn(400, 10, 0), 30), "off: no shortfall");
        }

        // ---- the one predicate ---------------------------------------------------

        [Test]
        public void CanAfford_Off_ShortIsRefused()
        {
            Assert.IsFalse(SkillResolution.CanAfford(Bjorn(400, 10, 0), 30, 0));
        }

        [Test]
        public void CanAfford_On_ShortIsPaidInHealth_DownToExactlyOneHp()
        {
            var bjorn = Bjorn(400, 0, 5); // 30 short = 60 HP
            Assert.IsTrue(SkillResolution.CanAfford(bjorn, 30, 0));

            bjorn.CurrentHealth = 61;
            Assert.IsTrue(SkillResolution.CanAfford(bjorn, 30, 0), "61 - 60 leaves 1");
            bjorn.CurrentHealth = 60;
            Assert.IsFalse(SkillResolution.CanAfford(bjorn, 30, 0), "60 - 60 would leave 0");
        }

        [Test]
        public void CanAfford_CountsTheAuthoredHealthCostAndTheShortfallTogether()
        {
            var bjorn = Bjorn(400, 0, 5); // 10% of 400 = 40, plus 30 short = 60: 100 in all
            bjorn.CurrentHealth = 101;
            Assert.IsTrue(SkillResolution.CanAfford(bjorn, 30, 0, healthCostPercent: 10));
            bjorn.CurrentHealth = 100;
            Assert.IsFalse(SkillResolution.CanAfford(bjorn, 30, 0, healthCostPercent: 10));
        }

        // ---- the cast ------------------------------------------------------------

        [Test]
        public void ACastShortOnFury_SpendsTheFuryItHas_AndPaysTheRestInHealth()
        {
            var hero = Bjorn(400, 10, 5);
            var session = Fight(hero, Skill(furyCost: 30));

            bool cast = session.CastSkill(0, null);

            Assert.IsTrue(cast);
            Assert.AreEqual(0, hero.PrimaryPool.Current);
            Assert.AreEqual(360, AfterTheCast(session, hero), "20 short x 0.5% of 400 = 40");
        }

        [Test]
        public void ACastThePoolCanPayCostsNoHealth()
        {
            var hero = Bjorn(400, 50, 5);
            var session = Fight(hero, Skill(furyCost: 30));

            session.CastSkill(0, null);

            Assert.AreEqual(400, AfterTheCast(session, hero));
        }

        [Test]
        public void ACastThatWouldLeaveZero_IsRefused_AndSpendsNothing()
        {
            var hero = Bjorn(400, 10, 5);
            hero.CurrentHealth = 40; // 20 short = 40 HP, would leave 0
            var session = Fight(hero, Skill(furyCost: 30));

            bool cast = session.CastSkill(0, null);

            Assert.IsFalse(cast);
            Assert.AreEqual(10, hero.PrimaryPool.Current);
            Assert.AreEqual(40, hero.CurrentHealth);
        }

        [Test]
        public void SelfPaymentNeverTriggersCheatDeath_ItStaysLoaded()
        {
            var hero = Bjorn(400, 10, 5);
            hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.CheatDeathOncePerFight, 0) });
            hero.CurrentHealth = 41; // 40 to pay, leaves exactly 1
            var session = Fight(hero, Skill(furyCost: 30));

            Assert.IsTrue(session.CastSkill(0, null));

            Assert.AreEqual(1, AfterTheCast(session, hero));
            Assert.IsFalse(hero.CheatDeathSpent, "a payment is not damage: cheat death never hears it");
            Assert.AreEqual(0, session.Ledger.For("bjorn").DamageTaken, "and it is no Took row");
        }

        [Test]
        public void TheMenuRowAgreesWithTheCast()
        {
            var hero = Bjorn(400, 10, 5);
            var session = Fight(hero, Skill(furyCost: 30));

            hero.CurrentHealth = 40;
            Assert.IsFalse(session.SkillOptionsFor(hero)[0].Affordable);
            Assert.IsFalse(session.CastSkill(0, null));

            hero.CurrentHealth = 41;
            Assert.IsTrue(session.SkillOptionsFor(hero)[0].Affordable);
            Assert.IsTrue(session.CastSkill(0, null));
        }

        [Test]
        public void WithoutTheFlag_TheMenuAndTheCastBothRefuse()
        {
            var hero = Bjorn(400, 10, 0);
            var session = Fight(hero, Skill(furyCost: 30));

            Assert.IsFalse(session.SkillOptionsFor(hero)[0].Affordable);
            Assert.IsFalse(session.CastSkill(0, null));
            Assert.AreEqual(400, hero.CurrentHealth);
            Assert.AreEqual(10, hero.PrimaryPool.Current);
        }

        // The cast costs the turn and the foe replies before the call returns,
        // so health is read off the cast's own beat, as FightConsumableTests
        // does for a potion.
        private static int AfterTheCast(FightSession session, CombatantState hero) =>
            session.DrainBeats().First().Snapshot[hero].Health;
    }
}
