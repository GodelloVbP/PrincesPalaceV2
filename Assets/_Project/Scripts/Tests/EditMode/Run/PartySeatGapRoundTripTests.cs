using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // An empty seat is a state the MODEL has and the SAVE cannot express.
    //
    // PartyFormation keeps seats by index and leaves a hole where one is
    // vacated: SendToBench writes `_seats[seat] = null` in place, so benching
    // the front-ranker leaves [null, Mid, Rear]. That is deliberate -- the
    // type's own header says "POSITIONS ARE MECHANICAL, not cosmetic: seat 0
    // is the front rank enemy melee concentrates on".
    //
    // save.selectedCharacterIds is a COMPACT list with no notion of an index.
    // PartyController.Persist writes `Formation.SeatIds.Where(id => id !=
    // null).ToList()`, and PartyController.Load reads element i back into seat
    // i. So the hole closes on the way to disk, and everybody behind it moves
    // one seat forward -- Mid becomes the melee magnet the player did not put
    // there. SaveData.ActiveSquad reads that same compact list, so combat has
    // already promoted Mid before any reload; it is only the screen, still
    // painting the live model, that keeps showing the gap.
    //
    // WHICH SIDE IS WRONG IS THE OWNER'S CALL, which is why this is ignored
    // rather than fixed. Two coherent answers, and they are different games:
    // the save learns to carry a hole (an empty front rank is a formation the
    // player can choose), or the screen compacts on bench (a party never has a
    // hole, and the model should stop pretending it can). Both are design
    // decisions about what an empty front seat MEANS in a fight, and combat
    // has no representation for one today.
    //
    // The two production lines are transcribed here rather than called: they
    // live on a MonoBehaviour in Core (PartyController.Persist / .Load) that
    // an EditMode test cannot stand up. Kept verbatim so this fails to
    // describe the game the moment either line changes.
    public class PartySeatGapRoundTripTests
    {
        private const string Front = "hero";
        private const string Mid = "mage";
        private const string Rear = "rogue";

        private static List<PartyRosterEntry> Roster() => new List<PartyRosterEntry>
        {
            new PartyRosterEntry(Front, "Hero", hasArt: true),
            new PartyRosterEntry(Mid, "Mage", hasArt: true),
            new PartyRosterEntry(Rear, "Rogue", hasArt: true),
        };

        private static readonly Func<int, bool> NeverLocked = _ => false;

        // PartyController.Persist, verbatim.
        private static List<string> Persist(PartyFormation formation) =>
            formation.SeatIds.Where(id => id != null).ToList();

        // PartyController.Load's seat loop, reduced to the part that maps a
        // saved list back onto seats. The reconciliation guards it carries
        // alongside cannot fire here -- every id is in the roster and none
        // repeats.
        private static PartyFormation Load(IReadOnlyList<string> saved) =>
            new PartyFormation(Roster(), saved, PartyMode.Camp, 3, NeverLocked);

        [Test]
        [Ignore("owner's call: an empty seat is a state the model has and the save cannot express -- " +
                "either selectedCharacterIds learns to carry a hole, or the screen compacts on bench. " +
                "Both are decisions about what an empty front rank means in a fight.")]
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

            var reloaded = Load(Persist(formation));

            Assert.IsNull(reloaded.SeatIds[PartySeat.Front],
                "the front rank the player emptied is filled by the save round trip: " +
                "Mid is now the seat enemy melee concentrates on, unchosen");
            Assert.AreEqual(Mid, reloaded.SeatIds[PartySeat.Middle],
                "Mid should still be in the middle seat after the round trip");
        }
    }
}
