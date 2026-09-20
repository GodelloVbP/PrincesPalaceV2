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
    // THE NEW-DOT MODEL (plan 1.5) AND TICKREPORT'S TYPED ROWS (plan D5,
    // closing AUDIT #188), milestone E. Burn is the vehicle for every test
    // that is about the MODEL rather than about Thorn Tithe's own second
    // damage moment -- see ThornTitheTests for the post-action hook.
    //
    // Existing Poison is untouched by any of this (plan 1.5's own opening
    // line) and StatusEffectsTests already owns its coverage; nothing here
    // re-pins Poison's own arithmetic, only the two places it now shares code
    // with Burn/Thorned -- MitigationOf(Poison) == None and the grouped
    // TickReport rows.
    public class NewDotTests
    {
        private static CombatantState Holder(int maxHealth = 1000) =>
            new CombatantState("Holder", false, maxHealth, 0, 5, 5);

        private static CombatantState Caster(string name = "Caster") =>
            new CombatantState(name, true, 200, 50, 10, 10);

        // ---- 1.5: the snapshot, taken once, ignoring the caster afterwards ----

        // NOT ROUTED THROUGH A CAST: the claim under test is Tick's own
        // arithmetic, not FightSession's snapshot computation (that half is
        // TwoCastersDifferentStrengths_BothInstancesTickAtTheirOwnSnapshot,
        // below). ActiveStatus.Magnitude IS the stored snapshot (1.5), so
        // this builds one directly, mutates the Source's stats it would have
        // to re-read if it were being re-derived, and ticks -- the figure
        // must not move.
        [Test]
        public void ABurnTickIsUnchangedWhenTheCastersAttackDoublesAfterApplication()
        {
            var target = Holder();
            var caster = Caster();
            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 8, 3, caster));

            var before = StatusEffects.Tick(target);
            Assert.AreEqual(8, before.Rows.Single(r => r.Status == StatusEffectType.Burn).ToHealth);

            // The caster's own potency, whatever it is made of, doubles --
            // Attack is the one stat every damage formula in this codebase
            // reads, so doubling it is the most direct available proxy for
            // "got stronger after casting".
            caster.Attack *= 2;

            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 8, 3, caster));
            var after = StatusEffects.Tick(target);

            // Two instances now (this one plus the fresh one just added), and
            // NEITHER reads 16 -- the first is exactly what it always was,
            // and the second is a fresh snapshot at the SAME authored 8
            // because Tick never asks the caster anything at all.
            int total = after.Rows.Single(r => r.Status == StatusEffectType.Burn).ToHealth;
            Assert.AreEqual(16, total, "two live instances of 8 each -- neither doubled by the caster's change");
        }

        // THE SESSION-LEVEL HALF: the snapshot computation itself
        // (SkillPowerMultiplierFor * SpellScalingMultiplierFor, plan 1.5),
        // which lives on FightSession.ApplyStatusTo rather than on
        // StatusEffects.Tick. Two casters with different kit multipliers
        // land two DIFFERENT snapshots on the same target, and neither is
        // floored by the other (D3's own words for this contract).
        [Test]
        public void TwoCastersDifferentStrengths_BothInstancesTickAtTheirOwnSnapshot()
        {
            var weak = new CombatantState("Weak", true, 100, 50, 10, 10);
            var strong = new CombatantState("Strong", true, 100, 50, 10, 10);
            var foe = new CombatantState("Foe", false, 1000, 0, 5, 5);

            var session = new FightSession(
                new CombatEncounter(new[] { weak, strong }, new[] { foe }),
                new List<PlayerKit>
                {
                    new PlayerKit("weak", CharacterRole.Support, null, null, null, skillPowerMultiplier: 1f),
                    new PlayerKit("strong", CharacterRole.Support, null, null, null, skillPowerMultiplier: 3f),
                },
                new List<EnemyKit> { new EnemyKit(
                    new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Burn, 4, 3, weak);
            session.ApplyStatusToForTest(foe, StatusEffectType.Burn, 4, 3, strong);

            var magnitudes = StatusEffects.InstancesOf(foe, StatusEffectType.Burn)
                .Select(s => s.Magnitude).OrderBy(m => m).ToList();

            CollectionAssert.AreEqual(new[] { 4, 12 }, magnitudes,
                "the weak caster's 4x1 and the strong caster's 4x3 must both stand, neither floored by the other");
        }

        // ---- 1.5: affinity applies, and only affinity -------------------------

        [Test]
        public void ABurnTickOnAFireWeakHolder_DealsOneAndAHalfTimesItsSnapshot()
        {
            var foe = new EnemyKit(new ResolvedEnemy("fireweak", "Fire-weak", new StatBlock(), 0, 0, false,
                DamageType.Fire, DamageType.Physical, 0), false);
            var target = Holder();
            var session = new FightSession(
                new CombatEncounter(new[] { new CombatantState("Hero", true, 100, 0, 10, 10) }, new[] { target }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { foe },
                new SeededRandom(2)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(target, StatusEffectType.Burn, 10, 3, null);
            session.TickStatusesForTest(target);

            Assert.AreEqual(985, target.CurrentHealth,
                "10 magnitude at 1.5x weakness, rounded away from zero, is 15");
        }

        [Test]
        public void ABurnTickIgnoresTheHoldersMagicalDefence()
        {
            var target = Holder();
            target.MagicalDefense = 999999;
            target.PhysicalDefense = 999999;
            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 12, 3));

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(12, report.Rows.Single(r => r.Status == StatusEffectType.Burn).ToHealth,
                "a tick's stored Magnitude reaches health untouched -- defense is a DamagePipeline concern " +
                "and a tick never enters that funnel (MitigationOf == AffinityOnly, never a defense term)");
        }

        // ---- 1.5: attribution survives the caster's death ----------------------

        [Test]
        public void ABurnAppliedByACasterWhoThenDies_StillTicksAndStillNamesItsSource()
        {
            var target = Holder();
            var caster = Caster();
            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 6, 3, caster));

            caster.CurrentHealth = 0;
            Assert.IsFalse(caster.IsAlive, "fixture: the caster is dead");

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(6, report.Rows.Single(r => r.Status == StatusEffectType.Burn).ToHealth,
                "a dead caster's tick still lands -- nothing on the tick path gates on Source.IsAlive");
            Assert.AreSame(caster, StatusEffects.InstancesOf(target, StatusEffectType.Burn).Single().Source,
                "the status still names its source after the source has died");
        }

        [Test]
        public void ABurnTickThatKillsSettlesWithNobody()
        {
            var target = new CombatantState("Frail", false, 5, 0, 5, 5);
            var caster = Caster();
            var session = new FightSession(
                new CombatEncounter(new[] { caster }, new[] { target }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(
                    new ResolvedEnemy("frail", "Frail", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();

            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 50, 3, caster));
            session.TickStatusesForTest(target);

            Assert.IsFalse(target.IsAlive, "fixture: the tick must kill outright");
            Assert.AreEqual(1, session.Ledger.For("frail").TimesDowned,
                "the death is still settled and counted");
            Assert.AreEqual(0, session.Ledger.For(caster.Name).Kills,
                "but credited to Nobody -- the same answer Poison's own tick death already gives");
        }

        // ---- D5: typed rows, one per damaging status type per tick ------------

        [Test]
        public void OneTick_CarryingBurnAndPoison_ReportsBothRowsSeparately()
        {
            var target = Holder();
            target.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 7, 3));
            target.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 4, 3));

            var report = StatusEffects.Tick(target);

            Assert.AreEqual(2, report.Rows.Count, "two damage types, two rows -- never folded into one");
            var poison = report.Rows.Single(r => r.Status == StatusEffectType.Poison);
            var burn = report.Rows.Single(r => r.Status == StatusEffectType.Burn);
            Assert.AreEqual(DamageType.Poison, poison.Element);
            Assert.AreEqual(7, poison.ToHealth);
            Assert.AreEqual(DamageType.Fire, burn.Element);
            Assert.AreEqual(4, burn.ToHealth);
        }
    }
}
