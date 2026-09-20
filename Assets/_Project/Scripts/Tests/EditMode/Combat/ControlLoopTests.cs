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
    // A focused late-party pressure test. It deliberately uses the real
    // dispatcher, cooldown clocks, Fear skip, Root legality and initiative
    // displacement; a hand-written cooldown simulation would merely restate
    // the policy it was meant to test.
    public class ControlLoopTests
    {
        private static ResolvedSkill Court() => new ResolvedSkill("court_of_whispers", "Court of Whispers", "", "hero", 1,
            SkillEffect.Enthrall, SkillTargeting.AllEnemies, 14, 0, false, 0, 0, false,
            null, SpellPresentation.None, 0, appliesStatus: StatusEffectType.Vulnerable,
            statusMagnitude: 20, statusDuration: 1, queuePushSlots: 2, cooldownTurns: 4);

        private static ResolvedSkill Shackles() => new ResolvedSkill("velvet_shackles", "Velvet Shackles", "", "hero", 1,
            SkillEffect.Afflict, SkillTargeting.SingleEnemy, 9, 0, false, 0, 0, false,
            null, SpellPresentation.None, 0, appliesStatus: StatusEffectType.Rooted,
            statusDuration: 2, cooldownTurns: 3);

        private static ResolvedSkill Rebuke() => new ResolvedSkill("winters_rebuke", "Winter's Rebuke", "", "hero", 1,
            SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 8, 0, false, 0, 0, false,
            new[] { new DamageInstance(DamageType.Ice, 1) }, SpellPresentation.None, 0,
            appliesStatus: StatusEffectType.Chilled, statusMagnitude: 25, statusDuration: 2,
            cooldownTurns: 2);

        private static ResolvedSkill Scythe() => new ResolvedSkill("gale_scythe", "Gale Scythe", "", "hero", 1,
            SkillEffect.DamageAll, SkillTargeting.AllEnemies, 9, 0, false, 0, 0, false,
            new[] { new DamageInstance(DamageType.Wind, 1) }, SpellPresentation.None, 0,
            queuePushSlots: 1, cooldownTurns: 3);

        [Test]
        public void AlternatingCastersCannotDenyEveryEnemyTurnAcrossFiveRounds()
        {
            var heroes = new[]
            {
                new CombatantState("Shawn", true, 500, 200, 20, 14),
                new CombatantState("Odette", true, 500, 200, 20, 12),
                new CombatantState("Bjorn", true, 500, 200, 20, 10),
            };
            var foe = new CombatantState("Late enemy", false, 100000, 0, 12, 13);
            var skills = new[] { Court(), Shackles(), Rebuke(), Scythe() };
            var kits = heroes.Select((_, i) => new PlayerKit("hero" + i, CharacterRole.Support,
                skills, null, DamageType.Arcane)).ToList();
            var enemy = new ResolvedEnemy("late_enemy", "Late enemy", default, 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0, attackType: DamageType.Physical);
            var session = new FightSession(new CombatEncounter(heroes, new[] { foe }), kits,
                new[] { new EnemyKit(enemy, false) }, new SeededRandom(17));
            session.Begin();
            session.DrainBeats();

            int startingHealth = heroes.Sum(hero => hero.CurrentHealth);
            int playerTurns = 0;
            int enemyActions = 0;
            while (!session.IsOver && playerTurns < 15)
            {
                Assert.IsTrue(session.IsPlayerTurn, "enemy turns should auto-resolve between player commands");
                var actor = session.Current;
                var ready = session.SkillOptionsFor(actor).Where(option => option.Ready).Select(option => option.Skill).ToList();
                var chosen = ready.FirstOrDefault(skill => skill.Id == "court_of_whispers")
                    ?? ready.FirstOrDefault(skill => skill.Id == "velvet_shackles")
                    ?? ready.FirstOrDefault(skill => skill.Id == "winters_rebuke")
                    ?? ready.FirstOrDefault(skill => skill.Id == "gale_scythe");

                bool acted = chosen != null
                    ? session.CastSkill(chosen, chosen.Targeting == SkillTargeting.AllEnemies ? null : foe)
                    : session.ExecuteAttack(foe);
                Assert.IsTrue(acted, "the scripted control rotation chose an action the dispatcher refused");
                enemyActions += session.DrainBeats().Count(beat => ReferenceEquals(beat.Actor, foe)
                    && !ReferenceEquals(beat.Target, foe));
                playerTurns++;
            }

            int damageTaken = startingHealth - heroes.Sum(hero => hero.CurrentHealth);
            TestContext.Out.WriteLine($"enemyActions={enemyActions}; damageTaken={damageTaken}");
            Assert.Greater(enemyActions, 0,
                "three alternating control casters denied every enemy action for five rounds");
            Assert.Greater(damageTaken, 0,
                "three alternating control casters denied every enemy action for five rounds");
        }
    }
}
