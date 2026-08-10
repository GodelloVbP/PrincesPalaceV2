using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // AUDIT.md #16: CombatMath, StatBlock.Scaled/ScaledForElite, and several
    // FightController damage sites used three different rounding
    // conventions (away-from-zero in one place, to-even everywhere else),
    // which only actually disagree at an exact .5 tie. Pinned here with
    // literal ties (2.5, -2.5) rather than through any production formula —
    // a test that recomputed its expectation via Math.Round would pick up
    // whichever convention the code currently uses and could never catch a
    // future regression back to a mismatched one.
    public class RoundingTests
    {
        [Test]
        public void AwayFromZero_RoundsAPositiveTieUp()
        {
            Assert.AreEqual(3, Rounding.AwayFromZero(2.5f), "2.5 must round to 3, not 2 (banker's/to-even would pick 2)");
        }

        [Test]
        public void AwayFromZero_RoundsANegativeTieDown()
        {
            Assert.AreEqual(-3, Rounding.AwayFromZero(-2.5f), "-2.5 must round to -3, away from zero");
        }

        [Test]
        public void AwayFromZero_NonTieValues_RoundToTheNearestInt()
        {
            Assert.AreEqual(3, Rounding.AwayFromZero(2.6f));
            Assert.AreEqual(2, Rounding.AwayFromZero(2.4f));
        }
    }
}
