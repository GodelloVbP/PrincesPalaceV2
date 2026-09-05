using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.EditModeTests
{
    // THE SHELF, WITHOUT A SCENE.
    //
    // ShopStock takes its candidates and its per-item roll as parameters
    // rather than reaching for ContentDatabase, exactly as ItemOfferTable
    // already does -- which is what lets the whole rule be exercised here
    // with a hand-built pool instead of a Resources load.
    public class ShopStockTests
    {
        // A pool wide enough that "no duplicates" is a real constraint and
        // "an undersized pool" has to be built on purpose.
        private static List<ItemOffer> Pool(int count, int tier = 0)
        {
            return Enumerable.Range(0, count)
                .Select(i => new ItemOffer($"item_{tier}_{i}", tier))
                .ToList();
        }

        private static List<RelicOption> Relics(int count, RelicRarity rarity = RelicRarity.Common)
        {
            return Enumerable.Range(0, count)
                .Select(i => new RelicOption($"relic_{i}", rarity))
                .ToList();
        }

        private static Func<int, int> Stream(ulong seed)
        {
            var rng = new SeededRandom(seed);
            return bound => rng.NextInt(0, bound);
        }

        // A stand-in for ItemOfferRoll.RollOne: honed by a fixed amount, so a
        // shelf's prices are predictable without asserting on a distribution.
        private static Func<ItemOffer, ItemOffer> Hone(int plus, RiftTier rift = RiftTier.Ordinary)
        {
            return offer => offer.WithPlus(plus).WithModifiers(rift, new List<string>());
        }

        [Test]
        public void TheSectionsAreTheCountsTheScreenWillBuildFor()
        {
            Assert.AreEqual(4, ShopStock.GearCount);
            Assert.AreEqual(3, ShopStock.BookCount);
            Assert.AreEqual(3, ShopStock.RelicCount);
            Assert.AreEqual(3, ShopStock.SectionCount);

            // One order everywhere: gear, books, relics. The section index is
            // also the index into RunSnapshot.shopRerollsUsed, so a second
            // order would be a second thing to keep in step.
            Assert.AreEqual(0, ShopStock.GearSection);
            Assert.AreEqual(1, ShopStock.BookSection);
            Assert.AreEqual(2, ShopStock.RelicSection);

            Assert.AreEqual(ShopStock.GearCount, ShopStock.CountFor(ShopStock.GearSection));
            Assert.AreEqual(ShopStock.BookCount, ShopStock.CountFor(ShopStock.BookSection));
            Assert.AreEqual(ShopStock.RelicCount, ShopStock.CountFor(ShopStock.RelicSection));
        }

        [Test]
        public void AGearShelfIsAlwaysFullWidthAndIndexedInOrder()
        {
            var shelf = ShopStock.RollGear(Pool(30), depthStep: 8, maxTier: 10, Hone(0), Stream(1));

            Assert.AreEqual(ShopStock.GearCount, shelf.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, ShopStock.GearCount), shelf.Select(e => e.index));
            Assert.IsTrue(shelf.All(e => e.section == ShopStock.GearSection));
            Assert.IsTrue(shelf.All(e => e.kind == ShopEntryKind.Gear));
        }

        [Test]
        public void NoCardIsOfferedTwiceInOneSection()
        {
            for (ulong seed = 0; seed < 40; seed++)
            {
                var gear = ShopStock.RollGear(Pool(30), 8, 10, Hone(0), Stream(seed));
                CollectionAssert.AllItemsAreUnique(gear.Select(e => e.contentId),
                    "The same card twice at the same price is not variety; it reads as a broken roll.");

                var relics = ShopStock.RollRelics(Relics(12), Stream(seed));
                CollectionAssert.AllItemsAreUnique(relics.Select(e => e.contentId));
            }
        }

        // An undersized pool renders NO OFFER IN PLACE. It never shrinks the
        // row and never re-centres it: the screen binds cards by index, and a
        // row that re-centres moves every card the player was looking at.
        [Test]
        public void AnUndersizedPoolRendersNoOfferRatherThanShrinking()
        {
            var relics = ShopStock.RollRelics(Relics(1), Stream(7));

            Assert.AreEqual(ShopStock.RelicCount, relics.Count);
            Assert.IsFalse(relics[0].noOffer);
            Assert.IsTrue(relics[1].noOffer);
            Assert.AreEqual(1, relics[1].index, "The placeholder kept the slot it was standing in.");
            Assert.AreEqual(ShopEntryKind.Relic, relics[1].kind);

            var empty = ShopStock.RollRelics(Relics(0), Stream(7));
            Assert.AreEqual(ShopStock.RelicCount, empty.Count);
            Assert.IsTrue(empty.All(e => e.noOffer));
        }

        [Test]
        public void AnEmptyGearPoolStillFillsTheRowWithPlaceholders()
        {
            var shelf = ShopStock.RollGear(new List<ItemOffer>(), 8, 10, Hone(0), Stream(3));

            Assert.AreEqual(ShopStock.GearCount, shelf.Count);
            Assert.IsTrue(shelf.All(e => e.noOffer && e.section == ShopStock.GearSection));
        }

        // With no candidates (an empty pool, or every book already known by
        // every fielded character) the shelf is still BookCount wide, all
        // NO OFFER -- the count constant, the section index and the screen's
        // binding do not shrink just because nothing is offerable.
        [Test]
        public void AnEmptyBookPoolIsAllNoOffer()
        {
            var books = ShopStock.RollBooks(new List<ShopStock.BookCandidate>(), Stream(11));

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            Assert.IsTrue(books.All(e => e.noOffer && e.kind == ShopEntryKind.Book));
            Assert.IsTrue(books.All(e => e.section == ShopStock.BookSection));
        }

        // A null pool consumes nothing from its stream, same posture as an
        // empty one -- there is no draw to make against zero candidates.
        [Test]
        public void ANullBookPoolConsumesNothingFromItsStream()
        {
            int draws = 0;
            ShopStock.RollBooks(null, bound => { draws++; return 0; });

            Assert.AreEqual(0, draws);
        }

        [Test]
        public void BooksAreDrawnWithoutReplacementAndPricedByTier()
        {
            var candidates = new List<ShopStock.BookCandidate>
            {
                new ShopStock.BookCandidate("mud_burst", 1),
                new ShopStock.BookCandidate("static_fleece", 2),
                new ShopStock.BookCandidate("lightning_bolt", 3),
                new ShopStock.BookCandidate("golden_fleece", 4),
            };

            var books = ShopStock.RollBooks(candidates, Stream(13));

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            var offered = books.Where(e => !e.noOffer).ToList();
            Assert.AreEqual(ShopStock.BookCount, offered.Count, "four candidates for three slots, none should pad");
            CollectionAssert.AllItemsAreUnique(offered.Select(e => e.contentId).ToList());

            foreach (var entry in offered)
            {
                var source = candidates.First(c => c.SkillId == entry.contentId);
                Assert.AreEqual(ShopPricing.BookPrice(source.BookTier), entry.price);
            }
        }

        // Fewer candidates than BookCount pads with NO OFFER rather than
        // shrinking the row (§2d) -- same rule the relic shelf's own
        // undersized-pool test pins.
        [Test]
        public void FewerBookCandidatesThanSlotsPadsWithNoOffer()
        {
            var candidates = new List<ShopStock.BookCandidate> { new ShopStock.BookCandidate("mud_burst", 1) };
            var books = ShopStock.RollBooks(candidates, Stream(14));

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            Assert.AreEqual(1, books.Count(e => !e.noOffer));
            Assert.AreEqual(ShopStock.BookCount - 1, books.Count(e => e.noOffer));
        }

        [Test]
        public void AGearCardIsPricedByTierPlusAndRift()
        {
            var shelf = ShopStock.RollGear(Pool(30, tier: 2), 8, 10, Hone(2, RiftTier.RiftTouched), Stream(5));

            foreach (var entry in shelf)
            {
                // Pinned against §2c's literal, not against a recomputed
                // formula: a tier-2, +2, one-affix piece is 71 gold.
                Assert.AreEqual(71, entry.price);
                Assert.AreEqual(2, entry.plus);
                Assert.AreEqual((int)RiftTier.RiftTouched, entry.riftTier);
            }
        }

        [Test]
        public void ModifiersAreOrdinalSortedOnTheCard()
        {
            var entry = ShopStockEntry.Gear(0, "sword", 1,
                new[] { "zeal", "aegis", "Brutal", "" }, 2, 100);

            CollectionAssert.AreEqual(new[] { "Brutal", "aegis", "zeal" }, entry.modifiers,
                "Ordinal, so two cards that rolled the same affixes in a different order read as the same card.");
        }

        [Test]
        public void ARelicCardIsPricedByItsRarity()
        {
            var shelf = ShopStock.RollRelics(Relics(6, RelicRarity.Rare), Stream(9));

            Assert.IsTrue(shelf.All(e => e.price == 190));
        }

        // ASSUMPTION 11. A shelf whose cheapest card costs more than a normal
        // fight pays is a room that charged a fight and returned a reroll
        // button.
        //
        // The roll here hands back an expensive copy the first several times
        // and a cheap one afterwards, which is exactly the shape the bounded
        // re-draw is for.
        [Test]
        public void TheCheapestGearCardIsRedrawnUntilItIsAffordable()
        {
            int calls = 0;
            Func<ItemOffer, ItemOffer> stubborn = offer =>
            {
                calls++;
                return calls <= ShopStock.GearCount + 2
                    ? offer.WithPlus(4).WithModifiers(RiftTier.RiftForged, new List<string>())
                    : offer.WithPlus(0).WithModifiers(RiftTier.Ordinary, new List<string>());
            };

            var shelf = ShopStock.RollGear(Pool(30), 8, 10, stubborn, Stream(2));

            Assert.LessOrEqual(shelf.Min(e => e.price), ShopPricing.NormalFightPayoutAnchor,
                "Nothing on this shelf could be bought with a normal fight's payout.");
        }

        // Bounded, because the guarantee is worth having and a loop that
        // cannot terminate is not: at a depth where nothing prices under the
        // anchor, the honest answer is an expensive shelf.
        [Test]
        public void TheRedrawIsBoundedWhenNothingCanEverBeCheapEnough()
        {
            int calls = 0;
            Func<ItemOffer, ItemOffer> alwaysExpensive = offer =>
            {
                calls++;
                return offer.WithPlus(5).WithModifiers(RiftTier.Convergent, new List<string>());
            };

            var shelf = ShopStock.RollGear(Pool(30, tier: 5), 80, 10, alwaysExpensive, Stream(4));

            Assert.AreEqual(ShopStock.GearCount, shelf.Count);
            Assert.LessOrEqual(calls, ShopStock.GearCount + ShopStock.AffordabilityRedraws);
            Assert.Greater(shelf.Min(e => e.price), ShopPricing.NormalFightPayoutAnchor);
        }

        // A re-draw can only improve the shelf. Keeping a worse copy would
        // make the floor actively harmful on the rolls where it fires and
        // fails.
        [Test]
        public void ARedrawNeverMakesTheCheapestCardMoreExpensive()
        {
            int calls = 0;
            Func<ItemOffer, ItemOffer> worseEachTime = offer =>
            {
                calls++;
                int plus = Math.Min(5, calls);
                return offer.WithPlus(plus).WithModifiers(RiftTier.Convergent, new List<string>());
            };

            var shelf = ShopStock.RollGear(Pool(30, tier: 4), 64, 10, worseEachTime, Stream(6));

            // The first pass's cheapest was +1; nothing the re-draw produced
            // afterwards was cheaper, so it must still be on the shelf.
            Assert.AreEqual(ShopPricing.GearPrice(4, 1, (int)RiftTier.Convergent), shelf.Min(e => e.price));
        }

        // Same position, same shelf, forever -- otherwise quitting inside a
        // shop is a free reroll.
        [Test]
        public void TheSamePositionRollsTheSameShelf()
        {
            List<ShopStockEntry> Roll() =>
                ShopStock.RollGear(Pool(30), 24, 10, Hone(1), Stream(RngStreams.Derive(77UL, RngStreams.ShopGear, 24, 3)));

            CollectionAssert.AreEqual(
                Roll().Select(e => e.contentId + ":" + e.price).ToList(),
                Roll().Select(e => e.contentId + ":" + e.price).ToList());
        }

        // A paid reroll moves one coordinate and the shelf has to move with
        // it. Over a spread of nodes rather than one, so a single unlucky
        // collision cannot pass this by accident.
        [Test]
        public void ARerollChangesTheShelf()
        {
            int changed = 0;
            for (int node = 0; node < 20; node++)
            {
                string Shelf(int rerolls) => string.Join(",", ShopStock
                    .RollGear(Pool(30), 24, 10, Hone(1),
                        Stream(RngStreams.Derive(77UL, RngStreams.ShopGear, 24, node, rerolls)))
                    .Select(e => e.contentId));

                if (Shelf(0) != Shelf(1)) changed++;
            }

            Assert.GreaterOrEqual(changed, 18,
                "A paid reroll returned the same shelf too often to be a reroll.");
        }

        [Test]
        public void RollSectionDispatchesToTheRightShelf()
        {
            var books6 = new List<ShopStock.BookCandidate> { new ShopStock.BookCandidate("mud_burst", 1) };
            var gear = ShopStock.RollSection(ShopStock.GearSection, Pool(30), 8, 10, Hone(0), Relics(6), books6, Stream(1));
            var books = ShopStock.RollSection(ShopStock.BookSection, Pool(30), 8, 10, Hone(0), Relics(6), books6, Stream(1));
            var relics = ShopStock.RollSection(ShopStock.RelicSection, Pool(30), 8, 10, Hone(0), Relics(6), books6, Stream(1));

            Assert.IsTrue(gear.All(e => e.kind == ShopEntryKind.Gear));
            Assert.IsTrue(books.All(e => e.kind == ShopEntryKind.Book));
            Assert.IsTrue(relics.All(e => e.kind == ShopEntryKind.Relic));
        }
    }
}
