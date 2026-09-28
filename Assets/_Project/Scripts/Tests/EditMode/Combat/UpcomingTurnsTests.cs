using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // Covers CombatEncounter.UpcomingTurns, which is what the initiative
    // tracker renders. Kept separate from CombatEncounterTests because these
    // are about the DISPLAY contract (what the player is promised is coming)
    // rather than about turn resolution itself.
    public class UpcomingTurnsTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int speed, int maxHealth = 20)
        {
            return new CombatantState(name, isPlayerSide, maxHealth, 10, 5, speed);
        }

        [Test]
        public void FirstEntryIsWhoeverIsActingRightNow()
        {
            var fast = Fighter("Fast", true, speed: 10);
            var slow = Fighter("Slow", false, speed: 1);
            var encounter = new CombatEncounter(new[] { fast }, new[] { slow });

            var upcoming = encounter.UpcomingTurns(2);

            Assert.AreSame(encounter.Current, upcoming[0],
                "The tracker's first icon is the combatant currently acting, not the one after them.");
            Assert.AreSame(fast, upcoming[0]);
        }

        // Charge scheduling means speed decides how OFTEN you act, not
        // merely in what order, so the same combatant can and should appear
        // more than once before a slower one appears at all. Asserting
        // frequency is the only way to cover that.
        [Test]
        public void AFasterCombatantAppearsMoreOftenThanASlowerOne()
        {
            var medium = Fighter("Medium", true, speed: 5);
            var fastest = Fighter("Fastest", true, speed: 20);
            var slowest = Fighter("Slowest", false, speed: 1);
            var encounter = new CombatEncounter(new[] { medium, fastest }, new[] { slowest });

            var names = encounter.UpcomingTurns(30).Select(c => c.Name).ToList();

            int fastestTurns = names.Count(n => n == "Fastest");
            int mediumTurns = names.Count(n => n == "Medium");
            int slowestTurns = names.Count(n => n == "Slowest");

            Assert.Greater(fastestTurns, mediumTurns, "Speed 20 should out-turn speed 5");
            Assert.Greater(mediumTurns, slowestTurns, "Speed 5 should out-turn speed 1");
        }

        // The other half of the same contract, and the reason SpeedScale has
        // a MinRate at all: being outpaced must never become being frozen
        // out. A combatant who never gets a turn is a soft-lock, not a
        // disadvantage.
        [Test]
        public void EvenTheSlowestCombatantStillGetsTurns()
        {
            var blur = Fighter("Blur", true, speed: 40);
            var boulder = Fighter("Boulder", false, speed: 1);
            var encounter = new CombatEncounter(new[] { blur }, new[] { boulder });

            var names = encounter.UpcomingTurns(40).Select(c => c.Name).ToList();

            CollectionAssert.Contains(names, "Boulder",
                "A very slow combatant must still act eventually — starvation is a soft-lock, not a drawback.");
        }

        // The ceiling that makes turn-cheese impossible rather than merely
        // expensive. SpeedScale.MaxRate is 2.5, so however absurd the speed,
        // no combatant may take more than ~2.5 turns per baseline turn.
        [Test]
        public void NoAmountOfSpeedBreaksTheTurnsPerRoundCeiling()
        {
            var absurd = Fighter("Absurd", true, speed: 10_000);
            var baseline = Fighter("Baseline", false, speed: (int)SpeedScale.BaselineSpeed);
            var encounter = new CombatEncounter(new[] { absurd }, new[] { baseline });

            var names = encounter.UpcomingTurns(60).Select(c => c.Name).ToList();

            int absurdTurns = names.Count(n => n == "Absurd");
            int baselineTurns = names.Count(n => n == "Baseline");

            Assert.Greater(baselineTurns, 0, "The baseline combatant must not be frozen out");
            Assert.LessOrEqual(absurdTurns / (float)baselineTurns, SpeedScale.MaxRate + 0.5f,
                $"Speed 10000 took {absurdTurns} turns to baseline's {baselineTurns} — MaxRate is supposed to cap this");
        }

        // The bar has a fixed number of icons, so a small fight has to wrap
        // into the next round rather than render half-empty.
        [Test]
        public void WrapsIntoFollowingRoundsWhenAskedForMoreTurnsThanCombatants()
        {
            var hero = Fighter("Hero", true, speed: 10);
            var rat = Fighter("Rat", false, speed: 5);
            var encounter = new CombatEncounter(new[] { hero }, new[] { rat });

            var names = encounter.UpcomingTurns(5).Select(c => c.Name).ToArray();

            CollectionAssert.AreEqual(new[] { "Hero", "Rat", "Hero", "Rat", "Hero" }, names);
        }

        [Test]
        public void SkipsTheDefeated()
        {
            var hero = Fighter("Hero", true, speed: 30);
            var doomed = Fighter("Doomed", false, speed: 20);
            var survivor = Fighter("Survivor", false, speed: 10);
            var encounter = new CombatEncounter(new[] { hero }, new[] { doomed, survivor });

            doomed.CurrentHealth = 0;

            var upcoming = encounter.UpcomingTurns(4);
            var names = upcoming.Select(c => c.Name).ToArray();

            CollectionAssert.DoesNotContain(names, "Doomed",
                "A defeated combatant must never be shown as an upcoming turn — AdvanceTurn skips them, so promising their turn would be a lie.");
            Assert.AreEqual(4, upcoming.Count, "The tracker should still fill, not come up short because one entry was skipped");
            CollectionAssert.AllItemsAreNotNull(upcoming);
            Assert.IsTrue(upcoming.All(c => c.IsAlive), "Every projected turn must belong to someone still standing");
            Assert.AreSame(hero, upcoming[0], "The fastest living combatant opens");
        }

        // Two identical monsters are a normal encounter (a Fight room can
        // roll the same EnemyDefinition twice). They must occupy distinct
        // slots in the tracker rather than collapsing into one.
        [Test]
        public void DistinguishesTwoCombatantsWithIdenticalStatsAndName()
        {
            var hero = Fighter("Hero", true, speed: 30);
            var ratA = Fighter("Giant Rat", false, speed: 10);
            var ratB = Fighter("Giant Rat", false, speed: 10);
            var encounter = new CombatEncounter(new[] { hero }, new[] { ratA, ratB });

            var upcoming = encounter.UpcomingTurns(3);

            Assert.AreEqual(3, upcoming.Count);
            Assert.AreSame(hero, upcoming[0]);
            CollectionAssert.AreEquivalent(new[] { ratA, ratB }, new[] { upcoming[1], upcoming[2] },
                "Both rats should appear as separate entries, not the same one twice.");
        }

        [Test]
        public void ReturnsNothingOnceTheFightIsOver()
        {
            var hero = Fighter("Hero", true, speed: 10);
            var rat = Fighter("Rat", false, speed: 5);
            var encounter = new CombatEncounter(new[] { hero }, new[] { rat });

            rat.CurrentHealth = 0;

            Assert.IsTrue(encounter.IsOver);
            CollectionAssert.IsEmpty(encounter.UpcomingTurns(4),
                "With one side wiped there is no next turn, so the tracker should empty rather than loop the survivors forever.");
        }

        [Test]
        public void RejectsNonPositiveCounts()
        {
            var encounter = new CombatEncounter(new[] { Fighter("Hero", true, 10) }, new[] { Fighter("Rat", false, 5) });

            Assert.Throws<ArgumentOutOfRangeException>(() => encounter.UpcomingTurns(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => encounter.UpcomingTurns(-1));
        }

        // FightSession hands its initiative depth to UpcomingTurns
        // at every CommitBeat, so a zero depth must be refused at
        // construction rather than throw out of the middle of the first
        // action.
        [Test]
        public void AFightSessionRefusesAZeroInitiativeDepthAtConstruction()
        {
            var encounter = new CombatEncounter(new[] { Fighter("Hero", true, 10) }, new[] { Fighter("Rat", false, 5) });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FightSession(encounter, null, null, new Rng.SeededRandom(1), initiativeSlots: 0));
            Assert.DoesNotThrow(() =>
                new FightSession(encounter, null, null, new Rng.SeededRandom(1), initiativeSlots: 1));
        }
    }
}
