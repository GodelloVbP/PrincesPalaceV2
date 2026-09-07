using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // The Party screen's state machine, exercised without a screen, a save
    // or a scene -- PartyFormation only ever sees what this file hands it.
    //
    // FIXTURE: a standing three-seat squad (front/mid/rear) plus one
    // unseated roster member, reused by most tests so a failure's diff is
    // about the RULE under test and not about which ids a bespoke roster
    // happened to use.
    public class PartyFormationTests
    {
        private const string Front = "hero";
        private const string Mid = "mage";
        private const string Rear = "rogue";
        private const string Benched = "bard";

        private static List<PartyRosterEntry> DefaultRoster() => new List<PartyRosterEntry>
        {
            new PartyRosterEntry(Front, "Hero", hasArt: true),
            new PartyRosterEntry(Mid, "Mage", hasArt: true),
            new PartyRosterEntry(Rear, "Rogue", hasArt: false),
            new PartyRosterEntry(Benched, "Bard", hasArt: true),
        };

        private static string[] DefaultSeats() => new[] { Front, Mid, Rear };

        private static readonly Func<int, bool> NeverLocked = _ => false;

        private static PartyFormation Formation(
            PartyMode mode = PartyMode.Camp,
            int maxSeats = 3,
            Func<int, bool> isLocked = null,
            string[] seats = null,
            List<PartyRosterEntry> roster = null) =>
            new PartyFormation(roster ?? DefaultRoster(), seats ?? DefaultSeats(), mode, maxSeats, isLocked ?? NeverLocked);

        // ================================================================
        // Constructor rejections
        // ================================================================

        [Test]
        public void ConstructorRejectsASeatIdNotInTheRoster()
        {
            Assert.Throws<ArgumentException>(() =>
                new PartyFormation(DefaultRoster(), new[] { "ghost", Mid, Rear }, PartyMode.Camp, 3, NeverLocked));
        }

        [Test]
        public void ConstructorRejectsAnIdInTwoSeats()
        {
            Assert.Throws<ArgumentException>(() =>
                new PartyFormation(DefaultRoster(), new[] { Front, Front, Rear }, PartyMode.Camp, 3, NeverLocked));
        }

        [Test]
        public void ConstructorRejectsMoreThanThreeSeatIds()
        {
            Assert.Throws<ArgumentException>(() =>
                new PartyFormation(DefaultRoster(), new[] { Front, Mid, Rear, Benched }, PartyMode.Camp, 3, NeverLocked));
        }

        [Test]
        public void ConstructorPadsShortSeatListsWithNulls()
        {
            var formation = new PartyFormation(DefaultRoster(), new[] { Front }, PartyMode.Camp, 3, NeverLocked);

            Assert.AreEqual(Front, formation.SeatIds[0]);
            Assert.IsNull(formation.SeatIds[1]);
            Assert.IsNull(formation.SeatIds[2]);
        }

        [Test]
        public void ConstructorAcceptsNullSeatIdsAsAnEmptyFormation()
        {
            var formation = new PartyFormation(DefaultRoster(), null, PartyMode.Camp, 3, NeverLocked);

            Assert.AreEqual(0, formation.FilledCount);
        }

        // ================================================================
        // Queries: seats, closed, locked
        // ================================================================

        [Test]
        public void FilledCountCountsOnlyOccupiedSeats()
        {
            Assert.AreEqual(3, Formation().FilledCount);
        }

        [Test]
        public void SeatIdsReturnsACopyNotTheLiveArray()
        {
            var formation = Formation();
            var copy = (string[])formation.SeatIds;
            copy[PartySeat.Front] = "tampered";

            // Mutating the returned array must not reach the formation's own
            // seating -- SeatOf still finds Front's real occupant.
            Assert.AreEqual(PartySeat.Front, formation.SeatOf(Front));
        }

        [Test]
        public void SeatOfReturnsMinusOneForAnUnseatedMember()
        {
            Assert.AreEqual(-1, Formation().SeatOf(Benched));
        }

        [Test]
        public void SeatOfFindsASeatedMember()
        {
            Assert.AreEqual(PartySeat.Rear, Formation().SeatOf(Rear));
        }

        [Test]
        public void SeatsAtOrAboveMaxSeatsAreClosed()
        {
            var formation = Formation(maxSeats: 1);

            Assert.IsFalse(formation.IsSeatClosed(PartySeat.Front));
            Assert.IsTrue(formation.IsSeatClosed(PartySeat.Middle));
            Assert.IsTrue(formation.IsSeatClosed(PartySeat.Rear));
        }

        [Test]
        public void MaxSeatsOfTwoClosesOnlyTheRearSeat()
        {
            var formation = Formation(maxSeats: 2);

            Assert.IsFalse(formation.IsSeatClosed(PartySeat.Middle));
            Assert.IsTrue(formation.IsSeatClosed(PartySeat.Rear));
        }

        [Test]
        public void AClosedSeatIsNeverReportedAsLocked()
        {
            // The predicate says lock everything; a closed seat must still
            // read as closed rather than locked -- they are refused for
            // different reasons and SeatBadge/CardState need to tell them
            // apart.
            var formation = Formation(maxSeats: 1, seats: new[] { Front, null, null }, isLocked: _ => true);

            Assert.IsFalse(formation.IsSeatLocked(PartySeat.Middle));
            Assert.IsTrue(formation.IsSeatClosed(PartySeat.Middle));
        }

        [Test]
        public void AnOpenSeatReadsLockedStraightFromThePredicate()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Middle);

            Assert.IsTrue(formation.IsSeatLocked(PartySeat.Middle));
            Assert.IsFalse(formation.IsSeatLocked(PartySeat.Front));
        }

        // ================================================================
        // SeatBadge / IsValidDestination
        // ================================================================

        [Test]
        public void SeatBadgeIsNoneWithNothingSelected()
        {
            var formation = Formation();

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Front));
        }

        [Test]
        public void SeatBadgeIsNoneOnTheSelectedSeatItself()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Front));
        }

        [Test]
        public void SeatBadgeIsNoneOnAClosedSeat()
        {
            var formation = Formation(maxSeats: 2);
            formation.ClickSeat(PartySeat.Front);

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Rear));
        }

        [Test]
        public void SeatBadgeIsNoneOnALockedSeat()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Rear);
            formation.ClickSeat(PartySeat.Front);

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Rear));
        }

        [Test]
        public void SeatBadgeIsPlaceHereForAnEmptyDestination()
        {
            var formation = Formation(maxSeats: 2, seats: new[] { Front, null, null });
            formation.ClickCard(Benched);

            Assert.AreEqual(PartySeatBadge.PlaceHere, formation.SeatBadge(PartySeat.Middle));
        }

        [Test]
        public void SeatBadgeIsReplaceForAnOccupiedDestinationFromARosterSelection()
        {
            var formation = Formation();
            formation.ClickCard(Benched);

            Assert.AreEqual(PartySeatBadge.Replace, formation.SeatBadge(PartySeat.Front));
        }

        [Test]
        public void SeatBadgeIsSwapWithForAnOccupiedDestinationFromASeatSelection()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);

            Assert.AreEqual(PartySeatBadge.SwapWith, formation.SeatBadge(PartySeat.Rear));
        }

        [Test]
        public void IsValidDestinationIsExactlyBadgeNotNone()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Rear);
            formation.ClickCard(Benched);

            // Front: badge Replace -> valid. Rear: locked, badge None -> not.
            Assert.AreEqual(PartySeatBadge.Replace, formation.SeatBadge(PartySeat.Front));
            Assert.IsTrue(formation.IsValidDestination(PartySeat.Front));

            Assert.AreEqual(PartySeatBadge.None, formation.SeatBadge(PartySeat.Rear));
            Assert.IsFalse(formation.IsValidDestination(PartySeat.Rear));
        }

        // ================================================================
        // CardState
        // ================================================================

        [Test]
        public void CardStateReportsASeatedMemberActive()
        {
            var state = Formation().CardState(Front);

            Assert.IsTrue(state.IsActive);
            Assert.AreEqual(PartySeat.Front, state.Seat);
            Assert.IsFalse(state.IsBenched);
        }

        [Test]
        public void CardStateOnlyReportsBenchedDuringARun()
        {
            Assert.IsFalse(Formation(mode: PartyMode.Camp).CardState(Benched).IsBenched);
            Assert.IsTrue(Formation(mode: PartyMode.Run).CardState(Benched).IsBenched);
        }

        [Test]
        public void CardStateCarriesHasArtFromTheRosterEntry()
        {
            Assert.IsFalse(Formation().CardState(Rear).HasArt);
            Assert.IsTrue(Formation().CardState(Front).HasArt);
        }

        [Test]
        public void CardStateIsNeverSelectableInViewOnly()
        {
            var formation = Formation(mode: PartyMode.ViewOnly);

            Assert.IsFalse(formation.CardState(Front).IsSelectable);
            Assert.IsFalse(formation.CardState(Benched).IsSelectable);
        }

        [Test]
        public void CardStateIsNotSelectableForALockedSeatedMember()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Front);

            Assert.IsFalse(formation.CardState(Front).IsSelectable);
        }

        [Test]
        public void CardStateIsNotSelectableForAnUnseatedMemberDuringARun()
        {
            var formation = Formation(mode: PartyMode.Run);

            Assert.IsFalse(formation.CardState(Benched).IsSelectable);
        }

        [Test]
        public void CardStateThrowsForAnIdOutsideTheRoster()
        {
            Assert.Throws<ArgumentException>(() => Formation().CardState("ghost"));
        }

        // ================================================================
        // CanSendToBench
        // ================================================================

        [Test]
        public void CanSendToBenchIsTrueForAnUnlockedSeatSelectionInCamp()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);

            Assert.IsTrue(formation.CanSendToBench);
        }

        [Test]
        public void CanSendToBenchIsFalseForARosterSelection()
        {
            var formation = Formation();
            formation.ClickCard(Benched);

            Assert.IsFalse(formation.CanSendToBench);
        }

        [Test]
        public void CanSendToBenchIsFalseDuringARun()
        {
            var formation = Formation(mode: PartyMode.Run);
            formation.ClickCard(Front);

            Assert.IsFalse(formation.CanSendToBench);
        }

        [Test]
        public void CanSendToBenchIsFalseWhenOnlyOneSeatIsFilled()
        {
            var formation = Formation(seats: new[] { Front, null, null });
            formation.ClickSeat(PartySeat.Front);

            Assert.IsFalse(formation.CanSendToBench);
        }

        [Test]
        public void CanSendToBenchIsFalseWhenTheSelectedSeatBecomesLocked()
        {
            // A seat can't be SELECTED while locked (ClickSeat's own
            // "nothing selected" step refuses it by name), so the only way
            // to reach "selected and locked" is a predicate whose answer
            // changes after selection -- exactly the run-modifier case the
            // predicate seam exists for.
            bool locked = false;
            var formation = Formation(isLocked: _ => locked);
            formation.ClickSeat(PartySeat.Front);
            locked = true;

            Assert.IsFalse(formation.CanSendToBench);
        }

        // ================================================================
        // ClickSeat precedence
        // ================================================================

        [Test]
        public void ClickSeatInViewOnlyAlwaysRefuses()
        {
            var formation = Formation(mode: PartyMode.ViewOnly);
            var outcome = formation.ClickSeat(PartySeat.Front);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.FormationFixedInFight, outcome.Toast);
        }

        [Test]
        public void ClickSeatOnAClosedSeatRefuses()
        {
            var formation = Formation(maxSeats: 2);
            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatNotOpen, outcome.Toast);
        }

        [Test]
        public void ClickingAnEmptySeatWithNothingSelectedIsANoOp()
        {
            var formation = Formation(seats: new[] { Front, null, null });
            var outcome = formation.ClickSeat(PartySeat.Middle);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickingALockedOccupiedSeatWithNothingSelectedNamesTheOccupant()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Front);
            var outcome = formation.ClickSeat(PartySeat.Front);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickingAnOccupiedSeatWithNothingSelectedSelectsItsOccupant()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Rear);

            Assert.AreEqual(Rear, formation.SelectedId);
            Assert.AreEqual(PartySelectionSource.Seat(PartySeat.Rear), formation.SelectedFrom);
        }

        [Test]
        public void ClickingTheSelectedSeatAgainCancels()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Rear);
            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickingALockedSeatWhileSomethingElseIsSelectedRefuses()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Rear);
            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual(Front, formation.SelectedId); // the earlier selection survives a refusal
        }

        [Test]
        public void PlacingARosterSelectionIntoAnEmptySeatCommitsPlaced()
        {
            var formation = Formation(seats: new[] { Front, null, null });
            formation.ClickCard(Benched);
            var outcome = formation.ClickSeat(PartySeat.Middle);

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Placed, outcome.Toast);
            Assert.AreEqual("Bard", outcome.Actor);
            Assert.AreEqual(PartySeat.Middle, outcome.Seat);
            Assert.AreEqual(Benched, formation.SeatIds[PartySeat.Middle]);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void PlacingARosterSelectionIntoAnOccupiedSeatReplacesTheOccupant()
        {
            var formation = Formation();
            formation.ClickCard(Benched);
            var outcome = formation.ClickSeat(PartySeat.Front);

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Replaced, outcome.Toast);
            Assert.AreEqual("Bard", outcome.Actor);
            Assert.AreEqual("Hero", outcome.Other);
            Assert.AreEqual(Benched, formation.SeatIds[PartySeat.Front]);

            // The displaced member leaves the formation outright -- there is
            // no second seat for them to land in.
            Assert.AreEqual(-1, formation.SeatOf(Front));
        }

        [Test]
        public void MovingASeatedSelectionIntoAnEmptySeatCommitsMoved()
        {
            var formation = Formation(seats: new[] { Front, Mid, null });
            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Moved, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.AreEqual(Front, formation.SeatIds[PartySeat.Rear]);
            Assert.IsNull(formation.SeatIds[PartySeat.Front]);
        }

        [Test]
        public void SwappingTwoSeatedMembersCommitsSwapped()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.ClickSeat(PartySeat.Rear);

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Swapped, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.AreEqual("Rogue", outcome.Other);
            Assert.AreEqual(Rear, formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual(Front, formation.SeatIds[PartySeat.Rear]);
        }

        // ================================================================
        // ClickCard precedence
        // ================================================================

        [Test]
        public void ClickCardInViewOnlyRefusesBeforeCheckingTheId()
        {
            var formation = Formation(mode: PartyMode.ViewOnly);

            // "ghost" is not in the roster -- if the id check ran first this
            // would throw instead of returning a toast, which is exactly
            // the precedence step 1 vs step 2 fixes.
            var outcome = formation.ClickCard("ghost");

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.FormationFixedInFight, outcome.Toast);
        }

        [Test]
        public void ClickCardWithAnUnknownIdThrowsOutsideViewOnly()
        {
            var formation = Formation();

            Assert.Throws<ArgumentException>(() => formation.ClickCard("ghost"));
        }

        [Test]
        public void ClickingTheSelectedCardAgainClearsSelection()
        {
            var formation = Formation();
            formation.ClickCard(Benched);
            var outcome = formation.ClickCard(Benched);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickCardOnASeatedLockedMemberRefuses()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Front);
            var outcome = formation.ClickCard(Front);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickCardOnASeatedMemberSelectsFromThatSeat()
        {
            var formation = Formation();
            formation.ClickCard(Rear);

            Assert.AreEqual(Rear, formation.SelectedId);
            Assert.AreEqual(PartySelectionSource.Seat(PartySeat.Rear), formation.SelectedFrom);
        }

        [Test]
        public void ClickCardOnASeatedMemberReplacesAPriorRosterSelection()
        {
            var formation = Formation();
            formation.ClickCard(Benched);
            var outcome = formation.ClickCard(Rear);

            // A card is never a destination -- this REPLACES the pending
            // selection rather than committing anything.
            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(Rear, formation.SelectedId);
            Assert.AreEqual(PartySelectionSource.Seat(PartySeat.Rear), formation.SelectedFrom);
        }

        [Test]
        public void ClickCardOnAnUnseatedMemberDuringARunRefuses()
        {
            var formation = Formation(mode: PartyMode.Run);
            var outcome = formation.ClickCard(Benched);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.BenchedDuringRun, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void ClickCardOnAnUnseatedMemberInCampSelectsFromTheRoster()
        {
            var formation = Formation();
            formation.ClickCard(Benched);

            Assert.AreEqual(Benched, formation.SelectedId);
            Assert.AreEqual(PartySelectionSource.Roster, formation.SelectedFrom);
        }

        // ================================================================
        // SendToBench / DropOnRoster
        // ================================================================

        [Test]
        public void SendToBenchInViewOnlyRefuses()
        {
            var formation = Formation(mode: PartyMode.ViewOnly);

            Assert.AreEqual(PartyToastKind.FormationFixedInFight, formation.SendToBench().Toast);
        }

        [Test]
        public void SendToBenchWithNothingSelectedIsANoOp()
        {
            var formation = Formation();
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
        }

        [Test]
        public void SendToBenchWithARosterSelectionIsANoOp()
        {
            var formation = Formation();
            formation.ClickCard(Benched);
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
        }

        [Test]
        public void SendToBenchDuringARunRefuses()
        {
            var formation = Formation(mode: PartyMode.Run);
            formation.ClickCard(Front);
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.RepositionOnlyDuringRun, outcome.Toast);
        }

        [Test]
        public void BenchingTheLastOccupantIsRefused()
        {
            var formation = Formation(seats: new[] { Front, null, null });
            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.PartyNeverEmpty, outcome.Toast);
            Assert.AreEqual(1, formation.FilledCount);
        }

        [Test]
        public void SendToBenchOnASeatThatLockedAfterSelectionRefuses()
        {
            // Same reasoning as CanSendToBenchIsFalseWhenTheSelectedSeatBecomesLocked:
            // selecting a locked seat is impossible through the click path,
            // so the predicate has to change after the selection was made.
            bool locked = false;
            var formation = Formation(isLocked: _ => locked);
            formation.ClickSeat(PartySeat.Middle);
            locked = true;
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
        }

        [Test]
        public void SendToBenchCommitsAndEmptiesTheSeat()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Rear);
            var outcome = formation.SendToBench();

            Assert.IsTrue(outcome.Changed);
            Assert.AreEqual(PartyToastKind.Benched, outcome.Toast);
            Assert.AreEqual("Rogue", outcome.Actor);
            Assert.IsNull(formation.SeatIds[PartySeat.Rear]);
            Assert.AreEqual(2, formation.FilledCount);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void DropOnRosterIsTheSameOperationAsSendToBench()
        {
            var a = Formation();
            a.ClickSeat(PartySeat.Rear);
            var expected = a.SendToBench();

            var b = Formation();
            b.ClickSeat(PartySeat.Rear);
            var actual = b.DropOnRoster();

            Assert.AreEqual(expected.Changed, actual.Changed);
            Assert.AreEqual(expected.Toast, actual.Toast);
            Assert.AreEqual(expected.Actor, actual.Actor);
            Assert.AreEqual(a.SeatIds[PartySeat.Rear], b.SeatIds[PartySeat.Rear]);
        }

        // ================================================================
        // Cancel
        // ================================================================

        [Test]
        public void CancelClearsSelectionWithoutCommitting()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);
            var outcome = formation.Cancel();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.None, outcome.Toast);
            Assert.IsNull(formation.SelectedId);
            Assert.AreEqual(Front, formation.SeatIds[PartySeat.Front]); // nothing moved
        }

        // ================================================================
        // Drop -- parity with the click path
        // ================================================================

        [Test]
        public void DropInViewOnlyRefuses()
        {
            var formation = Formation(mode: PartyMode.ViewOnly);
            var outcome = formation.Drop(PartySelectionSource.Seat(PartySeat.Front), PartySeat.Rear);

            Assert.AreEqual(PartyToastKind.FormationFixedInFight, outcome.Toast);
        }

        [Test]
        public void DropFromASeatEqualsTwoClicksThroughTheSamePrecedence()
        {
            var a = Formation();
            a.ClickSeat(PartySeat.Front);
            var expected = a.ClickSeat(PartySeat.Rear);

            var b = Formation();
            var actual = b.Drop(PartySelectionSource.Seat(PartySeat.Front), PartySeat.Rear);

            Assert.AreEqual(expected.Changed, actual.Changed);
            Assert.AreEqual(expected.Toast, actual.Toast);
            Assert.AreEqual(expected.Actor, actual.Actor);
            Assert.AreEqual(expected.Other, actual.Other);
            Assert.AreEqual(a.SeatIds[PartySeat.Front], b.SeatIds[PartySeat.Front]);
            Assert.AreEqual(a.SeatIds[PartySeat.Rear], b.SeatIds[PartySeat.Rear]);
        }

        [Test]
        public void DropFromALockedSeatRefusesLikeClickingItWould()
        {
            var formation = Formation(isLocked: i => i == PartySeat.Front);
            var outcome = formation.Drop(PartySelectionSource.Seat(PartySeat.Front), PartySeat.Rear);

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(PartyToastKind.SeatLocked, outcome.Toast);
            Assert.AreEqual("Hero", outcome.Actor);
            Assert.IsNull(formation.SelectedId);
        }

        [Test]
        public void DropFromARosterSelectionEqualsClickCardThenClickSeat()
        {
            var a = Formation();
            a.ClickCard(Benched);
            var expected = a.ClickSeat(PartySeat.Middle);

            var b = Formation();
            b.ClickCard(Benched);
            var actual = b.Drop(PartySelectionSource.Roster, PartySeat.Middle);

            Assert.AreEqual(expected.Changed, actual.Changed);
            Assert.AreEqual(expected.Toast, actual.Toast);
            Assert.AreEqual(a.SeatIds[PartySeat.Middle], b.SeatIds[PartySeat.Middle]);
        }

        [Test]
        public void DropFromARosterSourceWithNoActiveSelectionThrows()
        {
            var formation = Formation();

            // PartySelectionSource.Roster carries no id -- there is nothing
            // for Drop to reselect from cold, so a caller reaching this
            // without first calling ClickCard(id) has a bug, not a
            // legitimate blocked outcome.
            Assert.Throws<InvalidOperationException>(() => formation.Drop(PartySelectionSource.Roster, PartySeat.Middle));
        }

        [Test]
        public void DropFromARosterSourceWhileASeatIsSelectedThrows()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);

            Assert.Throws<InvalidOperationException>(() => formation.Drop(PartySelectionSource.Roster, PartySeat.Middle));
        }

        // ================================================================
        // Invariants -- checked after a scripted run of commands, not
        // formula-derived (CODE_STANDARDS.md SS8: never recompute the thing
        // under test to build the expected value).
        // ================================================================

        [Test]
        public void NoIdEverOccupiesTwoSeatsAfterASequenceOfMoves()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);
            formation.ClickSeat(PartySeat.Rear); // swap
            formation.ClickCard(Benched);
            formation.ClickSeat(PartySeat.Middle); // replace

            var seen = new HashSet<string>();
            foreach (var id in formation.SeatIds)
            {
                if (id == null) continue;
                Assert.IsTrue(seen.Add(id), $"'{id}' occupies two seats after the sequence.");
            }
        }

        [Test]
        public void FilledCountNeverReachesZeroThroughBenching()
        {
            var formation = Formation();
            formation.ClickSeat(PartySeat.Front);
            formation.SendToBench();
            formation.ClickSeat(PartySeat.Middle);
            formation.SendToBench();

            // Two of three benched -- the third is the last occupant and
            // PartyNeverEmpty must have refused a third attempt.
            formation.ClickSeat(PartySeat.Rear);
            var outcome = formation.SendToBench();

            Assert.IsFalse(outcome.Changed);
            Assert.AreEqual(1, formation.FilledCount);
        }

        [Test]
        public void SeatsAtOrAboveMaxSeatsStayNullThroughoutCommands()
        {
            var formation = Formation(maxSeats: 2, seats: new[] { Front, Mid, null });
            formation.ClickCard(Benched);
            formation.ClickSeat(PartySeat.Middle); // replace within the open range
            formation.ClickSeat(PartySeat.Front);
            formation.ClickSeat(PartySeat.Middle); // swap within the open range

            Assert.IsNull(formation.SeatIds[PartySeat.Rear]);
        }

        [Test]
        public void SelectionIsNullAfterEveryCommittedOutcome()
        {
            var formation = Formation();

            formation.ClickSeat(PartySeat.Front);
            var placed = formation.ClickSeat(PartySeat.Rear);
            Assert.IsTrue(placed.Changed);
            Assert.IsNull(formation.SelectedId);

            formation.ClickCard(Benched);
            var replaced = formation.ClickSeat(PartySeat.Rear);
            Assert.IsTrue(replaced.Changed);
            Assert.IsNull(formation.SelectedId);

            formation.ClickSeat(PartySeat.Middle);
            var benched = formation.SendToBench();
            Assert.IsTrue(benched.Changed);
            Assert.IsNull(formation.SelectedId);
        }
    }
}
