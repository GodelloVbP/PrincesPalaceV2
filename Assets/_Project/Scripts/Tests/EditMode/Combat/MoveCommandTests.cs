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
    // Move: the command that replaced Hold Back. Trade field places with the
    // nearest living ally in one direction, and spend the turn doing it.
    //
    // Sessions built inline, same reasoning as ReachTests: every case is a
    // formation, and there is no formation a shared fixture could field that
    // would serve them all honestly.
    public class MoveCommandTests
    {
        private static CombatantState Member(string name, int speed, int health = 200) =>
            new CombatantState(name, true, health, 10, 20, speed);

        private static CombatantState Foe(string name = "Tank", int health = 100000) =>
            new CombatantState(name, false, health, 0, 1, 1);

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

        [Test]
        public void ASoloPartyCanMoveNeitherWay()
        {
            var lone = Member("Lone", 10);
            var session = Fight(lone);

            Assert.IsFalse(session.CanMove(lone, MoveDirection.Forward));
            Assert.IsFalse(session.CanMove(lone, MoveDirection.Back));

            Assert.IsFalse(session.Move(MoveDirection.Back));
            Assert.AreSame(lone, session.Current, "a refused move spends no turn");
            Assert.IsTrue(Messages(session).Any(m => m.Contains("nowhere to move back")));
        }

        [Test]
        public void TheFrontRankStepsBackAndTradesPlaces()
        {
            var front = Member("Front", 10);
            var behind = Member("Behind", 1);
            var session = Fight(front, behind);

            Assert.IsTrue(session.CanMove(front, MoveDirection.Back));
            Assert.IsFalse(session.CanMove(front, MoveDirection.Forward), "nobody stands in front of rank 0");

            Assert.IsTrue(session.Move(MoveDirection.Back));

            CollectionAssert.AreEqual(new[] { behind, front }, session.Encounter.PlayerParty.ToList());
            Assert.AreSame(behind, session.Encounter.FrontPartyMember);
            Assert.IsTrue(Messages(session).Any(m =>
                m.Contains("Front steps back, trading places with Behind")));
        }

        [Test]
        public void CorpsesAreSteppedOverAndKeepTheirListSlot()
        {
            // The nearest LIVING ally, not the nearest slot. A dead front
            // ranker must not wall the character behind it in.
            var dead = Member("Dead", 1);
            var mover = Member("Mover", 10);
            var session = Fight(dead, mover);
            dead.CurrentHealth = 0;

            Assert.AreSame(mover, session.Current);
            Assert.IsFalse(session.CanMove(mover, MoveDirection.Forward),
                "there is no LIVING ally ahead, only a body");
            Assert.IsFalse(session.CanMove(mover, MoveDirection.Back));

            // With a third member behind, forward is still refused and back is
            // the live direction -- and the corpse's own list slot never moves.
            var behind = Member("Behind", 1);
            var three = Fight(Member("Dead2", 1), Member("Mover2", 10), behind);
            three.Encounter.PlayerParty[0].CurrentHealth = 0;

            Assert.IsTrue(three.Move(MoveDirection.Back));

            Assert.AreEqual("Dead2", three.Encounter.PlayerParty[0].Name,
                "the corpse keeps slot 0; only the two living members traded");
            Assert.AreEqual("Behind", three.Encounter.PlayerParty[1].Name);
            Assert.AreEqual("Mover2", three.Encounter.PlayerParty[2].Name);
        }

        [Test]
        public void RootedRefusesTheMoveFromEitherSideOfTheSwap()
        {
            // ONE RULE, BOTH ROLES. Rooted means "cannot change field
            // position", and a swap changes two of them.
            var mover = Member("Mover", 10);
            var partner = Member("Partner", 1);
            var session = Fight(mover, partner);

            StatusEffects.Apply(mover.Statuses, StatusEffectType.Rooted, 0, 3);
            Assert.IsFalse(session.CanMove(mover, MoveDirection.Back));
            Assert.IsFalse(session.Move(MoveDirection.Back));
            Assert.IsTrue(Messages(session).Any(m => m.Contains("Mover is rooted.")));
            CollectionAssert.AreEqual(new[] { mover, partner }, session.Encounter.PlayerParty.ToList());

            var otherMover = Member("Mover", 10);
            var rootedPartner = Member("Partner", 1);
            var second = Fight(otherMover, rootedPartner);
            StatusEffects.Apply(rootedPartner.Statuses, StatusEffectType.Rooted, 0, 3);

            Assert.IsFalse(second.CanMove(otherMover, MoveDirection.Back));
            Assert.IsFalse(second.Move(MoveDirection.Back));
            Assert.IsTrue(Messages(second).Any(m => m.Contains("Partner is rooted.")));
            CollectionAssert.AreEqual(new[] { otherMover, rootedPartner },
                second.Encounter.PlayerParty.ToList());
            Assert.AreSame(otherMover, second.Current, "and neither refusal spent the turn");
        }

        [Test]
        public void ASuccessfulMoveSpendsTheTurn()
        {
            var front = Member("Front", 10);
            var behind = Member("Behind", 9);
            var session = Fight(front, behind);

            Assert.IsTrue(session.Move(MoveDirection.Back));

            Assert.AreNotSame(front, session.Current,
                "Move ends the turn -- that is its whole cost");
        }

        [Test]
        public void MoveFiresTheDeliberateMoveNoteForTheActingCharacterOnly()
        {
            // SPARRING BUCKLER, NOT SPARRING SABER, and for the reason
            // RelicMechanicsTests' Dancer's Anklet test already gives: a
            // one-turn Speed buff's whole lifecycle sits inside one
            // synchronous round trip, so by the time Move returns control the
            // Saber's buff has already ticked away. Buckler's ward rides
            // Shielded's 99-turn duration and survives it. The Saber's own
            // arithmetic is pinned through the direct seam in
            // RelicMechanicsTests.
            //
            // BOTH members wear one. Move fires the note twice -- once for the
            // mover, once for the partner it displaced -- but both fire with
            // the MOVER as the acting character, so only the mover is paid.
            // A note that credited the displaced partner would ward two
            // characters for one turn.
            var mover = new CombatantState("Mover", true, 200, 10, 20, 100);
            var partner = new CombatantState("Partner", true, 200, 10, 20, 1);
            var buckler = new List<ResolvedRelic>
            {
                new ResolvedRelic("buckler", "Sparring Buckler", "", RelicEffect.SparringBuckler, 0),
            };
            var kits = new List<PlayerKit>
            {
                new PlayerKit("mover", CharacterRole.Tank, null, buckler, DamageType.Physical),
                new PlayerKit("partner", CharacterRole.Tank, null, buckler, DamageType.Physical),
            };

            var session = new FightSession(
                new CombatEncounter(new[] { mover, partner }, new[] { Foe() }),
                kits, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();

            Assert.IsTrue(session.Move(MoveDirection.Back));

            Assert.AreEqual(90, StatusEffects.ConsumeWard(mover, 100).Damage,
                "10 shield points off the mover's next hit -- the footwork was theirs");
            Assert.IsFalse(StatusEffects.IsWarded(partner),
                "and nothing for the one who was displaced");
        }

        [Test]
        public void MovingChangesWhoTheEnemyMeleeCanReach()
        {
            // The whole reason a turn is worth spending on this.
            var front = Member("Front", 10);
            var behind = Member("Behind", 1);
            var session = Fight(front, behind);
            var foe = session.Encounter.Enemies[0];

            CollectionAssert.AreEqual(new[] { front },
                session.EligibleTargets(foe, Reach.Melee).ToList());

            session.Move(MoveDirection.Back);

            CollectionAssert.AreEqual(new[] { behind },
                session.EligibleTargets(foe, Reach.Melee).ToList());
        }

        [Test]
        public void AMovesBeatRecordsTheOrderItLeftBehindAndKeepsIt()
        {
            // THE SNAPSHOT IS A COPY, and this is the assertion that says so.
            //
            // The party list is written IN PLACE by SwapPartySlots, so a beat
            // holding a reference to it -- or a deferred LINQ query over it --
            // would report whatever order the round happened to finish on,
            // several beats after the one being drawn. That is the same bug
            // the vitals snapshot exists for, and it is invisible until two
            // moves land in one round.
            var front = Member("Front", 10);
            var behind = Member("Behind", 1);
            var session = Fight(front, behind);

            Assert.IsTrue(session.Move(MoveDirection.Back));

            var beats = session.DrainBeats();
            Assert.AreEqual(1, beats.Count, "one Move, one beat");
            var formation = beats[0].Formation;

            CollectionAssert.AreEqual(new[] { behind, front }, formation.Party.ToList(),
                "the beat records the order the swap PRODUCED -- captured at CommitBeat, " +
                "which is after SwapPartySlots ran");

            // And now move them back, which rewrites the very list the beat
            // would have been pointing at.
            Assert.IsTrue(session.Encounter.SwapPartySlots(0, 1));
            CollectionAssert.AreEqual(new[] { front, behind },
                session.Encounter.PlayerParty.ToList(), "the live list really did move again");

            CollectionAssert.AreEqual(new[] { behind, front }, formation.Party.ToList(),
                "the beat's formation is a copy and did not follow the list");
        }

        [Test]
        public void AFormationCarriesTheDeadInListOrderSoTheViewCanHoldTheirGround()
        {
            // LIST ORDER, CORPSES INCLUDED -- see BeatFormation's own header.
            // A formation of the living alone compacts on the very beat a kill
            // lands, which would slide the survivors forward through a body
            // still standing at full opacity.
            var dead = Member("Dead", 1);
            var mover = Member("Mover", 10);
            var behind = Member("Behind", 2);
            var session = Fight(dead, mover, behind);
            dead.CurrentHealth = 0;

            Assert.IsTrue(session.Move(MoveDirection.Back));

            var formation = session.DrainBeats()[0].Formation;
            CollectionAssert.AreEqual(new[] { dead, behind, mover }, formation.Party.ToList());
            Assert.AreEqual(1, session.Encounter.LivingRankOf(mover),
                "the RULE still ranks among the living only; the formation is the view's copy of the order");
        }
    }
}
