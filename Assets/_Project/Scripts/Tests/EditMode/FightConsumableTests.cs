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
    // Drinking a potion mid-fight.
    //
    // The satchel column has existed since the fight screen was rebuilt and had
    // nothing behind it -- Bind's satchel argument was never supplied and
    // ItemUsed had no subscriber. These pin the command that was missing.
    public class FightConsumableTests
    {
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            int health = 300, int mana = 30)
        {
            var hero = new CombatantState("Shawn", true, health, mana, 20, 0, 10);
            // Speed 9 against the hero's 10, so the monster genuinely replies
            // to every action. At speed 1 it barely acts and "the potion cost
            // the turn" becomes unobservable -- a fixture that quietly removes
            // the thing under test.
            var foe = new CombatantState("Rat", false, 5000, 10, 5, 0, 9);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null);

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(4))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        private static IEnumerable<string> Lines(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages);

        // What the potion did, read off the beat it recorded rather than off
        // live state. It costs the turn, so by the time the call returns the
        // monster has already swung -- live health is the potion MINUS the
        // reply, which is not what any of these are asking about.
        private static Vitals AfterTheDrink(FightSession session, CombatantState hero) =>
            session.DrainBeats().First().Snapshot[hero];

        [Test]
        public void APotionHeals()
        {
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            session.UseConsumable("Health Potion", 60, restoresMana: false);

            Assert.AreEqual(160, AfterTheDrink(session, hero).Health);
        }

        [Test]
        public void AnEtherRestoresManaRatherThanHealth()
        {
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;
            hero.CurrentMana = 5;

            session.UseConsumable("Ether", 15, restoresMana: true);
            var after = AfterTheDrink(session, hero);

            Assert.AreEqual(20, after.Mana);
            Assert.AreEqual(100, after.Health, "a mana potion does not heal");
        }

        [Test]
        public void HealingIsClampedAndTheNumberShownIsTheNumberThatHappened()
        {
            // A 200-point potion on a hero missing 30 restores 30. Reporting
            // 200 would be a lie the player can check against their own bar.
            var (session, hero, _) = Fight(health: 300);
            hero.CurrentHealth = 270;

            session.UseConsumable("Health Potion", 200, restoresMana: false);

            var beats = session.DrainBeats();
            Assert.AreEqual(300, beats.First().Snapshot[hero].Health, "clamped at full");
            Assert.IsTrue(beats.SelectMany(b => b.Messages).Any(m => m.Contains("recovers 30")),
                "the message reports what was actually restored");
        }

        [Test]
        public void DrinkingAtFullHealthSaysNothingChanged()
        {
            // Reachable: the player can always press the button. Silence would
            // read as the button being broken.
            var (session, hero, _) = Fight();

            session.UseConsumable("Health Potion", 50, restoresMana: false);

            Assert.IsTrue(Lines(session).Any(m => m.Contains("nothing changes")));
        }

        [Test]
        public void ItCostsTheTurn()
        {
            // A free heal would be strictly better than defending, and every
            // fight would open with the whole satchel.
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            session.UseConsumable("Health Potion", 20, restoresMana: false);
            var beats = session.DrainBeats();

            Assert.IsTrue(beats.Any(b => b.Actor != null && !b.Actor.IsPlayerSide),
                "the monster got its turn, so the potion cost one");
        }

        [Test]
        public void TheBeatCarriesItAsHealingSoTheNumberIsGreen()
        {
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            session.UseConsumable("Health Potion", 40, restoresMana: false);
            var beat = session.DrainBeats().First();

            Assert.AreEqual(40, beat.Amount);
            Assert.IsTrue(beat.IsHealing, "the one visual difference between being helped and being hit");
        }

        [Test]
        public void ANamelessItemIsStillUsable()
        {
            // Graceful degradation: content with a blank displayName must not
            // take the fight down.
            var (session, hero, _) = Fight();
            hero.CurrentHealth = 100;

            Assert.DoesNotThrow(() => session.UseConsumable("", 10, restoresMana: false));
            Assert.AreEqual(110, AfterTheDrink(session, hero).Health);
        }
    }
}
