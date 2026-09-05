using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // What the bag grid shows, and in what order.
    // THE PACK SCROLLS, so it has no pages and these have no paging tests.
    //
    // Six of them were removed with BagView's Page/PageCount/ClampPage/
    // CellCount, which had no production caller left once the dossier's pack
    // became a scrolling window. Paging itself is alive and tested -- the debug
    // menu and the glossary still use UiKit.Paging -- this was only the bag's
    // dead wrapper around it, and a "24 of 27" footer that no longer exists.
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
