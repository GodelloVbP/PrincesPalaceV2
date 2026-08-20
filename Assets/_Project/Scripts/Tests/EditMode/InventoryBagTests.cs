using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // What the bag grid shows, and in what order.
    public class InventoryBagTests
    {
        // Kind as int, mirroring Core's ItemKind: 0 Consumable, 1 Weapon,
        // 2 Equipment. Named here so the fixtures below read as intent.
        private const int Consumable = 0;
        private const int Weapon = 1;
        private const int Equipment = 2;

        private static BagItem Item(string id, int kind = Equipment, int tier = 0, int plus = 0,
                                    int count = 1, string name = null, bool equippable = true) =>
            new BagItem(id, name ?? id, kind, EquipmentSlot.Torso, tier, plus, count, "", equippable);

        private static string[] Ids(IEnumerable<BagItem> items) => items.Select(i => i.Id).ToArray();

        // ---- order ---------------------------------------------------------------

        [Test]
        public void WearableThingsComeBeforeConsumables()
        {
            // Page one is for decisions. A screen that opens on a wall of
            // potions buries the thing the player came to do.
            var sorted = BagView.Sorted(new[]
            {
                Item("potion", Consumable, equippable: false),
                Item("cuirass"),
            });

            CollectionAssert.AreEqual(new[] { "cuirass", "potion" }, Ids(sorted));
        }

        [Test]
        public void WithinAKindTheBestTierLeads()
        {
            var sorted = BagView.Sorted(new[]
            {
                Item("plain", tier: 0),
                Item("mythic", tier: 10),
                Item("fine", tier: 4),
            });

            CollectionAssert.AreEqual(new[] { "mythic", "fine", "plain" }, Ids(sorted));
        }

        [Test]
        public void TheHonedCopyLeadsThePlainOne()
        {
            // Same id, same tier, different plus -- the case generated gear
            // produces constantly.
            var sorted = BagView.Sorted(new[]
            {
                Item("sword", Weapon, tier: 3, plus: 0),
                Item("sword", Weapon, tier: 3, plus: 5),
            });

            CollectionAssert.AreEqual(new[] { 5, 0 }, sorted.Select(i => i.Plus).ToArray());
        }

        [Test]
        public void TheOrderIsTotal_SoTheGridNeverReshufflesUnderTheCursor()
        {
            // The rule this whole comparator exists for. Two items that compare
            // equal on every key would swap places between refreshes, moving a
            // cell out from under the player mid-click.
            var items = new[]
            {
                Item("b", Weapon, tier: 3, plus: 1),
                Item("a", Weapon, tier: 3, plus: 1),
                Item("c", Weapon, tier: 3, plus: 1),
            };

            var forwards = Ids(BagView.Sorted(items));
            var backwards = Ids(BagView.Sorted(items.Reverse()));

            CollectionAssert.AreEqual(forwards, backwards, "the input order must not survive into the output");
        }

        [Test]
        public void SortingNothingIsNotACrash()
        {
            CollectionAssert.IsEmpty(BagView.Sorted(null));
            CollectionAssert.IsEmpty(BagView.Sorted(new BagItem[0]));
        }

        // ---- paging ----------------------------------------------------------------

        [Test]
        public void AnEmptyBagIsStillOnePage()
        {
            // "PAGE 1 OF 1" over an empty grid is a state. "PAGE 1 OF 0" is a
            // bug report.
            Assert.AreEqual(1, BagView.PageCount(0));
        }

        [Test]
        public void ThePageBoundaryIsExact()
        {
            Assert.AreEqual(1, BagView.PageCount(BagView.CellCount), "a full page is one page");
            Assert.AreEqual(2, BagView.PageCount(BagView.CellCount + 1), "one over spills");
            Assert.AreEqual(2, BagView.PageCount(BagView.CellCount * 2));
            Assert.AreEqual(3, BagView.PageCount(BagView.CellCount * 2 + 1));
        }

        [Test]
        public void PagesAreClampedRatherThanTrusted()
        {
            // The controller pages by stepping an int; nothing stops it running
            // off either end when the bag shrinks under it.
            Assert.AreEqual(0, BagView.ClampPage(-4, 50));
            Assert.AreEqual(2, BagView.ClampPage(99, 50), "50 items is three pages, last index 2");
            Assert.AreEqual(0, BagView.ClampPage(3, 0), "an empty bag has only page 0");
        }

        [Test]
        public void APageHoldsExactlyItsOwnSlice()
        {
            var sorted = BagView.Sorted(Enumerable.Range(0, BagView.CellCount + 5)
                .Select(i => Item($"item{i:00}", tier: 100 - i)));

            var first = BagView.Page(sorted, 0);
            var second = BagView.Page(sorted, 1);

            Assert.AreEqual(BagView.CellCount, first.Count);
            Assert.AreEqual(5, second.Count, "the last page is short, not padded");
            CollectionAssert.IsEmpty(first.Select(i => i.Id).Intersect(second.Select(i => i.Id)),
                "no item appears on two pages");
        }

        [Test]
        public void EveryItemAppearsOnExactlyOnePage()
        {
            var sorted = BagView.Sorted(Enumerable.Range(0, 47).Select(i => Item($"item{i:00}", tier: i)));

            var walked = Enumerable.Range(0, BagView.PageCount(sorted.Count))
                .SelectMany(p => BagView.Page(sorted, p))
                .Select(i => i.Id)
                .ToList();

            CollectionAssert.AreEquivalent(Ids(sorted), walked, "walking every page must visit the whole bag");
            Assert.AreEqual(47, walked.Distinct().Count());
        }

        [Test]
        public void AskingForAPageBeyondTheEndGivesTheLastOne()
        {
            var sorted = BagView.Sorted(new[] { Item("only") });

            CollectionAssert.AreEqual(new[] { "only" }, Ids(BagView.Page(sorted, 7)));
        }
        // ---- the player's chosen order -------------------------------------------
        //
        // THREE KEYS, NOT THE FOUR THAT WERE ASKED FOR. "tier / rarity / +'s /
        // name" reads as four, and rarity is not a fourth axis: RarityBands
        // derives the band FROM the tier, so a rarity ordering is the tier
        // ordering with its precision thrown away. Two buttons drawing the same
        // list is the same failure as a control that stores nothing.
        [Test]
        public void RarityIsNotAFourthOrdering()
        {
            Assert.AreEqual(3, BagSort.All.Length,
                "a fourth sort key appeared - if it is rarity, it draws the tier list");
        }

        [Test]
        public void SortingByPlusPutsTheMostHonedFirst()
        {
            var sorted = BagSort.By(new[]
            {
                Item("plain", plus: 0),
                Item("honed", plus: 3),
                Item("middling", plus: 1),
            }, BagSortKey.Plus);

            CollectionAssert.AreEqual(new[] { "honed", "middling", "plain" }, Ids(sorted));
        }

        [Test]
        public void SortingByNameIsAlphabetical()
        {
            var sorted = BagSort.By(new[]
            {
                Item("c", name: "Cuirass"),
                Item("a", name: "Axe"),
                Item("b", name: "Boots"),
            }, BagSortKey.Name);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Ids(sorted));
        }

        // Tier defers to BagView.Sorted rather than restating that ordering, so
        // the pack's long-standing default stays exactly one implementation.
        [Test]
        public void TierIsTheOrderThePackAlreadyHad()
        {
            var items = new[]
            {
                Item("potion", Consumable, equippable: false),
                Item("cuirass", tier: 4),
                Item("dagger", Weapon, tier: 2),
            };

            CollectionAssert.AreEqual(Ids(BagView.Sorted(items)),
                Ids(BagSort.By(items, BagSortKey.Tier)));
        }

        // DETERMINISTIC TO THE LAST TIE-BREAK, whichever key is live. Generated
        // gear means dozens of stacks that differ only by plus, and any pair
        // left unordered would swap places between refreshes -- a list that
        // reshuffles under the cursor as things are equipped.
        [TestCase(BagSortKey.Tier)]
        [TestCase(BagSortKey.Plus)]
        [TestCase(BagSortKey.Name)]
        public void EveryOrderingIsStableAcrossRepeatedSorts(BagSortKey key)
        {
            var items = new[]
            {
                Item("a", name: "Same", tier: 3, plus: 1),
                Item("b", name: "Same", tier: 3, plus: 1),
                Item("c", name: "Same", tier: 3, plus: 1),
            };

            CollectionAssert.AreEqual(Ids(BagSort.By(items, key)), Ids(BagSort.By(items, key)),
                "two identical sorts disagreed, so the list reshuffles on every refresh");
        }

        [Test]
        public void SortingHandlesAnEmptyBag()
        {
            foreach (var key in BagSort.All)
            {
                CollectionAssert.IsEmpty(BagSort.By(null, key));
                CollectionAssert.IsEmpty(BagSort.By(new BagItem[0], key));
            }
        }

    }
}
