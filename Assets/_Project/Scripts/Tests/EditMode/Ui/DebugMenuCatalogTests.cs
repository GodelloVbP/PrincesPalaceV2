using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.DebugMenu;

namespace PrincesPalace.Domain.Tests
{
    // Filtering and paging for the debug item picker.
    //
    // Worth pinning despite being a developer tool, because the whole reason
    // the picker exists is that the naive version -- one button granting all
    // 261 items into a 20-cell bag -- produced 14 unnavigable pages. A picker
    // whose own paging is off by one recreates the problem it was built to
    // avoid, one page further along.
    public class DebugMenuCatalogTests
    {
        private const int Consumable = 0;
        private const int Weapon = 1;
        private const int Equipment = 2;

        private static List<DebugItem> Catalogue(int count, int kind = Weapon)
        {
            // Descending tier on purpose: if Filter did not sort, the fixture
            // would come back in this order and the tier assertions below
            // would fail rather than accidentally pass.
            return Enumerable.Range(0, count)
                .Select(i => new DebugItem($"item_{i:D3}", $"Item {i:D3}", kind, count - i))
                .ToList();
        }

        [Test]
        public void AnEmptyCatalogueIsStillOnePage()
        {
            // "PAGE 1 OF 0" is not a thing a player or a developer should ever
            // read, and the pager has to clamp against something.
            Assert.AreEqual(1, DebugMenuCatalog.PageCount(0));
            Assert.AreEqual(0, DebugMenuCatalog.ClampPage(3, 0));
            CollectionAssert.IsEmpty(DebugMenuCatalog.Page(new List<DebugItem>(), 0));
        }

        [Test]
        public void AFullPageDoesNotSpillIntoASecondOne()
        {
            // The off-by-one that matters: exactly RowsPerPage items is ONE
            // page, not one page plus an empty one.
            int rows = DebugMenuCatalog.RowsPerPage;

            Assert.AreEqual(1, DebugMenuCatalog.PageCount(rows));
            Assert.AreEqual(2, DebugMenuCatalog.PageCount(rows + 1));
        }

        [Test]
        public void APageNeverReturnsMoreRowsThanTheScreenBuilt()
        {
            // The screen declares exactly RowsPerPage row nodes. A page
            // returning more would silently drop items with nothing saying so.
            var filtered = DebugMenuCatalog.Filter(Catalogue(100), DebugMenuCatalog.KindAll);

            for (int page = 0; page < DebugMenuCatalog.PageCount(filtered.Count); page++)
            {
                Assert.LessOrEqual(DebugMenuCatalog.Page(filtered, page).Count, DebugMenuCatalog.RowsPerPage);
            }
        }

        [Test]
        public void EveryItemAppearsOnExactlyOnePage()
        {
            // Walking the pages must reconstruct the list -- no gaps at a page
            // seam, no duplicates. This is the assertion that would have caught
            // a start-index that used PageCount instead of RowsPerPage.
            var filtered = DebugMenuCatalog.Filter(Catalogue(37), DebugMenuCatalog.KindAll);

            var walked = new List<string>();
            for (int page = 0; page < DebugMenuCatalog.PageCount(filtered.Count); page++)
            {
                walked.AddRange(DebugMenuCatalog.Page(filtered, page).Select(i => i.Id));
            }

            CollectionAssert.AreEqual(filtered.Select(i => i.Id).ToList(), walked);
            Assert.AreEqual(37, walked.Distinct().Count());
        }

        [Test]
        public void TheFilterKeepsOnlyTheAskedForKind()
        {
            var mixed = Catalogue(5, Consumable)
                .Concat(Catalogue(7, Weapon))
                .Concat(Catalogue(3, Equipment))
                .ToList();

            Assert.AreEqual(5, DebugMenuCatalog.Filter(mixed, Consumable).Count);
            Assert.AreEqual(7, DebugMenuCatalog.Filter(mixed, Weapon).Count);
            Assert.AreEqual(3, DebugMenuCatalog.Filter(mixed, Equipment).Count);
        }

        [Test]
        public void KindAllKeepsEverything()
        {
            var mixed = Catalogue(5, Consumable).Concat(Catalogue(7, Weapon)).ToList();

            Assert.AreEqual(12, DebugMenuCatalog.Filter(mixed, DebugMenuCatalog.KindAll).Count);
        }

        [Test]
        public void RowsAreSortedByTierBeforeName()
        {
            // Tier is the axis a debug menu is searched on -- "give me a tier 4
            // weapon" is the question. Sorting by name would scatter the tiers
            // across every page and make the pager useless for the one job the
            // menu has.
            var filtered = DebugMenuCatalog.Filter(Catalogue(20), DebugMenuCatalog.KindAll);

            for (int i = 1; i < filtered.Count; i++)
            {
                Assert.LessOrEqual(filtered[i - 1].Tier, filtered[i].Tier);
            }
        }

        [Test]
        public void NameBreaksATierTieAndIdBreaksANameTie()
        {
            // Fully determined ordering, so the list cannot reshuffle between
            // two Refreshes and move a row out from under a click.
            var tied = new List<DebugItem>
            {
                new DebugItem("b", "Same Name", Weapon, 3),
                new DebugItem("a", "Same Name", Weapon, 3),
                new DebugItem("c", "Aardvark", Weapon, 3),
            };

            var sorted = DebugMenuCatalog.Filter(tied, DebugMenuCatalog.KindAll);

            Assert.AreEqual("c", sorted[0].Id, "name orders before the id tie-break");
            Assert.AreEqual("a", sorted[1].Id);
            Assert.AreEqual("b", sorted[2].Id);
        }

        [Test]
        public void ClampingHoldsAtBothEnds()
        {
            var filtered = DebugMenuCatalog.Filter(Catalogue(25), DebugMenuCatalog.KindAll);
            int last = DebugMenuCatalog.PageCount(filtered.Count) - 1;

            Assert.AreEqual(0, DebugMenuCatalog.ClampPage(-5, filtered.Count));
            Assert.AreEqual(last, DebugMenuCatalog.ClampPage(last + 5, filtered.Count));
        }

        [Test]
        public void ANullCatalogueDegradesRatherThanThrowing()
        {
            // Graceful degradation is the house style, and this one is
            // reachable: ContentDatabase.Items is read fresh on every Refresh
            // and can be empty mid content-rebuild in the editor.
            Assert.DoesNotThrow(() => DebugMenuCatalog.Filter(null, DebugMenuCatalog.KindAll));
            CollectionAssert.IsEmpty(DebugMenuCatalog.Filter(null, DebugMenuCatalog.KindAll));
            CollectionAssert.IsEmpty(DebugMenuCatalog.Page(null, 0));
        }
    }
}
