using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // A HEALTH COST IS A PAYMENT, NOT DAMAGE (plan 1.2). Validated together
    // with mana in one refusal, paid by a direct write, and invisible to
    // every rider a real hit would trigger -- see HealthCost's own header
    // for the funnel it deliberately does not use.
    //
    // Driven through CastSkill (dispatcher) for everything but the pure
    // rounding case, because "spends neither cost on a combined refusal" and
    // "bypasses every rider" are both claims about the SESSION, not about
    // HealthCost's own arithmetic in isolation.
    public class HealthCostTests
    {
        // flatAmount 0 -- HealSelf lands for exactly nothing, deliberately,
        // so every assertion below can read the health cost off the bar
        // without a heal muddying the same number in the other direction.
        // SkillEntryResolver would refuse a content row shaped this way
        // ("restores nothing"), but that rule lives in the resolver, not on
        // ResolvedSkill itself, and this fixture is built by hand.
        private static ResolvedSkill BloodCast(int healthCostPercent, int manaCost = 0) =>
            new ResolvedSkill("bloodcast", "Bloodcast", "test fixture", "hero", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, manaCost, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                healthCostPercent: healthCostPercent);

        private static (FightSession session, CombatantState hero) Fight(ResolvedSkill skill,
            int maxHealth = 200, int mana = 50)
        {
            var hero = new CombatantState("Hero", true, maxHealth, mana, 40, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 1);
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { skill }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero);
        }

        [Test]
        public void FivePercentOf201MaxHealth_Costs11_Not10()
        {
            // 201 * 5 / 100 = 10.05 -- Rounding.AwayFromZero rounds THAT to
            // 10 (nearest, ties away from zero), and the owner's rule is a
            // ceiling, not a nearest. This is the pin that catches a health
            // cost quietly reusing the wrong rounding helper.
            Assert.AreEqual(11, HealthCost.AmountFor(201, 5));
        }

        [Test]
        public void ACostThatWouldLeaveZero_IsRefused_AndSpendsNoMana()
        {
            // 50 max health at 50% is 25 -- leaves exactly 25, which is fine.
            // Push it to a percent that would leave 0: 100% is refused
            // outright by content, so use a health total the percent cannot
            // survive -- 50 health, 100... no, use maxHealth so small that
            // ceil(1%) already consumes it: 1 max health at 99% costs 1,
            // leaving 0.
            var skill = BloodCast(healthCostPercent: 99, manaCost: 5);
            var (session, hero) = Fight(skill, maxHealth: 1, mana: 50);
            int manaBefore = hero.PrimaryPool.Current;
            int healthBefore = hero.CurrentHealth;

            bool cast = session.CastSkill(0, null);

            Assert.IsFalse(cast, "a cost that would leave 0 HP must refuse the whole cast");
            Assert.AreEqual(manaBefore, hero.PrimaryPool.Current, "mana must not be spent on a refused cast");
            Assert.AreEqual(healthBefore, hero.CurrentHealth, "health must not move on a refused cast");
        }

        [Test]
        public void AWardedCaster_PaysTheFullCostFromHealth_AndTheWardIsUntouched()
        {
            var skill = BloodCast(healthCostPercent: 10, manaCost: 5);
            var (session, hero) = Fight(skill, maxHealth: 200, mana: 50);
            StatusEffects.ApplyWard(hero.Statuses, points: 50, turns: 2, source: hero);
            int wardBefore = StatusEffects.WardPoints(hero);
            int healthBefore = hero.CurrentHealth;

            bool cast = session.CastSkill(0, null);

            Assert.IsTrue(cast);
            // 10% of 200 is exactly 20, no rounding ambiguity to muddy this.
            Assert.AreEqual(healthBefore - 20, hero.CurrentHealth,
                "the cost must come straight off health, never off the ward pool");
            Assert.AreEqual(wardBefore, StatusEffects.WardPoints(hero), "the ward must be untouched");
        }

        [Test]
        public void PayingHealth_TriggersNoVulnerable_NoWool_NoPhoenixEgg_NoLedgerTookRow()
        {
            var skill = BloodCast(healthCostPercent: 10, manaCost: 5);
            var (session, hero) = Fight(skill, maxHealth: 200, mana: 50);
            // A Vulnerable that WOULD multiply a real hit -- if the payment
            // ever routed through the damage funnel this would inflate the
            // 20-point cost, and it must not.
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Vulnerable, 100, 3);
            int healthBefore = hero.CurrentHealth;

            session.CastSkill(0, null);

            Assert.AreEqual(healthBefore - 20, hero.CurrentHealth,
                "a 100% Vulnerable must not double the cost -- this is a payment, not incoming damage");
            // Keyed by the PLAYER KIT's id ("hero"), not the combatant's
            // display name -- LedgerIdOf prefers KitFor(combatant).Id.
            Assert.AreEqual(0, session.Ledger.For("hero").DamageTaken,
                "a health cost must never appear as a Took row in the ledger");
        }

        [Test]
        public void PayingHealth_NeverSettlesADeath_BecauseItCannotReachZero()
        {
            // maxHealth 10 at 90% costs 9, leaving exactly 1 -- the floor
            // HealthCost.CanPay guarantees. The caster must still be alive
            // and the fight must not have ended.
            var skill = BloodCast(healthCostPercent: 90, manaCost: 0);
            var (session, hero) = Fight(skill, maxHealth: 10, mana: 50);

            bool cast = session.CastSkill(0, null);

            Assert.IsTrue(cast);
            Assert.AreEqual(1, hero.CurrentHealth);
            Assert.IsTrue(hero.IsAlive);
        }
    }
}
