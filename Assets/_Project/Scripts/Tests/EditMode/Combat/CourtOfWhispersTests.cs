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
    public class CourtOfWhispersTests
    {
        private static ResolvedSkill Court() =>
            new ResolvedSkill("court_of_whispers", "Court of Whispers",
                "Terrifies ordinary enemies for a turn. Bosses are only delayed. You are left open.",
                "sheep", 1, SkillEffect.Enthrall, SkillTargeting.AllEnemies,
                14, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Vulnerable, statusMagnitude: 20, statusDuration: 1,
                queuePushSlots: 2, cooldownTurns: 4, freeAction: true);

        private static EnemyKit Enemy(string id, bool boss) =>
            new EnemyKit(new ResolvedEnemy(id, id, default, 0, 0, boss,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static (FightSession session, CombatantState hero, CombatantState ordinary,
            CombatantState boss) Fight()
        {
            var hero = new CombatantState("Hero", true, 500, 50, 40, 40);
            var ordinary = new CombatantState("Rat", false, 100, 0, 5, 14);
            var boss = new CombatantState("Choir", false, 500, 0, 10, 9);
            var session = new FightSession(
                new CombatEncounter(new[] { hero }, new[] { ordinary, boss }),
                new[] { new PlayerKit("sheep", CharacterRole.Support, new[] { Court() }, null, null) },
                new[] { Enemy("rat", false), Enemy("hollow_choir", true) },
                new SeededRandom(7));
            return (session, hero, ordinary, boss);
        }

        [Test]
        public void OrdinaryEnemiesAreFearedWhileBossesAreDelayed()
        {
            var (session, _, ordinary, boss) = Fight();
            float ordinaryCharge = session.Encounter.ChargeOf(ordinary);
            float bossCharge = session.Encounter.ChargeOf(boss);

            Assert.IsTrue(session.CastSkill(0, null));

            var fear = ordinary.Statuses.Single(s => s.Type == StatusEffectType.Feared);
            Assert.AreEqual(Fear.VulnerablePercent, fear.Magnitude);
            Assert.AreEqual(Fear.DefaultTurns, fear.TurnsRemaining);
            Assert.IsFalse(boss.Statuses.Any(s => s.Type == StatusEffectType.Feared));
            Assert.AreEqual(ordinaryCharge, session.Encounter.ChargeOf(ordinary),
                "ordinary enemies are controlled by Fear, not delayed as well");
            Assert.Less(session.Encounter.ChargeOf(boss), bossCharge,
                "the boss fallback must move the boss later in the order");
        }

        [Test]
        public void TheCasterPaysVulnerableEvenWhenEveryEnemyIsABoss()
        {
            var (session, hero, ordinary, boss) = Fight();
            ordinary.CurrentHealth = 0;

            Assert.IsTrue(session.CastSkill(0, null));

            Assert.IsFalse(boss.Statuses.Any(s => s.Type == StatusEffectType.Feared));
            var exposed = hero.Statuses.Single(s => s.Type == StatusEffectType.Vulnerable);
            Assert.AreEqual(20, exposed.Magnitude);
            Assert.AreEqual(1, exposed.TurnsRemaining);
        }

        [Test]
        public void TheDetailCardNamesTheBossFallbackBeforeTheCast()
        {
            var (session, hero, _, _) = Fight();

            var panel = FightHudModel.DetailForSkill(session, hero, Court());

            Assert.That(panel.Stats, Has.Some.EqualTo(("TARGET", "ALL ENEMIES; BOSSES DELAYED")));
        }
    }
}
