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
    // RandomLegalPolicy and GreedyAggressivePolicy over FightSession.
    public class BotPolicyTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static (FightSession session, CombatantState hero, List<CombatantState> foes) HeroVsMany(
            params int[] foeHealths)
        {
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foes = foeHealths
                .Select((hp, i) => Fighter($"Foe{i}", false, maxHealth: hp, attack: 1, speed: 1))
                .ToList();

            var encounter = new CombatEncounter(new[] { hero }, foes);
            // A free basic spell (0 mana) so a target behind the front rank
            // is reachable at all -- Attack is front-rank-only (see
            // CombatEncounter.CanMeleeReach), and with more than one foe
            // that is the only way this fixture can show GreedyAggressive
            // choosing BETWEEN targets rather than being handed just one.
            var basicSpell = new ResolvedSpellTier(1, "Bolt", 0, 1f, 0);
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical, basicSpell);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foes);
        }

        [Test]
        public void RandomLegalPolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new RandomLegalPolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyAggressivePolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new GreedyAggressivePolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyAggressivePolicy_WithTwoReachableFoes_TargetsTheLowerHpOne()
        {
            // foes[0] (200 HP) is the front rank, reachable by Attack or the
            // basic spell; foes[1] (40 HP) sits behind it and is reachable
            // only by the basic spell (Attack is front-rank-only -- see
            // CombatEncounter.CanMeleeReach). GreedyAggressive must still
            // pick the lower-HP one across both kinds of reach.
            var (session, hero, foes) = HeroVsMany(200, 40);
            var policy = new GreedyAggressivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foes[1], chosen.Target, "the 40-HP foe is lower than the 200-HP one and should be targeted first");
        }

        [Test]
        public void GreedyAggressivePolicy_BelowHealthThreshold_UsesAHealingItemOverAttacking()
        {
            var (session, hero, foes) = HeroVsMany(60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.2f);
            var policy = new GreedyAggressivePolicy();
            var satchel = new List<SatchelStack> { new SatchelStack("potion", "Potion", 3, false) };
            var legal = FightAction.LegalActions(session, hero, satchel);

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Item, chosen.Kind);
            Assert.AreEqual("potion", chosen.ItemId);
        }
    }
}
