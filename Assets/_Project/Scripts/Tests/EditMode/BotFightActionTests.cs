using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // FightAction.LegalActions -- the menu a policy is asked to pick from.
    public class BotFightActionTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static (FightSession session, CombatantState hero, CombatantState foe) OneOnOne(
            int foeHealth = 100, int heroSpeed = 10)
        {
            var hero = Fighter("Hero", true, speed: heroSpeed);
            var foe = Fighter("Foe", false, maxHealth: foeHealth, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        [Test]
        public void LegalActions_OneOnOneWithNoSkillsOrItems_IncludesAttackOnTheFoeAndHoldBack()
        {
            var (session, hero, foe) = OneOnOne();

            var legal = FightAction.LegalActions(session, session.Current, System.Array.Empty<SatchelStack>());

            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Attack && a.Target == foe),
                "a lone reachable foe must offer an Attack");
            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.HoldBack),
                "HoldBack is always legal");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.BasicSpell),
                "no basic spell was granted, so none should be offered");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Skill),
                "no skills were granted, so none should be offered");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Item),
                "an empty satchel offers nothing");
        }

        [Test]
        public void LegalActions_WithASatchelStack_OffersItOnlyWhenItsCountIsPositive()
        {
            var (session, hero, foe) = OneOnOne();
            var satchel = new List<SatchelStack>
            {
                new SatchelStack("potion", "Potion", 2, false),
                new SatchelStack("elixir", "Elixir", 0, true),
            };

            var legal = FightAction.LegalActions(session, session.Current, satchel);

            Assert.IsTrue(legal.Any(a => a.Kind == FightActionKind.Item && a.ItemId == "potion"),
                "a stack with count > 0 must be offered");
            Assert.IsFalse(legal.Any(a => a.Kind == FightActionKind.Item && a.ItemId == "elixir"),
                "a stack with count 0 must not be offered");
        }

        [Test]
        public void Apply_HoldBack_BanksAnActionAndEndsTheTurn()
        {
            var (session, hero, foe) = OneOnOne();
            Assert.AreEqual(0, hero.BankedActions);

            FightAction.Apply(session, new FightAction(FightActionKind.HoldBack));

            Assert.AreEqual(1, hero.BankedActions);
        }

        [Test]
        public void Apply_Attack_DealsDamageToTheTarget()
        {
            var (session, hero, foe) = OneOnOne();
            int before = foe.CurrentHealth;

            FightAction.Apply(session, new FightAction(FightActionKind.Attack, foe));

            Assert.Less(foe.CurrentHealth, before, "an attack must land some damage on its target");
        }
    }
}
