using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The preview's running order, pinned. Everything here is about what an
    // AUTHOR sees, so every assertion is on the sequence rather than on the
    // damage: a showcase that quietly reordered or substituted an ability would
    // still produce a perfectly good-looking fight and teach the author the
    // wrong thing about their own mob.
    public class EnemyShowcaseTests
    {
        private static EnemyAbility Plain() =>
            EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f);

        private static EnemyAbility Skill(string name, float weight) =>
            EnemyAbility.LegacyAttack(name, 2f, weight);

        // The enemy key is only ever used for reference identity.
        private static readonly object Mob = new object();
        private static readonly object OtherMob = new object();

        [Test]
        public void TakesEveryAbilityInAuthoredOrderThenSwingsPlainly()
        {
            var pool = new List<EnemyAbility> { Plain(), Skill("Slam", 0.2f), Skill("Roar", 0.1f) };
            var showcase = new EnemyShowcase();

            Assert.AreEqual(1, showcase.Next(Mob, pool), "turn 1 should be the first authored ability");
            Assert.AreEqual(2, showcase.Next(Mob, pool), "turn 2 should be the second");
            Assert.AreEqual(-1, showcase.Next(Mob, pool), "turn 3 should fall through to the plain attack");

            CollectionAssert.AreEqual(new[] { "Slam", "Roar", FightSession.IntentAttack }, showcase.Played);
        }

        // The plain swing sits at index 0 of every authored pool
        // (FightEncounterAdapter puts it there), and opening on it would show
        // the author the least interesting thing their mob does.
        [Test]
        public void ThePlainSwingIsTheEncoreNotTheOpener()
        {
            var pool = new List<EnemyAbility> { Plain(), Skill("Slam", 1f) };
            var showcase = new EnemyShowcase();

            Assert.AreEqual(1, showcase.Next(Mob, pool));
        }

        // Weight zero is FightSession.EffectivePoolFor's way of saying "not
        // this turn" -- a summon already at its cap, a swing while rooted.
        [Test]
        public void AnAbilityWhoseWeightIsZeroIsNamedAndSkipped()
        {
            var pool = new List<EnemyAbility> { Plain(), Skill("Summon Sapling", 0f), Skill("Slam", 1f) };
            var reported = new List<string>();
            var showcase = new EnemyShowcase(reported.Add);

            Assert.AreEqual(2, showcase.Next(Mob, pool), "the unmet entry is skipped, not substituted silently");

            CollectionAssert.AreEqual(new[] { "Summon Sapling" }, showcase.Skipped);
            Assert.AreEqual(1, reported.Count);
            StringAssert.Contains("Summon Sapling", reported[0]);
        }

        [Test]
        public void AMobWithNoAbilitiesJustSwings()
        {
            var pool = new List<EnemyAbility> { Plain() };
            var showcase = new EnemyShowcase();

            Assert.AreEqual(-1, showcase.Next(Mob, pool));
            Assert.AreEqual(-1, showcase.Next(Mob, pool), "and keeps swinging afterwards");
        }

        // -Formation full puts three copies of one mob on the stage. Each
        // should show the whole kit; a shared cursor would have the second copy
        // start where the first left off, so the author would see each ability
        // once across the stage instead of once per monster.
        [Test]
        public void EachCopyOnTheStageWalksItsOwnSequence()
        {
            var pool = new List<EnemyAbility> { Plain(), Skill("Slam", 1f), Skill("Roar", 1f) };
            var showcase = new EnemyShowcase();

            Assert.AreEqual(1, showcase.Next(Mob, pool));
            Assert.AreEqual(1, showcase.Next(OtherMob, pool));
            Assert.AreEqual(2, showcase.Next(Mob, pool));
            Assert.AreEqual(2, showcase.Next(OtherMob, pool));
        }

        [Test]
        public void ScriptLengthCountsTheAbilitiesPlusTheClosingSwing()
        {
            Assert.AreEqual(1, EnemyShowcase.ScriptLength(new List<EnemyAbility> { Plain() }));
            Assert.AreEqual(3, EnemyShowcase.ScriptLength(
                new List<EnemyAbility> { Plain(), Skill("Slam", 1f), Skill("Roar", 1f) }));
            Assert.AreEqual(1, EnemyShowcase.ScriptLength(null));
        }

        // A legacy monster's skill is authored with a power, and one authored
        // at exactly 1.0 wears the same shape as the plain swing. The plain
        // attack is identified by its LABEL for that reason; this is the case
        // that made it necessary.
        [Test]
        public void ALegacySkillAtPowerOneIsStillAnAbility()
        {
            var pool = new List<EnemyAbility>
            {
                Plain(),
                EnemyAbility.LegacyAttack("Bite", 1f, 0.5f),
            };

            Assert.AreEqual(1, new EnemyShowcase().Next(Mob, pool),
                "a legacy skill at power 1.0 must not be mistaken for the basic attack");
        }

        // The showcase is a preview's opinion and must never be the default:
        // the fight the game plays keeps its weighted roll.
        [Test]
        public void AFightWithNoShowcaseStillRolls()
        {
            var session = new FightSession(
                new CombatEncounter(
                    new[] { new CombatantState("Hero", true, 50, 5, 10, 2) },
                    new[] { new CombatantState("Mob", false, 50, 0, 5, 1) }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(default(ResolvedEnemy), false) },
                new Domain.Rng.SeededRandom(11));

            Assert.IsNull(session.Showcase);
        }
    }
}
