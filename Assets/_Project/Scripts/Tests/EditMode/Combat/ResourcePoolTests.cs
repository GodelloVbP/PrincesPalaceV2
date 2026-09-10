using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // THE POOL ITSELF, with no fight around it.
    //
    // SignatureResourceTests already covers the signature SLOT through a real
    // session -- how Wool is granted, spent and soaked. This covers the
    // arithmetic every pool shares, including the half nothing shipped
    // authors yet: the idle decay. A rule that is inert in content is exactly
    // the rule a behavioural test cannot reach, so it is pinned here with
    // fixture numbers rather than left to the day Fury lands.
    //
    // Every expected value is a LITERAL. Nothing here recomputes the
    // production arithmetic to build its own answer (CLAUDE.md gotcha 5).
    public class ResourcePoolTests
    {
        private static ResolvedPool Row(
            int capacity = 100,
            int gainPerTurn = 0, int gainOnAttack = 0, int gainOnDamageTaken = 0,
            int decayPerIdleTurn = 0,
            PoolDecayTrigger decayUnless = PoolDecayTrigger.Damage,
            PoolStartRule startRule = PoolStartRule.Zero, int startValue = 0,
            bool restoredByManaEffects = true,
            PoolCapacityRule capacityRule = PoolCapacityRule.Fixed) =>
            new ResolvedPool("fixture", "Fixture", "FIX",
                capacityRule, capacity,
                gainPerTurn, gainOnAttack, gainOnDamageTaken,
                decayPerIdleTurn, decayUnless,
                startRule, startValue,
                "#FFFFFF", "#000000", "#CCCCCC",
                false, true, restoredByManaEffects, false,
                0);

        // ---- what a pool opens a fight holding ---------------------------------

        [Test]
        public void TheStartRuleDecidesWhatThePoolOpensWith()
        {
            Assert.AreEqual(100, new ResourcePool(Row(startRule: PoolStartRule.Full), 100, 0).Current,
                "Full is mana: a budget, handed over whole");
            Assert.AreEqual(0, new ResourcePool(Row(startRule: PoolStartRule.Zero), 100, 0).Current,
                "Zero is a rage bar: an arc, earned from nothing");
            Assert.AreEqual(35, new ResourcePool(Row(startRule: PoolStartRule.Value, startValue: 35), 100, 0).Current);

            // A start value bigger than the pool is the pool, not an
            // overfilled counter that the first Gain would silently correct.
            Assert.AreEqual(100, new ResourcePool(Row(startRule: PoolStartRule.Value, startValue: 400), 100, 0).Current);
        }

        [Test]
        public void ThePoolCopiesItsDefinitionRatherThanHoldingIt()
        {
            var row = Row(gainOnAttack: 15, gainOnDamageTaken: 10, decayPerIdleTurn: 10,
                restoredByManaEffects: false);
            var pool = new ResourcePool(row, 100, 3);

            Assert.AreEqual("fixture", pool.Id);
            Assert.AreEqual("Fixture", pool.DisplayName);
            Assert.AreEqual("FIX", pool.ShortTag);
            Assert.AreEqual(100, pool.Max);
            Assert.AreEqual(3, pool.GainPerTurn);
            Assert.AreEqual(15, pool.GainOnAttack);
            Assert.AreEqual(10, pool.GainOnDamageTaken);
            Assert.AreEqual(10, pool.DecayPerIdleTurn);
            Assert.IsFalse(pool.RestoredByManaEffects);

            // A ResolvedPool is SHARED by every fight that fields the pool --
            // ContentDatabase hands out one instance for the whole session --
            // so a per-fight counter must not be one field access away from
            // the catalogue. Spending the pool must not touch the row.
            pool.Gain(40);
            pool.TrySpend(10);
            Assert.AreEqual(100, row.Capacity, "the fight edited the catalogue");
            Assert.AreEqual(0, row.StartValue);
        }

        // ---- gain and clamp ----------------------------------------------------

        [Test]
        public void GainReportsWhatLandedAndNeverExceedsCapacity()
        {
            var pool = new ResourcePool(Row(), 100, 0);

            Assert.AreEqual(15, pool.Gain(15));
            Assert.AreEqual(15, pool.Current);

            Assert.AreEqual(85, pool.Gain(400), "the clamp is reported, not hidden");
            Assert.AreEqual(100, pool.Current);
            Assert.IsTrue(pool.IsFull);

            Assert.AreEqual(0, pool.Gain(1), "a full pool gains nothing further");
            Assert.AreEqual(0, pool.Gain(-5), "and a negative gain is not a spend");
            Assert.AreEqual(100, pool.Current);
        }

        [Test]
        public void SpendingIsAllOrNothingForACostAndClampedForADrain()
        {
            var pool = new ResourcePool(Row(), 100, 0);
            pool.Gain(20);

            Assert.IsFalse(pool.TrySpend(21), "an ability fires at full price or not at all");
            Assert.AreEqual(20, pool.Current, "a refused cost must leave the pool untouched");

            Assert.IsTrue(pool.TrySpend(20));
            Assert.AreEqual(0, pool.Current);

            pool.Gain(6);
            Assert.AreEqual(6, pool.SpendUpTo(1000), "a drain takes whatever is there and says how much");
            Assert.AreEqual(0, pool.Current, "and never goes below zero");
        }

        // ---- the idle decay ----------------------------------------------------

        [Test]
        public void ThePerTurnGainAppliesBeforeTheIdleDecay()
        {
            // Ten in, ten out, on a turn that was idle: the net is zero
            // rather than a decay applied to a number the gain was about to
            // replace. Order is the authored one (the plan's P4).
            var pool = new ResourcePool(Row(gainPerTurn: 10, decayPerIdleTurn: 10), 100, 10);
            pool.Gain(40);

            pool.TickTurnStart();

            Assert.AreEqual(40, pool.Current);
        }

        [Test]
        public void UnderDamageOnlyDealingOrTakingAHitKeepsThePool()
        {
            var idle = new ResourcePool(Row(decayPerIdleTurn: 10), 100, 0);
            idle.Gain(40);
            idle.NoteActivity(PoolActivity.Action);
            idle.TickTurnStart();
            Assert.AreEqual(30, idle.Current,
                "a turn spent on Provoke, an item or a Move is idle under the Damage rule");

            var bloodied = new ResourcePool(Row(decayPerIdleTurn: 10), 100, 0);
            bloodied.Gain(40);
            bloodied.NoteActivity(PoolActivity.Damage);
            bloodied.TickTurnStart();
            Assert.AreEqual(40, bloodied.Current);
        }

        [Test]
        public void UnderAnyActionDoingAnythingAtAllKeepsThePool()
        {
            var acted = new ResourcePool(
                Row(decayPerIdleTurn: 10, decayUnless: PoolDecayTrigger.AnyAction), 100, 0);
            acted.Gain(40);
            acted.NoteActivity(PoolActivity.Action);
            acted.TickTurnStart();
            Assert.AreEqual(40, acted.Current);

            // Damage IMPLIES action, so the wider rule is satisfied by the
            // narrower event without every call site reporting both.
            var hit = new ResourcePool(
                Row(decayPerIdleTurn: 10, decayUnless: PoolDecayTrigger.AnyAction), 100, 0);
            hit.Gain(40);
            hit.NoteActivity(PoolActivity.Damage);
            hit.TickTurnStart();
            Assert.AreEqual(40, hit.Current);
        }

        [Test]
        public void ActivityIsForgottenAtEachTurnStart()
        {
            var pool = new ResourcePool(Row(decayPerIdleTurn: 10), 100, 0);
            pool.Gain(40);

            pool.NoteActivity(PoolActivity.Damage);
            pool.TickTurnStart();
            Assert.AreEqual(40, pool.Current, "fixture: the fighting turn kept it");

            pool.TickTurnStart();
            Assert.AreEqual(30, pool.Current,
                "last turn's blow must not pay for this turn's idleness");
        }

        [Test]
        public void DecayNeverTakesThePoolBelowZero()
        {
            var pool = new ResourcePool(Row(decayPerIdleTurn: 10), 100, 0);
            pool.Gain(4);

            pool.TickTurnStart();
            Assert.AreEqual(0, pool.Current);

            pool.TickTurnStart();
            Assert.AreEqual(0, pool.Current);
        }

        // ---- what a mana effect can and cannot fill ---------------------------

        [Test]
        public void APoolThatRefusesManaEffectsIsNotFilledByOne()
        {
            var earned = new CombatantState("Bear", true, 100,
                new ResourcePool(Row(restoredByManaEffects: false), 100, 0), 10, 10);

            Assert.IsFalse(CombatMath.CanRestoreMana(earned),
                "the predicate the bot and the affordance text ask BEFORE the effect resolves");
            Assert.AreEqual(0, CombatMath.RestoreMana(earned, 50),
                "a mana potion must not be a rage potion");
            Assert.AreEqual(0, earned.CurrentMana);

            var mana = new CombatantState("Owl", true, 100,
                new ResourcePool(Row(restoredByManaEffects: true), 100, 0), 10, 10);

            Assert.IsTrue(CombatMath.CanRestoreMana(mana));
            Assert.AreEqual(50, CombatMath.RestoreMana(mana, 50));
            Assert.AreEqual(50, mana.CurrentMana);

            // Still clamped, and the clamp is still reported.
            Assert.AreEqual(50, CombatMath.RestoreMana(mana, 500));
            Assert.AreEqual(100, mana.CurrentMana);
        }

        // ---- the compatibility view -------------------------------------------

        [Test]
        public void TheThreeManaReadersSeeThePrimaryPool()
        {
            var hero = new CombatantState("Shawn", true, 100,
                new ResourcePool(Row(gainPerTurn: 3, startRule: PoolStartRule.Full), 34, 3), 10, 10);

            Assert.AreEqual(34, hero.MaxMana);
            Assert.AreEqual(34, hero.CurrentMana);
            Assert.AreEqual(3, hero.ManaRegen);

            hero.PrimaryPool.TrySpend(6);
            Assert.AreEqual(28, hero.CurrentMana, "the view is the pool, not a copy of it");
        }

        [Test]
        public void TheIntConstructorStillBuildsAFullManaPool()
        {
            // 254 call sites hand an int. It has to keep meaning exactly what
            // it meant: a mana pool, full, that only ever goes down.
            var hero = new CombatantState("Fixture", true, 100, 30, 10, 10);

            Assert.AreEqual("mana", hero.PrimaryPool.Id);
            Assert.AreEqual("MP", hero.PrimaryPool.ShortTag);
            Assert.AreEqual(30, hero.MaxMana);
            Assert.AreEqual(30, hero.CurrentMana);
            Assert.AreEqual(0, hero.ManaRegen);
            Assert.AreEqual(0, hero.PrimaryPool.DecayPerIdleTurn);
            Assert.IsTrue(hero.PrimaryPool.RestoredByManaEffects);
            Assert.IsTrue(hero.PrimaryPool.AllowsSpellBooks);
        }
    }
}
