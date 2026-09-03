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

            Assert.IsTrue(locks.OncePerCombat("x:hero1"), "the first call must be free");
            Assert.IsFalse(locks.OncePerCombat("x:hero1"), "a second call in the same combat must be refused");

            locks.ResetTurn("hero1");
            Assert.IsFalse(locks.OncePerCombat("x:hero1"), "a turn boundary does not reset a per-combat lock");
        }

        [Test]
        public void OncePerTurnResetsAtTheTurnBoundary()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerTurn("y:hero1"));
            Assert.IsFalse(locks.OncePerTurn("y:hero1"), "spent for this turn");

            locks.ResetTurn("hero1");
            Assert.IsTrue(locks.OncePerTurn("y:hero1"), "a fresh turn re-arms it");
        }

        [Test]
        public void DifferentKeysDoNotShareOneLock()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerCombat("a:hero1"));
            Assert.IsTrue(locks.OncePerCombat("b:hero1"), "a different key must have its own gate");
        }

        // Finding 1: ResetTurn used to clear the WHOLE per-turn set on any
        // actor's turn boundary. A lock owned by "hero1" must survive an
        // "enemy1" turn boundary and only clear on hero1's own.
        [Test]
        public void ResetTurnOnlyClearsTheGivenOwnersLocks()
        {
            var locks = new CombatLocks();

            Assert.IsTrue(locks.OncePerTurn("berserkers_vest:hero1"));
            Assert.IsTrue(locks.OncePerTurn("sparring_buckler:enemy1"));

            // enemy1's turn starts -- hero1's lock must not be touched.
            locks.ResetTurn("enemy1");
            Assert.IsFalse(locks.OncePerTurn("berserkers_vest:hero1"), "another owner's turn boundary must not re-arm hero1's lock");
            Assert.IsTrue(locks.OncePerTurn("sparring_buckler:enemy1"), "enemy1's own lock was cleared by enemy1's turn boundary");

            // hero1's own turn starts -- now hero1's lock re-arms.
            locks.ResetTurn("hero1");
            Assert.IsTrue(locks.OncePerTurn("berserkers_vest:hero1"), "hero1's own turn boundary must re-arm hero1's lock");
        }
    }
}
