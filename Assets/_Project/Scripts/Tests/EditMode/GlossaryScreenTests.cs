using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    public class GlossaryScreenTests
    {
        private static UiNode Tree() => GlossaryScreen.Build().Root;

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void CloseWearsSilverAndThePooledRowsStayUnthemed()
        {
            var screen = GlossaryScreen.Build();

            Assert.AreEqual(ButtonTheme.Silver, screen.CloseButton.Node.Theme);
            Assert.IsNull(screen.CategoryButtons[0].Node.Theme, "pooled, pinned-width tab strip");
            Assert.IsNull(screen.Rows[0].Node.Theme, "pooled, pinned-width row");
        }

        [Test]
        public void ItAuditsCleanInsideTheHubItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(HubScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void ThereIsOneRailButtonPerCategory()
        {
            // Built by walking the enum, so appending a category adds a button
            // and nothing else. If this ever fails, someone hard-coded a count.
            var screen = GlossaryScreen.Build();

            Assert.AreEqual(GlossaryCatalog.Categories.Count, screen.CategoryButtons.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryLabels.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryCounts.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryMarkers.Count);
        }

        [Test]
        public void ThereIsOneRowPerPageSlot()
        {
            var screen = GlossaryScreen.Build();

            Assert.AreEqual(GlossaryCatalog.RowsPerPage, screen.Rows.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowNames.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowMetas.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowMarkers.Count);
        }

        [Test]
        public void EveryCategoryHasADisplayName()
        {
            // Spelled out rather than ToString(), so a category is never
            // discovered to be rendering as its enum member name.
            foreach (var category in GlossaryCatalog.Categories)
            {
                string name = GlossaryCatalog.DisplayName(category);

                Assert.IsNotEmpty(name);
                Assert.AreNotEqual(category.ToString(), name,
                    $"{category} is falling through to ToString()");
            }
        }

        [Test]
        public void AddingACategoryCannotSilentlyShareADisplayName()
        {
            var names = GlossaryCatalog.Categories.Select(GlossaryCatalog.DisplayName).ToList();

            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void TheScreenAndTheLockNoteStartHidden()
        {
            var screen = GlossaryScreen.Build();

            Assert.IsTrue(screen.Root.StartInactive);
            Assert.IsTrue(screen.DetailLockedBy.Node.StartInactive);
            Assert.IsTrue(screen.EmptyHint.Node.StartInactive);
        }

        [Test]
        public void SelectionMarkersStartInvisible()
        {
            var screen = GlossaryScreen.Build();

            foreach (var marker in screen.CategoryMarkers.Concat(screen.RowMarkers))
            {
                StringAssert.EndsWith("00", marker.Node.ColorHex);
            }
        }

        [Test]
        public void EveryRailAndRowLayerIsDecor()
        {
            // Decor clears the raycast, so a label cannot eat the click meant
            // for the button it sits on.
            foreach (var node in Walk(Tree()))
            {
                bool isChild = node.Name.StartsWith("GlossaryCategory") || node.Name.StartsWith("GlossaryRow");
                if (!isChild || node.Kind == UiNodeKind.Button) continue;

                Assert.IsTrue(node.Decor, $"{node.Name} would steal its button's click");
            }
        }

        // ---- the paging rules ------------------------------------------------------

        private static List<GlossaryEntry> Entries(int count) =>
            Enumerable.Range(0, count).Select(i => new GlossaryEntry("id" + i, "Name " + i, "", "")).ToList();

        [Test]
        public void AnEmptyCategoryIsStillOnePage()
        {
            Assert.AreEqual(1, GlossaryCatalog.PageCount(0));
            Assert.IsEmpty(GlossaryCatalog.Page(new List<GlossaryEntry>(), 0));
            Assert.IsEmpty(GlossaryCatalog.Page(null, 0));
        }

        [Test]
        public void AFullPageDoesNotCreateAnEmptyNextOne()
        {
            // Exactly RowsPerPage entries is ONE page, not two. The off-by-one
            // here is the classic, and it shows up as a blank page the player
            // can flip to.
            Assert.AreEqual(1, GlossaryCatalog.PageCount(GlossaryCatalog.RowsPerPage));
            Assert.AreEqual(2, GlossaryCatalog.PageCount(GlossaryCatalog.RowsPerPage + 1));
        }

        [Test]
        public void PagingIsClampedAtBothEnds()
        {
            var entries = Entries(GlossaryCatalog.RowsPerPage * 2);

            Assert.AreEqual(0, GlossaryCatalog.ClampPage(-5, entries.Count));
            Assert.AreEqual(1, GlossaryCatalog.ClampPage(99, entries.Count));
        }

        [Test]
        public void TheLastPageIsShortRatherThanPadded()
        {
            var entries = Entries(GlossaryCatalog.RowsPerPage + 3);

            Assert.AreEqual(3, GlossaryCatalog.Page(entries, 1).Count);
        }

        [Test]
        public void UnlockedCountIgnoresLockedEntries()
        {
            // The rail says "17 of 40", which is what makes a glossary
            // something to fill in rather than a reference table.
            var mixed = new List<GlossaryEntry>
            {
                new GlossaryEntry("a", "A", "", ""),
                new GlossaryEntry("b", "B", "", "", locked: true, lockedBy: "x"),
                new GlossaryEntry("c", "C", "", ""),
            };

            Assert.AreEqual(2, GlossaryCatalog.UnlockedCount(mixed));
            Assert.AreEqual(0, GlossaryCatalog.UnlockedCount(null));
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}
