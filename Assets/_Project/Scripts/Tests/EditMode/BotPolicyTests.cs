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

        // ---- GreedyDefensivePolicy ---------------------------------------

        private static (FightSession session, CombatantState hero, List<CombatantState> foes) HeroVsManyWithSkills(
            IReadOnlyList<ResolvedSkill> skills, params int[] foeHealths)
        {
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foes = foeHealths
                .Select((hp, i) => Fighter($"Foe{i}", false, maxHealth: hp, attack: 1, speed: 1))
                .ToList();

            var encounter = new CombatEncounter(new[] { hero }, foes);
            var basicSpell = new ResolvedSpellTier(1, "Bolt", 0, 1f, 0);
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical, basicSpell);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foes);
        }

        private static ResolvedSkill WardSkill() =>
            new ResolvedSkill("ward", "Fleece Ward", "", "hero", 1, SkillEffect.Ward,
                SkillTargeting.Self, 0, 0, false, 100, 0, false, null, SpellPresentation.None, 0);

        private static ResolvedSkill HealSelfSkill() =>
            new ResolvedSkill("mend", "Mend", "", "hero", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 50, 0, false, null, SpellPresentation.None, 0);

        [Test]
        public void GreedyDefensivePolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new GreedyDefensivePolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyDefensivePolicy_BelowHealthThreshold_HealsOverAttacking()
        {
            var (session, hero, foes) = HeroVsMany(60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.4f); // under GreedyDefensive's 50% line
            var policy = new GreedyDefensivePolicy();
            var satchel = new List<SatchelStack> { new SatchelStack("potion", "Potion", 3, false) };
            var legal = FightAction.LegalActions(session, hero, satchel);

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Item, chosen.Kind);
            Assert.AreEqual("potion", chosen.ItemId);
        }

        [Test]
        public void GreedyDefensivePolicy_HealSkillReady_HealsOverAttackingEvenWithNoItem()
        {
            var skills = new List<ResolvedSkill> { HealSelfSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.4f);
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Skill, chosen.Kind);
            var option = session.SkillOptionsFor(hero).First(o => o.Index == chosen.SkillIndex);
            Assert.AreEqual(SkillEffect.HealSelf, option.Skill.Effect);
        }

        [Test]
        public void GreedyDefensivePolicy_AtFullHealthWithAWardReady_UsesTheWardBeforeAttacking()
        {
            var skills = new List<ResolvedSkill> { WardSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 60);
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Skill, chosen.Kind);
            var option = session.SkillOptionsFor(hero).First(o => o.Index == chosen.SkillIndex);
            Assert.AreEqual(SkillEffect.Ward, option.Skill.Effect);
        }

        [Test]
        public void GreedyDefensivePolicy_WithNoHealOrWardToTake_TargetsTheMoreThreateningEnemy()
        {
            // foe0: low HP, harmless (Attack 1). foe1: high HP, dangerous
            // (Attack 100) and reachable only via the basic spell (Attack is
            // front-rank-only). GreedyAggressive would target foe0 for being
            // lowest-HP; GreedyDefensive must target foe1 for being the
            // bigger threat instead.
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foe0 = Fighter("Foe0", false, maxHealth: 10, attack: 1, speed: 1);
            var foe1 = Fighter("Foe1", false, maxHealth: 200, attack: 100, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe0, foe1 });
            var basicSpell = new ResolvedSpellTier(1, "Bolt", 0, 1f, 0);
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical, basicSpell);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foe1, chosen.Target,
                "foe1's higher Attack makes it the bigger threat despite foe0 having lower HP");
        }

        // ---- Lookahead2Policy ----------------------------------------------

        [Test]
        public void Lookahead2Policy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new Lookahead2Policy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void Lookahead2Policy_WhenOneOptionKillsAndAnotherOnlyChips_PicksTheKill()
        {
            // foe0 (front rank, 500 HP): reachable by Attack or the basic
            // spell, but neither kills it. foe1 (back rank, 1 HP): reachable
            // only by the basic spell, and any positive hit kills it. The
            // kill must win even though it is not the biggest single number
            // on the menu.
            var (session, hero, foes) = HeroVsMany(500, 1);
            var policy = new Lookahead2Policy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foes[1], chosen.Target, "the lethal hit on the 1-HP foe must beat any chip damage on the 500-HP one");
        }

        [Test]
        public void Lookahead2Policy_SameSeedTwice_PicksTheSameAction()
        {
            var (sessionA, heroA, foesA) = HeroVsMany(200, 40);
            var (sessionB, heroB, foesB) = HeroVsMany(200, 40);
            var policy = new Lookahead2Policy();
            var legalA = FightAction.LegalActions(sessionA, heroA, System.Array.Empty<SatchelStack>());
            var legalB = FightAction.LegalActions(sessionB, heroB, System.Array.Empty<SatchelStack>());

            var chosenA = policy.Choose(sessionA, heroA, legalA, new SeededRandom(7));
            var chosenB = policy.Choose(sessionB, heroB, legalB, new SeededRandom(7));

            Assert.AreEqual(chosenA.Kind, chosenB.Kind);
            Assert.AreEqual(chosenA.Target?.Name, chosenB.Target?.Name);
        }

        // ---- Archetypes.Create ----------------------------------------------

        [Test]
        public void Archetypes_Create_KnowsAllFourNames()
        {
            foreach (string name in Archetypes.Names)
            {
                var made = Archetypes.Create(name);
                Assert.IsNotNull(made, $"'{name}' is listed in Archetypes.Names but Create returned null");
                Assert.IsInstanceOf<IFightPolicy>(made);
                Assert.IsInstanceOf<IRunPolicy>(made);
            }
        }

        [Test]
        public void Archetypes_Create_AnUnknownName_ReturnsNull()
        {
            Assert.IsNull(Archetypes.Create("NotAnArchetype"));
        }
    }
}
