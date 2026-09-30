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

        // ---- the header: the real Reach rule, no status pill (C21, C22) ------------------

        [Test]
        public void TheSubtitleStatesTheRealReachRule()
        {
            Assert.AreEqual(
                "The front rank takes the enemies' blows. Melee skills can strike only the enemy front rank.",
                UiStrings.PartyFormationSubtitle.Format());

            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);
            var subtitle = Find(solved, "PartyFormationSubtitle");
            Assert.IsNotNull(subtitle, "the pane has no subtitle");
            Assert.AreEqual(UiStrings.PartyFormationSubtitle.Key, subtitle.Source.Text.Key);
        }

        // The lintel's mode pill states the mode, and a refusal goes to the
        // menu's notice: the pane keeps neither.
        [Test]
        public void ThePaneHasNoStatusPillOfItsOwn()
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);

            Assert.IsNull(Find(solved, "PartyStatusPill"));
            Assert.IsNull(Find(solved, "PartyStatusText"));
            Assert.IsNull(Find(solved, "PartyStatusDot"));
        }

        // ---- the facts that decide placement (C22) ----------------------------------------

        [Test]
        public void EverySeatCarriesItsHealthBarItsHpLineAndItsDefenceLine()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            AssertLength(PartyLayout.SeatCount, screen.SeatVitals, nameof(screen.SeatVitals));
            AssertLength(PartyLayout.SeatCount, screen.SeatHpFills, nameof(screen.SeatHpFills));
            AssertLength(PartyLayout.SeatCount, screen.SeatHpTexts, nameof(screen.SeatHpTexts));
            AssertLength(PartyLayout.SeatCount, screen.SeatDefTexts, nameof(screen.SeatDefTexts));

            Assert.AreEqual(UiStrings.PartySeatHp.Key, screen.SeatHpTexts[0].Node.Text.Key);
            Assert.AreEqual(UiStrings.PartySeatDefences.Key, screen.SeatDefTexts[0].Node.Text.Key);
            Assert.AreEqual("HP {0} / {1}", UiStrings.PartySeatHp.Template);
            Assert.AreEqual("Phys def {0} · Mag def {1}", UiStrings.PartySeatDefences.Template);
        }

        // Grows by width from the track's left edge, so it is pivoted there.
        [Test]
        public void TheHealthFillGrowsFromTheTracksLeftEdge()
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);

            var track = Find(solved, "PartySeat0HpTrack");
            var fill = Find(solved, "PartySeat0HpFill");

            Assert.AreEqual(track.Rect.Left, fill.Rect.Left, 0.01f);
            Assert.AreEqual(track.Rect.Width, fill.Rect.Width, 0.01f);
            Assert.AreEqual(412f, PartyLayout.BarWidth, 0.01f, "a seat is 492 wide and the bar keeps 40 px each side");
        }

        [Test]
        public void TheCarryPreviewHasATitleAndTwoColumnsOfThreeLinesPerSeat()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            foreach (var list in new[]
            {
                screen.SeatBadgeTexts, screen.SeatBadgeCarriedColumns, screen.SeatBadgeCarriedNames,
                screen.SeatBadgeCarriedHps, screen.SeatBadgeCarriedDefs, screen.SeatBadgeSeatedColumns,
                screen.SeatBadgeSeatedNames, screen.SeatBadgeSeatedHps, screen.SeatBadgeSeatedDefs,
            })
            {
                AssertLength(PartyLayout.SeatCount, list, "a carry preview list");
            }

            var solved = UiSolver.Solve(screen.Root, UiFrames.Reference);
            var carried = Find(solved, "PartySeat0BadgeCarried");
            var seated = Find(solved, "PartySeat0BadgeSeated");
            Assert.Less(carried.Rect.Right, seated.Rect.Left, "the two columns overlap: they sit side by side");
            Assert.AreEqual(carried.Rect.Centre.Y, seated.Rect.Centre.Y, 0.01f);

            Assert.AreEqual(130f, PartyLayout.BadgeHeight, 0.01f);
            Assert.AreEqual(468f, PartyLayout.BadgeWidth, 0.01f);
        }

        // ---- the roster is one node, and it fits under Formation --------------------------

        [Test]
        public void TheRosterIsOneSectionHoldingItsHairlineHeadingAndCards()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            Assert.IsTrue(screen.RosterSection.IsValid, "no roster section node was declared");
            Assert.IsFalse(screen.RosterSection.Node.StartInactive,
                "the section starts shown: the controller decides whether the roster is worth its space");

            var section = screen.RosterSection.Node;
            var names = new HashSet<string>();
            void Collect(UiNode node)
            {
                names.Add(node.Name);
                if (node.Children == null) return;
                foreach (var child in node.Children) Collect(child);
            }

            Collect(section);

            foreach (var expected in new[] { "PartyHairline", "PartyRosterHeading", "PartyRosterDropZone",
                                             "PartyCard0Button", "PartyCard2Button" })
            {
                Assert.IsTrue(names.Contains(expected), $"'{expected}' is not inside the roster section");
            }

            Assert.IsFalse(names.Contains("PartySeat0Button"), "a seat is inside the roster section");
        }

        [TestCase(1)]
        [TestCase(3)]
        public void TheRosterRowStaysInsideThePaneUnderTheSeats(int rosterCount)
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(rosterCount)).Root, UiFrames.Reference);

            var seats = Find(solved, "PartyFormationPanel");
            var section = Find(solved, "PartyRosterSection");

            Assert.Less(section.Rect.Top, seats.Rect.Bottom + 0.01f, "the roster starts above the seats' foot");
            Assert.GreaterOrEqual(PartyLayout.RosterSectionBottom, PartyLayout.ContentBottom,
                "the roster's cards run off the pane's floor");
            Assert.AreEqual(194f, PartyLayout.CardHeight, 0.01f);
            Assert.AreEqual(176f, PartyLayout.CardWidth, 0.01f);
            Assert.AreEqual(7, PartyLayout.MaxRosterCards(), "176 px cards and a 12 px gap in 1480 px");
        }

        // ---- the type floor: every line boxed for its own font (C14) ----------------------

        // A label whose box is shorter than one line of its own font draws
        // nothing in TMP: a Body 20 line is 26.5 px measured, 1.33 of the size.
        [Test]
        public void EveryLabelAndButtonIsBoxedForAtLeastOneLineOfItsFont()
        {
            var tooShort = new List<string>();

            void Walk(UiNode node)
            {
                if ((node.Kind == UiNodeKind.Label || node.Kind == UiNodeKind.Button) && node.Theme == null
                    && node.FontSize > 0 && node.Size.Y < node.FontSize * 1.33f - 0.01f)
                {
                    tooShort.Add($"{node.Name} ({node.Size.Y} < {node.FontSize * 1.33f})");
                }

                if (node.Children == null) return;
                foreach (var child in node.Children) Walk(child);
            }

            Walk(PartyScreen.Build(new PartyInputs(3)).Root);

            Assert.IsEmpty(tooShort, "boxed below one line, so TMP draws nothing: " + string.Join(", ", tooShort));
        }

        // The pane's own text, pinned by literal: what a seat reads at.
        [Test]
        public void ASeatsLinesAreOnTheRolesAndSizesTheSpecNames()
        {
            var solved = UiSolver.Solve(PartyScreen.Build(new PartyInputs(3)).Root, UiFrames.Reference);

            void Expect(string name, TypographyRole role, int size)
            {
                var node = Find(solved, name)?.Source;
                Assert.IsNotNull(node, $"no node '{name}'");
                Assert.AreEqual(role, node.Role, name);
                Assert.AreEqual(size, node.FontSize, name);
            }

            Expect("PartySeat0Name", TypographyRole.Body, 22);
            Expect("PartySeat0Role", TypographyRole.Body, 20);
            Expect("PartySeat0Hp", TypographyRole.Body, 24);
            Expect("PartySeat0Defences", TypographyRole.Body, 20);
            Expect("PartySeat0Label", TypographyRole.TacticalData, 24);
            Expect("PartySeat0BadgeText", TypographyRole.Body, 22);
            Expect("PartyCard0Name", TypographyRole.Body, 20);
            Expect("PartyCard0Tag", TypographyRole.Body, 20);
            Expect("PartyFormationSubtitle", TypographyRole.Body, 20);
            Expect("PartyFormationHeading", TypographyRole.FunctionalHeading, 24);
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

        // ---- P4: the drag ghost and the roster drop zone ----------------------------

        [Test]
        public void TheDragGhostExistsHiddenByDefault()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));

            Assert.IsTrue(screen.DragGhost.IsValid, "no drag ghost node was declared");
            Assert.IsTrue(screen.DragGhost.Node.StartInactive, "the drag ghost starts shown");
            Assert.IsTrue(screen.DragGhostArt.IsValid, "the ghost's art node was not declared");
            Assert.IsTrue(screen.DragGhostMonogramPlate.IsValid, "the ghost's monogram plate was not declared");
            Assert.IsTrue(screen.DragGhostMonogramLetter.IsValid, "the ghost's monogram letter was not declared");
        }

        [Test]
        public void TheRosterDropZoneExistsBehindEveryCardInDrawOrder()
        {
            var screen = PartyScreen.Build(new PartyInputs(3));
            Assert.IsTrue(screen.RosterDropZone.IsValid, "no roster drop zone node was declared");

            var order = DrawOrder(screen.Root);
            int zoneIndex = order.IndexOf("PartyRosterDropZone");
            Assert.AreNotEqual(-1, zoneIndex, "the roster drop zone is not in the tree");

            for (int i = 0; i < 3; i++)
            {
                string cardName = $"PartyCard{i}Button";
                int cardIndex = order.IndexOf(cardName);
                Assert.AreNotEqual(-1, cardIndex, $"{cardName} is not in the tree");

                // uGUI draws later siblings on top -- an earlier index in
                // preorder draw order is what "behind" means here.
                Assert.Less(zoneIndex, cardIndex, $"the roster drop zone should draw BEHIND {cardName}");
            }
        }

        // ---- the tab strip still fits with Party present -----------------------------

        [Test]
        public void TheTabStripStillFitsWithPartyPresentInBothSets()
        {
            var estimated = SystemMenuLabelWidths.Estimated();
            foreach (bool inRun in new[] { false, true })
            {
                var visible = SystemMenuTabs.VisibleIndices(inRun);
                var bar = SystemMenuLayout.Arrange(SystemMenuTabs.Visible(inRun),
                    visible.Select(i => estimated.Full[i]).ToList(),
                    visible.Select(i => estimated.Short[i]).ToList());

                Assert.IsFalse(bar.ShortLabels,
                    $"the {(inRun ? "in-run" : "out-of-run")} bar needs its short labels now Party is in it");
            }
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

        // Preorder over the DECLARED tree (not the solved one): a parent's
        // own draw order is its Children list order, and preorder visits a
        // sibling before any of a LATER sibling's own descendants -- exactly
        // what "does A draw behind B" needs to compare two names by index.
        private static List<string> DrawOrder(UiNode root)
        {
            var order = new List<string>();

            void Walk(UiNode node)
            {
                order.Add(node.Name);
                if (node.Children == null) return;
                foreach (var child in node.Children) Walk(child);
            }

            Walk(root);
            return order;
        }
    }
}
