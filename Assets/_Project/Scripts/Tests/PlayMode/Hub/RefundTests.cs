using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Character.Refund, the dossier minus's model-side half. See
    // CharacterDossierScreenTests.TheMinusSitsOppositeThePlusInsideItsCell for
    // the button's geometry and DossierRefundTests for which copies of the
    // screen show it.
    //
    // PlayMode, not EditMode: Character is save-shaped mutable state in Core
    // (Data/Character.cs), and CODE_STANDARDS.md §1 draws the EditMode line at
    // Domain only -- an EditMode test cannot reference it.
    public class RefundTests
    {
        [Test]
        public void RefundingTwiceOfThreeInvestedLeavesOneAndReturnsTwoPoints()
        {
            var character = new Character("test");
            character.unspentStatPoints = 3;

            character.Invest(AbilityScore.Strength);
            character.Invest(AbilityScore.Strength);
            character.Invest(AbilityScore.Strength);

            Assert.AreEqual(3, character.investedAbilityScores.strength);
            Assert.AreEqual(0, character.unspentStatPoints);

            Assert.IsTrue(character.Refund(AbilityScore.Strength));
            Assert.IsTrue(character.Refund(AbilityScore.Strength));

            Assert.AreEqual(1, character.investedAbilityScores.strength);
            Assert.AreEqual(2, character.unspentStatPoints);
        }

        [Test]
        public void RefundingAScoreWithNothingInvestedChangesNothing()
        {
            var character = new Character("test");
            character.unspentStatPoints = 5;

            Assert.IsFalse(character.Refund(AbilityScore.Wisdom));

            Assert.AreEqual(0, character.investedAbilityScores.wisdom);
            Assert.AreEqual(5, character.unspentStatPoints);
        }
    }
}
