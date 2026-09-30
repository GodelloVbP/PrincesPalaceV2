using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.Domain.Tests
{
    // THE ROSTER SECTION SHOWS ONLY WHEN IT HAS SOMETHING TO SAY (plan C22): a
    // character sits out, or a seat is closed. With everyone seated the seats
    // are the whole story and the roster only repeats them.
    public class PartyRosterVisibilityTests
    {
        private static List<PartyRosterEntry> Roster(params string[] ids)
        {
            var roster = new List<PartyRosterEntry>();
            foreach (var id in ids) roster.Add(new PartyRosterEntry(id, id, hasArt: true));
            return roster;
        }

        private static PartyFormation Formation(string[] roster, string[] seats, int maxSeats = 3,
                                                PartyMode mode = PartyMode.Camp) =>
            new PartyFormation(Roster(roster), seats, mode, maxSeats, _ => false);

        [Test]
        public void EveryoneSeatedHidesTheRoster()
        {
            var formation = Formation(new[] { "a", "b", "c" }, new[] { "a", "b", "c" });

            Assert.IsFalse(formation.RosterVisible);
        }

        [Test]
        public void ACharacterWhoSitsOutShowsTheRoster()
        {
            var formation = Formation(new[] { "a", "b", "c", "d" }, new[] { "a", "b", "c" });

            Assert.IsTrue(formation.RosterVisible);
        }

        [Test]
        public void AnEmptySeatWithSomeoneToPlaceInItShowsTheRoster()
        {
            var formation = Formation(new[] { "a", "b", "c" }, new[] { "a", "b" });

            Assert.IsTrue(formation.RosterVisible, "c is unseated");
        }

        [Test]
        public void AnEmptySeatWithNobodyToPlaceInItLeavesTheRosterHidden()
        {
            var formation = Formation(new[] { "a", "b" }, new[] { "a", "b" });

            Assert.IsFalse(formation.RosterVisible, "both characters are seated: the third seat has no one to offer");
        }

        [Test]
        public void AClosedSeatShowsTheRosterEvenWhenEveryoneIsSeated()
        {
            var formation = Formation(new[] { "a", "b" }, new[] { "a", "b" }, maxSeats: 2);

            Assert.IsTrue(formation.IsSeatClosed(2));
            Assert.IsTrue(formation.RosterVisible);
        }

        [TestCase(PartyMode.Camp)]
        [TestCase(PartyMode.Run)]
        [TestCase(PartyMode.ViewOnly)]
        public void TheModeDoesNotDecideVisibility(PartyMode mode)
        {
            Assert.IsFalse(Formation(new[] { "a", "b", "c" }, new[] { "a", "b", "c" }, mode: mode).RosterVisible);
            Assert.IsTrue(Formation(new[] { "a", "b", "c", "d" }, new[] { "a", "b", "c" }, mode: mode).RosterVisible);
        }

        [Test]
        public void BenchingSomeoneBringsTheRosterBack()
        {
            var formation = Formation(new[] { "a", "b", "c" }, new[] { "a", "b", "c" });
            Assert.IsFalse(formation.RosterVisible, "fixture: everyone starts seated");

            formation.ClickSeat(PartySeat.Rear);
            Assert.IsTrue(formation.CanSendToBench, "fixture: Camp lets a seated member be benched");
            formation.SendToBench();

            Assert.IsTrue(formation.RosterVisible);
        }
    }
}
