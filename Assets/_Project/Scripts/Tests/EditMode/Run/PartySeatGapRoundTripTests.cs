using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // An empty seat is a state the model has AND the save now carries.
    //
    // PartyFormation keeps seats by index and leaves a hole where one is
    // vacated: SendToBench writes `_seats[seat] = null` in place, so benching
    // the front-ranker leaves [null, Mid, Rear]. That is deliberate -- the
    // type's own header says "POSITIONS ARE MECHANICAL, not cosmetic: seat 0
    // is the front rank enemy melee concentrates on".
    //
    // The save carries the hole: an empty front rank is a formation the
    // player can choose, so the list is a seat list, and an empty seat is
    // the empty string in place.
    //
    // WHY "" AND NOT null: JsonUtility serialises a null element of a
    // List<string> as "" and reads it back as "", so a null hole would not
    // survive its own round trip. The sentinel is the one the serialiser
    // already produces rather than a second one layered over it -- see
    // SaveData.IsEmptySeat, which is what every reader asks.
    //
    // The two production lines are transcribed here rather than called: they
    // live on a MonoBehaviour in Core (PartyController.Persist / .Refresh)
    // that an EditMode test cannot stand up, and SaveData itself is Core. Kept
    // verbatim so this fails to describe the game the moment either moves.
    public class PartySeatGapRoundTripTests
    {
        private const string Front = "hero";
        private const string Mid = "mage";
        private const string Rear = "rogue";

        // SaveData.EmptySeat, transcribed for the same reason the two methods
        // below are: SaveData is Core and this suite is engine-free Domain.
        private const string EmptySeat = "";

        private static List<PartyRosterEntry> Roster() => new List<PartyRosterEntry>
        {
            new PartyRosterEntry(Front, "Hero", hasArt: true),
            new PartyRosterEntry(Mid, "Mage", hasArt: true),
            new PartyRosterEntry(Rear, "Rogue", hasArt: true),
        };

        private static readonly Func<int, bool> NeverLocked = _ => false;

        // PartyController.SeatList, verbatim -- what Persist writes.
        private static List<string> Persist(PartyFormation formation)
        {
            var seats = formation.SeatIds.Select(id => id ?? EmptySeat).ToList();
            while (seats.Count > 0 && string.IsNullOrEmpty(seats[seats.Count - 1]))
            {
                seats.RemoveAt(seats.Count - 1);
            }

            return seats;
        }

        // PartyController.Refresh's seat loop, verbatim.
        private static PartyFormation Load(IReadOnlyList<string> saved)
        {
            var rosterSet = new HashSet<string>(Roster().Select(r => r.Id));
            var seats = new List<string>();
            var seen = new HashSet<string>();

            for (int i = 0; i < PartySeat.Count; i++)
            {
                string id = i < saved.Count ? saved[i] : null;
                if (string.IsNullOrEmpty(id)) id = null;
                if (id != null && (!rosterSet.Contains(id) || !seen.Add(id))) id = null;
                seats.Add(id);
            }

            return new PartyFormation(Roster(), seats, PartyMode.Camp, 3, NeverLocked);
        }

        [Test]
        public void BenchingTheFrontRanker_DoesNotPromoteTheMiddleSeatOnTheWayThroughTheSave()
        {
            var formation = new PartyFormation(
                Roster(), new[] { Front, Mid, Rear }, PartyMode.Camp, 3, NeverLocked);

            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.SendToBench();

            Assert.IsTrue(outcome.Changed, "benching the front-ranker should have committed");
            Assert.IsNull(formation.SeatIds[PartySeat.Front],
                "the model keeps the hole where the benched member was");
            Assert.AreEqual(Mid, formation.SeatIds[PartySeat.Middle],
                "and leaves everybody else where the player put them");

            var written = Persist(formation);
            CollectionAssert.AreEqual(new[] { EmptySeat, Mid, Rear }, written,
                "the save compacted the hole away instead of keeping the seat empty in place");

            var reloaded = Load(written);

            Assert.IsNull(reloaded.SeatIds[PartySeat.Front],
                "the front rank the player emptied is filled by the save round trip: " +
                "Mid is now the seat enemy melee concentrates on, unchosen");
            Assert.AreEqual(Mid, reloaded.SeatIds[PartySeat.Middle],
                "Mid should still be in the middle seat after the round trip");
            Assert.AreEqual(Rear, reloaded.SeatIds[PartySeat.Rear],
                "and Rear should still be in the rear seat");
        }

        // THE TRAILING HOLE IS NOT KEPT, and that is not an inconsistency: a
        // hole carries positional information only when somebody sits behind
        // it. Trimming the tail is what keeps a solo save one entry long
        // rather than one entry and two blanks, which is the shape every
        // reader of this list already expects.
        [Test]
        public void BenchingTheRearRanker_WritesTwoSeatsRatherThanTwoAndABlank()
        {
            var formation = new PartyFormation(
                Roster(), new[] { Front, Mid, Rear }, PartyMode.Camp, 3, NeverLocked);

            formation.ClickSeat(PartySeat.Rear);
            Assert.IsTrue(formation.SendToBench().Changed, "benching the rear-ranker should have committed");

            CollectionAssert.AreEqual(new[] { Front, Mid }, Persist(formation));
        }
    }
}
