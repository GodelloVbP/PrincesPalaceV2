using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // The paperdoll's own rules, with no scene, no ScriptableObject and no
    // running game — which is the whole reason EquipmentLoadout lives in
    // Domain and only ever moves ids around.
    public class EquipmentLoadoutTests
    {
        [Test]
        public void AFreshLoadout_HasEverySlotEmpty()
        {
            var loadout = new EquipmentLoadout();

            foreach (var slot in EquipmentSlots.All)
            {
                Assert.IsTrue(loadout.IsEmpty(slot), $"{slot} should start empty");
                Assert.AreEqual("", loadout.Get(slot), $"{slot} should read as empty string, never null");
            }
        }

        [Test]
        public void Set_ThenGet_ReturnsTheItem()
        {
            var loadout = new EquipmentLoadout();

            string displaced = loadout.Set(EquipmentSlot.Gloves, "gloves_of_strength");

            Assert.AreEqual("", displaced, "Filling an empty slot displaces nothing");
            Assert.AreEqual("gloves_of_strength", loadout.Get(EquipmentSlot.Gloves));
            Assert.IsFalse(loadout.IsEmpty(EquipmentSlot.Gloves));
        }

        // The invariant that stops an equip from destroying an item: what
        // was in the slot has to come back out, or the caller has nothing to
        // put back in the bag.
        [Test]
        public void Set_OverAFullSlot_ReturnsWhatItReplaced()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Head, "iron_helm");

            string displaced = loadout.Set(EquipmentSlot.Head, "crown");

            Assert.AreEqual("iron_helm", displaced);
            Assert.AreEqual("crown", loadout.Get(EquipmentSlot.Head));
        }

        [Test]
        public void Clear_EmptiesTheSlotAndReturnsWhatWasThere()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Torso, "leather_cuirass");

            Assert.AreEqual("leather_cuirass", loadout.Clear(EquipmentSlot.Torso));
            Assert.IsTrue(loadout.IsEmpty(EquipmentSlot.Torso));
            Assert.AreEqual("", loadout.Clear(EquipmentSlot.Torso), "Clearing an already-empty slot is a no-op");
        }

        // "Empty" must have exactly one representation, or a slot can be
        // simultaneously present-but-blank and absent, and IsEmpty and the
        // serialized list would disagree.
        [Test]
        public void SettingAnEmptyId_RemovesTheEntryRatherThanLeavingATombstone()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Legs, "padded_greaves");

            loadout.Set(EquipmentSlot.Legs, "");

            Assert.IsTrue(loadout.IsEmpty(EquipmentSlot.Legs));
            Assert.AreEqual(0, loadout.slots.Count, "The backing entry should be gone, not blanked");
        }

        [Test]
        public void AWeapon_FirstFillsWeapon1_ThenWeapon2()
        {
            var loadout = new EquipmentLoadout();

            Assert.AreEqual(EquipmentSlot.Weapon1, loadout.ResolveTargetSlot(EquipmentSlot.Weapon1));
            loadout.Set(EquipmentSlot.Weapon1, "iron_sword");

            Assert.AreEqual(EquipmentSlot.Weapon2, loadout.ResolveTargetSlot(EquipmentSlot.Weapon1),
                "A second weapon should go to the free hand, not replace the first");
        }

        // With both hands full there is no free slot, and doing nothing
        // would read as a broken click — so the third weapon replaces the
        // first hand rather than silently failing.
        [Test]
        public void AThirdWeapon_ReplacesWeapon1WhenBothHandsAreFull()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Weapon1, "iron_sword");
            loadout.Set(EquipmentSlot.Weapon2, "bronze_dagger");

            Assert.IsNull(loadout.FirstFreeSlotFor(EquipmentSlot.Weapon1));
            Assert.AreEqual(EquipmentSlot.Weapon1, loadout.ResolveTargetSlot(EquipmentSlot.Weapon1));
        }

        [Test]
        public void ANonWeapon_OnlyEverResolvesToItsOwnSlot()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Head, "iron_helm");

            Assert.AreEqual(EquipmentSlot.Head, loadout.ResolveTargetSlot(EquipmentSlot.Head),
                "A helmet has nowhere else to go, so a second one replaces the first");
        }

        [Test]
        public void EquippedItemIds_IsInSlotOrderAndCountsBothHands()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Weapon2, "iron_sword");
            loadout.Set(EquipmentSlot.Head, "iron_helm");
            loadout.Set(EquipmentSlot.Weapon1, "iron_sword");

            CollectionAssert.AreEqual(
                new[] { "iron_helm", "iron_sword", "iron_sword" },
                loadout.EquippedItemIds(),
                "Paperdoll order, and the same sword worn twice must count twice");
        }

        [Test]
        public void FindSlotHolding_ReportsTheSlotOrNull()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Shoes, "swift_boots");

            Assert.AreEqual(EquipmentSlot.Shoes, loadout.FindSlotHolding("swift_boots"));
            Assert.IsNull(loadout.FindSlotHolding("iron_helm"));
            Assert.IsNull(loadout.FindSlotHolding(""));
        }

        [Test]
        public void RemoveWhere_DropsOnlyWhatTheCallerRejects_AndHandsItBack()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Head, "iron_helm");
            loadout.Set(EquipmentSlot.Gloves, "deleted_item");
            loadout.Set(EquipmentSlot.Shoes, "swift_boots");

            var removed = loadout.RemoveWhere(id => id == "deleted_item");

            CollectionAssert.AreEqual(new[] { "deleted_item" }, removed);
            Assert.IsTrue(loadout.IsEmpty(EquipmentSlot.Gloves));
            Assert.AreEqual("iron_helm", loadout.Get(EquipmentSlot.Head));
            Assert.AreEqual("swift_boots", loadout.Get(EquipmentSlot.Shoes));
        }
    }

    public class EquipmentSlotsTests
    {
        [Test]
        public void All_CoversEveryDeclaredSlot()
        {
            Assert.AreEqual(8, EquipmentSlots.All.Length,
                "Head, Necklace, Torso, Legs, Shoes, Gloves and two hands");
            CollectionAssert.AllItemsAreUnique(EquipmentSlots.All);
        }

        [Test]
        public void EverySlot_HasADisplayName()
        {
            foreach (var slot in EquipmentSlots.All)
            {
                Assert.IsNotEmpty(EquipmentSlots.DisplayName(slot), $"{slot} has no display name");
            }
        }

        // The one asymmetry in the whole system, pinned so it can't quietly
        // become "any item fits anywhere".
        [Test]
        public void AWeapon_FitsEitherHand_AndNothingElseIsInterchangeable()
        {
            Assert.IsTrue(EquipmentSlots.Accepts(EquipmentSlot.Weapon1, EquipmentSlot.Weapon1));
            Assert.IsTrue(EquipmentSlots.Accepts(EquipmentSlot.Weapon2, EquipmentSlot.Weapon1));

            Assert.IsFalse(EquipmentSlots.Accepts(EquipmentSlot.Weapon1, EquipmentSlot.Weapon2),
                "Nothing is authored as Weapon2, so it should not resolve backwards into Weapon1");
            Assert.IsFalse(EquipmentSlots.Accepts(EquipmentSlot.Shoes, EquipmentSlot.Head));
            Assert.IsFalse(EquipmentSlots.Accepts(EquipmentSlot.Weapon1, EquipmentSlot.Gloves));
        }

        [Test]
        public void TryParse_AcceptsTheEnumNamesCaseInsensitively_AndTheWeaponAlias()
        {
            Assert.IsTrue(EquipmentSlots.TryParse("gloves", out var gloves));
            Assert.AreEqual(EquipmentSlot.Gloves, gloves);

            Assert.IsTrue(EquipmentSlots.TryParse("Weapon", out var weapon));
            Assert.AreEqual(EquipmentSlot.Weapon1, weapon, "The bare alias means the first hand");

            Assert.IsTrue(EquipmentSlots.TryParse("  Weapon2 ", out var offHand));
            Assert.AreEqual(EquipmentSlot.Weapon2, offHand);
        }

        [Test]
        public void TryParse_RejectsNonsenseAndBlanks()
        {
            Assert.IsFalse(EquipmentSlots.TryParse("Backpack", out _));
            Assert.IsFalse(EquipmentSlots.TryParse("", out _));
            Assert.IsFalse(EquipmentSlots.TryParse(null, out _));
            // Enum.TryParse happily accepts a bare number and produces a
            // value with no name; the slot list must not.
            Assert.IsFalse(EquipmentSlots.TryParse("99", out _));
        }

        [Test]
        public void IsWeaponSlot_IsTrueForExactlyTheTwoHands()
        {
            Assert.IsTrue(EquipmentSlots.IsWeaponSlot(EquipmentSlot.Weapon1));
            Assert.IsTrue(EquipmentSlots.IsWeaponSlot(EquipmentSlot.Weapon2));
            Assert.IsFalse(EquipmentSlots.IsWeaponSlot(EquipmentSlot.Head));
            Assert.IsFalse(EquipmentSlots.IsWeaponSlot(EquipmentSlot.Gloves));
        }

        // The hover-comparison panel's whole premise: try a swap on a copy
        // without touching the real loadout.
        [Test]
        public void Clone_CopiesEverySlot_AsIndependentEntries()
        {
            var original = new EquipmentLoadout();
            original.Set(EquipmentSlot.Weapon1, "sword_p0", plus: 3);
            original.Set(EquipmentSlot.Head, "iron_helm");

            var clone = original.Clone();

            Assert.AreEqual("sword_p0", clone.Get(EquipmentSlot.Weapon1));
            Assert.AreEqual(3, clone.GetPlus(EquipmentSlot.Weapon1));
            Assert.AreEqual("iron_helm", clone.Get(EquipmentSlot.Head));
        }

        [Test]
        public void Clone_MutatingTheClone_NeverTouchesTheOriginal()
        {
            var original = new EquipmentLoadout();
            original.Set(EquipmentSlot.Weapon1, "sword_p0");

            var clone = original.Clone();
            clone.Set(EquipmentSlot.Weapon1, "staff_p0");
            clone.Set(EquipmentSlot.Head, "iron_helm");

            Assert.AreEqual("sword_p0", original.Get(EquipmentSlot.Weapon1),
                "Mutating the clone's entry object must not mutate the original's");
            Assert.IsTrue(original.IsEmpty(EquipmentSlot.Head),
                "Adding a slot to the clone must not add it to the original");
        }

        // Same premise as the test above, for the list-typed axis: a shared
        // List<string> reference between the original and the clone would be
        // exactly this aliasing bug, just invisible until something calls
        // .Add on it rather than replacing the whole slot.
        [Test]
        public void Clone_CopiesModifierIdsAsAnIndependentList()
        {
            var original = new EquipmentLoadout();
            original.Set(EquipmentSlot.Weapon1, "sword_p0",
                modifierIds: new List<string> { "fiery" }, riftTier: 1);

            var clone = original.Clone();

            // Reach past Get/Set's own defensive copies into the entries
            // directly -- that is what actually exercises whether Clone()
            // shared the backing List<string> instance between the two
            // loadouts' EquipmentSlotEntry objects.
            var clonedEntry = clone.slots.Find(e => e.slot == EquipmentSlot.Weapon1);
            clonedEntry.modifierIds.Add("swift");

            var originalEntry = original.slots.Find(e => e.slot == EquipmentSlot.Weapon1);
            CollectionAssert.AreEquivalent(new[] { "fiery" }, originalEntry.modifierIds,
                "Clone() must give each entry its own modifierIds list, not share the original's");
            Assert.AreEqual(1, original.GetRiftTier(EquipmentSlot.Weapon1));
        }

        // ---- rolled modifiers (Phase A1: storage only) -------------------------

        [Test]
        public void Set_ThenGet_RoundTripsModifierIdsAndRiftTier()
        {
            var loadout = new EquipmentLoadout();

            loadout.Set(EquipmentSlot.Weapon1, "sword_p0", plus: 2,
                modifierIds: new List<string> { "fiery", "swift" }, riftTier: 2);

            CollectionAssert.AreEquivalent(new[] { "fiery", "swift" },
                loadout.GetModifierIds(EquipmentSlot.Weapon1));
            Assert.AreEqual(2, loadout.GetRiftTier(EquipmentSlot.Weapon1));
        }

        [Test]
        public void AnEmptySlot_ReportsNoModifiersAndZeroRiftTier()
        {
            var loadout = new EquipmentLoadout();

            CollectionAssert.IsEmpty(loadout.GetModifierIds(EquipmentSlot.Weapon1));
            Assert.AreEqual(0, loadout.GetRiftTier(EquipmentSlot.Weapon1));
        }

        // THE aliasing bug this whole axis is at risk of: GetModifierIds must
        // never hand back the entry's own backing list, or a caller mutating
        // what it got back would silently corrupt the loadout.
        [Test]
        public void GetModifierIds_MutatingTheReturnedList_NeverTouchesTheLoadout()
        {
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Weapon1, "sword_p0", modifierIds: new List<string> { "fiery" });

            var returned = loadout.GetModifierIds(EquipmentSlot.Weapon1);
            returned.Add("swift");

            CollectionAssert.AreEquivalent(new[] { "fiery" }, loadout.GetModifierIds(EquipmentSlot.Weapon1),
                "the list handed to the caller must be a copy, not the entry's own list");
        }

        [Test]
        public void RemoveEntriesWhere_HandsBackThePlusAsWellAsTheId()
        {
            // SaveData.Reconcile used the ids-only overload and re-added every
            // orphan at the default plus, so a renamed content id quietly
            // turned a +5 heirloom into a plain one -- with the item count
            // still correct, which is why nothing ever caught it.
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Torso, "cuirass", 5);
            loadout.Set(EquipmentSlot.Head, "helm", 0);

            var removed = loadout.RemoveEntriesWhere(id => id == "cuirass");

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual("cuirass", removed[0].itemId);
            Assert.AreEqual(5, removed[0].plus, "the honing has to survive being handed back");
            Assert.AreEqual("helm", loadout.Get(EquipmentSlot.Head), "and nothing else moved");
        }

        [Test]
        public void RemoveEntriesWhere_HandsBackModifierIdsAndRiftTierToo()
        {
            // Same rule as the plus test above, extended to the two fields
            // this phase adds: an orphan's roll must survive being handed
            // back, or a renamed content id would quietly strip a
            // Convergent item's affixes on the way to the stash.
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Torso, "cuirass", 5,
                modifierIds: new List<string> { "fiery", "swift" }, riftTier: 2);

            var removed = loadout.RemoveEntriesWhere(id => id == "cuirass");

            Assert.AreEqual(1, removed.Count);
            CollectionAssert.AreEquivalent(new[] { "fiery", "swift" }, removed[0].modifierIds);
            Assert.AreEqual(2, removed[0].riftTier);
        }

        [Test]
        public void RemoveEntriesWhere_StillSweepsTombstones()
        {
            // Same tolerant posture as the ids-only overload it sits beside.
            var loadout = new EquipmentLoadout();
            loadout.Set(EquipmentSlot.Torso, "cuirass");
            loadout.slots.Add(new EquipmentSlotEntry(EquipmentSlot.Head, "", 0));

            loadout.RemoveEntriesWhere(id => false);

            Assert.AreEqual(1, loadout.slots.Count, "the empty entry was swept even though nothing was rejected");
        }
}
}
