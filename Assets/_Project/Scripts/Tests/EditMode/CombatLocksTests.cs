using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (f): a once-per-X gate, keyed by an arbitrary string.
    public class CombatLocksTests
    {
        [Test]
        public void OncePerCombatFiresOnceThenRefusesForTheRestOfTheFight()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerCombat("x"), "the first call must be free");
            Assert.IsFalse(locks.OncePerCombat("x"), "a second call in the same combat must be refused");

            locks.ResetTurn();
            Assert.IsFalse(locks.OncePerCombat("x"), "a turn boundary does not reset a per-combat lock");
        }

        [Test]
        public void OncePerTurnResetsAtTheTurnBoundary()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerTurn("y"));
            Assert.IsFalse(locks.OncePerTurn("y"), "spent for this turn");

            locks.ResetTurn();
            Assert.IsTrue(locks.OncePerTurn("y"), "a fresh turn re-arms it");
        }

        [Test]
        public void DifferentKeysDoNotShareOneLock()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerCombat("a"));
            Assert.IsTrue(locks.OncePerCombat("b"), "a different key must have its own gate");
        }
    }
}
