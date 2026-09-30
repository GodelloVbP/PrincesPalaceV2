using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Glossary;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The GLOSSARY pane's geometry and tree (plan C25, spec section 5) and the
    // catalog it pages over. UiAudit re-solves the whole menu at four aspects
    // (SystemMenuScreenTests, MenuTypeLintTests), so overlap, overflow and the
    // type floor are covered there; what is pinned here is the arithmetic in
    // literals and the pairing of nodes to what fills them.
    public class GlossaryScreenTests
    {
        private static GlossaryScreen Screen() => GlossaryScreen.Build();

        // ---- the columns ---------------------------------------------------------------

        [Test]
        public void TheThreeColumnsAreTheSpecs280520And672AndFillTheAreaExactly()
        {
            Assert.AreEqual(1520f, GlossaryLayout.AreaWidth, 0.001f);
            Assert.AreEqual(756f, GlossaryLayout.AreaHeight, 0.001f);

            Assert.AreEqual(280f, GlossaryLayout.RailWidth, 0.001f);
            Assert.AreEqual(520f, GlossaryLayout.ListWidth, 0.001f);
            Assert.AreEqual(672f, GlossaryLayout.DetailWidth, 0.001f);

            Assert.AreEqual(-760f, GlossaryLayout.RailLeft, 0.001f);
            Assert.AreEqual(-456f, GlossaryLayout.ListLeft, 0.001f);
            Assert.AreEqual(88f, GlossaryLayout.DetailLeft, 0.001f);
            Assert.AreEqual(760f, GlossaryLayout.DetailLeft + GlossaryLayout.DetailWidth, 0.001f,
                "the detail column does not end at the area's right edge");
        }

        [Test]
        public void ThePaneIsTheWholeContentArea()
        {
            Assert.AreEqual(1600f, GlossaryLayout.PaneWidth, 0.001f);
            Assert.AreEqual(804f, GlossaryLayout.PaneHeight, 0.001f);
        }

        // ---- the rail ---------------------------------------------------------------------

        [Test]
        public void TheRailStacksEveryCategoryInsideTheArea()
        {
            Assert.AreEqual(346f, GlossaryLayout.CategoryCentreY(0), 0.001f);
            Assert.AreEqual(-130f, GlossaryLayout.CategoryCentreY(7), 0.001f);

            float bottom = GlossaryLayout.CategoryCentreY(GlossaryCatalog.Categories.Count - 1)
                           - GlossaryLayout.CategoryRowHeight * 0.5f;
            Assert.GreaterOrEqual(bottom, -GlossaryLayout.AreaHeight * 0.5f, "the last category leaves the area");
        }

        // ---- the list ----------------------------------------------------------------------

        [Test]
        public void TheListIsEightRowsOf64WithThePagerUnderThem()
        {
            Assert.AreEqual(8, GlossaryCatalog.RowsPerPage);
            Assert.AreEqual(64f, GlossaryLayout.ListRowHeight, 0.001f);
            Assert.AreEqual(346f, GlossaryLayout.ListRowCentreY(0), 0.001f);
            Assert.AreEqual(-102f, GlossaryLayout.ListRowCentreY(7), 0.001f);
            Assert.AreEqual(-134f, GlossaryLayout.ListRowsBottom, 0.001f);

            Assert.AreEqual(-168f, GlossaryLayout.PagerCentreY, 0.001f);
            Assert.AreEqual(-196f, GlossaryLayout.PagerLabelX, 0.001f);
            Assert.AreEqual(-290f, GlossaryLayout.PagerPrevX, 0.001f);
            Assert.AreEqual(-102f, GlossaryLayout.PagerNextX, 0.001f);

            float pagerTop = GlossaryLayout.PagerCentreY + GlossaryLayout.PagerArrowHeight * 0.5f;
            Assert.Less(pagerTop, GlossaryLayout.ListRowsBottom, "the pager runs into the last row");
        }

        [Test]
        public void TheNameAndMetaLinesFitInsideTheirRow()
        {
            Assert.LessOrEqual(GlossaryLayout.ListNameHeight + GlossaryLayout.ListMetaHeight,
                GlossaryLayout.ListRowHeight);
        }

        // ---- the detail --------------------------------------------------------------------

        [Test]
        public void TheDetailIsAnIconBesideTheNameAndMetaWithTheBodyUnderThem()
        {
            Assert.AreEqual(148f, GlossaryLayout.DetailIconCentreX, 0.001f);
            Assert.AreEqual(318f, GlossaryLayout.DetailIconCentreY, 0.001f);
            Assert.AreEqual(232f, GlossaryLayout.DetailTextLeft, 0.001f);
            Assert.AreEqual(528f, GlossaryLayout.DetailTextWidth, 0.001f);
            Assert.AreEqual(350f, GlossaryLayout.DetailNameCentreY, 0.001f);
            Assert.AreEqual(309f, GlossaryLayout.DetailMetaCentreY, 0.001f);
            Assert.AreEqual(34f, GlossaryLayout.DetailBodyCentreY, 0.001f);
            Assert.AreEqual(-204f, GlossaryLayout.DetailLockedByCentreY, 0.001f);

            float metaBottom = GlossaryLayout.DetailMetaCentreY - GlossaryLayout.DetailMetaHeight * 0.5f;
            float iconBottom = GlossaryLayout.DetailIconCentreY - GlossaryLayout.DetailIconSize * 0.5f;
            Assert.GreaterOrEqual(metaBottom, iconBottom - 0.001f, "the meta line drops below the icon");

            float bodyTop = GlossaryLayout.DetailBodyCentreY + GlossaryLayout.DetailBodyHeight * 0.5f;
            Assert.LessOrEqual(bodyTop, iconBottom, "the body starts over the icon");

            float lockedBottom = GlossaryLayout.DetailLockedByCentreY - GlossaryLayout.DetailLockedByHeight * 0.5f;
            Assert.GreaterOrEqual(lockedBottom, -GlossaryLayout.AreaHeight * 0.5f, "the lock note leaves the area");
        }

        // The Defense curve is the longest entry the glossary authors, and the
        // body box is what it has to fit; UiTextFitAudit measures a sample, this
        // pins the box's own height against a plain floor on how tall it is.
        [Test]
        public void TheBodyBoxIsTallEnoughForAnEightLineEntry()
        {
            Assert.GreaterOrEqual(GlossaryLayout.DetailBodyHeight, 8 * 30f);
        }

        // ---- the tree ------------------------------------------------------------------------

        [Test]
        public void ThereIsOneRailButtonPerCategory()
        {
            // Built by walking the enum, so appending a category adds a button
            // and nothing else. If this ever fails, someone hard-coded a count.
            var screen = Screen();

            Assert.AreEqual(8, GlossaryCatalog.Categories.Count);
            Assert.AreEqual(GlossaryCatalog.Categories.Count, screen.CategoryButtons.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryLabels.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryCounts.Count);
            Assert.AreEqual(screen.CategoryButtons.Count, screen.CategoryMarkers.Count);
        }

        [Test]
        public void ThereIsOneRowPerPageSlot()
        {
            var screen = Screen();

            Assert.AreEqual(GlossaryCatalog.RowsPerPage, screen.Rows.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowNames.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowMetas.Count);
            Assert.AreEqual(screen.Rows.Count, screen.RowMarkers.Count);
        }

        [Test]
        public void ARailButtonAndARowAreQuietChromelessBodyButtons()
        {
            // No plate: the pane sits on the menu's fill like RUN, and a row
            // that is only read has no click cue to make.
            var screen = Screen();

            foreach (var button in screen.CategoryButtons.Concat(screen.Rows))
            {
                Assert.IsTrue(button.Node.Chromeless, $"{button.Node.Name} wears a plate");
                Assert.AreEqual(UiClickCue.Silent, button.Node.Click, $"{button.Node.Name} makes a click cue");
                Assert.AreEqual(TypographyRole.Body, button.Node.Role, $"{button.Node.Name} has no Body role");
            }
        }

        // The rail is there from the first frame; a row is shown by the
        // controller for as many entries as the page holds. A rail button that
        // started hidden was unreachable by the pad.
        [Test]
        public void TheRailStartsShownAndTheRowsStartHidden()
        {
            var screen = Screen();

            foreach (var button in screen.CategoryButtons)
            {
                Assert.IsFalse(button.Node.StartInactive, $"{button.Node.Name} starts hidden");
            }

            foreach (var row in screen.Rows)
            {
                Assert.IsTrue(row.Node.StartInactive, $"{row.Node.Name} starts shown with nothing in it");
            }
        }

        [Test]
        public void ThePaneNoLongerCarriesAModalFrameOrAClose()
        {
            // The standalone 1890x945 glossary and its Close button are gone;
            // the menu's own frame and close are the only ones.
            var names = Walk(Screen().Root).Select(n => n.Name).ToList();

            CollectionAssert.DoesNotContain(names, "GlossaryFrame");
            CollectionAssert.DoesNotContain(names, "GlossaryCloseButton");
            CollectionAssert.DoesNotContain(names, "GlossaryPanel");
        }

        [Test]
        public void TheHintsTheLockNoteAndThePagerStartHidden()
        {
            var screen = Screen();

            Assert.IsTrue(screen.DetailLockedBy.Node.StartInactive);
            Assert.IsTrue(screen.EmptyHint.Node.StartInactive);
            Assert.IsTrue(screen.PrevPageButton.Node.StartInactive);
            Assert.IsTrue(screen.NextPageButton.Node.StartInactive);
            Assert.IsTrue(screen.PageLabel.Node.StartInactive);
        }

        [Test]
        public void SelectionMarkersStartInvisible()
        {
            var screen = Screen();

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
            foreach (var node in Walk(Screen().Root))
            {
                bool isChild = node.Name.StartsWith("GlossaryCategory") || node.Name.StartsWith("GlossaryRow");
                if (!isChild || node.Kind == UiNodeKind.Button) continue;

                Assert.IsTrue(node.Decor, $"{node.Name} would steal its button's click");
            }
        }

        // ---- the categories ------------------------------------------------------------------

        // Spec section 5 and plan C21: the rail's order, and "Skills" where the
        // rail said "Spells".
        [Test]
        public void TheCategoriesRunRelicsStatusesMonstersSkillsItemsTalentsMechanicsDeeds()
        {
            CollectionAssert.AreEqual(
                new[] { "RELICS", "STATUSES", "MONSTERS", "SKILLS", "ITEMS", "TALENTS", "MECHANICS", "DEEDS" },
                GlossaryCatalog.Categories.Select(GlossaryCatalog.DisplayName).ToList());

            CollectionAssert.AreEqual(
                new[]
                {
                    GlossaryCategory.Relics, GlossaryCategory.Statuses, GlossaryCategory.Monsters,
                    GlossaryCategory.Skills, GlossaryCategory.Items, GlossaryCategory.Talents,
                    GlossaryCategory.Mechanics, GlossaryCategory.Achievements,
                },
                GlossaryCatalog.Categories.ToList());
        }

        [Test]
        public void EveryCategoryHasADisplayName()
        {
            foreach (var category in GlossaryCatalog.Categories)
            {
                Assert.IsNotEmpty(GlossaryCatalog.DisplayName(category));
            }
        }

        [Test]
        public void AddingACategoryCannotSilentlyShareADisplayName()
        {
            var names = GlossaryCatalog.Categories.Select(GlossaryCatalog.DisplayName).ToList();

            CollectionAssert.AllItemsAreUnique(names);
        }

        // ---- the paging rules ------------------------------------------------------------------

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
            // Exactly eight entries is ONE page, not two. The off-by-one here
            // is the classic, and it shows up as a blank page the player can
            // flip to.
            Assert.AreEqual(1, GlossaryCatalog.PageCount(8));
            Assert.AreEqual(2, GlossaryCatalog.PageCount(9));
        }

        [Test]
        public void PagingIsClampedAtBothEnds()
        {
            var entries = Entries(16);

            Assert.AreEqual(0, GlossaryCatalog.ClampPage(-5, entries.Count));
            Assert.AreEqual(1, GlossaryCatalog.ClampPage(99, entries.Count));
        }

        [Test]
        public void TheLastPageIsShortRatherThanPadded()
        {
            var entries = Entries(11);

            Assert.AreEqual(3, GlossaryCatalog.Page(entries, 1).Count);
            Assert.AreEqual("Name 8", GlossaryCatalog.Page(entries, 1)[0].Name);
        }

        [Test]
        public void UnlockedCountIgnoresLockedEntries()
        {
            // The rail says "17 / 40", which is what makes a glossary
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
