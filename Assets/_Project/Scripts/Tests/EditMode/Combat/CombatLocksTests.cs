using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (f): a once-per-X gate, owned by a COMBATANT and named by a key.
    //
    // The owner is tracked as its own identity, not spelled into the key
    // string: a suffix convention on "berserkers_vest:" + the ledger id
    // would be fragile (an owner id that happens to end another one, or
    // contain the separator, aliases two owners silently), and the ledger
    // id is not an identity -- it deliberately groups every combatant of
    // one enemy type under one row, so three rats would be one lock owner.
    public class CombatLocksTests
    {
        private static CombatantState Rat(string name = "Rat") =>
            new CombatantState(name, false, 100, 0, 5, 5);

        private const string Vest = "berserkers_vest";
        private const string Buckler = "sparring_buckler";

        [Test]
        public void OncePerCombatFiresOnceThenRefusesForTheRestOfTheFight()
        {
            var locks = new CombatLocks();
            var hero = Rat("Hero");

            Assert.IsTrue(locks.OncePerCombat(hero, Vest), "the first call must be free");
            Assert.IsFalse(locks.OncePerCombat(hero, Vest), "a second call in the same combat must be refused");

            locks.ResetTurn(hero);
            Assert.IsFalse(locks.OncePerCombat(hero, Vest), "a turn boundary does not reset a per-combat lock");
        }

        [Test]
        public void OncePerTurnResetsAtTheTurnBoundary()
        {
            var locks = new CombatLocks();
            var hero = Rat("Hero");

            Assert.IsTrue(locks.OncePerTurn(hero, Vest));
            Assert.IsFalse(locks.OncePerTurn(hero, Vest), "spent for this turn");

            locks.ResetTurn(hero);
            Assert.IsTrue(locks.OncePerTurn(hero, Vest), "a fresh turn re-arms it");
        }

        [Test]
        public void DifferentKeysDoNotShareOneLock()
        {
            var locks = new CombatLocks();
            var hero = Rat("Hero");

            Assert.IsTrue(locks.OncePerCombat(hero, Vest));
            Assert.IsTrue(locks.OncePerCombat(hero, Buckler), "a different key must have its own gate");
        }

        // A lock owned by the hero must survive an enemy's turn boundary and
        // only clear on the hero's own.
        [Test]
        public void ResetTurnOnlyClearsTheGivenOwnersLocks()
        {
            var locks = new CombatLocks();
            var hero = Rat("Hero");
            var enemy = Rat("Enemy");

            Assert.IsTrue(locks.OncePerTurn(hero, Vest));
            Assert.IsTrue(locks.OncePerTurn(enemy, Buckler));

            // The enemy's turn starts -- the hero's lock must not be touched.
            locks.ResetTurn(enemy);
            Assert.IsFalse(locks.OncePerTurn(hero, Vest), "another owner's turn boundary must not re-arm the hero's lock");
            Assert.IsTrue(locks.OncePerTurn(enemy, Buckler), "the enemy's own lock was cleared by the enemy's turn boundary");

            // The hero's own turn starts -- now the hero's lock re-arms.
            locks.ResetTurn(hero);
            Assert.IsTrue(locks.OncePerTurn(hero, Vest), "the hero's own turn boundary must re-arm the hero's lock");
        }

        // THE RISK THIS GUARDS. Three rats in a room are three combatants and
        // one ledger id -- LedgerIdOf returns enemy.Source.Id and that
        // grouping is deliberate, because a fight's damage table wants one
        // "Rat" row, not three. A lock keyed on that same string would make
        // them one lock owner: the first rat to claim a once-per-turn gate
        // would spend it for its littermates, and any one rat's turn
        // boundary would re-arm it for all of them.
        [Test]
        public void TwoCombatantsOfTheSameTypeDoNotShareAPerTurnLock()
        {
            var locks = new CombatLocks();
            var first = Rat();
            var second = Rat();

            Assert.IsTrue(locks.OncePerTurn(first, Vest));
            Assert.IsTrue(locks.OncePerTurn(second, Vest),
                "a second rat is a second owner, however the ledger chooses to group them");
        }

        [Test]
        public void OneCombatantsTurnBoundaryDoesNotReArmAnIdenticalTwins()
        {
            var locks = new CombatLocks();
            var first = Rat();
            var second = Rat();

            Assert.IsTrue(locks.OncePerTurn(first, Vest));
            locks.ResetTurn(second);

            Assert.IsFalse(locks.OncePerTurn(first, Vest),
                "the second rat's turn start cleared the first rat's lock out from under it");
        }

        [Test]
        public void ANullOwnerOrABlankKeyClaimsNothing()
        {
            var locks = new CombatLocks();

            Assert.IsFalse(locks.OncePerTurn(null, Vest));
            Assert.IsFalse(locks.OncePerCombat(null, Vest));
            Assert.IsFalse(locks.OncePerTurn(Rat(), ""));
            Assert.DoesNotThrow(() => locks.ResetTurn(null));
        }
    }
}
