using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // Field seats (docs/PLAN_BELLWETHER_KIT.md 1.1, 3.1, M1): three seats a
    // side, empty seats real, Move is one seat through the one placement rule
    // (CombatEncounter.PlaceAt), and close-range reach still reads rank among
    // the living. Every expected seat is a literal: 0 front, 1 middle, 2 rear.
    public class FieldSeatTests
    {
        private static CombatantState Member(string name, int speed, int health = 5000) =>
            new CombatantState(name, true, health, 10, 20, speed);

        // Attack 1 against 5000 health: the foe's swings can never decide a
        // case here, only its reach can.
        private static CombatantState Foe() => new CombatantState("Tank", false, 100000, 0, 1, 1);

        private static FightSession Fight(params CombatantState[] party)
        {
            var session = new FightSession(
                new CombatEncounter(party, new[] { Foe() }), null, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return session;
        }

        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages());

        private static int[] Seats(FightSession session, params CombatantState[] members) =>
            members.Select(m => session.Encounter.SeatOf(m)).ToArray();

        // ---- a lone character walks the whole line ---------------------------

        [Test]
        public void ASoloShawnStepsBackThroughAllThreeSeatsAndForwardAgain()
        {
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);

            Assert.AreEqual(0, session.Encounter.SeatOf(shawn), "a fight opens with the lone member at the front");
            Assert.IsFalse(session.CanMove(shawn, MoveDirection.Forward), "nothing is in front of the front seat");
            Assert.IsTrue(session.CanMove(shawn, MoveDirection.Back), "the empty middle is a legal step");

            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.IsTrue(Messages(session).Any(m => m == "Shawn steps back into the empty middle."));
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
            Assert.AreSame(shawn, session.Current, "the foe answered and the turn came back round");

            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.IsFalse(session.CanMove(shawn, MoveDirection.Back), "nothing is behind the rear seat");

            Assert.IsTrue(session.Move(MoveDirection.Forward));
            Assert.IsTrue(Messages(session).Any(m => m == "Shawn steps forward into the empty middle."));
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));

            Assert.IsTrue(session.Move(MoveDirection.Forward));
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));

            Assert.AreEqual(0, session.Encounter.LivingRankOf(shawn),
                "rank is among the living, and he is the only one: rank 0 in every seat");
        }

        [Test]
        public void ARefusedStepOffTheEndOfTheLineSpendsNothing()
        {
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);
            Messages(session);

            Assert.IsFalse(session.Move(MoveDirection.Forward));
            Assert.AreSame(shawn, session.Current, "a refused move spends no turn");
            Assert.AreEqual(0, session.DrainBeats().Count, "and records no beat");
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m == "Shawn has nowhere to move forward."));
        }

        // ---- close-range reach is unchanged -----------------------------------

        [Test]
        public void MeleeStillReachesALoneShawnInTheRearSeat()
        {
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);
            var foe = session.Encounter.Enemies[0];

            session.Move(MoveDirection.Back);
            session.Move(MoveDirection.Back);
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));

            CollectionAssert.AreEqual(new[] { shawn }, session.EligibleTargets(foe, Reach.Melee).ToList(),
                "no hiding in an empty rear: melee reads rank among the living");
            Assert.AreSame(shawn, session.Encounter.FrontPartyMember);
        }

        [Test]
        public void MeleeStillReachesTheLastSurvivorInTheRearSeat()
        {
            var fallen = Member("Fallen", 1);
            var survivor = Member("Survivor", 10);
            var session = Fight(fallen, survivor);
            var foe = session.Encounter.Enemies[0];
            fallen.CurrentHealth = 0;

            Assert.AreEqual(0, session.Encounter.SeatOf(survivor), "a corpse holds no seat");
            session.Move(MoveDirection.Back);
            session.Move(MoveDirection.Back);
            Assert.AreEqual(2, session.Encounter.SeatOf(survivor));

            CollectionAssert.AreEqual(new[] { survivor }, session.EligibleTargets(foe, Reach.Melee).ToList());
        }

        // ---- shorter parties stand in seats of three (R1) ----------------------

        [Test]
        public void ADuoOpensFrontAndMiddleAndTheSecondCanStepIntoTheEmptyRear()
        {
            var front = Member("Front", 1);
            var second = Member("Second", 10);
            var session = Fight(front, second);

            CollectionAssert.AreEqual(new[] { 0, 1 }, Seats(session, front, second));
            Assert.IsNull(session.Encounter.OccupantOf(2), "the rear seat is empty");

            Assert.AreSame(second, session.Current);
            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.IsTrue(Messages(session).Any(m => m == "Second steps back into the empty rear."));

            CollectionAssert.AreEqual(new[] { 0, 2 }, Seats(session, front, second));
            Assert.IsNull(session.Encounter.OccupantOf(1), "the middle is empty now");
            Assert.AreEqual(1, session.Encounter.LivingRankOf(second), "still rank 1: nobody died");
            CollectionAssert.AreEqual(new[] { front, second }, session.Encounter.PlayerParty.ToList(),
                "stepping into an empty seat changes no one's list order");
        }

        [Test]
        public void ATrioThatLostItsFrontClosesUpToFrontAndMiddle()
        {
            var a = Member("A", 1);
            var b = Member("B", 2);
            var c = Member("C", 3);
            var session = Fight(a, b, c);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Seats(session, a, b, c));

            a.CurrentHealth = 0;

            CollectionAssert.AreEqual(new[] { -1, 0, 1 }, Seats(session, a, b, c),
                "everyone behind the fallen moves forward one seat");
            Assert.IsNull(session.Encounter.OccupantOf(2));
        }

        [Test]
        public void ATrioThatLostItsMiddleClosesUpOnlyBehindIt()
        {
            var a = Member("A", 1);
            var b = Member("B", 2);
            var c = Member("C", 3);
            var session = Fight(a, b, c);

            b.CurrentHealth = 0;

            CollectionAssert.AreEqual(new[] { 0, -1, 1 }, Seats(session, a, b, c));
        }

        [Test]
        public void ADeathInFrontOfAnEmptySeatMovesTheOneBehindForwardOneSeat()
        {
            // Front 0, the middle emptied by Second stepping back, Second 2.
            // Front falls: Second is behind him and moves forward ONE seat,
            // to the middle -- the empty seat stays in front of him, and he
            // is still rank 0 for every melee rule.
            var front = Member("Front", 1);
            var second = Member("Second", 10);
            var session = Fight(front, second);
            session.Move(MoveDirection.Back);
            CollectionAssert.AreEqual(new[] { 0, 2 }, Seats(session, front, second));

            front.CurrentHealth = 0;

            Assert.AreEqual(1, session.Encounter.SeatOf(second));
            Assert.AreEqual(0, session.Encounter.LivingRankOf(second));
            Assert.IsNull(session.Encounter.OccupantOf(0));
        }

        // ---- a full party is exactly what it was -------------------------------

        [Test]
        public void AFullPartysSeatIsItsRankAndAStepBackIsTheOldTrade()
        {
            var a = Member("A", 10);
            var b = Member("B", 2);
            var c = Member("C", 1);
            var session = Fight(a, b, c);

            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.IsTrue(Messages(session).Any(m => m == "A steps back, trading places with B."));

            CollectionAssert.AreEqual(new[] { b, a, c }, session.Encounter.PlayerParty.ToList());
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, Seats(session, a, b, c));
            foreach (var m in new[] { a, b, c })
            {
                Assert.AreEqual(session.Encounter.LivingRankOf(m), session.Encounter.SeatOf(m), m.Name);
            }
        }

        // ---- rooted, both ends -------------------------------------------------

        [Test]
        public void ARootedLoneShawnCannotStepIntoAnEmptySeat()
        {
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);
            StatusEffects.Apply(shawn.Statuses, StatusEffectType.Rooted, 0, 3);
            Messages(session);

            Assert.IsFalse(session.CanMove(shawn, MoveDirection.Back));
            Assert.IsFalse(session.Move(MoveDirection.Back));
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m == "Shawn is rooted."));
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
        }

        [Test]
        public void PlaceAtRefusesARootedOccupantAndChangesNothing()
        {
            var mover = Member("Mover", 10);
            var anchor = Member("Anchor", 1);
            var session = Fight(mover, anchor);
            StatusEffects.Apply(anchor.Statuses, StatusEffectType.Rooted, 0, 3);

            Assert.AreEqual(PlaceOutcome.OccupantRooted,
                session.Encounter.PlaceAt(mover, 1, out var occupant));
            Assert.AreSame(anchor, occupant);
            CollectionAssert.AreEqual(new[] { 0, 1 }, Seats(session, mover, anchor));

            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(mover, 2, out occupant),
                "the empty rear has no occupant to be rooted");
            Assert.IsNull(occupant);
            CollectionAssert.AreEqual(new[] { 2, 1 }, Seats(session, mover, anchor));
        }

        [Test]
        public void PlaceAtRefusesItsOwnSeatOffTheFieldTheDeadAndEnemies()
        {
            var shawn = Member("Shawn", 10);
            var dead = Member("Dead", 1);
            var session = Fight(shawn, dead);
            dead.CurrentHealth = 0;
            var foe = session.Encounter.Enemies[0];

            Assert.AreEqual(PlaceOutcome.NoSuchSeat, session.Encounter.PlaceAt(shawn, 0, out _));
            Assert.AreEqual(PlaceOutcome.NoSuchSeat, session.Encounter.PlaceAt(shawn, 3, out _));
            Assert.AreEqual(PlaceOutcome.NoSuchSeat, session.Encounter.PlaceAt(shawn, -1, out _));
            Assert.AreEqual(PlaceOutcome.NotOnTheField, session.Encounter.PlaceAt(dead, 2, out _));
            Assert.AreEqual(PlaceOutcome.NotOnTheField, session.Encounter.PlaceAt(foe, 1, out _));
            Assert.AreEqual(0, session.Encounter.SeatOf(foe), "an enemy's seat is its living rank");
        }

        [Test]
        public void PlaceAtCanJumpTwoSeatsForTheCallersThatNeedIt()
        {
            // Move is one seat; Palace Passage and Reposition (later
            // milestones) place anywhere, through this same door.
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);

            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, 2, out _));
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, 0, out _));
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            CollectionAssert.AreEqual(new[] { shawn }, session.Encounter.PartyField.ToList(),
                "no trailing holes are kept once he is back at the front");
        }

        // ---- the beat carries the seats ---------------------------------------

        [Test]
        public void EachBeatKeepsTheSeatsItResolvedIn()
        {
            var shawn = Member("Shawn", 10);
            var session = Fight(shawn);
            session.DrainBeats();

            session.Move(MoveDirection.Back);
            var first = session.DrainBeats().First(b => b.Actor == shawn).Formation;

            session.Move(MoveDirection.Back);
            var second = session.DrainBeats().First(b => b.Actor == shawn).Formation;

            Assert.AreEqual(1, FieldSeating.SeatIn(first.PartyField, shawn, c => c.IsAlive));
            Assert.AreEqual(2, FieldSeating.SeatIn(second.PartyField, shawn, c => c.IsAlive));
            CollectionAssert.AreEqual(new[] { shawn }, first.Party.ToList(),
                "Party stays members only; the holes live in PartyField");
        }

        // ---- the counting rule itself ------------------------------------------

        [Test]
        public void ACorpseThatStillHoldsItsPlaceKeepsTheLineBehindItWhereItWas()
        {
            // The stage's reading: a body that has not finished fading holds
            // its seat, so the survivor behind it has not moved yet.
            var body = Member("Body", 1);
            var survivor = Member("Survivor", 1);
            body.CurrentHealth = 0;
            var field = new[] { body, survivor };

            Assert.AreEqual(0, FieldSeating.SeatIn(field, survivor, c => c.IsAlive));
            Assert.AreEqual(1, FieldSeating.SeatIn(field, survivor, c => true));
        }

        [Test]
        public void ARevivalThatOverfillsTheLineDropsTheFrontmostSurplusHole()
        {
            // [A, B, hole, C] only arises when A died, B and C rearranged
            // around the hole, and A stood up again (Second Life): four
            // holders for three seats. The hole is the one that yields.
            var a = Member("A", 1);
            var b = Member("B", 1);
            var c = Member("C", 1);
            var field = new[] { a, b, null, c };

            Assert.AreEqual(1, FieldSeating.ExcessHoles(field, m => m.IsAlive));
            Assert.AreEqual(0, FieldSeating.SeatIn(field, a, m => m.IsAlive));
            Assert.AreEqual(1, FieldSeating.SeatIn(field, b, m => m.IsAlive));
            Assert.AreEqual(2, FieldSeating.SeatIn(field, c, m => m.IsAlive));
        }
    }
}
