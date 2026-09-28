using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // Save-compat for the item-modifier storage fields (Phase A1 of the
    // item-modifier plan), at the level a real save actually loads through:
    // SaveData.Migrate()/Reconcile(), not just the DTOs in isolation
    // (ItemModifierStorageTests covers those in EditMode).
    //
    // A REAL pre-A1 save simply has no "modifierIds"/"riftTier" keys inside
    // its InventoryEntry/EquipmentSlotEntry objects. This builds a save the
    // normal way, then strips exactly those keys back out of the JSON to
    // reproduce that old shape byte-for-byte, rather than hand-authoring an
    // entire SaveData literal (which would drift from the real schema the
    // moment an unrelated field changed).
    public class ItemModifierSaveCompatTests
    {
        // Only these two tokens exist anywhere in a fresh SaveData's JSON --
        // both fields are new in this phase, so stripping them by literal
        // text is unambiguous and cannot eat an unrelated field.
        private static string StripToOldShape(string json) =>
            json.Replace(",\"modifierIds\":[]", "").Replace(",\"riftTier\":0", "");

        [Test]
        public void AnOldShapedSave_LoadsWithEmptyModifiersAndZeroRiftTier_AndNoDataLoss()
        {
            var save = SaveData.CreateNew();
            var character = save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: CreateNew should seed at least one roster member");

            var item = ContentDatabase.Equippables.FirstOrDefault();
            Assert.IsNotNull(item, "fixture: content has no equippable item to test with");

            character.equipment.Set(item.equipSlot, item.id, plus: 5);
            InventoryOps.Add(save.stockpiledItems, item.id, 3, plus: 2);
            save.Gold = 777;

            string oldJson = StripToOldShape(JsonUtility.ToJson(save));
            StringAssert.DoesNotContain("modifierIds", oldJson, "fixture: the stripped JSON must actually be the old shape");
            StringAssert.DoesNotContain("riftTier", oldJson, "fixture: the stripped JSON must actually be the old shape");

            SaveData loaded = null;
            Assert.DoesNotThrow(() => loaded = JsonUtility.FromJson<SaveData>(oldJson));
            Assert.DoesNotThrow(() => loaded.Migrate());

            Assert.AreEqual(777, loaded.Gold, "an unrelated field must survive untouched");

            var loadedCharacter = loaded.roster.First(c => c.definitionId == character.definitionId);
            Assert.AreEqual(item.id, loadedCharacter.equipment.Get(item.equipSlot), "the worn item id must survive");
            Assert.AreEqual(5, loadedCharacter.equipment.GetPlus(item.equipSlot), "the honing must survive");
            CollectionAssert.IsEmpty(loadedCharacter.equipment.GetModifierIds(item.equipSlot));
            Assert.AreEqual(0, loadedCharacter.equipment.GetRiftTier(item.equipSlot));

            var stockEntry = loaded.stockpiledItems.First(e => e.itemId == item.id && e.plus == 2);
            Assert.AreEqual(3, stockEntry.count, "the stack count must survive");
            Assert.IsNotNull(stockEntry.modifierIds, "must be a real empty list, not null");
            Assert.AreEqual(0, stockEntry.modifierIds.Count);
            Assert.AreEqual(0, stockEntry.riftTier);
        }

        // The weapon-family modifier axis is gone (sturdy/nimble/hallowed/
        // arcane/verdant/heavy/quick/cunning), so an item id like
        // "sword_sturdy_p5" no longer resolves through ContentDatabase at
        // all. Reconcile()'s tolerant-prune path handles this generically --
        // the SAME path a renamed/removed item of any kind goes through, no
        // special-casing added for weapons specifically
        // (Domain/Equipment/EquipmentLoadout.cs RemoveEntriesWhere, called
        // from SaveData.Reconcile): the slot is cleared, and because
        // ContentDatabase.GetItem returns null for the
        // orphaned id, it is NOT handed back to the stash (the stash-return
        // branch only runs when the item still resolves under some other
        // classification) -- so the character just ends up unequipped in
        // that slot, and the save loads without throwing.
        [Test]
        public void AnEquippedCollapsedWeaponId_DoesNotThrow_AndLeavesTheSlotUnequipped()
        {
            var save = SaveData.CreateNew();
            var character = save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: CreateNew should seed at least one roster member");

            // A pre-Phase-B weapon id: "<family>_<modifier>_p<tier>". No
            // family generates ids in this shape any more (the collapsed
            // scheme is "<family>_p<tier>"), so ContentDatabase.GetItem must
            // return null for it -- the exact "content update between
            // quitting and resuming" case Reconcile's own comments describe.
            const string collapsedWeaponId = "sword_sturdy_p5";
            Assert.IsNull(ContentDatabase.GetItem(collapsedWeaponId),
                "fixture: this id must not resolve, or the test proves nothing about degradation");

            character.equipment.Set(EquipmentSlot.Weapon1, collapsedWeaponId, plus: 5);

            Assert.DoesNotThrow(() => save.Reconcile());

            Assert.IsTrue(character.equipment.IsEmpty(EquipmentSlot.Weapon1),
                "an item id that no longer resolves must leave the slot unequipped, not crash or keep a dangling reference");
            Assert.IsFalse(save.stockpiledItems.Any(e => e.itemId == collapsedWeaponId),
                "an unresolvable id has nothing to hand back to the stash either -- it is simply gone");
        }
    }
}
