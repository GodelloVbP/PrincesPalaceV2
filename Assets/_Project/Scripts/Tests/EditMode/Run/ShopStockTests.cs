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
            var shelf = ShopStock.RollGear(Pool(30), depthStep: 8, maxTier: 10, Stream(1));

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
                var gear = ShopStock.RollGear(Pool(30), 8, 10, Stream(seed));
                CollectionAssert.AllItemsAreUnique(gear.Select(e => e.contentId),
                    "The same card twice at the same price is not variety; it reads as a broken roll.");

                var relics = ShopStock.RollRelics(Relics(12), 0, Stream(seed));
                CollectionAssert.AllItemsAreUnique(relics.Select(e => e.contentId));
            }
        }

        // An undersized pool renders NO OFFER IN PLACE. It never shrinks the
        // row and never re-centres it: the screen binds cards by index, and a
        // row that re-centres moves every card the player was looking at.
        [Test]
        public void AnUndersizedPoolRendersNoOfferRatherThanShrinking()
        {
            var relics = ShopStock.RollRelics(Relics(1), 0, Stream(7));

            Assert.AreEqual(ShopStock.RelicCount, relics.Count);
            Assert.IsFalse(relics[0].noOffer);
            Assert.IsTrue(relics[1].noOffer);
            Assert.AreEqual(1, relics[1].index, "The placeholder kept the slot it was standing in.");
            Assert.AreEqual(ShopEntryKind.Relic, relics[1].kind);

            var empty = ShopStock.RollRelics(Relics(0), 0, Stream(7));
            Assert.AreEqual(ShopStock.RelicCount, empty.Count);
            Assert.IsTrue(empty.All(e => e.noOffer));
        }

        [Test]
        public void AnEmptyGearPoolStillFillsTheRowWithPlaceholders()
        {
            var shelf = ShopStock.RollGear(new List<ItemOffer>(), 8, 10, Stream(3));

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
            var books = ShopStock.RollBooks(new List<ShopStock.BookCandidate>(), 0, Stream(11));

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
            ShopStock.RollBooks(null, 0, bound => { draws++; return 0; });

            Assert.AreEqual(0, draws);
        }

        [Test]
        public void BooksAreDrawnWithoutReplacementAndPricedByTier()
        {
            // Four real book ids (skills.json's bookOnly rows), not that this
            // test reads the catalogue at all -- RollBooks only sees the
            // candidate list below. static_fleece/golden_fleece are gone
            // (AUDIT #150); frost_flare/cinderfault stand in.
            var candidates = new List<ShopStock.BookCandidate>
            {
                new ShopStock.BookCandidate("mud_burst", 1),
                new ShopStock.BookCandidate("frost_flare", 2),
                new ShopStock.BookCandidate("lightning_bolt", 3),
                new ShopStock.BookCandidate("cinderfault", 4),
            };

            var books = ShopStock.RollBooks(candidates, 0, Stream(13));

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            var offered = books.Where(e => !e.noOffer).ToList();
            Assert.AreEqual(ShopStock.BookCount, offered.Count, "four candidates for three slots, none should pad");
            CollectionAssert.AllItemsAreUnique(offered.Select(e => e.contentId).ToList());

            foreach (var entry in offered)
            {
                var source = candidates.First(c => c.SkillId == entry.contentId);
                Assert.AreEqual(ShopPricing.BookAuthored(source.BookTier), entry.price);
            }
        }

        // Fewer candidates than BookCount pads with NO OFFER rather than
        // shrinking the row (§2d) -- same rule the relic shelf's own
        // undersized-pool test pins.
        [Test]
        public void FewerBookCandidatesThanSlotsPadsWithNoOffer()
        {
            var candidates = new List<ShopStock.BookCandidate> { new ShopStock.BookCandidate("mud_burst", 1) };
            var books = ShopStock.RollBooks(candidates, 0, Stream(14));

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            Assert.AreEqual(1, books.Count(e => !e.noOffer));
            Assert.AreEqual(ShopStock.BookCount - 1, books.Count(e => e.noOffer));
        }

        [Test]
        public void AGearCardIsPricedByItsTier()
        {
            var shelf = ShopStock.RollGear(Pool(30, tier: 2), 0, 10, Stream(5));

            foreach (var entry in shelf)
            {
                // Pinned against ShopPricingTests' own literal (GearBase/
                // GearPerTier 21/5), not against a recomputed formula: a
                // tier-2 piece with no affix is 31 gold at step 0. The
                // candidate pool is all tier 2, so the tier-boosted TARGET
                // this rolls against cannot move which tier actually prices
                // here.
                Assert.AreEqual(31, entry.price);
                Assert.AreEqual(2, entry.tier, "the card carries the tier of the copy it sells");
                Assert.IsEmpty(entry.modifiers);
            }
        }

        // "GEAR ROLLS ONE TIER ABOVE THE MAP'S FLOOR TIER". A pool spanning
        // tiers 0-4 at a depth whose floor tier is 0 (depthStep 8) draws at
        // tier 1 exactly: the target is the floor tier plus the boost, and
        // there is no spread around it.
        [Test]
        public void GearTargetsOneTierAboveTheFloorTier()
        {
            var pool = Enumerable.Range(0, 5)
                .SelectMany(tier => Enumerable.Range(0, 6).Select(i => new ItemOffer($"item_{tier}_{i}", tier)))
                .ToList();

            for (ulong seed = 0; seed < 20; seed++)
            {
                var shelf = ShopStock.RollGear(pool, depthStep: 8, maxTier: 10, Stream(seed));
                foreach (var entry in shelf.Where(e => !e.noOffer))
                {
                    Assert.AreEqual(1, entry.tier, $"seed {seed}: {entry.contentId}");
                }
            }
        }

        [Test]
        public void ModifiersAreOrdinalSortedOnTheCard()
        {
            var entry = ShopStockEntry.Gear(0, "sword", 1,
                new[] { "zeal", "aegis", "Brutal", "" }, 100);

            CollectionAssert.AreEqual(new[] { "Brutal", "aegis", "zeal" }, entry.modifiers,
                "Ordinal, so two cards that rolled the same affixes in a different order read as the same card.");
        }

        [Test]
        public void ARelicCardIsPricedByItsRarity()
        {
            var shelf = ShopStock.RollRelics(Relics(6, RelicRarity.Rare), 0, Stream(9));

            Assert.IsTrue(shelf.All(e => e.price == 190));
        }

        // Every price on a shelf is its step-0 amount through ScalePrice at
        // the step the shelf is rolled at (1.075 a step, floored). Literals
        // hand-derived at step 8, where the multiplier is 1.7835: a Rare
        // relic 190 -> 338, a tier-4 book 145 -> 258, a tier-1 gear card
        // 26 -> 46.
        [Test]
        public void EveryShelfPriceIsScaledByTheStepItWasRolledAt()
        {
            var relics = ShopStock.RollRelics(Relics(6, RelicRarity.Rare), 8, Stream(9));
            Assert.IsTrue(relics.All(e => e.price == 338));

            var books = ShopStock.RollBooks(
                new List<ShopStock.BookCandidate> { new ShopStock.BookCandidate("cinderfault", 4) }, 8, Stream(13));
            Assert.AreEqual(258, books.Single(e => !e.noOffer).price);

            var gear = ShopStock.RollGear(Pool(30, tier: 1), 8, 10, Stream(5));
            Assert.IsTrue(gear.All(e => e.price == 46), string.Join(",", gear.Select(e => e.price)));
        }

        // Same position, same shelf, forever -- otherwise quitting inside a
        // shop is a free reroll.
        [Test]
        public void TheSamePositionRollsTheSameShelf()
        {
            List<ShopStockEntry> Roll() =>
                ShopStock.RollGear(Pool(30), 24, 10, Stream(RngStreams.Derive(77UL, RngStreams.ShopGear, 24, 3)));

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
                    .RollGear(Pool(30), 24, 10,
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
            var gear = ShopStock.RollSection(ShopStock.GearSection, Pool(30), 8, 10, Relics(6), books6, Stream(1));
            var books = ShopStock.RollSection(ShopStock.BookSection, Pool(30), 8, 10, Relics(6), books6, Stream(1));
            var relics = ShopStock.RollSection(ShopStock.RelicSection, Pool(30), 8, 10, Relics(6), books6, Stream(1));

            Assert.IsTrue(gear.All(e => e.kind == ShopEntryKind.Gear));
            Assert.IsTrue(books.All(e => e.kind == ShopEntryKind.Book));
            Assert.IsTrue(relics.All(e => e.kind == ShopEntryKind.Relic));
        }

        // draftable: false (Kinship) never reaches the relic shelf: the shelf
        // rolls from RunOrchestrator.AvailableRelicOptions, which is
        // RelicPool.Available. A pool of one real relic plus the undraftable
        // one leaves one card and two placeholders on every seed.
        [Test]
        public void AnUndraftableRelicIsNeverOnTheRelicShelf()
        {
            var all = new[]
            {
                new RelicOption("dual_wield", RelicRarity.Common),
                new RelicOption("kinship", RelicRarity.Common, draftable: false),
            };

            for (ulong seed = 1; seed <= 50; seed++)
            {
                var shelf = ShopStock.RollRelics(RelicPool.Available(all, new HashSet<string>()), 0, Stream(seed));

                Assert.IsFalse(shelf.Any(e => e.contentId == "kinship"), $"seed {seed}");
                Assert.AreEqual(1, shelf.Count(e => !e.noOffer), $"seed {seed}");
            }
        }
    }
}
