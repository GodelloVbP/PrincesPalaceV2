using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // Save-compat for the copy fields (tier, modifierIds) on InventoryEntry
    // and EquipmentSlotEntry.
    //
    // Both types are [Serializable] and round-tripped by JsonUtility directly
    // -- the same mechanism SaveData itself uses -- so testing the DTOs here
    // is testing the exact save-compat claim their own header comments make,
    // without needing Core/SaveData or a scene. Hand-written JSON stands in
    // for a save written before the copy model: it has no tier key, and still
    // carries the plus and riftTier keys that were removed.
    public class ItemModifierStorageTests
    {
        [Test]
        public void AnOldShapedInventoryEntry_LoadsWithEmptyModifiersAndTierZero()
        {
            const string oldJson = @"{""itemId"":""iron_sword"",""count"":2,""plus"":3,""riftTier"":2}";

            var entry = JsonUtility.FromJson<InventoryEntry>(oldJson);

            Assert.AreEqual("iron_sword", entry.itemId, "an unrelated field must not be disturbed");
            Assert.AreEqual(2, entry.count);
            Assert.AreEqual(0, entry.tier, "a missing tier loads as 0");
            Assert.IsNotNull(entry.modifierIds, "must be a real empty list, not null");
            Assert.AreEqual(0, entry.modifierIds.Count);
        }

        [Test]
        public void AnOldShapedEquipmentSlotEntry_LoadsWithEmptyModifiersAndTierZero()
        {
            const string oldJson = @"{""slot"":2,""itemId"":""cuirass"",""plus"":5,""riftTier"":1}";

            var entry = JsonUtility.FromJson<EquipmentSlotEntry>(oldJson);

            Assert.AreEqual("cuirass", entry.itemId);
            Assert.AreEqual(0, entry.tier);
            Assert.IsNotNull(entry.modifierIds, "must be a real empty list, not null");
            Assert.AreEqual(0, entry.modifierIds.Count);
        }

        [Test]
        public void ANewShapedInventoryEntry_RoundTripsThroughJsonUtility()
        {
            var original = new InventoryEntry("iron_sword", 1, new List<string> { "fiery", "swift" }, tier: 4);

            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<InventoryEntry>(json);

            Assert.AreEqual(original.itemId, restored.itemId);
            Assert.AreEqual(4, restored.tier);
            CollectionAssert.AreEquivalent(original.modifierIds, restored.modifierIds);
            StringAssert.DoesNotContain("plus", json);
            StringAssert.DoesNotContain("riftTier", json);
        }

        [Test]
        public void ANewShapedEquipmentSlotEntry_RoundTripsThroughJsonUtility()
        {
            var original = new EquipmentSlotEntry(EquipmentSlot.Weapon1, "iron_sword",
                new List<string> { "fiery", "swift" }, tier: 4);

            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<EquipmentSlotEntry>(json);

            Assert.AreEqual(original.itemId, restored.itemId);
            Assert.AreEqual(4, restored.tier);
            CollectionAssert.AreEquivalent(original.modifierIds, restored.modifierIds);
            StringAssert.DoesNotContain("plus", json);
            StringAssert.DoesNotContain("riftTier", json);
        }
    }
}
