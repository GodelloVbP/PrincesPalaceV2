using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // ScalingSet: every axis a combatant's damage can ride, combined. Expected
    // values are PINNED literals, not the production formula run a second
    // time (CLAUDE.md gotcha #5).
    public class ScalingSetTests
    {
        private static ScalingProfile Riding(AbilityScore score, ScalingGrade grade)
        {
            return ScalingProfile.None.With(score, grade);
        }

        private static AbilityScoreBlock Scores(int strength = 10, int dexterity = 10, int constitution = 10,
            int wisdom = 10, int intelligence = 10, int charisma = 10)
        {
            return new AbilityScoreBlock(strength, dexterity, constitution, wisdom, intelligence, charisma);
        }

        [Test]
        public void TwoNeutralSlots_MultiplyOutToExactlyOne()
        {
            var set = new ScalingSet(ScalingProfile.None, ScalingProfile.None, ScalingProfile.None);
            Assert.IsTrue(set.IsNeutral);
            Assert.AreEqual(1f, set.MultiplierFor(Scores(strength: 20, intelligence: 3)), 0.0001f);
        }

        // The property the whole set exists for: two S-graded slots at the
        // same +10-over-neutral score add to 3.00 (1 + 1.0 + 1.0), not the
        // 4.00 (2.00 * 2.00) they would compound to if each slot's own
        // multiplier were applied in turn.
        [Test]
        public void TwoSlots_ContributeAdditivelyRatherThanCompounding()
        {
            var sTwenty = Riding(AbilityScore.Strength, ScalingGrade.S);
            var set = new ScalingSet(sTwenty, sTwenty, ScalingProfile.None);

            Assert.AreEqual(3.00f, set.MultiplierFor(Scores(strength: 20)), 0.0001f);
        }

        // The floor has to apply to the SUMMED total, not per slot -- two
        // disastrously-matched slots (S on Strength at Strength 0, each
        // individually 0.25f floored) must not sum to 0.50f. They sum to
        // -1.00 raw (1 + -1.0 + -1.0) and THAT is what gets floored, once.
        [Test]
        public void TheFloorAppliesToTheSummedTotal_NotPerSlot()
        {
            var sZero = Riding(AbilityScore.Strength, ScalingGrade.S);
            var set = new ScalingSet(sZero, sZero, ScalingProfile.None);

            Assert.AreEqual(ScalingProfile.MinimumMultiplier, set.MultiplierFor(Scores(strength: 0)), 0.0001f);
        }

        [Test]
        public void ASingleProfile_ConvertsImplicitlyAndMultipliesExactlyAsItsOwnScalingProfileWould()
        {
            var sword = Riding(AbilityScore.Strength, ScalingGrade.A).With(AbilityScore.Dexterity, ScalingGrade.C);
            ScalingSet set = sword;

            Assert.AreEqual(sword.MultiplierFor(Scores(strength: 18, dexterity: 14)),
                set.MultiplierFor(Scores(strength: 18, dexterity: 14)), 0.0001f);
        }

        [Test]
        public void None_IsNeutralAndEqualToDefault()
        {
            Assert.IsTrue(ScalingSet.None.IsNeutral);
            Assert.AreEqual(default(ScalingSet), ScalingSet.None);
        }

        // Each slot's own attribution is exactly what BonusFor(scores, one)
        // says for that slot -- summed across all three, same relationship
        // BonusFor(scores) has to MultiplierFor.
        [Test]
        public void BonusFor_SumsEachSlotsOwnAttribution()
        {
            var mainHandStr = Riding(AbilityScore.Strength, ScalingGrade.B);
            var spellTierStr = Riding(AbilityScore.Strength, ScalingGrade.C);
            var set = new ScalingSet(spellTierStr, mainHandStr, ScalingProfile.None);
            var scores = Scores(strength: 20);

            float expected = mainHandStr.BonusFor(scores, AbilityScore.Strength)
                + spellTierStr.BonusFor(scores, AbilityScore.Strength);
            Assert.AreEqual(expected, set.BonusFor(scores, AbilityScore.Strength), 0.0001f);
            Assert.AreEqual(1f + set.BonusFor(scores), set.MultiplierFor(scores), 0.0001f);
        }
    }
}
