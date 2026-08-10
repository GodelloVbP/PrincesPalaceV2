using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The session resolving a fight, with no scene anywhere.
    //
    // This is the conversion the whole decomposition is for: in v1 every one of
    // these would have loaded the Gameplay scene, built a fight overlay and
    // clicked a button. Here they run in milliseconds.
    public class FightSessionTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, 0, speed);

        // Variance off everywhere below, so a damage assertion is a fact rather
        // than a range. The roll has its own tests.
        private static FightSession Session(CombatEncounter encounter) =>
            new FightSession(encounter, null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };

        private static (FightSession session, CombatantState hero, CombatantState foe) OneOnOne(
            int foeHealth = 100, int heroSpeed = 10)
        {
            var hero = Fighter("Hero", true, speed: heroSpeed);
            var foe = Fighter("Foe", false, maxHealth: foeHealth, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            return (Session(encounter), hero, foe);
        }

        [Test]
        public void ExecuteAttack_DamagesTheTargetAndRecordsOneBeat()
        {
            var (session, hero, foe) = OneOnOne();
            int before = foe.CurrentHealth;

            session.ExecuteAttack(foe);

            Assert.Less(foe.CurrentHealth, before, "the swing should land");

            var beats = session.DrainBeats();
            Assert.AreEqual(1, beats.Count);
            Assert.AreSame(hero, beats[0].Actor);
            Assert.AreSame(foe, beats[0].Target);
            Assert.Greater(beats[0].Amount, 0, "the beat carries what it dealt, for the floating number");
        }

        [Test]
        public void TheBeatSnapshotIsFrozenAtResolveTime()
        {
            // The whole reason snapshots exist: by the time a beat is PLAYED,
            // later actions in the same chain have already moved live state.
            var (session, _, foe) = OneOnOne();

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];
            int recorded = beat.Snapshot[foe].Health;

            foe.CurrentHealth = 1;

            Assert.AreEqual(recorded, beat.Snapshot[foe].Health, "a snapshot must not follow live state");
            Assert.AreNotEqual(1, recorded);
        }

        [Test]
        public void ThePreSnapshotHoldsVitalsFromBeforeTheBlow()
        {
            // A spell's bolt takes most of a second to arrive; dropping the
            // target's HP the instant the beat opens shows the damage before
            // the spell has left the ceiling.
            var (session, _, foe) = OneOnOne();
            int before = foe.CurrentHealth;

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];

            Assert.AreEqual(before, beat.PreSnapshot[foe].Health);
            Assert.Less(beat.Snapshot[foe].Health, beat.PreSnapshot[foe].Health);
        }

        [Test]
        public void MessagesLandOnTheBeatThatProducedThem()
        {
            var (session, _, foe) = OneOnOne();

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];

            CollectionAssert.IsNotEmpty(beat.Messages);
            Assert.IsTrue(beat.Messages.Any(m => m.Contains("attacks")),
                "the swing's own line belongs to the swing's beat, not to the log up front");
        }

        [Test]
        public void AMessageAfterTheBeatClosed_RetroAttachesToIt()
        {
            // AUDIT #13. An extra turn earned by a kill is decided AFTER the
            // beat committed but describes the same moment. Left as an
            // immediate message it lands OLDER than the blow that caused it,
            // and the trim drops from the front -- so the consequence gets
            // discarded to make room for its own cause.
            var (session, _, foe) = OneOnOne();

            session.ExecuteAttack(foe);
            session.AppendMessage("Bloodlust grants an extra turn!");

            var beats = session.DrainBeats();
            Assert.AreEqual(1, beats.Count, "no new beat should be invented for a trailing message");
            Assert.IsTrue(beats[0].Messages.Contains("Bloodlust grants an extra turn!"));
            CollectionAssert.IsEmpty(session.DrainImmediateMessages());
        }

        [Test]
        public void AMessageBeforeAnyBeat_StaysImmediate()
        {
            // Fight-opening lines have no beat to belong to and must still show.
            var (session, _, _) = OneOnOne();

            session.AppendMessage("The bog witch blocks your path!");

            CollectionAssert.IsEmpty(session.DrainBeats());
            CollectionAssert.Contains(session.DrainImmediateMessages(), "The bog witch blocks your path!");
        }

        [Test]
        public void AKillPosesTheTargetDefeated_AndSaysSo()
        {
            var (session, _, foe) = OneOnOne(foeHealth: 1);

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];

            Assert.IsFalse(foe.IsAlive);
            Assert.AreEqual(FightSession.Stances.Defeated, beat.Stances[foe]);
            Assert.IsTrue(beat.Messages.Any(m => m.Contains("is defeated")));
        }

        [Test]
        public void ASurvivorPosesHurt()
        {
            var (session, _, foe) = OneOnOne(foeHealth: 500);

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];

            Assert.IsTrue(foe.IsAlive);
            Assert.AreEqual(FightSession.Stances.Hurt, beat.Stances[foe]);
        }

        [Test]
        public void TheBeatCarriesTheTurnOrderAsItStoodWhenItResolved()
        {
            // v1 sized this snapshot from `initiativeIcons.Length` -- combat
            // logic reading a UI array to decide how far ahead to simulate.
            // It comes from FightHudSpec now, through the constructor.
            //
            // A tough foe on purpose: UpcomingTurns correctly returns nothing
            // once a fight is decided, so a one-shot kill would test the
            // encounter-is-over path rather than the projection.
            var (session, _, foe) = OneOnOne(foeHealth: 1000);

            session.ExecuteAttack(foe);
            var beat = session.DrainBeats()[0];

            Assert.IsNotNull(beat.TurnOrder);
            Assert.AreEqual(FightHudSpec.InitiativeSlots, beat.TurnOrder.Count);
        }

        [Test]
        public void DrainingIsDestructive_SoNoBeatPlaysTwice()
        {
            var (session, _, foe) = OneOnOne();

            session.ExecuteAttack(foe);
            Assert.AreEqual(1, session.DrainBeats().Count);
            CollectionAssert.IsEmpty(session.DrainBeats(), "a drained beat must not come back");
        }

        [Test]
        public void MeleeReachIsAskedOfTheSession_NotRecomputedByTheView()
        {
            var hero = Fighter("Hero", true, speed: 10);
            var front = Fighter("Front", false);
            var back = Fighter("Back", false);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { front, back }));

            Assert.IsTrue(session.CanMeleeReach(front));
            Assert.IsFalse(session.CanMeleeReach(back));
        }

        [Test]
        public void AnActorWithNoKitHasNoSkillOptions()
        {
            // A direct/test entry with no character behind it. Empty rather than
            // a throw: the same graceful posture the rest of the project takes.
            var (session, hero, _) = OneOnOne();
            CollectionAssert.IsEmpty(session.SkillOptionsFor(hero));
        }
    }
}
