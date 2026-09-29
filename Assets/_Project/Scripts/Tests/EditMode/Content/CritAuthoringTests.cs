using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // CRITICAL HITS, the authored half (PLAN_BJORN_CONSTELLATIONS Phase 1):
    // the two party stats on StatBlock, and the two enemy authoring fields
    // (RawEnemyEntry.attackCrits, RawEnemyAbility.crits) reaching the resolved
    // record and the kit. The combat rule itself is CritTests'.
    public class CritAuthoringTests
    {
        // ---- the party stats ---------------------------------------------------

        [Test]
        public void TheCritStatsAreAppendedAfterMagicalDefense()
        {
            // Content stores StatType as an ordinal: appended, never inserted.
            Assert.AreEqual(5, (int)StatType.MagicalDefense);
            Assert.AreEqual(6, (int)StatType.CritChance);
            Assert.AreEqual(7, (int)StatType.CritDamage);
        }

        [Test]
        public void TheCritStatsRideTheIndexerAndSum()
        {
            var a = new StatBlock(0, 0, 0, critChance: 3, critDamage: 25);
            var b = StatBlock.ForStat(StatType.CritChance, 2) + StatBlock.ForStat(StatType.CritDamage, 10);

            var sum = a + b;

            Assert.AreEqual(5, sum[StatType.CritChance]);
            Assert.AreEqual(35, sum[StatType.CritDamage]);
        }

        [Test]
        public void ScalingABodyLeavesItsCritStatsAlone()
        {
            var block = new StatBlock(100, 10, 10, critChance: 4, critDamage: 20);

            var elite = block.ScaledForElite(1.4f, 1.15f, 1.15f);
            var scaled = block.Scaled(2f);

            Assert.AreEqual(4, elite.critChance);
            Assert.AreEqual(20, elite.critDamage);
            Assert.AreEqual(4, scaled.critChance);
            Assert.AreEqual(20, scaled.critDamage);
        }

        [Test]
        public void ThePartyTotalsAreTheBaselinePlusTheBonus()
        {
            Assert.AreEqual(5, CritRules.PartyChancePercent(StatBlock.Zero));
            Assert.AreEqual(150, CritRules.PartyDamagePercent(StatBlock.Zero));

            var keen = new StatBlock(0, 0, 0, critChance: 10, critDamage: 25);
            Assert.AreEqual(15, CritRules.PartyChancePercent(keen));
            Assert.AreEqual(175, CritRules.PartyDamagePercent(keen));
        }

        [Test]
        public void ThePartyTotalsAreClamped()
        {
            Assert.AreEqual(100, CritRules.PartyChancePercent(new StatBlock(0, 0, 0, critChance: 400)));
            Assert.AreEqual(0, CritRules.PartyChancePercent(new StatBlock(0, 0, 0, critChance: -40)));
            Assert.AreEqual(100, CritRules.PartyDamagePercent(new StatBlock(0, 0, 0, critDamage: -90)));
        }

        // ---- the enemy authoring fields ------------------------------------------

        private static RawEnemyEntry Brute(bool attackCrits = false, float attackWeight = 1f, bool abilityCrits = false) =>
            new RawEnemyEntry
            {
                id = "brute", displayName = "Brute", maxHealth = 150,
                attackWeight = attackWeight,
                attackCrits = attackCrits,
                abilities = new[] { new RawEnemyAbility { skillId = "slam", weight = 1f, crits = abilityCrits } },
            };

        private static bool TryResolve(RawEnemyEntry entry, out ResolvedEnemy enemy, out List<string> errors)
        {
            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out errors);
            enemy = ok ? resolved[0] : null;
            return ok;
        }

        [Test]
        public void NothingAuthoredMeansNoCrit()
        {
            Assert.IsTrue(TryResolve(Brute(), out var enemy, out var errors), string.Join("; ", errors));

            Assert.IsFalse(enemy.AttackCrits);
            Assert.IsFalse(enemy.Abilities[0].Crits);
        }

        [Test]
        public void BothAuthoringFieldsReachTheResolvedRecord()
        {
            Assert.IsTrue(TryResolve(Brute(attackCrits: true, abilityCrits: true), out var enemy, out var errors),
                string.Join("; ", errors));

            Assert.IsTrue(enemy.AttackCrits);
            Assert.IsTrue(enemy.Abilities[0].Crits);
        }

        [Test]
        public void ACritOnAPlainAttackThatNeverSwingsIsRefused()
        {
            Assert.IsFalse(TryResolve(Brute(attackCrits: true, attackWeight: 0f), out _, out var errors));
            StringAssert.Contains("attackCrits", errors[0]);
        }

        [Test]
        public void TheLegacyPoolsPlainSwingCarriesTheAuthoredCrit()
        {
            var source = new ResolvedEnemy("brute", "Brute", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0) { AttackCrits = true };

            var kit = new EnemyKit(source, false);

            Assert.IsTrue(kit.Abilities[0].IsPlainSwing);
            Assert.IsTrue(kit.Abilities[0].Crits);
        }

        // ---- relics and the telegraph ----------------------------------------------

        [Test]
        public void TheRelicCritModifiersMapToTheirOwnStats()
        {
            Assert.AreEqual(RelicStat.CritChance, new RelicModifier(RelicModifierType.CritChanceFlat, 5).Stat);
            Assert.AreEqual(RelicStat.CritDamage, new RelicModifier(RelicModifierType.CritDamageFlat, 5).Stat);
            Assert.AreEqual(12, RelicModifiers.Apply(0, RelicStat.CritChance,
                new[] { new RelicModifier(RelicModifierType.CritChanceFlat, 5), new RelicModifier(RelicModifierType.CritChanceFlat, 7) }));
        }

        [Test]
        public void ARelicBonusIsClampedWithTheRestOfThePartyTotal()
        {
            Assert.AreEqual(100, CritRules.PartyChancePercent(StatBlock.Zero, relicBonus: 500));
            Assert.AreEqual(175, CritRules.PartyDamagePercent(StatBlock.Zero, relicBonus: 25));
        }

        [Test]
        public void ACritTelegraphSaysCriticalInTheTooltip()
        {
            var target = new CombatantState("Shawn", true, 300, 30, 10, 10);
            var crit = new EnemyIntent("Attack", EnemyIntentKind.Attack, target, 36, willCrit: true);
            var plain = new EnemyIntent("Attack", EnemyIntentKind.Attack, target, 24);

            StringAssert.Contains("for about 36 damage (critical)", FightHudModel.IntentTooltip("Brute", crit));
            StringAssert.DoesNotContain("critical", FightHudModel.IntentTooltip("Brute", plain));
        }
    }
}
