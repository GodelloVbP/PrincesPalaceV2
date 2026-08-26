using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // Putting gear on and taking it off.
    //
    // The rule under test everywhere below is that equipping is a MOVE, not a
    // copy: the item leaves the bag, whatever it displaced comes back, and both
    // halves happen or neither does. An item count that stays right while an
    // item silently changes is the failure mode these exist to catch.
    public class EquipmentMoveTests
    {
        private const EquipmentSlot Torso = EquipmentSlot.Torso;
        private const EquipmentSlot Weapon1 = EquipmentSlot.Weapon1;
        private const EquipmentSlot Weapon2 = EquipmentSlot.Weapon2;

        private static List<InventoryEntry> Bag(params InventoryEntry[] entries) =>
            new List<InventoryEntry>(entries);

        private static InventoryEntry Entry(string id, int count, int plus = 0) =>
            new InventoryEntry(id, count, plus);

        private static int TotalHeld(List<InventoryEntry> bag, EquipmentLoadout worn, string id) =>
            InventoryOps.Count(bag, id) + worn.EquippedItemIds().Count(worn_id => worn_id == id);

        // ---- equipping is a move ------------------------------------------------

        [Test]
        public void EquippingTakesTheItemOutOfTheBag()
        {
            var bag = Bag(Entry("cuirass", 1));
            var worn = new EquipmentLoadout();

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, "cuirass", Torso, isEquippable: true));

            Assert.AreEqual("cuirass", worn.Get(Torso));
            CollectionAssert.IsEmpty(bag, "it was moved, not copied");
        }

        [Test]
        public void UnequippingPutsItBack()
        {
            var bag = Bag();
            var worn = new EquipmentLoadout();
            worn.Set(Torso, "cuirass", 2);

            Assert.IsTrue(EquipMove.TryUnequip(worn, bag, Torso));

            Assert.AreEqual("", worn.Get(Torso));
            Assert.AreEqual(1, InventoryOps.CountAt(bag, "cuirass", 2), "and at the plus it was worn at");
        }

        [Test]
        public void ASwapConservesEveryItem()
        {
            // Nothing is created and nothing is destroyed -- the count of each
            // id across bag AND body is the same before and after.
            var bag = Bag(Entry("plate", 1));
            var worn = new EquipmentLoadout();
            worn.Set(Torso, "cuirass");

            EquipMove.TryEquip(worn, bag, "plate", Torso, isEquippable: true);

            Assert.AreEqual(1, TotalHeld(bag, worn, "plate"));
            Assert.AreEqual(1, TotalHeld(bag, worn, "cuirass"));
        }

        // ---- the recorded item-destroying bug ------------------------------------

        [Test]
        public void ADisplacedPlusFiveComesBackAsAPlusFive()
        {
            // THE bug this file exists for. Reading only the displaced ID and
            // re-adding it at the default plus turns a +5 into a +0 -- and the
            // item count stays correct, so no count-based assertion anywhere
            // would ever notice.
            var bag = Bag(Entry("plate", 1));
            var worn = new EquipmentLoadout();
            worn.Set(Torso, "cuirass", 5);

            EquipMove.TryEquip(worn, bag, "plate", Torso, isEquippable: true);

            Assert.AreEqual(1, InventoryOps.CountAt(bag, "cuirass", 5), "the honing survived the swap");
            Assert.AreEqual(0, InventoryOps.CountAt(bag, "cuirass", 0), "it did not come back plain");
        }

        // ---- rolled modifiers survive the same moves plus already does ---------

        [Test]
        public void ADisplacedRollComesBackWithItsModifiersAndRiftTierIntact()
        {
            // Same bug class as ADisplacedPlusFiveComesBackAsAPlusFive, for
            // the two fields this phase adds: reading only the id (or only
            // the id and plus) and re-adding at the defaults would silently
            // strip a Convergent item's affixes the moment something else
            // gets equipped into its slot.
            var bag = Bag(Entry("plate", 1));
            var worn = new EquipmentLoadout();
            worn.Set(Torso, "cuirass", 5, modifierIds: new List<string> { "fiery", "swift" }, riftTier: 2);

            EquipMove.TryEquip(worn, bag, "plate", Torso, isEquippable: true);

            var displacedEntry = bag.First(e => e.itemId == "cuirass");
            CollectionAssert.AreEquivalent(new[] { "fiery", "swift" }, displacedEntry.modifierIds);
            Assert.AreEqual(2, displacedEntry.riftTier);
            Assert.AreEqual(5, displacedEntry.plus);
        }

        [Test]
        public void EquipUnequipReequip_PreservesModifierIdsAndRiftTierWithNoDuplication()
        {
            var bag = Bag(Entry("sword", 1));
            bag[0].modifierIds = new List<string> { "astral" };
            bag[0].riftTier = 3;
            var worn = new EquipmentLoadout();

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, "sword", Weapon1, isEquippable: true,
                modifierIds: new List<string> { "astral" }, riftTier: 3));
            Assert.IsTrue(EquipMove.TryUnequip(worn, bag, Weapon1));

            Assert.AreEqual(1, bag.Count, "no duplication across the round trip");
            CollectionAssert.AreEquivalent(new[] { "astral" }, bag[0].modifierIds);
            Assert.AreEqual(3, bag[0].riftTier);
            Assert.AreEqual(0, bag[0].plus);

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, "sword", Weapon1, isEquippable: true,
                modifierIds: bag[0].modifierIds, riftTier: bag[0].riftTier));

            CollectionAssert.AreEquivalent(new[] { "astral" }, worn.GetModifierIds(Weapon1));
            Assert.AreEqual(3, worn.GetRiftTier(Weapon1));
        }

        [Test]
        public void EquippingASpecificCopyWearsThatCopy()
        {
            // The bag holds the same id at two plus levels; the caller says
            // which row was clicked.
            var bag = Bag(Entry("sword", 1, 0), Entry("sword", 1, 7));
            var worn = new EquipmentLoadout();

            EquipMove.TryEquip(worn, bag, "sword", Weapon1, isEquippable: true, plus: 7);

            Assert.AreEqual(7, worn.GetPlus(Weapon1));
            Assert.AreEqual(1, InventoryOps.CountAt(bag, "sword", 0), "the plain one is untouched");
            Assert.AreEqual(0, InventoryOps.CountAt(bag, "sword", 7));
        }

        // ---- refusals change nothing ---------------------------------------------

        [Test]
        public void AnItemNotInTheBagIsRefused()
        {
            var bag = Bag();
            var worn = new EquipmentLoadout();

            Assert.IsFalse(EquipMove.TryEquip(worn, bag, "cuirass", Torso, isEquippable: true));

            CollectionAssert.IsEmpty(worn.EquippedItemIds(), "nothing was worn on the strength of an item nobody owns");
        }

        [Test]
        public void SomethingUnwearableIsRefusedWithoutConsumingIt()
        {
            // A potion clicked in the bag must still be there afterwards.
            var bag = Bag(Entry("potion", 3));
            var worn = new EquipmentLoadout();

            Assert.IsFalse(EquipMove.TryEquip(worn, bag, "potion", Torso, isEquippable: false));

            Assert.AreEqual(3, InventoryOps.Count(bag, "potion"));
            CollectionAssert.IsEmpty(worn.EquippedItemIds());
        }

        [Test]
        public void ASlotThatCannotTakeTheItemIsRefusedBeforeAnythingMoves()
        {
            var bag = Bag(Entry("cuirass", 1));
            var worn = new EquipmentLoadout();

            Assert.IsFalse(EquipMove.TryEquip(worn, bag, "cuirass", Torso, isEquippable: true,
                preferredSlot: EquipmentSlot.Head));

            Assert.AreEqual(1, InventoryOps.Count(bag, "cuirass"), "still in the bag");
            CollectionAssert.IsEmpty(worn.EquippedItemIds());
        }

        [Test]
        public void UnequippingAnEmptySlotIsRefused()
        {
            var bag = Bag();

            Assert.IsFalse(EquipMove.TryUnequip(new EquipmentLoadout(), bag, Torso));

            CollectionAssert.IsEmpty(bag, "nothing was conjured into the bag");
        }

        // ---- the weapon rules ------------------------------------------------------

        [Test]
        public void TwoWeaponsFillBothHands()
        {
            var bag = Bag(Entry("sword", 1), Entry("dagger", 1));
            var worn = new EquipmentLoadout();

            EquipMove.TryEquip(worn, bag, "sword", Weapon1, isEquippable: true);
            EquipMove.TryEquip(worn, bag, "dagger", Weapon1, isEquippable: true);

            Assert.AreEqual("sword", worn.Get(Weapon1));
            Assert.AreEqual("dagger", worn.Get(Weapon2), "a Weapon1 item also fits the off hand");
        }

        [Test]
        public void AThirdWeaponReplacesTheMainHand()
        {
            // Both hands full, so ResolveTargetSlot picks the first accepting
            // slot rather than doing nothing -- a click that visibly does
            // nothing reads as a broken button.
            var bag = Bag(Entry("axe", 1));
            var worn = new EquipmentLoadout();
            worn.Set(Weapon1, "sword");
            worn.Set(Weapon2, "dagger");

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, "axe", Weapon1, isEquippable: true));

            Assert.AreEqual("axe", worn.Get(Weapon1));
            Assert.AreEqual(1, InventoryOps.Count(bag, "sword"), "the displaced main hand came back");
        }

        [Test]
        public void ANullBagOrLoadoutIsSurvivable()
        {
            Assert.IsFalse(EquipMove.TryEquip(null, Bag(), "cuirass", Torso, true));
            Assert.IsFalse(EquipMove.TryEquip(new EquipmentLoadout(), null, "cuirass", Torso, true));
            Assert.IsFalse(EquipMove.TryUnequip(null, Bag(), Torso));
        }
    }
}
