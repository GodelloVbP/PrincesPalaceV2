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
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

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

        // ---- opening a fight -------------------------------------------------
        //
        // Reported as "every game after the first elite, none of the buttons
        // respond anymore": monsters standing still, no intent icons, every
        // verb dead, and nothing in the log.
        //
        // AutoResolveEnemyTurns has two callers -- Begin, and the path that runs
        // AFTER the player acts -- so enemies only ever moved in reply to a
        // move. TurnOrder.Start gives turn one to the highest-initiative
        // combatant, and when that was a monster nothing existed to resolve its
        // turn. It held the turn forever. Every symptom follows: intents are
        // telegraphed only on the player's turn, CanAct requires the player's
        // turn, nothing throws, and the stranded-turn watchdog watches _isBusy,
        // which was never set.
        //
        // Begin had 36 call sites and every one was in Tests/. These two pin the
        // contract the game now depends on.

        [Test]
        public void Begin_WhenAMonsterOpens_ResolvesItsTurnAndHandsTheFightToThePlayer()
        {
            // A NARROW speed gap on purpose. TurnOrder advances by rate, so a
            // monster many times the party's speed opens with many consecutive
            // turns -- its own header warns that at Speed 10000 that is a
            // hundred of them and the opponent never acts. The first draft of
            // this test used 40 against 1 and the hero was dead before Begin
            // returned, which asserts something else entirely.
            var hero = Fighter("Hero", true, speed: 10);
            var quick = Fighter("Quick", false, attack: 1, speed: 11);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { quick }));

            Assert.IsFalse(session.IsPlayerTurn, "fixture check: the monster should hold turn one");

            session.Begin();

            Assert.IsTrue(session.IsPlayerTurn,
                "the monster's opening turn was never resolved, so the player is never asked to act " +
                "and every verb stays disabled");
            Assert.IsNotEmpty(session.IntentFor(quick),
                "a fight the player can act in must telegraph what the monster does next");
        }

        [Test]
        public void Begin_IsIdempotent()
        {
            // The player's door and the tests' door both call it, and
            // GrantTurnStart is the half that would show a double call: turn one
            // would pay its regen and tick its statuses twice.
            var (session, hero, foe) = OneOnOne();

            session.Begin();
            int afterFirst = hero.CurrentHealth;
            session.Begin();

            Assert.AreEqual(afterFirst, hero.CurrentHealth,
                "a second Begin granted a second turn start");
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

        // A whole round in one pass, which is the case the recorded order
        // exists for and the one nothing pinned until now. The test above
        // asserts a single beat carries A queue; this asserts that two beats of
        // the same chain carry DIFFERENT ones, and that neither is the order
        // the chain finished on.
        //
        // Without that, painting the tracker from live state during playback
        // looks right in every one-beat fixture and is wrong in every real
        // round: the monster is still mid-swing on screen while the row has
        // already moved past its turn.
        [Test]
        public void EachBeatOfAChainCarriesTheQueueAsItStoodAtThatBeat()
        {
            // Matched speeds so the reply lands in the SAME pass as the swing
            // that provoked it, a tough foe so the fight is not over before the
            // queue means anything, and a feeble one so the hero survives being
            // hit back.
            var hero = Fighter("Hero", true, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, attack: 1, speed: 10);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.Begin();

            Assert.IsTrue(session.IsPlayerTurn, "fixture check: the hero should hold turn one");

            session.ExecuteAttack(foe);
            var beats = session.DrainBeats();

            Assert.AreEqual(2, beats.Count,
                "fixture check: one swing and one reply, both resolved before any of it is drawn");

            Assert.AreSame(hero, beats[0].Actor);
            Assert.AreSame(hero, beats[0].TurnOrder[0],
                "the beat's own actor holds slot 0 -- the queue is captured before the turn advances");

            Assert.AreSame(foe, beats[1].Actor);
            Assert.AreSame(foe, beats[1].TurnOrder[0],
                "the monster's reply must show the monster acting, not whoever acts after it");

            // AND NEITHER IS THE END OF THE ROUND, which is what a view reading
            // live state would have painted on both of them.
            var afterwards = session.Encounter.UpcomingTurns(FightHudSpec.InitiativeSlots);
            Assert.AreSame(hero, afterwards[0], "fixture check: the turn came back to the hero");
            Assert.AreNotSame(afterwards[0], beats[1].TurnOrder[0],
                "the reply's recorded queue is a different moment from the one the chain ended on");
        }

        [Test]
        public void ABeatsTurnOrderIsACopy_AndDoesNotFollowTheQueueOnwards()
        {
            // The same rule BeatFormation states for position: a beat holds a
            // MATERIALISED order, never a live view or a deferred query, or
            // every beat of a chain would report the same list -- whatever the
            // queue held when the view first asked.
            var hero = Fighter("Hero", true, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, attack: 1, speed: 10);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.Begin();

            session.ExecuteAttack(foe);
            var first = session.DrainBeats()[0];
            var recorded = first.TurnOrder.ToList();

            // Another full round, which really does advance the schedule.
            session.ExecuteAttack(foe);
            session.DrainBeats();

            CollectionAssert.AreEqual(recorded, first.TurnOrder.ToList(),
                "the first beat's queue moved when the schedule did");
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

            Assert.IsTrue(session.CanReachEnemy(hero, Reach.Melee, front));
            Assert.IsFalse(session.CanReachEnemy(hero, Reach.Melee, back));
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
