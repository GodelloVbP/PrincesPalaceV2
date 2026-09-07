using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // A LOCKED SEAT'S OCCUPANT CANNOT LEAVE IT, whichever gesture asks.
    //
    // docs/handoffs/party_screen/GAP_AUDIT.md row 37 states the contract as
    // "locked position rejects any placement, and its occupant can't be moved
    // out either -- both directions blocked", and cites two call sites for it:
    // ClickSeat's destination check for the way in, SendToBench's own check
    // for the way out. SendToBench is not the only way out -- a seat-to-seat
    // move or swap vacates the ORIGIN seat too, and ClickSeat checks the lock
    // on the DESTINATION only.
    //
    // Reaching "selected, and now locked" needs the predicate's answer to
    // change after the selection, which is exactly the case
    // PartyFormationTests.SendToBenchOnASeatThatLockedAfterSelectionRefuses
    // and CanSendToBenchIsFalseWhenTheSelectedSeatBecomesLocked already
    // exercise -- the run-modifier seam PartyFormation's own header describes.
    public class PartyFormationSeatLockTests
    {
        private const string Front = "hero";
        private const string Mid = "mage";
        private const string Rear = "rogue";
        private const string Benched = "bard";

        private static List<PartyRosterEntry> Roster() => new List<PartyRosterEntry>
        {
            new PartyRosterEntry(Front, "Hero", hasArt: true),
            new PartyRosterEntry(Mid, "Mage", hasArt: true),
            new PartyRosterEntry(Rear, "Rogue", hasArt: false),
            new PartyRosterEntry(Benched, "Bard", hasArt: true),
        };

        private static PartyFormation Formation(Func<int, bool> isLocked, string[] seats = null) =>
            new PartyFormation(Roster(), seats ?? new[] { Front, Mid, Rear }, PartyMode.Camp, 3, isLocked);

        [Test]
        public void SwappingOutOfASeatThatLockedAfterSelectionIsRefused()
        {
            bool locked = false;
            var formation = Formation(i => locked && i == PartySeat.Front);
            formation.ClickSeat(PartySeat.Front);
            locked = true;

            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsFalse(outcome.Changed, "a locked seat's occupant swapped itself out");
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.AreEqual(PartySeat.Front, outcome.Seat);
            Assert.AreEqual(Front, formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual(Rear, formation.SeatIds[PartySeat.Rear]);
        }

        [Test]
        public void MovingOutOfASeatThatLockedAfterSelectionIsRefused()
        {
            bool locked = false;
            var formation = Formation(i => locked && i == PartySeat.Front,
                                      new[] { Front, Mid, null });
            formation.ClickSeat(PartySeat.Front);
            locked = true;

            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsFalse(outcome.Changed, "a locked seat's occupant walked to an empty seat");
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual(Front, formation.SeatIds[PartySeat.Front]);
            Assert.IsNull(formation.SeatIds[PartySeat.Rear]);
        }

        // The refusal above has to be visible before the click, or every other
        // seat draws a pill promising a move the model will not make. Same
        // bargain SeatBadge already keeps for the selected seat itself.
        [Test]
        public void NoSeatIsAValidDestinationOnceTheSourceSeatLocks()
        {
            bool locked = false;
            var formation = Formation(i => locked && i == PartySeat.Front,
                                      new[] { Front, Mid, null });
            formation.ClickSeat(PartySeat.Front);
            locked = true;

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Middle));
            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Rear));
            Assert.IsFalse(formation.IsValidDestination(PartySeat.Rear));
        }

        // Cancelling must still work, exactly as ClickSeat's own comment says:
        // the source-seat click is checked before any lock.
        [Test]
        public void TheSourceSeatCanStillBeClickedToCancelAfterItLocks()
        {
            bool locked = false;
            var formation = Formation(i => locked && i == PartySeat.Front);
            formation.ClickSeat(PartySeat.Front);
            locked = true;

            var outcome = formation.ClickSeat(PartySeat.Front);

            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
        }

        // A roster selection is unaffected: the card being placed sits in no
        // seat, so there is no origin lock to consult.
        [Test]
        public void ARosterSelectionStillPlacesWhileAnUnrelatedSeatIsLocked()
        {
            var formation = Formation(i => i == PartySeat.Front, new[] { Front, null, null });
            formation.ClickCard(Benched);

            var outcome = formation.ClickSeat(PartySeat.Middle);

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Placed, outcome.Toast);
            Assert.AreEqual(Benched, formation.SeatIds[PartySeat.Middle]);
        }
    }
}
