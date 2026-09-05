using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // Save-compat for the two fields Phase A1 of the item-modifier plan adds
    // (modifierIds, riftTier) on InventoryEntry and EquipmentSlotEntry.
    //
    // Both types are [Serializable] and round-tripped by JsonUtility directly
    // -- the same mechanism SaveData itself uses -- so testing the DTOs here
    // is testing the exact save-compat claim their own header comments make,
    // without needing Core/SaveData or a scene. Hand-written JSON stands in
    // for "a save written before this field existed": a real old save simply
    // has no modifierIds/riftTier keys in its item objects, and this is that
    // shape, verbatim.
    public class ItemModifierStorageTests
    {
        [Test]
        public void AnOldShapedInventoryEntry_LoadsWithEmptyModifiersAndZeroRiftTier()
        {
            // Exactly what an entry written before this phase looks like on
            // disk: itemId, count, plus -- and nothing else.
            const string oldJson = @"{""itemId"":""iron_sword"",""count"":2,""plus"":3}";

            var entry = JsonUtility.FromJson<InventoryEntry>(oldJson);

            Assert.AreEqual("iron_sword", entry.itemId, "an unrelated field must not be disturbed");
            Assert.AreEqual(2, entry.count);
            Assert.AreEqual(3, entry.plus);
            Assert.IsNotNull(entry.modifierIds, "must be a real empty list, not null");
            Assert.AreEqual(0, entry.modifierIds.Count);
            Assert.AreEqual(0, entry.riftTier);
        }

        [Test]
        public void AnOldShapedEquipmentSlotEntry_LoadsWithEmptyModifiersAndZeroRiftTier()
        {
            const string oldJson = @"{""slot"":2,""itemId"":""cuirass"",""plus"":5}";

            var entry = JsonUtility.FromJson<EquipmentSlotEntry>(oldJson);

            Assert.AreEqual("cuirass", entry.itemId);
            Assert.AreEqual(5, entry.plus);
            Assert.IsNotNull(entry.modifierIds, "must be a real empty list, not null");
            Assert.AreEqual(0, entry.modifierIds.Count);
            Assert.AreEqual(0, entry.riftTier);
        }

        [Test]
        public void ANewShapedInventoryEntry_RoundTripsThroughJsonUtility()
        {
            var original = new InventoryEntry("iron_sword", 1, 4,
                new List<string> { "fiery", "swift" }, riftTier: 2);

            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<InventoryEntry>(json);

            Assert.AreEqual(original.itemId, restored.itemId);
            Assert.AreEqual(original.plus, restored.plus);
            Assert.AreEqual(original.riftTier, restored.riftTier);
            CollectionAssert.AreEquivalent(original.modifierIds, restored.modifierIds);
        }

        [Test]
        public void ANewShapedEquipmentSlotEntry_RoundTripsThroughJsonUtility()
        {
            var original = new EquipmentSlotEntry(EquipmentSlot.Weapon1, "iron_sword", 4,
                new List<string> { "fiery", "swift" }, riftTier: 2);

            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<EquipmentSlotEntry>(json);

            Assert.AreEqual(original.itemId, restored.itemId);
            Assert.AreEqual(original.plus, restored.plus);
            Assert.AreEqual(original.riftTier, restored.riftTier);
            CollectionAssert.AreEquivalent(original.modifierIds, restored.modifierIds);
        }
    }
}
