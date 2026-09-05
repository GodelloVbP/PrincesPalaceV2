using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // What the ledger counts, driven through a real fight rather than by
    // poking the counters directly.
    //
    // Poking them would test arithmetic nobody doubts. The thing that can
    // actually be wrong is ATTRIBUTION -- whether a blow lands in the swinger's
    // column, whether a shield's absorption is counted as damage taken, whether
    // an AOE counts once or once per enemy. All of that only exists inside
    // FightSession, so that is what these drive.
    public class CombatLedgerTests
    {
        private const string HeroId = "shawn";

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            int foeHealth = 5000, DamageType? attackType = null, int foeSpeed = 1)
        {
            var hero = new CombatantState("Shawn", true, 400, 40, 25, 10);
            var foe = new CombatantState("Rat", false, foeHealth, 10, 5, foeSpeed);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit(HeroId, CharacterRole.Tank, null, null, attackType);

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(7))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        [Test]
        public void ABlowLandsInTheSwingersColumnAndTheTargetsAtOnce()
        {
            var (session, _, foe) = Fight();
            int before = foe.CurrentHealth;

            session.ExecuteAttack(foe);

            int dealt = before - foe.CurrentHealth;
            Assert.Greater(dealt, 0, "fixture: the swing did something");

            Assert.AreEqual(dealt, session.Ledger.For(HeroId).TotalDealt);
            Assert.AreEqual(dealt, session.Ledger.For("Rat").DamageTaken);
        }

        [Test]
        public void APhysicalSwingIsCountedAsPhysical()
        {
            var (session, _, foe) = Fight(attackType: DamageType.Physical);

            session.ExecuteAttack(foe);
            var line = session.Ledger.For(HeroId);

            Assert.Greater(line.PhysicalDealt, 0);
            Assert.AreEqual(0, line.OtherDealt);
        }

        [Test]
        public void AnElementalSwingIsCountedAsOther()
        {
            // The split is exactly CombatMath.IsPhysical's: Physical rides the
            // weapon, everything else rides the spell. A fire attacker's damage
            // must not land in the physical column.
            var (session, _, foe) = Fight(attackType: DamageType.Fire);

            session.ExecuteAttack(foe);
            var line = session.Ledger.For(HeroId);

            Assert.AreEqual(0, line.PhysicalDealt);
            Assert.Greater(line.OtherDealt, 0);
        }

        [Test]
        public void BeingHitIsRecordedAgainstWhoeverGotHit()
        {
            // The hero swings, the monster replies. Speed 9 against the hero's
            // 10 so the reply actually lands -- at speed 1 the monster barely
            // acts and the assertion becomes unobservable.
            var (session, hero, foe) = Fight(foeSpeed: 9);
            int before = hero.CurrentHealth;

            session.ExecuteAttack(foe);

            int lost = before - hero.CurrentHealth;
            Assert.Greater(lost, 0, "fixture: the monster hit back");
            Assert.AreEqual(lost, session.Ledger.For(HeroId).DamageTaken);
        }

        [Test]
        public void WhatAShieldAteIsNeverFoldedIntoWhatWasTaken()
        {
            // Adding the two would double-count every blow against a ward, and
            // it is the one arithmetic mistake here that would look plausible
            // in every individual number.
            var ledger = new CombatLedger();

            ledger.Took(HeroId, amount: 40, shielded: 15);
            var line = ledger.For(HeroId);

            Assert.AreEqual(40, line.DamageTaken);
            Assert.AreEqual(15, line.Shielded);
        }

        [Test]
        public void AKillIsCreditedToWhoeverLandedIt()
        {
            var (session, _, foe) = Fight(foeHealth: 1);

            session.ExecuteAttack(foe);

            Assert.IsFalse(foe.IsAlive, "fixture: one swing kills a 1 HP rat");
            Assert.AreEqual(1, session.Ledger.For(HeroId).Kills);
            Assert.AreEqual(1, session.Ledger.For("Rat").TimesDowned);
        }

        [Test]
        public void HealingIsCountedAtWhatWasActuallyRestoredNotWhatWasOffered()
        {
            // CombatMath.Heal clamps at max health, so a 200-point potion on a
            // character missing 30 restored 30. Counting the offer would make
            // every healer's column a lie the player can check against a bar.
            var (session, hero, _) = Fight();
            hero.CurrentHealth = hero.MaxHealth - 30;

            session.UseConsumable("Health Potion", 200, restoresMana: false);

            Assert.AreEqual(30, session.Ledger.For(HeroId).Healed);
        }

        [Test]
        public void ManaRestorationIsNotCountedAsHealing()
        {
            var (session, hero, _) = Fight();
            hero.CurrentMana = 5;

            session.UseConsumable("Ether", 20, restoresMana: true);

            Assert.AreEqual(0, session.Ledger.For(HeroId).Healed, "mana is not health");
        }

        [Test]
        public void ACombatantWhoDidNothingHasALineOfZeroesRatherThanNoLine()
        {
            // Saves every caller a null guard, and "did nothing" is a true
            // statement worth being able to render.
            var ledger = new CombatLedger();

            var line = ledger.For("nobody");

            Assert.IsNotNull(line);
            Assert.AreEqual(0, line.TotalDealt);
            Assert.AreEqual(0, line.DamageTaken);
        }

        [Test]
        public void AnUnknownIdNeverThrowsAndNeverInventsARow()
        {
            var ledger = new CombatLedger();

            Assert.DoesNotThrow(() => ledger.Dealt(null, DamageType.Physical, 10));
            Assert.DoesNotThrow(() => ledger.Took("", 10));
            Assert.DoesNotThrow(() => ledger.Restored(null, 10));
            Assert.IsTrue(ledger.IsEmpty);
        }

        [Test]
        public void FoldingOneLedgerIntoAnotherSumsEveryColumn()
        {
            // The operation the whole id-keyed shape exists for: a fight's
            // totals added into a run's.
            var a = new CombatLedger();
            a.Dealt(HeroId, DamageType.Physical, 100);
            a.Took(HeroId, 40, shielded: 10);
            a.Restored(HeroId, 25);
            a.ScoredKill(HeroId);

            var b = new CombatLedger();
            b.Dealt(HeroId, DamageType.Fire, 60);
            b.Took(HeroId, 20, shielded: 5);
            b.ScoredKill(HeroId);

            a.Add(b);
            var line = a.For(HeroId);

            Assert.AreEqual(100, line.PhysicalDealt);
            Assert.AreEqual(60, line.OtherDealt);
            Assert.AreEqual(160, line.TotalDealt);
            Assert.AreEqual(60, line.DamageTaken);
            Assert.AreEqual(15, line.Shielded);
            Assert.AreEqual(25, line.Healed);
            Assert.AreEqual(2, line.Kills);
        }

        [Test]
        public void FoldingKeepsIdsInFirstSeenOrder()
        {
            // A screen listing the party must not reshuffle between two fights.
            // Dictionary enumeration order is not a contract.
            var a = new CombatLedger();
            a.Dealt("first", DamageType.Physical, 1);
            a.Dealt("second", DamageType.Physical, 1);

            var b = new CombatLedger();
            b.Dealt("third", DamageType.Physical, 1);
            b.Dealt("first", DamageType.Physical, 1);

            a.Add(b);

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, a.Ids.ToList());
        }

        [Test]
        public void FoldingNullIsANoOp()
        {
            var a = new CombatLedger();
            a.Dealt(HeroId, DamageType.Physical, 10);

            Assert.DoesNotThrow(() => a.Add(null));
            Assert.AreEqual(10, a.For(HeroId).TotalDealt);
        }
    }
}
