using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // ItemInstance.SameStack is the one merge predicate (docs/PLAN_EVENTS_BELL_
    // AND_CARAVAN.md 3.3). Ordinary copies stack exactly as InventoryOps always
    // stacked them; a copy with a lot is only ever its own stack, so a fake can
    // never be picked out as "the one that did not merge", and removing or
    // equipping one copy can never spend another.
    public class ItemInstanceTests
    {
        private const EquipmentSlot Torso = EquipmentSlot.Torso;

        private static ItemInstance Plain(string id, int tier = 0) => ItemInstance.Of(id, tier);

        private static ItemInstance Lot(string id, string lot, bool fake = false, int fightsLeft = 0) =>
            ItemInstance.Of(id, provenance: new Provenance(lot, fake, fightsLeft));

        // ---- the predicate ----------------------------------------------------------

        [Test]
        public void OrdinaryCopies_StackOnTheModifierSet_NotItsOrder()
        {
            var a = ItemInstance.Of("coif", 3, new List<string> { "fiery", "swift" });
            var b = ItemInstance.Of("coif", 3, new List<string> { "swift", "fiery" });

            Assert.IsTrue(ItemInstance.SameStack(a, b));
        }

        [Test]
        public void OrdinaryCopies_AtADifferentTier_AreDifferentStacks()
        {
            Assert.IsFalse(ItemInstance.SameStack(Plain("coif", 3), Plain("coif", 5)));
        }

        [Test]
        public void ALotCopy_IsNeverTheSameStackAsAnOrdinaryCopy()
        {
            Assert.IsFalse(ItemInstance.SameStack(Lot("potion", "lot-1"), Plain("potion")));
            Assert.IsFalse(ItemInstance.SameStack(Plain("potion"), Lot("potion", "lot-1")));
        }

        [Test]
        public void TwoLots_OfTheSameItem_AreDifferentStacks()
        {
            Assert.IsFalse(ItemInstance.SameStack(Lot("potion", "lot-1"), Lot("potion", "lot-2")));
        }

        [Test]
        public void ALot_IsTheSameStackAsItself()
        {
            Assert.IsTrue(ItemInstance.SameStack(Lot("potion", "lot-1", fake: true), Lot("potion", "lot-1", fake: true)));
        }

        // ---- the bag ------------------------------------------------------------------

        [Test]
        public void LotCopies_NeverMerge_WithEachOtherOrWithOrdinaryCopies()
        {
            var bag = new List<InventoryEntry>();

            InventoryOps.Add(bag, Plain("potion"), 2);
            InventoryOps.Add(bag, Lot("potion", "lot-1"));
            InventoryOps.Add(bag, Lot("potion", "lot-2", fake: true));
            InventoryOps.Add(bag, Plain("potion"));

            Assert.AreEqual(3, bag.Count);
            Assert.AreEqual(3, bag[0].count);
            Assert.AreEqual(1, bag[1].count);
            Assert.AreEqual(1, bag[2].count);
            Assert.AreEqual(5, InventoryOps.Count(bag, "potion"));
        }

        [Test]
        public void RemovingAGenuineCopy_NeverSpendsTheFake()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Lot("potion", "lot-1", fake: true));
            InventoryOps.Add(bag, Lot("potion", "lot-2"));

            Assert.IsTrue(InventoryOps.TryRemoveAt(bag, Lot("potion", "lot-2")));

            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual("lot-1", bag[0].provenance.lot);
            Assert.IsTrue(bag[0].provenance.fake);
        }

        [Test]
        public void RemovingAnOrdinaryCopy_NeverSpendsALot()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Lot("potion", "lot-1", fake: true));

            Assert.IsFalse(InventoryOps.TryRemoveAt(bag, Plain("potion")));
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void AnEntry_ReadsBackTheInstanceItWasBuiltFrom()
        {
            var entry = new InventoryEntry(Lot("cuirass", "lot-7", fake: true, fightsLeft: 3), 1);

            var back = entry.Instance;

            Assert.AreEqual("cuirass", back.ItemId);
            Assert.AreEqual("lot-7", back.Lot);
            Assert.IsTrue(back.IsFake);
            Assert.AreEqual(3, back.FightsLeft);
        }

        [Test]
        public void AnInstance_DoesNotChange_WhenTheEntryItCameFromDoes()
        {
            var entry = new InventoryEntry(Lot("cuirass", "lot-7", fake: true, fightsLeft: 3), 1);
            var read = entry.Instance;

            entry.provenance.fightsLeft = 1;

            Assert.AreEqual(3, read.FightsLeft);
        }

        // ---- the body -----------------------------------------------------------------

        [Test]
        public void Equipping_ALotCopy_CarriesItsProvenanceOntoTheSlot()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Lot("cuirass", "lot-7", fake: true, fightsLeft: 2));
            var worn = new EquipmentLoadout();

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, Lot("cuirass", "lot-7"), Torso, isEquippable: true));

            var on = worn.GetInstance(Torso);
            Assert.AreEqual("lot-7", on.Lot);
            Assert.IsTrue(on.IsFake);
            Assert.AreEqual(2, on.FightsLeft);
            Assert.AreEqual(0, bag.Count);
        }

        [Test]
        public void Unequipping_ALotCopy_PutsItBackAsItsOwnStack()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Plain("cuirass"));
            InventoryOps.Add(bag, Lot("cuirass", "lot-7", fake: true, fightsLeft: 2));
            var worn = new EquipmentLoadout();
            Assert.IsTrue(EquipMove.TryEquip(worn, bag, Lot("cuirass", "lot-7"), Torso, isEquippable: true));

            Assert.IsTrue(EquipMove.TryUnequip(worn, bag, Torso));

            Assert.AreEqual(2, bag.Count);
            Assert.AreEqual("", bag[0].provenance.lot);
            Assert.AreEqual("lot-7", bag[1].provenance.lot);
            Assert.IsTrue(bag[1].provenance.fake);
            Assert.AreEqual(2, bag[1].provenance.fightsLeft);
        }

        [Test]
        public void ALotCopy_DisplacedByAnEquip_ComesBackWithItsProvenance()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Lot("cuirass", "lot-7", fake: true, fightsLeft: 1));
            InventoryOps.Add(bag, Plain("brigandine"));
            var worn = new EquipmentLoadout();
            Assert.IsTrue(EquipMove.TryEquip(worn, bag, Lot("cuirass", "lot-7"), Torso, isEquippable: true));

            Assert.IsTrue(EquipMove.TryEquip(worn, bag, Plain("brigandine"), Torso, isEquippable: true));

            Assert.AreEqual("brigandine", worn.Get(Torso));
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual("lot-7", bag[0].provenance.lot);
            Assert.AreEqual(1, bag[0].provenance.fightsLeft);
        }

        [Test]
        public void EquippingAPlainCopy_DoesNotFindALotCopyOfTheSameItem()
        {
            var bag = new List<InventoryEntry>();
            InventoryOps.Add(bag, Lot("cuirass", "lot-7", fake: true));
            var worn = new EquipmentLoadout();

            Assert.IsFalse(EquipMove.TryEquip(worn, bag, Plain("cuirass"), Torso, isEquippable: true));

            Assert.AreEqual("", worn.Get(Torso));
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void Clone_KeepsEachSlotsProvenance()
        {
            var worn = new EquipmentLoadout();
            worn.Put(Torso, Lot("cuirass", "lot-7", fake: true, fightsLeft: 2));

            var copy = worn.Clone();
            worn.slots[0].provenance.fightsLeft = 0;

            Assert.AreEqual(2, copy.GetInstance(Torso).FightsLeft);
            Assert.AreEqual("lot-7", copy.GetInstance(Torso).Lot);
        }

        // ---- the copy model (docs/PLAN_ITEMS.md 2.3) ----------------------

        [TestCase(-2, 0)]
        [TestCase(7, 7)]
        [TestCase(10, 10)]
        [TestCase(11, 10)]
        [TestCase(12, 10)]
        [TestCase(15, 10)]
        public void Of_ClampsTierToTheCap(int asked, int stored)
        {
            Assert.AreEqual(stored, ItemInstance.Of("brigandine_boots", asked).Tier);
            Assert.AreEqual(10, ItemInstance.TierCap);
        }

        [Test]
        public void CopiesOfOneBase_AtDifferentTiers_AreDifferentStacks()
        {
            Assert.IsTrue(ItemInstance.SameStack(ItemInstance.Of("sword", 4), ItemInstance.Of("sword", 4)));
            Assert.IsFalse(ItemInstance.SameStack(ItemInstance.Of("sword", 4), ItemInstance.Of("sword", 5)));
        }

        [Test]
        public void TierRoundTrips_ThroughEveryEntryThatHoldsACopy()
        {
            var copy = ItemInstance.Of("sword", 9, new[] { "keen" });

            Assert.AreEqual(9, new InventoryEntry(copy, 1).Instance.Tier);
            Assert.AreEqual(9, new EquipmentSlotEntry(EquipmentSlot.Weapon1, copy).Instance.Tier);

            var loadout = new EquipmentLoadout();
            loadout.Put(EquipmentSlot.Weapon1, copy);
            Assert.AreEqual(9, loadout.Clone().GetInstance(EquipmentSlot.Weapon1).Tier);
            Assert.AreEqual(9, copy.WithProvenance(new Provenance("lot-1")).Tier);

            var card = new PrincesPalace.Domain.Rewards.ShopStockEntry { kind = PrincesPalace.Domain.Rewards.ShopEntryKind.Gear, contentId = "sword", tier = 9 };
            Assert.AreEqual(9, card.GearInstance().Tier);
        }
    }
}
