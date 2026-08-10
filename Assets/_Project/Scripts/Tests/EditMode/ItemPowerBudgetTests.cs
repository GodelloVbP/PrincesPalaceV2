using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class ItemPowerBudgetTests
    {
        [Test]
        public void ScalingGradeWorth_None_IsZero()
        {
            Assert.AreEqual(0f, ItemPowerBudget.ScalingGradeWorth(ScalingGrade.None, referenceAttack: 20f));
        }

        [Test]
        public void ScalingGradeWorth_S_IsTenPercentPerCommittedPointTimesReference()
        {
            // 0.10 (S's per-point rate) x 10 (assumed committed points) x 20 (reference) = 20.
            Assert.AreEqual(20f, ItemPowerBudget.ScalingGradeWorth(ScalingGrade.S, referenceAttack: 20f), 0.0001f);
        }

        [Test]
        public void ScalingGradeWorth_HigherGrade_IsAlwaysWorthMore()
        {
            float e = ItemPowerBudget.ScalingGradeWorth(ScalingGrade.E, 20f);
            float c = ItemPowerBudget.ScalingGradeWorth(ScalingGrade.C, 20f);
            float s = ItemPowerBudget.ScalingGradeWorth(ScalingGrade.S, 20f);

            Assert.Less(e, c);
            Assert.Less(c, s);
        }

        [Test]
        public void TotalPower_NoScaling_IsJustTheAttackBonus()
        {
            float power = ItemPowerBudget.TotalPower(10, ScalingProfile.None, referenceAttack: 10f);
            Assert.AreEqual(10f, power);
        }

        [Test]
        public void TotalPower_OneGradedStat_AddsItsWorthOnTopOfTheFlatBonus()
        {
            var scaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            float power = ItemPowerBudget.TotalPower(10, scaling, referenceAttack: 10f);

            // 10 flat + (0.10 x 10 x 10) = 10 + 10 = 20.
            Assert.AreEqual(20f, power, 0.0001f);
        }

        [Test]
        public void TotalPower_TwoGradedStats_SumsBoth()
        {
            var scaling = ScalingProfile.None
                .With(AbilityScore.Strength, ScalingGrade.S)
                .With(AbilityScore.Dexterity, ScalingGrade.C);
            float power = ItemPowerBudget.TotalPower(16, scaling, referenceAttack: 16f);

            // 16 + (0.10 x 10 x 16) + (0.03 x 10 x 16) = 16 + 16 + 4.8 = 36.8.
            Assert.AreEqual(36.8f, power, 0.0001f);
        }

        // The core promise this budget exists to check: a modifier that
        // trades a strong primary for a weak secondary can land at
        // essentially the same total as one that split its grades more
        // evenly — "different, not strictly better" made numeric.
        [Test]
        public void TotalPower_DifferentGradeSplits_CanLandAtTheSameTotal()
        {
            // A: one S primary, one C secondary.
            var profileA = ScalingProfile.None
                .With(AbilityScore.Strength, ScalingGrade.S)
                .With(AbilityScore.Dexterity, ScalingGrade.C);
            // B: one A primary, one B secondary — a different split.
            var profileB = ScalingProfile.None
                .With(AbilityScore.Wisdom, ScalingGrade.A)
                .With(AbilityScore.Strength, ScalingGrade.B);

            float powerA = ItemPowerBudget.TotalPower(16, profileA, 16f);
            float powerB = ItemPowerBudget.TotalPower(16, profileB, 16f);

            // Not asserting exact equality (that would just re-derive the
            // grade table) — asserting they land close enough that neither
            // is a strictly dominant choice, which is the actual property
            // that matters to a player picking between them.
            float ratio = powerA / powerB;
            Assert.Greater(ratio, 0.8f);
            Assert.Less(ratio, 1.25f);
        }
    }
}
