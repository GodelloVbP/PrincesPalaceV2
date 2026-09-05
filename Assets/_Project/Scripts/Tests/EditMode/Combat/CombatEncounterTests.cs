using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    public class CombatEncounterTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int speed, int maxHealth = 20)
        {
            return new CombatantState(name, isPlayerSide, maxHealth, 10, 5, speed);
        }

        [Test]
        public void Constructor_OrdersTurnsBySpeedDescending()
        {
            var slow = Fighter("Slow", true, speed: 1);
            var fast = Fighter("Fast", true, speed: 10);
            var enemy = Fighter("Enemy", false, speed: 5);

            var encounter = new CombatEncounter(new[] { slow, fast }, new[] { enemy });

            Assert.AreSame(fast, encounter.Current, "The fastest combatant should act first");
        }

        [Test]
        public void Constructor_ThrowsWithNoPlayerParty()
        {
            var enemy = Fighter("Enemy", false, speed: 5);

            Assert.Throws<ArgumentException>(() => new CombatEncounter(Array.Empty<CombatantState>(), new[] { enemy }));
        }

        [Test]
        public void Constructor_ThrowsWithNoEnemies()
        {
            var player = Fighter("Player", true, speed: 5);

            Assert.Throws<ArgumentException>(() => new CombatEncounter(new[] { player }, Array.Empty<CombatantState>()));
        }

        // ---- FrontEnemy (Phase 6) --------------------------------------------

        [Test]
        public void FrontEnemy_IsTheFirstEnemyByConstructionOrder()
        {
            var player = Fighter("Player", true, speed: 5);
            var first = Fighter("First", false, speed: 5);
            var second = Fighter("Second", false, speed: 5);

            var encounter = new CombatEncounter(new[] { player }, new[] { first, second });

            Assert.AreSame(first, encounter.FrontEnemy);
        }

        [Test]
        public void FrontEnemy_SkipsADeadFirstEnemy()
        {
            var player = Fighter("Player", true, speed: 5);
            var first = Fighter("First", false, speed: 5);
            var second = Fighter("Second", false, speed: 5);
            first.CurrentHealth = 0;

            var encounter = new CombatEncounter(new[] { player }, new[] { first, second });

            Assert.AreSame(second, encounter.FrontEnemy, "The front is whoever is still standing nearest the front, not a fixed slot");
        }

        [Test]
        public void FrontEnemy_IsNullOnceEveryEnemyIsDead()
        {
            var player = Fighter("Player", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 5);
            enemy.CurrentHealth = 0;

            // A CombatEncounter with every enemy already dead is not a real
            // scenario the game reaches (IsOver would already be true), but
            // FrontEnemy should degrade gracefully rather than throw if
            // asked anyway.
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            Assert.IsNull(encounter.FrontEnemy);
        }

        // Replaces AdvanceTurn_CyclesThroughEveryLivingCombatant, whose name
        // was the contract: everyone acts once, in order, then it wraps.
        // Charge scheduling deliberately abandons that — equal turn COUNTS
        // were exactly the limitation it exists to remove. What must still
        // hold is that everyone eventually acts, and that being faster means
        // acting more.
        [Test]
        public void AdvanceTurn_GivesEveryLivingCombatantTurns_MoreOftenTheFasterTheyAre()
        {
            var a = Fighter("A", true, speed: 10);
            var b = Fighter("B", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 1);

            var encounter = new CombatEncounter(new[] { a, b }, new[] { enemy });

            var turns = new List<CombatantState> { encounter.Current };
            for (int i = 0; i < 40; i++)
            {
                encounter.AdvanceTurn();
                turns.Add(encounter.Current);
            }

            int aTurns = turns.Count(c => ReferenceEquals(c, a));
            int bTurns = turns.Count(c => ReferenceEquals(c, b));
            int enemyTurns = turns.Count(c => ReferenceEquals(c, enemy));

            Assert.Greater(aTurns, bTurns, "Speed 10 should out-turn speed 5");
            Assert.Greater(bTurns, enemyTurns, "Speed 5 should out-turn speed 1");
            Assert.Greater(enemyTurns, 0, "Nobody may be starved of turns entirely");
        }

        [Test]
        public void AdvanceTurn_SkipsADefeatedCombatantsTurn()
        {
            var a = Fighter("A", true, speed: 10);
            var b = Fighter("B", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 1);
            var encounter = new CombatEncounter(new[] { a, b }, new[] { enemy });

            CombatMath.ApplyDamage(b, 999); // B is defeated but stays in the order

            // Asserted over many turns rather than by naming who is next.
            // Under charge scheduling the combatant after A is whoever
            // charges fastest, not the next list entry — but a corpse must
            // never be handed a turn no matter how the schedule falls.
            for (int i = 0; i < 30; i++)
            {
                encounter.AdvanceTurn();
                Assert.AreNotSame(b, encounter.Current, "A defeated combatant was handed a turn");
                Assert.IsTrue(encounter.Current.IsAlive, "Current must always be someone still standing");
            }
        }

        [Test]
        public void IsOver_FalseWhileBothSidesHaveALivingMember()
        {
            var player = Fighter("Player", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            Assert.IsFalse(encounter.IsOver);
        }

        [Test]
        public void IsOver_TrueOnceAllEnemiesAreDefeated()
        {
            var player = Fighter("Player", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            CombatMath.ApplyDamage(enemy, 999);

            Assert.IsTrue(encounter.IsOver);
            Assert.IsTrue(encounter.PlayerWon);
        }

        [Test]
        public void IsOver_TrueOnceTheWholePartyIsDefeated()
        {
            var player = Fighter("Player", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            CombatMath.ApplyDamage(player, 999);

            Assert.IsTrue(encounter.IsOver);
            Assert.IsFalse(encounter.PlayerWon);
        }

        [Test]
        public void AdvanceTurn_ThrowsOnceTheEncounterIsOver()
        {
            var player = Fighter("Player", true, speed: 5);
            var enemy = Fighter("Enemy", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            CombatMath.ApplyDamage(enemy, 999);

            Assert.Throws<InvalidOperationException>(() => encounter.AdvanceTurn());
        }

        [Test]
        public void LivingEnemies_ExcludesDefeatedCombatants()
        {
            var player = Fighter("Player", true, speed: 10);
            var enemyA = Fighter("A", false, speed: 5);
            var enemyB = Fighter("B", false, speed: 1);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemyA, enemyB });

            CombatMath.ApplyDamage(enemyA, 999);

            CollectionAssert.AreEquivalent(new[] { enemyB }, encounter.LivingEnemies);
            CollectionAssert.AreEquivalent(new[] { enemyA, enemyB }, encounter.Enemies, "Defeated combatants stay in the full roster");
        }

        [Test]
        public void IsPlayerTurn_ReflectsWhoseTurnItCurrentlyIs()
        {
            var player = Fighter("Player", true, speed: 10);
            var enemy = Fighter("Enemy", false, speed: 1);
            var encounter = new CombatEncounter(new[] { player }, new[] { enemy });

            Assert.IsTrue(encounter.IsPlayerTurn, "The faster combatant opens, and here that is the player");

            // Tracks Current's side, always — asserted as an invariant rather
            // than by advancing once and expecting the enemy. A speed-10
            // player against a speed-1 enemy now genuinely acts several times
            // in a row, so "advance once, it must be theirs" was only ever
            // true under the old fixed rotation.
            bool enemyEverActed = false;
            for (int i = 0; i < 30; i++)
            {
                encounter.AdvanceTurn();
                Assert.AreEqual(encounter.Current.IsPlayerSide, encounter.IsPlayerTurn);
                enemyEverActed |= !encounter.IsPlayerTurn;
            }

            Assert.IsTrue(enemyEverActed, "The enemy must get a turn eventually, however outpaced");
        }

        // ---- TryAddEnemy (Forest Warden's Roar) --------------------------------

        [Test]
        public void TryAddEnemy_JoinsTheLivingRosterUnderTheCap()
        {
            var player = Fighter("Player", true, speed: 5);
            var boss = Fighter("Boss", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { boss });

            var rat = Fighter("Rat", false, speed: 5);
            bool added = encounter.TryAddEnemy(rat, maxSlots: 3);

            Assert.IsTrue(added);
            CollectionAssert.Contains(encounter.Enemies, rat);
            CollectionAssert.Contains(encounter.LivingEnemies.ToList(), rat);
        }

        [Test]
        public void TryAddEnemy_RefusesOnceTheStageIsFull()
        {
            var player = Fighter("Player", true, speed: 5);
            var boss = Fighter("Boss", false, speed: 5);
            var firstRat = Fighter("Rat 1", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { boss, firstRat });

            var secondRat = Fighter("Rat 2", false, speed: 5);
            bool added = encounter.TryAddEnemy(secondRat, maxSlots: 2);

            Assert.IsFalse(added, "the stage only has 2 slots and both are already taken");
            CollectionAssert.DoesNotContain(encounter.Enemies, secondRat);
        }

        [Test]
        public void TryAddEnemy_TheNewArrivalEventuallyGetsATurn()
        {
            var player = Fighter("Player", true, speed: 1);
            var boss = Fighter("Boss", false, speed: 1);
            var encounter = new CombatEncounter(new[] { player }, new[] { boss });

            var rat = Fighter("Rat", false, speed: 20);
            Assert.IsTrue(encounter.TryAddEnemy(rat, maxSlots: 3));

            bool ratActed = false;
            for (int i = 0; i < 30 && !ratActed; i++)
            {
                encounter.AdvanceTurn();
                ratActed = ReferenceEquals(encounter.Current, rat);
            }

            Assert.IsTrue(ratActed, "a summon that never enters the turn order would stand on stage forever doing nothing");
        }

        [Test]
        public void TryAddEnemy_RefusesANullCombatant()
        {
            var player = Fighter("Player", true, speed: 5);
            var boss = Fighter("Boss", false, speed: 5);
            var encounter = new CombatEncounter(new[] { player }, new[] { boss });

            Assert.IsFalse(encounter.TryAddEnemy(null, maxSlots: 3));
        }
    }
}
