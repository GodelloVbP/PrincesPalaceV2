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
    //
    // REBUILT 2026-09-23 (debug menu overhaul, phase 1) for the category/
    // sub-filter model: what was Filter(all, kind) is now
    // Filter(all, category, subFilter), RowsPerPage doubled to 24 (2 columns
    // x 12), and a DebugItem carries a Category plus one optional SubKey.
    public class DebugMenuCatalogTests
    {
        private const int WeaponKind = 1;
        private const int EquipmentKind = 2;
        private const int ConsumableKind = 0;

        // Pinned literals, not read off the constant, so a change to either
        // number is a deliberate, visible edit here rather than a test that
        // silently keeps pace with whatever the source says.
        [Test]
        public void RowsPerPageIsTwoColumnsOfTwelve()
        {
            Assert.AreEqual(2, DebugMenuCatalog.Columns);
            Assert.AreEqual(12, DebugMenuCatalog.RowsPerColumn);
            Assert.AreEqual(24, DebugMenuCatalog.RowsPerPage);
        }

        // Descending tier on purpose: if Filter did not sort, the fixture
        // would come back in this order and the tier assertions below would
        // fail rather than accidentally pass.
        private static List<DebugItem> Catalogue(int count, DebugCategory category = DebugCategory.Weapons,
            int kind = WeaponKind)
        {
            return Enumerable.Range(0, count)
                .Select(i => new DebugItem($"item_{i:D3}", $"Item {i:D3}", kind, count - i, category))
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
            var filtered = DebugMenuCatalog.Filter(Catalogue(100), DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll);

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
            var filtered = DebugMenuCatalog.Filter(Catalogue(37), DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll);

            var walked = new List<string>();
            for (int page = 0; page < DebugMenuCatalog.PageCount(filtered.Count); page++)
            {
                walked.AddRange(DebugMenuCatalog.Page(filtered, page).Select(i => i.Id));
            }

            CollectionAssert.AreEqual(filtered.Select(i => i.Id).ToList(), walked);
            Assert.AreEqual(37, walked.Distinct().Count());
        }

        [Test]
        public void TheFilterKeepsOnlyItemsInTheAskedForCategory()
        {
            var mixed = Catalogue(5, DebugCategory.Consumables, ConsumableKind)
                .Concat(Catalogue(7, DebugCategory.Weapons, WeaponKind))
                .Concat(Catalogue(3, DebugCategory.Equipment, EquipmentKind))
                .ToList();

            Assert.AreEqual(5, DebugMenuCatalog.Filter(mixed, DebugCategory.Consumables,
                DebugMenuCatalog.SubFilterAll).Count);
            Assert.AreEqual(7, DebugMenuCatalog.Filter(mixed, DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll).Count);
            Assert.AreEqual(3, DebugMenuCatalog.Filter(mixed, DebugCategory.Equipment,
                DebugMenuCatalog.SubFilterAll).Count);
        }

        [Test]
        public void SubFilterAllKeepsEveryItemInTheCategory()
        {
            var mixed = Catalogue(5, DebugCategory.Consumables, ConsumableKind)
                .Concat(Catalogue(7, DebugCategory.Weapons, WeaponKind))
                .ToList();

            Assert.AreEqual(7, DebugMenuCatalog.Filter(mixed, DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll).Count);
        }

        [Test]
        public void WeaponsSubFilterMatchesOnTier()
        {
            // Weapons' sub-filter row is the tier chips -- the sub-filter
            // value IS the tier itself, no separate SubKey needed.
            var mixed = new List<DebugItem>
            {
                new DebugItem("a", "A", WeaponKind, 3, DebugCategory.Weapons),
                new DebugItem("b", "B", WeaponKind, 3, DebugCategory.Weapons),
                new DebugItem("c", "C", WeaponKind, 5, DebugCategory.Weapons),
            };

            Assert.AreEqual(2, DebugMenuCatalog.Filter(mixed, DebugCategory.Weapons, 3).Count);
            Assert.AreEqual(1, DebugMenuCatalog.Filter(mixed, DebugCategory.Weapons, 5).Count);
            Assert.AreEqual(0, DebugMenuCatalog.Filter(mixed, DebugCategory.Weapons, 4).Count);
        }

        [Test]
        public void EquipmentSubFilterMatchesOnSubKeyNotTier()
        {
            // Equipment's sub-filter row is the slot chips -- SubKey, a
            // separate axis from Tier, since two different slots can share a
            // tier and the same slot spans every tier.
            var mixed = new List<DebugItem>
            {
                new DebugItem("head1", "Head Tier1", EquipmentKind, 1, DebugCategory.Equipment, subKey: 0),
                new DebugItem("head2", "Head Tier9", EquipmentKind, 9, DebugCategory.Equipment, subKey: 0),
                new DebugItem("legs1", "Legs Tier1", EquipmentKind, 1, DebugCategory.Equipment, subKey: 3),
            };

            var headOnly = DebugMenuCatalog.Filter(mixed, DebugCategory.Equipment, 0);
            Assert.AreEqual(2, headOnly.Count, "both head pieces match slot 0 regardless of tier");

            var legsOnly = DebugMenuCatalog.Filter(mixed, DebugCategory.Equipment, 3);
            Assert.AreEqual(1, legsOnly.Count);
        }

        [Test]
        public void ASetsRowIsInvisibleToAnotherCategorysFilter()
        {
            var sets = new List<DebugItem>
            {
                new DebugItem("dragon_set", "Dragon Set", -1, 4, DebugCategory.Sets, setId: "dragon_set"),
            };

            CollectionAssert.IsEmpty(DebugMenuCatalog.Filter(sets, DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll));
            Assert.AreEqual(1, DebugMenuCatalog.Filter(sets, DebugCategory.Sets,
                DebugMenuCatalog.SubFilterAll).Count);
        }

        [Test]
        public void RowsAreSortedByTierBeforeName()
        {
            // Tier is the axis you actually search on in a debug menu -- "give
            // me a tier 4 weapon" is the question. Sorting by name would
            // scatter the tiers across every page and make the pager useless
            // for the one thing the menu is for.
            var filtered = DebugMenuCatalog.Filter(Catalogue(20), DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll);

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
                new DebugItem("b", "Same Name", WeaponKind, 3, DebugCategory.Weapons),
                new DebugItem("a", "Same Name", WeaponKind, 3, DebugCategory.Weapons),
                new DebugItem("c", "Aardvark", WeaponKind, 3, DebugCategory.Weapons),
            };

            var sorted = DebugMenuCatalog.Filter(tied, DebugCategory.Weapons, DebugMenuCatalog.SubFilterAll);

            Assert.AreEqual("c", sorted[0].Id, "name orders before the id tie-break");
            Assert.AreEqual("a", sorted[1].Id);
            Assert.AreEqual("b", sorted[2].Id);
        }

        [Test]
        public void ClampingHoldsAtBothEnds()
        {
            var filtered = DebugMenuCatalog.Filter(Catalogue(25), DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll);
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
            Assert.DoesNotThrow(() => DebugMenuCatalog.Filter(null, DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll));
            CollectionAssert.IsEmpty(DebugMenuCatalog.Filter(null, DebugCategory.Weapons,
                DebugMenuCatalog.SubFilterAll));
            CollectionAssert.IsEmpty(DebugMenuCatalog.Page(null, 0));
        }

        [Test]
        public void ThePlusStepperClampsAtZeroAndTen()
        {
            // 10 is ItemUpgrade.MaxPlus, pinned as a literal so a change to
            // the cap is a decision this test sees rather than follows.
            Assert.AreEqual(0, DebugMenuCatalog.StepPlus(0, -1));
            Assert.AreEqual(1, DebugMenuCatalog.StepPlus(0, 1));
            Assert.AreEqual(10, DebugMenuCatalog.StepPlus(10, 1));
            Assert.AreEqual(9, DebugMenuCatalog.StepPlus(10, -1));
        }

        [Test]
        public void OnlyResourcesAndToolsAreActionTabs()
        {
            var actions = System.Enum.GetValues(typeof(DebugCategory)).Cast<DebugCategory>()
                .Where(DebugMenuCatalog.IsAction);

            CollectionAssert.AreEquivalent(new[] { DebugCategory.Resources, DebugCategory.Tools }, actions);
        }

        [Test]
        public void ActionRowsKeepTheirDeclaredOrderNotAlphabetical()
        {
            // Tier is the row's position on an action tab. "+10000" sorts
            // before "+1000" by name; the declared order must win.
            var rows = new[]
            {
                new DebugItem("r:0", "+100 GOLD", -1, 0, DebugCategory.Resources),
                new DebugItem("r:1", "+1000 GOLD", -1, 1, DebugCategory.Resources),
                new DebugItem("r:2", "+10000 GOLD", -1, 2, DebugCategory.Resources),
            };

            var sorted = DebugMenuCatalog.Filter(rows.Reverse(), DebugCategory.Resources, DebugMenuCatalog.SubFilterAll);

            CollectionAssert.AreEqual(new[] { "r:0", "r:1", "r:2" }, sorted.Select(r => r.Id));
        }
    }
}
