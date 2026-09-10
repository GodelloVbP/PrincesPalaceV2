using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE PROMISE A Fixed POOL MAKES, and the only test that can check it.
    //
    // The shipped catalogue has one row and it is WisdomDerived, so nothing
    // built from content can demonstrate what Fixed does -- and "0..100,
    // always" is the whole reason capacityRule is an enum rather than a bool
    // on a number. These pin it against fixture rows, which is why the rule
    // lives in Domain (PoolPrecedence) with Core supplying the sums.
    //
    // Literal expected values throughout, including the ability-score terms:
    // recomputing AbilityDerivation.MaxManaBonus here would make the test
    // agree with the formula rather than pin it (CLAUDE.md gotcha 5).
    public class PoolPrecedenceTests
    {
        private static ResolvedPool Row(PoolCapacityRule rule, int capacity, int gainPerTurn = 0) =>
            new ResolvedPool("fixture", "Fixture", "FIX",
                rule, capacity,
                gainPerTurn, 0, 0,
                0, PoolDecayTrigger.Damage,
                PoolStartRule.Full, 0,
                "#FFFFFF", "#000000", "#CCCCCC",
                false, true, true, false,
                0);

        private static AbilityScoreBlock Wis(int wisdom) =>
            new AbilityScoreBlock(10, 10, 10, wisdom, 10, 10);

        // MaxManaBonus is (WIS - 10) * 2, so WIS 8 is -4 and WIS 20 is +20 --
        // written out rather than called, and pinned in
        // AbilityDerivationTests' own boundary table.
        private const int BonusAtWis8 = -4;
        private const int BonusAtWis20 = 20;

        [Test]
        public void AFixedPoolIsItsAuthoredCapacityAtEveryWisdom()
        {
            var fixedRow = Row(PoolCapacityRule.Fixed, 100);

            Assert.AreEqual(100, PoolPrecedence.Capacity(fixedRow, BonusAtWis8, null, 0));
            Assert.AreEqual(100, PoolPrecedence.Capacity(fixedRow, BonusAtWis20, null, 0));

            // Fixture check on the two numbers above, so this test fails
            // loudly if the derivation moves rather than quietly pinning a
            // stale table.
            Assert.AreEqual(BonusAtWis8, AbilityDerivation.MaxManaBonus(Wis(8)));
            Assert.AreEqual(BonusAtWis20, AbilityDerivation.MaxManaBonus(Wis(20)));
        }

        [Test]
        public void AFixedPoolIgnoresRelicsAndGearToo()
        {
            var fixedRow = Row(PoolCapacityRule.Fixed, 100);
            var relics = new List<RelicModifier>
            {
                new RelicModifier(RelicModifierType.MaxManaPercent, 50),
                new RelicModifier(RelicModifierType.MaxManaFlat, 25),
            };

            Assert.AreEqual(100, PoolPrecedence.Capacity(fixedRow, BonusAtWis20, relics, 40),
                "a bar that says 0..100 is 0..100 after a relic, a talent and a Runic charm - " +
                "that promise is the entire point of the Fixed rule, and a Max Mana pick being " +
                "dead for its owner is the price the plan accepted for it");
        }

        [Test]
        public void AWisdomDerivedPoolTakesEverySourceInOrder()
        {
            var mana = Row(PoolCapacityRule.WisdomDerived, 30);

            Assert.AreEqual(26, PoolPrecedence.Capacity(mana, BonusAtWis8, null, 0),
                "30 base, WIS 8 costs 4");
            Assert.AreEqual(50, PoolPrecedence.Capacity(mana, BonusAtWis20, null, 0),
                "30 base, WIS 20 pays 20");

            // Percent first against the base, then the flat relic, then the
            // gear modifier LAST -- RelicModifiers.Apply reads RelicModifier
            // and never ModifierEffect, so the two cannot be summed before
            // the multiply. 50 * 1.5 = 75, + 25 = 100, + 40 = 140.
            var relics = new List<RelicModifier>
            {
                new RelicModifier(RelicModifierType.MaxManaPercent, 50),
                new RelicModifier(RelicModifierType.MaxManaFlat, 25),
            };

            Assert.AreEqual(140, PoolPrecedence.Capacity(mana, BonusAtWis20, relics, 40));
        }

        [Test]
        public void CapacityNeverGoesNegative()
        {
            var tiny = Row(PoolCapacityRule.WisdomDerived, 4);

            Assert.AreEqual(0, PoolPrecedence.Capacity(tiny, -40, null, 0),
                "a dreadful Wisdom empties the pool; it does not owe mana");
            Assert.AreEqual(0, PoolPrecedence.Capacity(tiny, 0, null, -400));
        }

        [Test]
        public void IncomeFollowsTheSameRuleAsCapacity()
        {
            // Mana's shape: a base of 0 with every point of regen derived, so
            // the row reading 0 does not mean mana stopped regenerating.
            Assert.AreEqual(3, PoolPrecedence.GainPerTurn(Row(PoolCapacityRule.WisdomDerived, 30), 3));
            Assert.AreEqual(0, PoolPrecedence.GainPerTurn(Row(PoolCapacityRule.WisdomDerived, 30), -2),
                "a negative derived regen is no regen, not a drain");

            // A Fixed pool's authored income is the whole income: a pool that
            // ignored outside capacity but not outside income would be two
            // rules wearing one word.
            Assert.AreEqual(5, PoolPrecedence.GainPerTurn(Row(PoolCapacityRule.Fixed, 100, 5), 3));
            Assert.AreEqual(0, PoolPrecedence.GainPerTurn(Row(PoolCapacityRule.Fixed, 100), 3));
        }
    }
}
