using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The Party pane: Formation (3 fixed seats) above Roster (N cards).
    //
    // NO CONTROLLER EXISTS YET -- this covers exactly what a declared tree can
    // promise: it audits clean, its seats and cards carry the state a later
    // controller will need, and the one dimension that can outgrow its row
    // (Roster) is guarded at build time the way RunStats and Options guard
    // theirs.
    public class PartyScreenTests
    {
        private static IEnumerable<int> RosterCountsToAudit()
        {
            yield return 1;
            yield return 3;
            yield return PartyLayout.MaxRosterCards();
        }

        [TestCaseSource(nameof(RosterCountsToAudit))]
        public void TheScreenAuditsCleanAtEveryFrame(int rosterCount)
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(rosterCount)).Root, frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)} with {rosterCount} roster cards, first 5 of " +
                    $"{errors.Count}: " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void TheGuardThrowsOnePastTheMaxRosterCards_NamingTheCount()
        {
            int overflow = PartyLayout.MaxRosterCards() + 1;

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => PartyScreen.Build(new PartyInputs(overflow)));

            StringAssert.Contains(overflow.ToString(), ex.Message,
                "the guard's message should name the count that no longer fits");
        }

        [Test]
        public void APaneWithNoRosterCardsIsRefusedToo()
        {
            // count = 0 is not "one past the max" but the guard's own
            // RosterFits(count > 0 ...) has to refuse it for the same reason:
            // an empty roster row is not a row.
            Assert.Throws<System.InvalidOperationException>(
                () => PartyScreen.Build(new PartyInputs(0)));
        }

        // ---- the seat labels: FRONT is drawn nearest the enemy ---------------------

        [Test]
        public void SeatLabelsReadRearMiddleFrontLeftToRight()
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);

            var labels = new[] { "PartySeat0Label", "PartySeat1Label", "PartySeat2Label" }
                .Select(name => Find(solved, name))
                .OrderBy(n => n.Rect.Centre.X)
                .ToList();

            // Seat index 0 is FRONT (the front-rank rule's own squad index 0),
            // but it draws RIGHTMOST, nearest the "facing the enemy" ribbon --
            // PartyLayout.VisualColumnForSeat is the one place that mapping
            // lives; this pins the observable consequence of it.
            Assert.AreEqual(UiStrings.PartySeatRear.Key, labels[0].Source.Text.Key,
                "the leftmost seat should read REAR");
            Assert.AreEqual(UiStrings.PartySeatMiddle.Key, labels[1].Source.Text.Key,
                "the middle seat should read MIDDLE");
            Assert.AreEqual(UiStrings.PartySeatFront.Key, labels[2].Source.Text.Key,
                "the rightmost seat, nearest the ribbon, should read FRONT");
        }

        // ---- the shared bottom lines -------------------------------------------------

        [Test]
        public void EverySeatArtSlotSharesOneBottomLine()
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);

            var bottoms = Enumerable.Range(0, PartyLayout.SeatCount)
                .Select(i => Find(solved, $"PartySeat{i}Art").Rect.Bottom)
                .ToList();

            for (int i = 1; i < bottoms.Count; i++)
            {
                Assert.AreEqual(bottoms[0], bottoms[i], 0.01f,
                    $"seat {i}'s art slot does not share the feet line seat 0's does");
            }
        }

        [Test]
        public void EveryCardArtSlotSharesOneBottomLine()
        {
            const int count = 5;
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(count)).Root, UiFrames.Reference);

            var bottoms = Enumerable.Range(0, count)
                .Select(i => Find(solved, $"PartyCard{i}Art").Rect.Bottom)
                .ToList();

            for (int i = 1; i < bottoms.Count; i++)
            {
                Assert.AreEqual(bottoms[0], bottoms[i], 0.01f,
                    $"card {i}'s art slot does not share the line card 0's does");
            }
        }

        // ---- every runtime-toggled node exists, one per seat/card -------------------

        [Test]
        public void EverySeatListHasExactlyOneEntryPerSeat()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            AssertLength(PartyLayout.SeatCount, screen.SeatButtons, nameof(screen.SeatButtons));
            AssertLength(PartyLayout.SeatCount, screen.SeatBadges, nameof(screen.SeatBadges));
            AssertLength(PartyLayout.SeatCount, screen.SeatArts, nameof(screen.SeatArts));
            AssertLength(PartyLayout.SeatCount, screen.SeatMonogramPlates, nameof(screen.SeatMonogramPlates));
            AssertLength(PartyLayout.SeatCount, screen.SeatMonogramLetters, nameof(screen.SeatMonogramLetters));
            AssertLength(PartyLayout.SeatCount, screen.SeatGlows, nameof(screen.SeatGlows));
            AssertLength(PartyLayout.SeatCount, screen.SeatNames, nameof(screen.SeatNames));
            AssertLength(PartyLayout.SeatCount, screen.SeatRoles, nameof(screen.SeatRoles));
            AssertLength(PartyLayout.SeatCount, screen.SeatRings, nameof(screen.SeatRings));
            AssertLength(PartyLayout.SeatCount, screen.SeatScrims, nameof(screen.SeatScrims));
            AssertLength(PartyLayout.SeatCount, screen.SeatScrimCaptions, nameof(screen.SeatScrimCaptions));
        }

        [TestCase(1)]
        [TestCase(5)]
        public void EveryCardListHasExactlyOneEntryPerCard(int count)
        {
            var screen = PartyScreen.Build(new PartyInputs(count));

            AssertLength(count, screen.CardButtons, nameof(screen.CardButtons));
            AssertLength(count, screen.CardArts, nameof(screen.CardArts));
            AssertLength(count, screen.CardMonogramPlates, nameof(screen.CardMonogramPlates));
            AssertLength(count, screen.CardMonogramLetters, nameof(screen.CardMonogramLetters));
            AssertLength(count, screen.CardNames, nameof(screen.CardNames));
            AssertLength(count, screen.CardRoles, nameof(screen.CardRoles));
            AssertLength(count, screen.CardTags, nameof(screen.CardTags));
            AssertLength(count, screen.CardRings, nameof(screen.CardRings));
            AssertLength(count, screen.CardSelectedTags, nameof(screen.CardSelectedTags));
            AssertLength(count, screen.CardWashes, nameof(screen.CardWashes));
        }

        // ---- every declared runtime state starts hidden, the controller's to show --

        [Test]
        public void EveryOverlayStateStartsSwitchedOff()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            CollectionAssert.IsEmpty(NotInactive(screen.SeatBadges), "a seat badge starts shown");
            CollectionAssert.IsEmpty(NotInactive(screen.SeatArts), "a seat art slot starts shown with no sprite");
            CollectionAssert.IsEmpty(NotInactive(screen.SeatRings), "a seat ring starts shown");
            CollectionAssert.IsEmpty(NotInactive(screen.SeatScrims), "a seat scrim starts shown");
            CollectionAssert.IsEmpty(NotInactive(screen.CardArts), "a card art slot starts shown with no sprite");
            CollectionAssert.IsEmpty(NotInactive(screen.CardRings), "a card ring starts shown");
            CollectionAssert.IsEmpty(NotInactive(screen.CardSelectedTags), "a card's SELECTED tag starts shown");
            CollectionAssert.IsEmpty(NotInactive(screen.CardWashes), "a card's dim wash starts shown");

            Assert.IsTrue(screen.CancelLink.Node.StartInactive, "Cancel starts shown with nothing selected");
            Assert.IsTrue(screen.BenchLink.Node.StartInactive,
                "Send to bench starts shown with nothing selected");
            Assert.IsTrue(screen.Toast.Node.StartInactive, "the toast starts shown with nothing to say");
        }

        [Test]
        public void EveryMonogramStandeeStartsShownAsTheNoArtFallback()
        {
            // The reverse of the overlay states above: with no art loaded yet
            // (every seat and card, today), the monogram is what a build
            // should show -- an Image with no sprite renders as a filled
            // rectangle, so leaving THAT active by default would be worse
            // than showing nothing.
            var screen = PartyScreen.Build(new PartyInputs(3));

            CollectionAssert.IsEmpty(NotActive(screen.SeatMonogramPlates), "a seat monogram plate starts hidden");
            CollectionAssert.IsEmpty(NotActive(screen.SeatMonogramLetters),
                "a seat monogram letter starts hidden");
            CollectionAssert.IsEmpty(NotActive(screen.CardMonogramPlates), "a card monogram plate starts hidden");
            CollectionAssert.IsEmpty(NotActive(screen.CardMonogramLetters),
                "a card monogram letter starts hidden");
        }

        // ---- the tab strip still fits with Party present -----------------------------

        [Test]
        public void TheTabStripStillFitsWithPartyPresentInBothSets()
        {
            Assert.IsTrue(SystemMenuLayout.StripFits(SystemMenuTabs.Visible(inRun: false)),
                "the out-of-run bar no longer fits now Party is in it");
            Assert.IsTrue(SystemMenuLayout.StripFits(SystemMenuTabs.Visible(inRun: true)),
                "the in-run bar no longer fits now Party is in it");
        }

        // ---- helpers -------------------------------------------------------------

        private static void AssertLength(int expected, List<NodeRef> list, string name) =>
            Assert.AreEqual(expected, list.Count, $"{name} should have one entry per seat/card");

        private static List<string> NotInactive(IEnumerable<NodeRef> refs) =>
            refs.Where(r => !r.Node.StartInactive).Select(r => r.Node.Name).ToList();

        private static List<string> NotActive(IEnumerable<NodeRef> refs) =>
            refs.Where(r => r.Node.StartInactive).Select(r => r.Node.Name).ToList();

        private static SolvedNode Find(SolvedNode root, string name) =>
            root.Name == name ? root : root.Descendants().FirstOrDefault(n => n.Name == name);
    }
}
