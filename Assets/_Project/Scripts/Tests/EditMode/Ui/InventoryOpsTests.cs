using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.Domain.Tests
{
    // The bag's rules.
    //
    // FIRST COVERAGE, and the reason the files moved: InventoryOps lived in
    // Data/, which compiles into the Core assembly, and the EditMode suite may
    // only reference Domain. The code was always engine-free and always pure --
    // it was simply unreachable by any test in the project. Every rule below
    // has been shipping unverified.
    public class InventoryOpsTests
    {
        private static List<InventoryEntry> Bag(params InventoryEntry[] entries) =>
            new List<InventoryEntry>(entries);

        private static InventoryEntry Entry(string id, int count, int tier = 0) =>
            new InventoryEntry(id, count, tier: tier);

        // ---- stacking ---------------------------------------------------------

        [Test]
        public void AddingTheSameItemAgainGrowsTheStack()
        {
            var bag = Bag(Entry("potion", 2));

            InventoryOps.Add(bag, "potion", 3);

            Assert.AreEqual(1, bag.Count, "one stack, not two");
            Assert.AreEqual(5, bag[0].count);
        }

        [Test]
        public void TwoTiersAreTwoStacks()
        {
            // THE load-bearing rule. A tier-5 sword and a tier-0 sword are the
            // same item id and different objects -- merging them would silently
            // upgrade one and destroy the other.
            var bag = Bag();

            InventoryOps.Add(bag, "sword", 1, tier: 0);
            InventoryOps.Add(bag, "sword", 1, tier: 5);

            Assert.AreEqual(2, bag.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 5 }, bag.Select(e => e.tier).ToArray());
        }

        // ---- rolled modifiers (Phase A1: storage only) -------------------------

        [Test]
        public void TwoEntriesWithIdenticalRolledModifiers_MergeIntoOneStack()
        {
            // Structural-equality stacking: two drops that happened to roll
            // the EXACT same affixes at the same tier are functionally
            // identical, so merging them is correct -- the same reasoning that
            // already lets two plain swords stack.
            var bag = Bag();

            InventoryOps.Add(bag, "sword", 1, tier: 0, modifierIds: new List<string> { "fiery", "swift" });
            InventoryOps.Add(bag, "sword", 1, tier: 0, modifierIds: new List<string> { "swift", "fiery" });

            Assert.AreEqual(1, bag.Count, "same roll, order-independent -- one stack");
            Assert.AreEqual(2, bag[0].count);
        }

        [Test]
        public void ADifferentRoll_NeverMergesEvenAtTheSameTier()
        {
            var bag = Bag();

            InventoryOps.Add(bag, "sword", 1, tier: 0, modifierIds: new List<string> { "fiery" });
            InventoryOps.Add(bag, "sword", 1, tier: 0, modifierIds: new List<string> { "swift" });

            Assert.AreEqual(2, bag.Count, "different affixes -- must not merge");
        }

        [Test]
        public void APlainItem_NeverMergesWithAModifiedOneAtTheSameTier()
        {
            var bag = Bag();

            InventoryOps.Add(bag, "sword", 1, tier: 3);
            InventoryOps.Add(bag, "sword", 1, tier: 3, modifierIds: new List<string> { "fiery" });

            Assert.AreEqual(2, bag.Count, "an empty modifierIds list is its own key, not a wildcard");
        }

        [Test]
        public void CountSumsEveryTier()
        {
            // "How many swords do I own" is a different question from "how many
            // tier-3 swords", and both have callers.
            var bag = Bag(Entry("sword", 2, 0), Entry("sword", 1, 3));

            Assert.AreEqual(3, InventoryOps.Count(bag, "sword"));
            Assert.AreEqual(2, InventoryOps.CountAt(bag, "sword", 0));
            Assert.AreEqual(1, InventoryOps.CountAt(bag, "sword", 3));
            Assert.AreEqual(0, InventoryOps.CountAt(bag, "sword", 9), "a tier nobody holds");
        }

        // ---- removal ----------------------------------------------------------

        [Test]
        public void RemovingTheLastOneDropsTheEntryRatherThanLeavingAGhost()
        {
            // "Absent" and "present at zero" must not both be reachable, or
            // every reader needs to test for both and one of them will forget.
            var bag = Bag(Entry("potion", 1));

            Assert.IsTrue(InventoryOps.TryRemove(bag, "potion"));

            CollectionAssert.IsEmpty(bag);
        }

        [Test]
        public void RemovingTakesTheLowestTierHeld()
        {
            // Spend the worst copy first. The alternative -- taking whatever is
            // first in the list -- would consume a tier 5 to pay a cost a tier 0
            // could have covered.
            var bag = Bag(Entry("sword", 1, 5), Entry("sword", 1, 0), Entry("sword", 1, 3));

            Assert.IsTrue(InventoryOps.TryRemove(bag, "sword"));

            CollectionAssert.AreEquivalent(new[] { 3, 5 }, bag.Select(e => e.tier).ToArray());
        }

        [Test]
        public void RemovingAnExactCopyTakesThatOneAndNoOther()
        {
            var bag = Bag(Entry("sword", 1, 0), Entry("sword", 1, 5));

            Assert.IsTrue(InventoryOps.TryRemoveAt(bag, "sword", 5));

            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(0, bag[0].tier, "the plain one survives");
        }

        [Test]
        public void RemovingWhatIsNotThereFailsWithoutTouchingTheBag()
        {
            var bag = Bag(Entry("potion", 2));

            Assert.IsFalse(InventoryOps.TryRemove(bag, "elixir"));
            Assert.IsFalse(InventoryOps.TryRemoveAt(bag, "potion", 4), "held, but not at that tier");

            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(2, bag[0].count);
        }

        // ---- the degenerate cases a save file can actually produce -------------

        [Test]
        public void NonsenseAddsAreIgnoredRatherThanStored()
        {
            // A zero-count add would create exactly the ghost entry the removal
            // rule works to prevent.
            var bag = Bag();

            InventoryOps.Add(bag, "potion", 0);
            InventoryOps.Add(bag, "potion", -3);
            InventoryOps.Add(bag, "", 1);
            InventoryOps.Add(bag, null, 1);

            CollectionAssert.IsEmpty(bag);
        }

        [Test]
        public void ANullBagIsSurvivable()
        {
            // Reachable from a hand-edited or pre-migration save.
            Assert.DoesNotThrow(() => InventoryOps.Add(null, "potion"));
            Assert.AreEqual(0, InventoryOps.Count(null, "potion"));
            Assert.IsFalse(InventoryOps.TryRemove(null, "potion"));
        }

        [Test]
        public void AddAndRemoveRoundTripToNothing()
        {
            // The invariant every caller leans on: the bag is a multiset and
            // these two are inverses.
            var bag = Bag();

            InventoryOps.Add(bag, "potion", 3, tier: 2);
            for (int i = 0; i < 3; i++) Assert.IsTrue(InventoryOps.TryRemoveAt(bag, "potion", 2));

            CollectionAssert.IsEmpty(bag);
            Assert.IsFalse(InventoryOps.TryRemoveAt(bag, "potion", 2), "and it stays empty");
        }
    }
}
