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
    // CRITICAL HITS (PLAN_BJORN_CONSTELLATIONS Phase 1) -- CritRules, the
    // DamagePipeline crit step, the session's beat/log reporting, authored
    // enemy crits in the telegraph, and the bot's expected-value preview.
    //
    // Every expected figure is a LITERAL (CLAUDE.md gotcha 5): a test that
    // recomputed "damage x 1.5" through CritRules would agree with any bug in
    // it. Fixtures carry no defense, so AfterResistance passes the raw figure
    // through untouched and the literal is the crit arithmetic alone.
    public class CritTests
    {
        private static CombatantState Hero(int attack = 20) =>
            new CombatantState("Hero", true, 500, 100, attack, 10);

        private static CombatantState Foe(int attack = 20, int health = 1000, int speed = 9) =>
            new CombatantState("Foe", false, health, 10, attack, speed);

        private static ResolvedSkill Skill(SkillEffect effect, SkillTargeting targeting, int power = 100,
                                           string owner = "hero", int flatAmount = 0) =>
            new ResolvedSkill("test_" + effect, "Test " + effect, "", owner, 1, effect, targeting,
                0, 0, false, power, flatAmount, false,
                null, SpellPresentation.None, 0);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        private static FightSession Session(CombatantState hero, CombatantState foe, PlayerKit kit = null,
                                            List<EnemyKit> enemyKits = null, ulong seed = 3) =>
            new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                kit == null ? null : new List<PlayerKit> { kit }, enemyKits, new SeededRandom(seed))
            { DamageVarianceRange = 0f };

        private static DamagePipeline.Outcome Swing(int raw, CombatantState attacker, CombatantState target,
                                                    SeededRandom rng, bool? crit = null) =>
            DamagePipeline.AfterDefences(raw, attacker, target, attackType: null,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: rng, resolveWard: null,
                crit: crit);

        // ---- the rule's own numbers ------------------------------------------

        [Test]
        public void TheBaselineIsFivePercentForOneHundredFiftyPercent()
        {
            Assert.AreEqual(5, CritRules.BaseChancePercent);
            Assert.AreEqual(150, CritRules.BaseDamagePercent);
        }

        [Test]
        public void APartyMemberStartsAtTheBaseline_AnEnemyAtNothing()
        {
            var hero = Hero();
            var foe = Foe();

            Assert.AreEqual(5, hero.CritChancePercent);
            Assert.AreEqual(150, hero.CritDamagePercent);
            Assert.AreEqual(0, foe.CritChancePercent);
            Assert.AreEqual(0, CritRules.ChanceFor(foe));
        }

        [Test]
        public void ACritMultipliesAKnownHitByOneAndAHalf()
        {
            var hero = Hero();

            Assert.AreEqual(60, CritRules.Apply(40, hero));
            // 31.5 rounds away from zero, the damage path's one rounding rule.
            Assert.AreEqual(32, CritRules.Apply(21, hero));
        }

        [Test]
        public void ACritDamageStatMovesTheMultiplier_ButNeverBelowAPlainHit()
        {
            var hero = Hero();

            hero.CritDamagePercent = 200;
            Assert.AreEqual(80, CritRules.Apply(40, hero));

            hero.CritDamagePercent = 50;
            Assert.AreEqual(40, CritRules.Apply(40, hero));
        }

        // ---- the pipeline ----------------------------------------------------

        [Test]
        public void ThePipelineAppliesTheCritBeforeDefense_AndReportsIt()
        {
            var hero = Hero();
            hero.CritChancePercent = 100;

            var outcome = Swing(40, hero, Foe(), new SeededRandom(1));

            Assert.IsTrue(outcome.IsCrit);
            Assert.AreEqual(60, outcome.Damage);
        }

        [Test]
        public void ACritLandsOnTheOutgoingAmount_SoArmourCutsTheCritFigure()
        {
            // 41 raw -> 62 on the attack side (61.5 rounded away), THEN 100
            // physical defense halves it: 62 * 100 / 200 = 31. A crit on the
            // finished figure would read 41 * 100 / 200 = 20 -> 30 instead.
            var hero = Hero();
            hero.CritChancePercent = 100;
            var armoured = Foe();
            armoured.PhysicalDefense = 100;

            var outcome = Swing(41, hero, armoured, new SeededRandom(1));

            Assert.IsTrue(outcome.IsCrit);
            Assert.AreEqual(31, outcome.Damage);
        }

        [Test]
        public void NoChanceNoCrit()
        {
            var hero = Hero();
            hero.CritChancePercent = 0;

            var outcome = Swing(40, hero, Foe(), new SeededRandom(1));

            Assert.IsFalse(outcome.IsCrit);
            Assert.AreEqual(40, outcome.Damage);
        }

        [Test]
        public void TheRollRidesTheSeededGenerator_SameSeedSameCrits()
        {
            var hero = Hero();
            hero.CritChancePercent = 50;

            List<bool> Run(ulong seed)
            {
                var rng = new SeededRandom(seed);
                return Enumerable.Range(0, 200).Select(_ => Swing(40, hero, Foe(), rng).IsCrit).ToList();
            }

            var first = Run(11);
            CollectionAssert.AreEqual(first, Run(11), "the same seed must crit on the same swings");
            Assert.That(first.Count(c => c), Is.InRange(1, 199), "a 50% roll must actually vary");
            CollectionAssert.AreNotEqual(first, Run(12), "a different seed is a different fight");
        }

        [Test]
        public void APreviewNeverRollsACrit()
        {
            var hero = Hero();
            hero.CritChancePercent = 100;

            var outcome = Swing(40, hero, Foe(), rng: null);

            Assert.IsFalse(outcome.IsCrit, "rng == null is a preview; it must not crit nor draw");
            Assert.AreEqual(40, outcome.Damage);
        }

        [Test]
        public void AnEnemyNeverRollsACrit_AndSpendsNoDraw()
        {
            var foe = Foe();
            foe.CritChancePercent = 100; // ignored: monsters never crit at random

            var rng = new SeededRandom(5);
            for (int i = 0; i < 50; i++)
            {
                var outcome = Swing(40, foe, Hero(), rng);
                Assert.IsFalse(outcome.IsCrit);
                Assert.AreEqual(40, outcome.Damage);
            }

            Assert.AreEqual(new SeededRandom(5).State, rng.State, "a monster's swing must not touch the stream");
        }

        [Test]
        public void AnAuthoredCritIsCertain_EvenInAPreview_AndSpendsNoDraw()
        {
            var foe = Foe();
            var rng = new SeededRandom(5);

            var live = Swing(40, foe, Hero(), rng, crit: true);
            var preview = Swing(40, foe, Hero(), rng: null, crit: true);

            Assert.IsTrue(live.IsCrit);
            Assert.AreEqual(60, live.Damage);
            Assert.IsTrue(preview.IsCrit);
            Assert.AreEqual(60, preview.Damage);
            Assert.AreEqual(new SeededRandom(5).State, rng.State);
        }

        [Test]
        public void AMissNeverCrits()
        {
            var hero = Hero();
            hero.CritChancePercent = 100;
            var foe = Foe();
            foe.ModifierEffects = new ModifierEffectSet(new[] { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

            var outcome = Swing(40, hero, foe, new SeededRandom(3));

            Assert.IsTrue(outcome.IsMiss);
            Assert.IsFalse(outcome.IsCrit);
        }

        // ---- the session: beat, log, heals -------------------------------------

        [Test]
        public void APartySwingThatCrits_DealsOneAndAHalf_AndFlagsTheBeat()
        {
            // Attack 20 x the 1.2 basic-attack coefficient = 24 raw; a crit is 36.
            var hero = Hero(attack: 20);
            hero.CritChancePercent = 100;
            var foe = Foe();

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.AreEqual(964, foe.CurrentHealth);
            var beat = session.DrainBeats().First(b => b.Actor == hero);
            Assert.IsTrue(beat.Crit);
            Assert.AreEqual(36, beat.Amount);
            Assert.Contains(FightSession.CritLine, beat.Messages.ToList());
        }

        [Test]
        public void APartySwingThatDoesNotCrit_IsTheOldSwing()
        {
            var hero = Hero(attack: 20);
            hero.CritChancePercent = 0;
            var foe = Foe();

            var session = Session(hero, foe);
            session.ExecuteAttack(foe);

            Assert.AreEqual(976, foe.CurrentHealth);
            var beat = session.DrainBeats().First(b => b.Actor == hero);
            Assert.IsFalse(beat.Crit);
            Assert.IsFalse(beat.Messages.Contains(FightSession.CritLine));
        }

        [Test]
        public void ASweepRecordsEachTargetsCrit()
        {
            var hero = Hero(attack: 20);
            hero.CritChancePercent = 100;
            var a = Foe();
            var b = new CombatantState("Foe B", false, 1000, 10, 20, 9);
            var sweep = Skill(SkillEffect.DamageAll, SkillTargeting.AllEnemies);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { a, b }),
                new List<PlayerKit> { new PlayerKit("hero", CharacterRole.Tank, new[] { sweep }, null, null) },
                null, new SeededRandom(3)) { DamageVarianceRange = 0f };
            Assert.IsTrue(session.CastSkill(0, a));

            var beat = session.DrainBeats().First(x => x.Actor == hero);
            Assert.IsTrue(beat.HasPerTargetResults);
            Assert.IsTrue(beat.Results.All(r => r.Crit), "every target of a 100% sweep crits");
        }

        [Test]
        public void HealsNeverCrit()
        {
            int HealedBy(int chance)
            {
                var hero = Hero();
                hero.CritChancePercent = chance;
                hero.CurrentHealth = 100;
                var heal = Skill(SkillEffect.HealSelf, SkillTargeting.Self, flatAmount: 40);
                var session = Session(hero, Foe(),
                    new PlayerKit("hero", CharacterRole.Tank, new[] { heal }, null, null));
                Assert.IsTrue(session.CastSkill(0, hero));

                // Read off the heal's own beat, not the health bar: the foe
                // swings back before the cast returns.
                var beat = session.DrainBeats().First(x => x.Actor == hero);
                Assert.IsTrue(beat.IsHealing);
                Assert.IsFalse(beat.Crit, "a heal beat never reads as a crit");
                return beat.Amount;
            }

            Assert.AreEqual(HealedBy(0), HealedBy(100), "a 100% crit chance must not move a heal");
            Assert.AreEqual(40, HealedBy(100), "the flat 40 lands whole, never 60");
        }

        // ---- enemies: authored crits and the telegraph --------------------------

        private static (FightSession session, CombatantState hero, CombatantState foe) EnemyFight(
            EnemyAbility ability, int foeAttack = 20)
        {
            var hero = Hero();
            hero.CritChancePercent = 0;
            var foe = Foe(attack: foeAttack);
            var kits = new List<EnemyKit> { new EnemyKit(Source("brute"), false, new List<EnemyAbility> { ability }) };
            var session = Session(hero, foe, enemyKits: kits);
            session.Begin();
            return (session, hero, foe);
        }

        [Test]
        public void AnAuthoredCritSwing_IsTelegraphed_AndLandsExactlyAsShown()
        {
            // Attack 20, no basic coefficient for monsters: 20 raw, 30 on a crit.
            var (session, hero, foe) = EnemyFight(
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f, crits: true));

            var intent = session.IntentDetailFor(foe).Value;
            Assert.IsTrue(intent.WillCrit);
            Assert.AreEqual(30, intent.ExpectedDamage);

            session.ExecuteAttack(foe);

            Assert.AreEqual(470, hero.CurrentHealth);
            var beat = session.DrainBeats().First(b => b.Actor == foe);
            Assert.IsTrue(beat.Crit);
        }

        [Test]
        public void AnUnauthoredEnemy_NeverCrits_WhateverItsChance()
        {
            var (session, hero, foe) = EnemyFight(
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f));
            foe.CritChancePercent = 100;

            var intent = session.IntentDetailFor(foe).Value;
            Assert.IsFalse(intent.WillCrit);
            Assert.AreEqual(20, intent.ExpectedDamage);

            session.ExecuteAttack(foe);

            Assert.AreEqual(480, hero.CurrentHealth);
            Assert.IsFalse(session.DrainBeats().Any(b => b.Actor == foe && b.Crit));
        }

        [Test]
        public void AnAuthoredCritSkill_IsTelegraphed_AndLandsExactlyAsShown()
        {
            var slam = Skill(SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, owner: "brute");
            var (session, hero, foe) = EnemyFight(EnemyAbility.Of(slam, 1f, crits: true));

            var intent = session.IntentDetailFor(foe).Value;
            Assert.IsTrue(intent.WillCrit);
            int expected = intent.ExpectedDamage;

            session.ExecuteAttack(foe);

            Assert.AreEqual(500 - expected, hero.CurrentHealth, "the telegraph is the blow, crit included");
            Assert.IsTrue(session.DrainBeats().Any(b => b.Actor == foe && b.Crit));
        }

        [Test]
        public void TheSameSkillUnauthored_TelegraphsTwoThirdsOfTheCrit()
        {
            // The pair pins the skill arm's multiplier without re-deriving the
            // skill formula: the authored intent is exactly 1.5x the plain one.
            var slam = Skill(SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, owner: "brute");
            var authored = EnemyFight(EnemyAbility.Of(slam, 1f, crits: true));
            var plainFight = EnemyFight(EnemyAbility.Of(slam, 1f));

            int withCrit = authored.session.IntentDetailFor(authored.foe).Value.ExpectedDamage;
            int without = plainFight.session.IntentDetailFor(plainFight.foe).Value.ExpectedDamage;

            Assert.AreEqual(20, without);
            Assert.AreEqual(30, withCrit);
        }

        [Test]
        public void AGatedAbilityKeepsItsCrit()
        {
            var ability = EnemyAbility.LegacyAttack("Cleave", 2f, 1f, crits: true);

            var gated = ability.WithWeight(0f);

            Assert.AreEqual(0f, gated.Weight);
            Assert.IsTrue(gated.Crits);
            Assert.AreEqual(2f, gated.Power);
            Assert.AreEqual("Cleave", gated.Label);
        }

        // ---- the bot: expected value ---------------------------------------------

        [Test]
        public void ThePreviewCountsAPartyCritAsExpectedValue()
        {
            // 100 x (1 + 5% x 50%) = 102.5 -> 103.
            Assert.AreEqual(103, CritRules.ExpectedDamage(100, Hero()));

            var keen = Hero();
            keen.CritChancePercent = 20;
            keen.CritDamagePercent = 200;
            // 100 x (1 + 20% x 100%) = 120.
            Assert.AreEqual(120, CritRules.ExpectedDamage(100, keen));
        }

        [Test]
        public void AnEnemysPreviewCarriesNoExpectedCrit()
        {
            var foe = Foe();
            foe.CritChancePercent = 100;

            Assert.AreEqual(100, CritRules.ExpectedDamage(100, foe));
        }

        [Test]
        public void TheBotsAttackPreviewIsTheSwingPlusItsExpectedCrit()
        {
            // 24 raw (20 x 1.2) x 1.025 = 24.6 -> 25; a 0% hero previews the raw 24.
            var hero = Hero(attack: 20);
            Assert.AreEqual(25, FightAction.PreviewAttackDamage(hero, Foe()));

            hero.CritChancePercent = 0;
            Assert.AreEqual(24, FightAction.PreviewAttackDamage(hero, Foe()));
        }
    }
}
