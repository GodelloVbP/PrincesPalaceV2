using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // Equipping something that raises max health carries current health
    // forward proportionally rather than leaving it where it was, which
    // would grow a permanently empty tail at the end of the bar. These
    // pin that rule and the two properties that make it safe.
    //
    // EVERY EXPECTED VALUE IS A LITERAL, never the formula written out a second
    // time. A test that recomputes what it is checking passes whatever the
    // production code does, which is the trap CLAUDE.md gotcha 5 records --
    // and it is exactly how the reward track's claim rule stayed wrong through
    // a test that asserted it.
    public class CarriedHealthTests
    {
        [Test]
        public void TheFractionSurvivesAMaximumGoingUp()
        {
            Assert.AreEqual(60, CarriedHealth.Rescaled(50, 100, 120));
            Assert.AreEqual(55, CarriedHealth.Rescaled(50, 100, 110));
            Assert.AreEqual(36, CarriedHealth.Rescaled(33, 100, 110));
        }

        [Test]
        public void AndAMaximumGoingDown()
        {
            Assert.AreEqual(50, CarriedHealth.Rescaled(60, 120, 100));
            Assert.AreEqual(25, CarriedHealth.Rescaled(30, 120, 100));
        }

        // THE PROPERTY THAT STOPS IT BEING AN EXPLOIT. Adding the difference on
        // the way in and clamping on the way out would let a player equip and
        // unequip their way to full health; scaling both ways gives back
        // exactly what it took.
        [Test]
        public void APutOnAndTakenOffItemLeavesHealthWhereItStarted()
        {
            int equipped = CarriedHealth.Rescaled(50, 100, 120);
            Assert.AreEqual(60, equipped);
            Assert.AreEqual(50, CarriedHealth.Rescaled(equipped, 120, 100));
        }

        [Test]
        public void FullStaysFullAndEmptyStaysEmpty()
        {
            Assert.AreEqual(120, CarriedHealth.Rescaled(100, 100, 120));

            // A downed character is not revived by a max-health item, which is
            // why the floor of 1 below applies only to someone still standing.
            Assert.AreEqual(0, CarriedHealth.Rescaled(0, 100, 120));
        }

        // A survivor cannot be scaled out of existence by a maximum collapsing.
        [Test]
        public void AStandingCharacterNeverScalesToZero()
        {
            Assert.AreEqual(1, CarriedHealth.Rescaled(1, 100, 10));
        }

        [Test]
        public void NothingMovesWhenTheMaximumDidNot()
        {
            Assert.AreEqual(37, CarriedHealth.Rescaled(37, 100, 100));

            // A maximum of zero is a character with no derived stats yet, not
            // a character who should be emptied.
            Assert.AreEqual(37, CarriedHealth.Rescaled(37, 0, 120));
            Assert.AreEqual(37, CarriedHealth.Rescaled(37, 100, 0));
        }
    }
}
